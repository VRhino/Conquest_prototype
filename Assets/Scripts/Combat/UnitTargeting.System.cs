using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// System that assigns a target enemy to each unit of a squad based on the
/// current squad state and player order.
/// Melee units score their K nearest candidates (L4-04, D6): proximity, squad focus and bias, scaled down by the
/// attackers each enemy already has across every squad (soft saturation, no hard cap) plus a bonus for the current
/// target. A unit within reach of its target keeps it; the rest look again every few tenths of a second, with a phase
/// per unit. New targets come only from the squad's enemy search (L4-09): the combat radius of its strategy and speed,
/// and the same level. A unit stopped by an enemy that is not its target scores again after <c>blockedRetargetTime</c> with
/// that enemy in and a bonus for it (P4, <see cref="UnitBlockedComponent"/>): it takes the enemy in its way unless that
/// one is already crowded, so the melee forms lines instead of pushing through one enemy to reach another, or piling up.
/// A rear-rank unit takes no enemy deeper than <c>rearRankTargetDepth</c> beyond its front rank's line (S9): it waits behind
/// the line instead of slipping through it.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(SquadAISystem))]
[UpdateAfter(typeof(SquadFSMSystem))]
[UpdateAfter(typeof(UnitDeathSystem))]
public partial class UnitTargetingSystem : SystemBase
{
    private static readonly Unity.Profiling.ProfilerMarker TargetingMarker =
        new Unity.Profiling.ProfilerMarker("Conquest.UnitTargeting");

    private struct Candidate
    {
        public Entity entity;
        public Entity owner;     // Enemy squad, or the hero itself.
        public float3 position;
        public bool isHero;
        public bool searchable;  // Passes the squad's enemy search: may be taken as a new target.
    }

    private readonly HashSet<Entity> _validTargets = new();
    private readonly List<Candidate> _candidates = new();
    private readonly Dictionary<Entity, int> _meleeCounts = new();
    private readonly Dictionary<Entity, int> _rangedCounts = new();
    private readonly Dictionary<Entity, int> _focusCounts = new();
    private int[] _nearest = new int[1];
    // The squad's front line this frame (S9): formation forward and the mean depth of its front rank; NaN without one.
    private float2 _forward;
    private float _frontLine;
    private float _rearRankDepth;
    private float[] _nearestDistancesSq = new float[1];

    public int DebugCandidateChecks { get; private set; }
    public int DebugRetargetCount { get; private set; }

    protected override void OnUpdate()
    {
        using var targetingSample = TargetingMarker.Auto();
        DebugCandidateChecks = 0;
        DebugRetargetCount = 0;

        var tuning = SystemAPI.HasSingleton<MovementTuningComponent>()
            ? SystemAPI.GetSingleton<MovementTuningComponent>() : MovementTuningComponent.Default;
        float bodyRadius = tuning.bodyRadius;
        var weights = TargetScoreWeights.From(tuning);
        var search = EnemySearchSettings.From(tuning);
        int candidateCount = math.max(1, tuning.targetCandidateCount);
        if (_nearest.Length < candidateCount)
        {
            _nearest = new int[candidateCount];
            _nearestDistancesSq = new float[candidateCount];
        }
        double now = SystemAPI.Time.ElapsedTime;
        float deltaTime = SystemAPI.Time.DeltaTime;
        bool inBattle = !SystemAPI.TryGetSingleton<MatchStateComponent>(out var match)
            || match.currentState == MatchState.InBattle;

        // Melee attackers per enemy over every squad: two squads on one enemy saturate it together (D6).
        _meleeCounts.Clear();
        foreach (var combat in SystemAPI.Query<RefRO<UnitCombatComponent>>()
                     .WithNone<UnitRangedStatsComponent, IsDeadComponent>())
        {
            Entity target = combat.ValueRO.target;
            if (target != Entity.Null) _meleeCounts[target] = Count(_meleeCounts, target) + 1;
        }

        foreach (var (state, units, detected, squad) in SystemAPI
                     .Query<RefRO<SquadStateComponent>,
                            DynamicBuffer<SquadUnitElement>,
                            DynamicBuffer<SquadTargetEntity>>()
                     .WithAll<SquadAIComponent>()
                     .WithEntityAccess())
        {
            // InCombat state is the sole gate for targeting — regardless of how it was
            // triggered (V press, damage received, squadmate hit). All three triggers
            // correctly set currentState=InCombat via the FSM; that is the source of truth.
            bool allow = state.ValueRO.currentState == SquadFSMState.InCombat && inBattle;

            // Squads outside combat (or without candidates) only need stale combat state cleared.
            if (!allow || detected.Length == 0)
            {
                for (int i = 0; i < units.Length; i++)
                {
                    Entity unit = units[i].Value;
                    if (!SystemAPI.Exists(unit) || !SystemAPI.HasComponent<UnitCombatComponent>(unit))
                        continue;

                    SystemAPI.GetComponentRW<UnitCombatComponent>(unit).ValueRW.target = Entity.Null;
                    if (SystemAPI.HasComponent<IsEngagingTag>(unit))
                        SystemAPI.SetComponentEnabled<IsEngagingTag>(unit, false);
                }
                continue;
            }

            BuildCandidates(squad, state.ValueRO.currentOrder, units, detected, tuning.enemySearchEnabled, search);
            Entity focus = SquadFocus(squad, state.ValueRO.currentOrder, units);
            UpdateFrontLine(squad, units, tuning.rearRankTargetDepth);
            _rangedCounts.Clear();
            int rangedUnitsInSquad = 0;

            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (!SystemAPI.Exists(unit) || !SystemAPI.HasComponent<UnitCombatComponent>(unit))
                    continue;

                var combat = SystemAPI.GetComponentRW<UnitCombatComponent>(unit);
                Entity previous = combat.ValueRO.target;
                bool currentValid = _validTargets.Contains(previous) && SquadCombatModeComponent.AllowsTarget(EntityManager, unit, previous);
                Entity chosen;
                if (SystemAPI.HasComponent<UnitRangedStatsComponent>(unit))
                {
                    rangedUnitsInSquad++;
                    chosen = currentValid ? previous : ClosestSearchable(unit);
                    if (chosen != Entity.Null) _rangedCounts[chosen] = Count(_rangedCounts, chosen) + 1;
                }
                else if (!BlockerDue(unit, previous, currentValid, bodyRadius, now, tuning.blockedRetargetTime, out Entity blocker)
                         && currentValid && (InReach(unit, previous, bodyRadius)
                             || !TargetScoring.ReevaluationDue(unit.Index, now, deltaTime, tuning.targetReevaluateInterval)))
                {
                    chosen = previous; // Engaged, or not its turn to look again.
                }
                else
                {
                    chosen = BestMeleeTarget(unit, currentValid ? previous : Entity.Null, blocker, focus, candidateCount, weights);
                    if (chosen != previous)
                    {
                        if (previous != Entity.Null) _meleeCounts[previous] = Count(_meleeCounts, previous) - 1;
                        if (chosen != Entity.Null) _meleeCounts[chosen] = Count(_meleeCounts, chosen) + 1;
                    }
                }

                if (chosen != Entity.Null && chosen != previous)
                    DebugRetargetCount++;
                combat.ValueRW.target = chosen;
            }

            SpreadRangedTargets(units, rangedUnitsInSquad);

            // Sync IsEngagingTag so other systems can query engagement state
            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (!SystemAPI.Exists(unit)
                    || !SystemAPI.HasComponent<IsEngagingTag>(unit)
                    || !SystemAPI.HasComponent<UnitCombatComponent>(unit))
                    continue;

                bool hasTarget = SystemAPI.GetComponent<UnitCombatComponent>(unit).target != Entity.Null;
                SystemAPI.SetComponentEnabled<IsEngagingTag>(unit, hasTarget);
            }
        }
    }

    /// <summary>
    /// Live candidates of the squad, once each, marked with the enemy search (L4-09): the combat radius of its strategy
    /// beyond the squad's extent around the anchor, and the same level as the squad.
    /// </summary>
    private void BuildCandidates(Entity squad, SquadOrderType order, DynamicBuffer<SquadUnitElement> units,
        DynamicBuffer<SquadTargetEntity> detected, bool searchEnabled, in EnemySearchSettings search)
    {
        _validTargets.Clear();
        _candidates.Clear();

        float3 centroid = float3.zero;
        int alive = 0;
        float maxRangedRange = 0f;
        for (int i = 0; i < units.Length; i++)
        {
            Entity unit = units[i].Value;
            if (!SystemAPI.Exists(unit) || !SystemAPI.HasComponent<LocalTransform>(unit)
                || SystemAPI.HasComponent<IsDeadComponent>(unit)) continue;
            centroid += SystemAPI.GetComponent<LocalTransform>(unit).Position;
            alive++;
            if (SystemAPI.HasComponent<UnitRangedStatsComponent>(unit))
                maxRangedRange = math.max(maxRangedRange, SystemAPI.GetComponent<UnitRangedStatsComponent>(unit).range);
        }
        if (alive > 0) centroid /= alive;

        bool hasAnchor = SystemAPI.HasComponent<SquadFormationAnchorComponent>(squad);
        var anchor = hasAnchor ? SystemAPI.GetComponent<SquadFormationAnchorComponent>(squad) : default;
        float2 reference = hasAnchor ? anchor.position.xz : centroid.xz;
        float squadRadius = 0f;
        for (int i = 0; i < units.Length; i++)
        {
            Entity unit = units[i].Value;
            if (!SystemAPI.Exists(unit) || !SystemAPI.HasComponent<LocalTransform>(unit)
                || SystemAPI.HasComponent<IsDeadComponent>(unit)) continue;
            squadRadius = math.max(squadRadius,
                math.distance(reference, SystemAPI.GetComponent<LocalTransform>(unit).Position.xz));
        }
        OrderSource source = SystemAPI.HasComponent<SquadResolvedOrderComponent>(squad)
            ? SystemAPI.GetComponent<SquadResolvedOrderComponent>(squad).source : OrderSource.Player;
        var strategy = EnemySearch.StrategyFor(order, source);
        // Archers search at least as far as they shoot.
        float radius = math.max(EnemySearch.CombatRadius(strategy, math.length(anchor.velocity.xz), search), maxRangedRange);

        for (int j = 0; j < detected.Length; j++)
        {
            DebugCandidateChecks++;
            Entity candidate = detected[j].Value;
            if (!SystemAPI.Exists(candidate) || !SystemAPI.HasComponent<LocalTransform>(candidate)
                || SystemAPI.HasComponent<IsDeadComponent>(candidate)) continue;
            bool isHero = SystemAPI.HasComponent<HeroLifeComponent>(candidate);
            if (isHero && !SystemAPI.GetComponent<HeroLifeComponent>(candidate).isAlive) continue;
            if (!_validTargets.Add(candidate)) continue;

            float3 position = SystemAPI.GetComponent<LocalTransform>(candidate).Position;
            bool searchable = !searchEnabled
                || (EnemySearch.WithinRadius(reference, squadRadius, position.xz, radius)
                    && EnemySearch.SameLevel(centroid.y, position, math.distance(reference, position.xz), search));
            _candidates.Add(new Candidate
            {
                entity = candidate,
                owner = SystemAPI.HasComponent<UnitOwnerComponent>(candidate)
                    ? SystemAPI.GetComponent<UnitOwnerComponent>(candidate).squad : candidate,
                position = position,
                isHero = isHero,
                searchable = searchable,
            });
        }
    }

    /// <summary>
    /// The squad's focus: the target of an Attack order, or else the enemy squad most of its melee units are on, so
    /// a squad tends to fight one enemy squad instead of splitting.
    /// </summary>
    private Entity SquadFocus(Entity squad, SquadOrderType order, DynamicBuffer<SquadUnitElement> units)
    {
        if (order == SquadOrderType.Attack && SystemAPI.HasComponent<SquadResolvedOrderComponent>(squad))
        {
            Entity ordered = SystemAPI.GetComponent<SquadResolvedOrderComponent>(squad).targetEntity;
            if (ordered != Entity.Null) return ordered;
        }

        _focusCounts.Clear();
        Entity focus = Entity.Null;
        int best = 0;
        for (int i = 0; i < units.Length; i++)
        {
            Entity unit = units[i].Value;
            if (!SystemAPI.Exists(unit) || !SystemAPI.HasComponent<UnitCombatComponent>(unit)
                || SystemAPI.HasComponent<UnitRangedStatsComponent>(unit)) continue;
            Entity target = SystemAPI.GetComponent<UnitCombatComponent>(unit).target;
            if (!_validTargets.Contains(target)) continue;
            Entity owner = SystemAPI.HasComponent<UnitOwnerComponent>(target)
                ? SystemAPI.GetComponent<UnitOwnerComponent>(target).squad : target;
            int count = Count(_focusCounts, owner) + 1;
            _focusCounts[owner] = count;
            if (count > best) { best = count; focus = owner; }
        }
        return focus;
    }

    /// <summary>Best scored target among the K nearest searchable candidates, the current one and the blocker (P4).</summary>
    private Entity BestMeleeTarget(Entity unit, Entity current, Entity blocker, Entity focus, int candidateCount,
        in TargetScoreWeights w)
    {
        float3 unitPos = SystemAPI.GetComponent<LocalTransform>(unit).Position;
        int found = 0;
        int currentIndex = -1, blockerIndex = -1;
        bool rearRank = !float.IsNaN(_frontLine) && SystemAPI.HasComponent<UnitGridSlotComponent>(unit)
            && SystemAPI.GetComponent<UnitGridSlotComponent>(unit).row > 0;
        for (int j = 0; j < _candidates.Count; j++)
        {
            DebugCandidateChecks++;
            var c = _candidates[j];
            if (!SquadCombatModeComponent.AllowsTarget(EntityManager, unit, c.entity)) continue;
            if (rearRank && TargetScoring.BeyondFrontLine(math.dot(c.position.xz, _forward), _frontLine, _rearRankDepth))
                continue;
            if (c.entity == current) currentIndex = j;
            if (c.entity == blocker) blockerIndex = j;
            if (!c.searchable) continue;
            TargetScoring.InsertNearest(_nearest, _nearestDistancesSq, ref found, candidateCount, j,
                math.distancesq(unitPos.xz, c.position.xz));
        }

        Entity best = Entity.Null;
        float bestScore = float.MinValue;
        for (int k = -2; k < found; k++)
        {
            // The current target and the blocker are scored even when they are not among the K nearest.
            int j = k == -2 ? currentIndex : k == -1 ? blockerIndex : _nearest[k];
            if (j < 0 || (k == -1 && j == currentIndex) || (k >= 0 && (j == currentIndex || j == blockerIndex))) continue;
            var c = _candidates[j];
            bool isCurrent = j == currentIndex;
            int others = Count(_meleeCounts, c.entity) - (isCurrent ? 1 : 0);
            float score = TargetScoring.Score(math.distance(unitPos.xz, c.position.xz),
                focus != Entity.Null && (c.owner == focus || c.entity == focus), c.isHero, others, isCurrent, w,
                j == blockerIndex);
            if (score > bestScore) { bestScore = score; best = c.entity; }
        }
        return best;
    }

    /// <summary>The enemy blocking the unit, when it is time to score it (P4).</summary>
    private bool BlockerDue(Entity unit, Entity previous, bool currentValid, float bodyRadius, double now,
        float threshold, out Entity blocker)
    {
        blocker = Entity.Null;
        if (threshold <= 0f || !SystemAPI.HasComponent<UnitBlockedComponent>(unit)) return false;
        var blocked = SystemAPI.GetComponent<UnitBlockedComponent>(unit);
        if (blocked.blocker == Entity.Null || blocked.blocker == previous) return false;
        bool take = TargetScoring.BlockerDue(_validTargets.Contains(blocked.blocker) && SquadCombatModeComponent.AllowsTarget(EntityManager, unit, blocked.blocker), now - blocked.since, threshold,
            currentValid && InReach(unit, previous, bodyRadius));
        if (take) blocker = blocked.blocker;
        return take;
    }

    /// <summary>The squad's formation forward and the mean depth of its living front rank along it (S9).</summary>
    private void UpdateFrontLine(Entity squad, DynamicBuffer<SquadUnitElement> units, float rearRankDepth)
    {
        _frontLine = float.NaN;
        _rearRankDepth = rearRankDepth;
        if (rearRankDepth <= 0f || !SystemAPI.HasComponent<SquadFormationAnchorComponent>(squad)) return;
        _forward = math.normalizesafe(math.mul(SystemAPI.GetComponent<SquadFormationAnchorComponent>(squad).rotation,
            math.forward()).xz);
        if (math.lengthsq(_forward) < 1e-6f) return;
        float sum = 0f;
        int front = 0;
        for (int i = 0; i < units.Length; i++)
        {
            Entity unit = units[i].Value;
            if (!SystemAPI.Exists(unit) || SystemAPI.HasComponent<IsDeadComponent>(unit)
                || !SystemAPI.HasComponent<UnitGridSlotComponent>(unit) || !SystemAPI.HasComponent<LocalTransform>(unit)
                || SystemAPI.GetComponent<UnitGridSlotComponent>(unit).row != 0) continue;
            sum += math.dot(SystemAPI.GetComponent<LocalTransform>(unit).Position.xz, _forward);
            front++;
        }
        if (front > 0) _frontLine = sum / front;
    }

    private Entity ClosestSearchable(Entity unit)
    {
        float3 unitPos = SystemAPI.GetComponent<LocalTransform>(unit).Position;
        float bestDist = float.MaxValue;
        Entity best = Entity.Null;
        for (int j = 0; j < _candidates.Count; j++)
        {
            DebugCandidateChecks++;
            var c = _candidates[j];
            if (!c.searchable || !SquadCombatModeComponent.AllowsTarget(EntityManager, unit, c.entity)) continue;
            float dist = math.distancesq(unitPos, c.position);
            if (dist < bestDist) { bestDist = dist; best = c.entity; }
        }
        return best;
    }

    private bool InReach(Entity unit, Entity target, float bodyRadius)
    {
        if (!SystemAPI.HasComponent<UnitWeaponComponent>(unit) || !SystemAPI.HasComponent<LocalTransform>(unit))
            return false;
        float3 uPos = SystemAPI.GetComponent<LocalTransform>(unit).Position;
        float3 tPos = SystemAPI.GetComponent<LocalTransform>(target).Position;
        return MeleeReach.InAttackRange(math.distance(uPos.xz, tPos.xz),
            SystemAPI.GetComponent<UnitWeaponComponent>(unit).attackRange, bodyRadius);
    }

    /// <summary>
    /// Soft target spreading for ranged units. If multiple targets are available, avoid over-focusing one
    /// target; if there is only one practical target, concentration is allowed.
    /// </summary>
    private void SpreadRangedTargets(DynamicBuffer<SquadUnitElement> units, int rangedUnitsInSquad)
    {
        int availableTargets = _validTargets.Count;
        // No dispersion needed when there is no real alternative.
        if (rangedUnitsInSquad == 0 || availableTargets <= 1) return;
        int softCap = math.max(1, (int)math.ceil((float)rangedUnitsInSquad / availableTargets));

        for (int i = 0; i < units.Length; i++)
        {
            Entity unit = units[i].Value;
            if (!SystemAPI.Exists(unit)
                || !SystemAPI.HasComponent<UnitCombatComponent>(unit)
                || !SystemAPI.HasComponent<UnitRangedStatsComponent>(unit))
                continue;

            var combat = SystemAPI.GetComponentRW<UnitCombatComponent>(unit);
            if (combat.ValueRO.target == Entity.Null
                || !_rangedCounts.TryGetValue(combat.ValueRO.target, out int currentCount))
                continue;

            // Small slack avoids excessive retarget churn.
            if (currentCount <= softCap + 1)
                continue;

            Entity bestCandidate = combat.ValueRO.target;
            int bestCount = currentCount;
            for (int j = 0; j < _candidates.Count; j++)
            {
                DebugCandidateChecks++;
                var candidate = _candidates[j];
                if (candidate.entity == combat.ValueRO.target || !candidate.searchable
                    || !SquadCombatModeComponent.AllowsTarget(EntityManager, unit, candidate.entity))
                    continue;

                int candidateCount = Count(_rangedCounts, candidate.entity);
                if (candidateCount < bestCount)
                {
                    bestCount = candidateCount;
                    bestCandidate = candidate.entity;
                }
            }

            // Reassign only when it actually improves spreading.
            if (bestCandidate != combat.ValueRO.target && bestCount + 1 < currentCount)
            {
                _rangedCounts[combat.ValueRO.target] = currentCount - 1;
                _rangedCounts[bestCandidate] = bestCount + 1;
                combat.ValueRW.target = bestCandidate;
                DebugRetargetCount++;
            }
        }
    }

    private static int Count(Dictionary<Entity, int> counts, Entity key) =>
        counts.TryGetValue(key, out int count) ? count : 0;
}

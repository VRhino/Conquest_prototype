using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// System that assigns a target enemy to each unit of a squad based on the
/// current squad state and player order.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(SquadAISystem))]
[UpdateAfter(typeof(SquadFSMSystem))]
[UpdateAfter(typeof(UnitDeathSystem))]
public partial class UnitTargetingSystem : SystemBase
{
    private static readonly Unity.Profiling.ProfilerMarker TargetingMarker =
        new Unity.Profiling.ProfilerMarker("Conquest.UnitTargeting");

    private readonly HashSet<Entity> _validTargets = new();
    private readonly Dictionary<Entity, int> _meleeCounts = new();
    private readonly Dictionary<Entity, int> _rangedCounts = new();

    public int DebugCandidateChecks { get; private set; }
    public int DebugRetargetCount { get; private set; }

    protected override void OnUpdate()
    {
        using var targetingSample = TargetingMarker.Auto();
        DebugCandidateChecks = 0;
        DebugRetargetCount = 0;

        int maxPerTarget = SystemAPI.HasSingleton<SquadSpawnConfigComponent>()
            ? SystemAPI.GetSingleton<SquadSpawnConfigComponent>().maxUnitsPerTarget
            : 2;
        float bodyRadius = (SystemAPI.HasSingleton<MovementTuningComponent>()
            ? SystemAPI.GetSingleton<MovementTuningComponent>() : MovementTuningComponent.Default).bodyRadius;

        foreach (var (state, units, detected) in SystemAPI
                     .Query<RefRO<SquadStateComponent>,
                            DynamicBuffer<SquadUnitElement>,
                            DynamicBuffer<SquadTargetEntity>>()
                     .WithAll<SquadAIComponent>())
        {
            // InCombat state is the sole gate for targeting — regardless of how it was
            // triggered (V press, damage received, squadmate hit). All three triggers
            // correctly set currentState=InCombat via the FSM; that is the source of truth.
            bool allow = state.ValueRO.currentState == SquadFSMState.InCombat
                && (!SystemAPI.TryGetSingleton<MatchStateComponent>(out var match)
                    || match.currentState == MatchState.InBattle);

            // Squads outside combat (or without candidates) only need stale combat state
            // cleared. Avoid allocating the two native accounting maps on this common path.
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

            _validTargets.Clear();
            _meleeCounts.Clear();
            _rangedCounts.Clear();
            for (int j = 0; j < detected.Length; j++)
            {
                DebugCandidateChecks++;
                Entity candidate = detected[j].Value;
                if (!SystemAPI.Exists(candidate) || !SystemAPI.HasComponent<LocalTransform>(candidate)
                    || SystemAPI.HasComponent<IsDeadComponent>(candidate)) continue;
                if (SystemAPI.HasComponent<HeroLifeComponent>(candidate)
                    && !SystemAPI.GetComponent<HeroLifeComponent>(candidate).isAlive) continue;
                _validTargets.Add(candidate);
            }
            int availableTargets = _validTargets.Count;
            var meleeEnemyCounts = _meleeCounts;
            var rangedEnemyCounts = _rangedCounts;
            int rangedUnitsInSquad = 0;

            // First pass: choose closest target for each unit
            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (!SystemAPI.Exists(unit) || !SystemAPI.HasComponent<UnitCombatComponent>(unit))
                    continue;

                var combat = SystemAPI.GetComponentRW<UnitCombatComponent>(unit);
                bool isRanged = SystemAPI.HasComponent<UnitRangedStatsComponent>(unit);
                if (isRanged) rangedUnitsInSquad++;

                bool currentValid = _validTargets.Contains(combat.ValueRO.target) && SquadCombatModeComponent.AllowsTarget(EntityManager, unit, combat.ValueRO.target);

                if (!currentValid)
                {
                    Entity previousTarget = combat.ValueRO.target;
                    float3 unitPos = SystemAPI.GetComponent<LocalTransform>(unit).Position;
                    float bestDist = float.MaxValue;
                    Entity bestEnemy = Entity.Null;
                    for (int j = 0; j < detected.Length; j++)
                    {
                        DebugCandidateChecks++;
                        Entity enemy = detected[j].Value;
                        if ((!_validTargets.Contains(enemy) || !SquadCombatModeComponent.AllowsTarget(EntityManager, unit, enemy)))
                            continue;
                        float3 enemyPos = SystemAPI.GetComponent<LocalTransform>(enemy).Position;
                        float dist = math.distancesq(unitPos, enemyPos);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestEnemy = enemy;
                        }
                    }
                    if (bestEnemy != Entity.Null && bestEnemy != previousTarget)
                        DebugRetargetCount++;
                    combat.ValueRW.target = bestEnemy;
                }

                // Target-cap accounting is melee-only.
                if (!isRanged && combat.ValueRW.target != Entity.Null)
                {
                    meleeEnemyCounts.TryAdd(combat.ValueRW.target, 0);
                    meleeEnemyCounts[combat.ValueRW.target] = meleeEnemyCounts[combat.ValueRW.target] + 1;
                }
                else if (isRanged && combat.ValueRW.target != Entity.Null)
                {
                    rangedEnemyCounts.TryAdd(combat.ValueRW.target, 0);
                    rangedEnemyCounts[combat.ValueRW.target] = rangedEnemyCounts[combat.ValueRW.target] + 1;
                }
            }

            // Second pass: redistribute if too many units target the same enemy
            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (!SystemAPI.Exists(unit) || !SystemAPI.HasComponent<UnitCombatComponent>(unit))
                    continue;

                // Ranged units are excluded from target cap redistribution.
                if (SystemAPI.HasComponent<UnitRangedStatsComponent>(unit))
                    continue;

                var combat = SystemAPI.GetComponentRW<UnitCombatComponent>(unit);
                if (combat.ValueRO.target == Entity.Null)
                    continue;

                // Skip units already in attack range of their current target
                if (SystemAPI.HasComponent<UnitWeaponComponent>(unit)
                    && SystemAPI.HasComponent<LocalTransform>(unit)
                    && SystemAPI.HasComponent<LocalTransform>(combat.ValueRO.target))
                {
                    float3 uPos = SystemAPI.GetComponent<LocalTransform>(unit).Position;
                    float3 tPos = SystemAPI.GetComponent<LocalTransform>(combat.ValueRO.target).Position;
                    float atkRange = SystemAPI.GetComponent<UnitWeaponComponent>(unit).attackRange;
                    if (MeleeReach.InAttackRange(math.distance(uPos.xz, tPos.xz), atkRange, bodyRadius))
                        continue;
                }

                if (!meleeEnemyCounts.TryGetValue(combat.ValueRO.target, out int count) || count <= maxPerTarget)
                    continue;

                for (int j = 0; j < detected.Length; j++)
                {
                    DebugCandidateChecks++;
                    Entity candidate = detected[j].Value;
                    if (candidate == combat.ValueRO.target || (!_validTargets.Contains(candidate) || !SquadCombatModeComponent.AllowsTarget(EntityManager, unit, candidate)))
                        continue;

                    int current = 0;
                    meleeEnemyCounts.TryGetValue(candidate, out current);
                    if (current < maxPerTarget)
                    {
                        meleeEnemyCounts[combat.ValueRO.target] = count - 1;
                        combat.ValueRW.target = candidate;
                        DebugRetargetCount++;
                        meleeEnemyCounts.TryAdd(candidate, 0);
                        meleeEnemyCounts[candidate] = current + 1;
                        break;
                    }
                }
            }

            // Third pass: soft target spreading for ranged units.
            // If multiple targets are available, avoid over-focusing one target.
            // If there is only one practical target, concentration is allowed.
            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (!SystemAPI.Exists(unit)
                    || !SystemAPI.HasComponent<UnitCombatComponent>(unit)
                    || !SystemAPI.HasComponent<UnitRangedStatsComponent>(unit))
                    continue;

                var combat = SystemAPI.GetComponentRW<UnitCombatComponent>(unit);
                if (combat.ValueRO.target == Entity.Null)
                    continue;

                if (detected.Length == 0)
                    continue;

                // No dispersion needed when there is no real alternative.
                if (availableTargets <= 1)
                    continue;

                if (!rangedEnemyCounts.TryGetValue(combat.ValueRO.target, out int currentCount))
                    continue;

                int softCap = math.max(
                    1,
                    (int)math.ceil((float)rangedUnitsInSquad / availableTargets));

                // Small slack avoids excessive retarget churn.
                if (currentCount <= softCap + 1)
                    continue;

                Entity bestCandidate = combat.ValueRO.target;
                int bestCount = currentCount;

                for (int j = 0; j < detected.Length; j++)
                {
                    DebugCandidateChecks++;
                    Entity candidate = detected[j].Value;
                    if (candidate == combat.ValueRO.target
                        || candidate == Entity.Null
                        || (!_validTargets.Contains(candidate) || !SquadCombatModeComponent.AllowsTarget(EntityManager, unit, candidate)))
                        continue;

                    int candidateCount = 0;
                    rangedEnemyCounts.TryGetValue(candidate, out candidateCount);
                    if (candidateCount < bestCount)
                    {
                        bestCount = candidateCount;
                        bestCandidate = candidate;
                    }
                }

                // Reassign only when it actually improves spreading.
                if (bestCandidate != combat.ValueRO.target && bestCount + 1 < currentCount)
                {
                    rangedEnemyCounts[combat.ValueRO.target] = currentCount - 1;
                    rangedEnemyCounts.TryAdd(bestCandidate, 0);
                    rangedEnemyCounts[bestCandidate] = bestCount + 1;
                    combat.ValueRW.target = bestCandidate;
                    DebugRetargetCount++;
                }
            }

            // Fourth pass: sync IsEngagingTag so other systems can query engagement state
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
}

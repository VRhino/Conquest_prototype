// DEBUG ONLY — read-only recorder for the movement baseline. Never writes components.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine.AI;

/// <summary>
/// Samples formation state, slot error, combat contact and navigation counters every frame while recording.
/// Disabled until <see cref="Begin"/>; <see cref="End"/> writes CSV files under Logs/MovementMetrics
/// and returns the analysed summary. Used by the MovementBaseline PlayMode scenarios.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class MovementMetricsRecorderSystem : SystemBase
{
    // Two bodies of 0.5 m closer than 90 % of their combined radius count as overlapping (Movimiento_Formaciones §5.1).
    const float OverlapDistance = 0.9f;
    /// <summary>Squadmates overlap only when closer than this fraction of their formation spacing (Testudo is tight on purpose).</summary>
    const float SquadmateOverlapFraction = 0.9f;
    /// <summary>Two destinations closer than this fraction of the spacing send two soldiers to one cell.</summary>
    const float StackedFraction = 0.5f;
    /// <summary>A path longer than this multiple of the straight distance (plus the slack) is a detour around a wall.</summary>
    const float DetourFactor = 1.5f;
    const float DetourSlack = 2f;
    /// <summary>Search radius to put the hero and a destination on the NavMesh for the detour check, m.</summary>
    const float DestinationSampleRadius = 1f;

    static bool _recording;
    static string _label;
    static Entity _squadFilter;
    static readonly List<MovementUnitSample> UnitSamples = new();
    static readonly List<MovementFrameSample> FrameSamples = new();
    static readonly Dictionary<long, float> EnemyOverlapSince = new();

    ProfilerRecorder _formationStateTime;
    ProfilerRecorder _navMeshTime;
    ProfilerRecorder _followFormationTime;
    ProfilerRecorder _avoidanceTime;
    ProfilerRecorder _slotResolveTime;
    readonly List<(float3 position, int team, int index, int squad, float spacing, bool engaged)> _bodies = new();
    readonly Dictionary<int2, List<int>> _grid = new();
    readonly HashSet<long> _overlapNow = new();
    readonly HashSet<int> _focusUnits = new();
    readonly List<float3> _destinations = new();
    readonly NavMeshPath _path = new();

    public static bool IsRecording => _recording;

    /// <summary>Starts recording. With a squad filter only that squad is sampled; overlaps cover every unit.</summary>
    public static void Begin(string label, Entity squadFilter = default)
    {
        _label = label;
        _squadFilter = squadFilter;
        UnitSamples.Clear();
        FrameSamples.Clear();
        EnemyOverlapSince.Clear();
        _recording = true;
    }

    /// <summary>Stops recording, writes the CSV files and returns the summary.</summary>
    public static MovementMetricsSummary End(out string directory) =>
        End(MovementMetricsAnalyzer.ContactLimits.Default, out directory);

    public static MovementMetricsSummary End(MovementMetricsAnalyzer.ContactLimits limits, out string directory)
    {
        _recording = false;
        var summary = MovementMetricsAnalyzer.Analyze(_label, FrameSamples, UnitSamples, limits);
        directory = WriteFiles(summary);
        return summary;
    }

    protected override void OnCreate()
    {
        // Entities names each system's marker "<world name> <system name>".
        _formationStateTime = Recorder(nameof(UnitFormationStateSystem));
        _navMeshTime = Recorder(nameof(UnitNavMeshSystem));
        _followFormationTime = Recorder(nameof(UnitFollowFormationSystem));
        _avoidanceTime = Recorder("UnitAvoidanceSystem");
        _slotResolveTime = Recorder(nameof(FormationSlotResolveSystem));
    }

    ProfilerRecorder Recorder(string system) =>
        ProfilerRecorder.StartNew(ProfilerCategory.Scripts, $"{World.Name} {system}");

    protected override void OnDestroy()
    {
        _formationStateTime.Dispose();
        _navMeshTime.Dispose();
        _followFormationTime.Dispose();
        _avoidanceTime.Dispose();
        _slotResolveTime.Dispose();
    }

    static double Ms(ProfilerRecorder recorder) => recorder.Valid ? recorder.LastValue * 1e-6 : -1;

    protected override void OnUpdate()
    {
        if (!_recording) return;
        int frame = UnityEngine.Time.frameCount;
        float time = (float)SystemAPI.Time.ElapsedTime;
        float dt = SystemAPI.Time.DeltaTime;
        int setDestination = UnitNavMeshSystem.DebugSetDestinationCallsThisFrame;
        int projections = UnitNavMeshSystem.DebugProjectionRequestsThisFrame;
        int slotQueries = FormationSlotResolveSystem.DebugQueriesThisFrame;

        FillFocusUnits();
        CountOverlaps(time, out int allyOverlaps, out int enemyOverlaps, out int persistentEnemyOverlaps,
            out int persistentEngaged);

        foreach (var (units, squad) in SystemAPI.Query<DynamicBuffer<SquadUnitElement>>()
                     .WithAll<SquadFormationAnchorComponent>().WithEntityAccess())
        {
            if (_squadFilter != Entity.Null && squad != _squadFilter) continue;
            bool anchorMoving = SystemAPI.HasComponent<SquadAnchorMovingTag>(squad)
                && SystemAPI.IsComponentEnabled<SquadAnchorMovingTag>(squad);
            bool reacting = SystemAPI.HasComponent<SquadResolvedOrderComponent>(squad)
                && SystemAPI.GetComponent<SquadResolvedOrderComponent>(squad).source == OrderSource.CombatReaction;
            var anchorData = SystemAPI.GetComponent<SquadFormationAnchorComponent>(squad);
            float3 anchor = anchorData.position;
            float2 forward = math.normalizesafe(math.mul(anchorData.rotation, math.forward()).xz);
            CountDestinationProblems(squad, units, out int stacked, out int detours, out int relocated, out int unresolved);
            int templateIndex = SystemAPI.HasComponent<FormationComponent>(squad)
                ? SystemAPI.GetComponent<FormationComponent>(squad).templateIndex : 0;
            int alliedShared = CountAlliedSharedCells(squad);
            FrameSamples.Add(new MovementFrameSample
            {
                frame = frame, time = time, deltaTime = dt, squad = squad.Index,
                anchorMoving = anchorMoving, anchorX = anchor.x, anchorZ = anchor.z, forwardX = forward.x, forwardZ = forward.y,
                setDestinationCalls = setDestination, projectionRequests = projections,
                formationStateMs = Ms(_formationStateTime), navMeshMs = Ms(_navMeshTime),
                followFormationMs = Ms(_followFormationTime), avoidanceMs = Ms(_avoidanceTime),
                slotResolveMs = Ms(_slotResolveTime),
                allyOverlaps = allyOverlaps, enemyOverlaps = enemyOverlaps,
                persistentEnemyOverlaps = persistentEnemyOverlaps, persistentEnemyOverlapsEngaged = persistentEngaged,
                stackedDestinations = stacked, detourDestinations = detours,
                relocatedSlots = relocated, unresolvedSlots = unresolved, slotResolveQueries = slotQueries,
                templateIndex = templateIndex, alliedSharedCells = alliedShared
            });

            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (!SystemAPI.HasComponent<UnitFormationStateComponent>(unit)
                    || !SystemAPI.HasComponent<LocalTransform>(unit)
                    || !SystemAPI.HasComponent<UnitTargetPositionComponent>(unit))
                    continue;
                UnitSamples.Add(SampleUnit(unit, i, squad.Index, frame, time, reacting));
            }
        }
    }

    /// <summary>Where the unit is actually heading in formation: its slot after NavMesh projection.</summary>
    float3 MovementDestination(Entity unit)
    {
        float3 requested = SystemAPI.HasComponent<UnitResolvedSlotComponent>(unit)
            ? SystemAPI.GetComponent<UnitResolvedSlotComponent>(unit).position
            : SystemAPI.GetComponent<UnitTargetPositionComponent>(unit).position;
        return SquadNavigationSystem.GetEffectiveFormationDestination(EntityManager, unit, requested);
    }

    /// <summary>
    /// S7 checks on the squad's formation destinations: pairs closer than <see cref="StackedFraction"/> of the spacing
    /// (two soldiers sent to one cell) and destinations the hero only reaches with a detour (the far side of a wall).
    /// </summary>
    void CountDestinationProblems(Entity squad, DynamicBuffer<SquadUnitElement> units, out int stacked, out int detours,
        out int relocated, out int unresolved)
    {
        stacked = detours = relocated = unresolved = 0;
        _destinations.Clear();
        NavMeshQueryFilter filter = default;
        bool hasFilter = false;
        for (int i = 0; i < units.Length; i++)
        {
            Entity unit = units[i].Value;
            if (!SystemAPI.HasComponent<UnitTargetPositionComponent>(unit)) continue;
            _destinations.Add(MovementDestination(unit));
            if (SystemAPI.HasComponent<UnitResolvedSlotComponent>(unit))
            {
                var status = SystemAPI.GetComponent<UnitResolvedSlotComponent>(unit).status;
                if (status == FormationSlotResolveStatus.Relocated) relocated++;
                else if (status == FormationSlotResolveStatus.Unresolved) unresolved++;
            }
            if (hasFilter || !SystemAPI.ManagedAPI.HasComponent<NavMeshAgent>(unit)) continue;
            var agent = SystemAPI.ManagedAPI.GetComponent<NavMeshAgent>(unit);
            if (agent == null) continue;
            filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            hasFilter = true;
        }

        float spacing = SystemAPI.HasComponent<FormationComponent>(squad)
            ? SystemAPI.GetComponent<FormationComponent>(squad).spacing : MovementTuningComponent.ReferenceSpacing;
        float stackedSq = math.square(StackedFraction * spacing);
        for (int i = 0; i < _destinations.Count; i++)
        for (int j = i + 1; j < _destinations.Count; j++)
            if (math.distancesq(_destinations[i].xz, _destinations[j].xz) < stackedSq) stacked++;

        if (!hasFilter || !SystemAPI.HasComponent<HeroWorldPositionComponent>(squad)) return;
        float3 hero = SystemAPI.GetComponent<HeroWorldPositionComponent>(squad).position;
        if (!NavMesh.SamplePosition(hero, out var heroHit, DestinationSampleRadius, filter)) return;
        foreach (float3 destination in _destinations)
        {
            if (!NavMesh.SamplePosition(destination, out var hit, DestinationSampleRadius, filter)
                || !NavMesh.CalculatePath(heroHit.position, hit.position, filter, _path)
                || _path.status != NavMeshPathStatus.PathComplete)
            {
                detours++;
                continue;
            }
            float length = 0f;
            var corners = _path.corners;
            for (int c = 1; c < corners.Length; c++) length += UnityEngine.Vector3.Distance(corners[c - 1], corners[c]);
            if (length > DetourFactor * math.distance(heroHit.position, hit.position) + DetourSlack) detours++;
        }
    }

    /// <summary>
    /// Destination pairs of <paramref name="squad"/> (already in <see cref="_destinations"/>) and any allied squad closer
    /// than <see cref="StackedFraction"/> of the wider spacing: two squads sending soldiers to one cell (S5, F17).
    /// </summary>
    int CountAlliedSharedCells(Entity squad)
    {
        if (!SystemAPI.HasComponent<TeamComponent>(squad) || !SystemAPI.HasComponent<FormationComponent>(squad)) return 0;
        var team = SystemAPI.GetComponent<TeamComponent>(squad).value;
        float spacing = SystemAPI.GetComponent<FormationComponent>(squad).spacing;
        int shared = 0;
        foreach (var (units, otherTeam, formation, other) in SystemAPI
                     .Query<DynamicBuffer<SquadUnitElement>, RefRO<TeamComponent>, RefRO<FormationComponent>>().WithEntityAccess())
        {
            if (other == squad || otherTeam.ValueRO.value != team) continue;
            float sharedSq = math.square(StackedFraction * math.max(spacing, formation.ValueRO.spacing));
            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (!SystemAPI.HasComponent<UnitTargetPositionComponent>(unit)) continue;
                float2 destination = MovementDestination(unit).xz;
                foreach (float3 mine in _destinations)
                    if (math.distancesq(mine.xz, destination) < sharedSq) shared++;
            }
        }
        return shared;
    }

    MovementUnitSample SampleUnit(Entity unit, int bufferIndex, int squadIndex, int frame, float time, bool reacting)
    {
        float3 slot = MovementDestination(unit);
        var transform = SystemAPI.GetComponent<LocalTransform>(unit);
        float3 position = transform.Position;
        quaternion rotation = transform.Rotation;
        float speed = 0f;
        if (SystemAPI.ManagedAPI.HasComponent<NavMeshAgent>(unit))
        {
            var agent = SystemAPI.ManagedAPI.GetComponent<NavMeshAgent>(unit);
            if (agent != null)
            {
                rotation = agent.transform.rotation; // The rotation resolver writes the GameObject root.
                speed = math.length(new float2(agent.velocity.x, agent.velocity.z));
            }
        }
        float3 forward = math.mul(rotation, math.forward());

        int target = -1;
        float targetDistance = -1f;
        bool attacking = false;
        if (SystemAPI.HasComponent<UnitCombatComponent>(unit))
        {
            var combat = SystemAPI.GetComponent<UnitCombatComponent>(unit);
            attacking = combat.isAttacking;
            if (combat.target != Entity.Null && SystemAPI.HasComponent<LocalTransform>(combat.target))
            {
                target = combat.target.Index;
                targetDistance = math.distance(position.xz, SystemAPI.GetComponent<LocalTransform>(combat.target).Position.xz);
            }
        }

        return new MovementUnitSample
        {
            frame = frame, time = time, squad = squadIndex,
            // Entity index: slot indices change when a new layout re-deals slots by distance.
            unit = unit.Index,
            state = SystemAPI.GetComponent<UnitFormationStateComponent>(unit).State,
            slotDistance = math.distance(position.xz, slot.xz),
            x = position.x, z = position.z, yaw = math.atan2(forward.x, forward.z),
            team = SystemAPI.HasComponent<TeamComponent>(unit) ? (int)SystemAPI.GetComponent<TeamComponent>(unit).value : -1,
            target = target, targetDistance = targetDistance, attacking = attacking, reacting = reacting,
            gear = SystemAPI.HasComponent<UnitMovementGearComponent>(unit)
                ? (int)SystemAPI.GetComponent<UnitMovementGearComponent>(unit).gear : -1,
            speed = speed, destX = slot.x, destZ = slot.z,
            row = SystemAPI.HasComponent<UnitGridSlotComponent>(unit) ? SystemAPI.GetComponent<UnitGridSlotComponent>(unit).row : -1
        };
    }

    /// <summary>Entity indices of the filtered squad; empty records every unit.</summary>
    void FillFocusUnits()
    {
        _focusUnits.Clear();
        if (_squadFilter == Entity.Null || !SystemAPI.HasBuffer<SquadUnitElement>(_squadFilter)) return;
        foreach (var element in SystemAPI.GetBuffer<SquadUnitElement>(_squadFilter))
            _focusUnits.Add(element.Value.Index);
    }

    /// <summary>
    /// Body overlaps with a 1 m grid. With a squad filter only pairs that include one of its units count, so other
    /// squads (idle at their spawn, reserves) do not pollute the measured scenario.
    /// </summary>
    void CountOverlaps(float time, out int ally, out int enemy, out int persistentEnemy, out int persistentEngaged)
    {
        ally = enemy = persistentEnemy = persistentEngaged = 0;
        _bodies.Clear();
        foreach (var cell in _grid.Values) cell.Clear();
        foreach (var (team, transform, entity) in SystemAPI.Query<RefRO<TeamComponent>, RefRO<LocalTransform>>()
                     .WithAll<UnitFormationStateComponent>().WithEntityAccess())
        {
            float3 p = transform.ValueRO.Position;
            int index = _bodies.Count;
            Entity squad = SystemAPI.HasComponent<UnitOwnerComponent>(entity)
                ? SystemAPI.GetComponent<UnitOwnerComponent>(entity).squad : Entity.Null;
            float spacing = squad != Entity.Null && SystemAPI.HasComponent<FormationComponent>(squad)
                ? SystemAPI.GetComponent<FormationComponent>(squad).spacing : MovementTuningComponent.ReferenceSpacing;
            bool engaged = SystemAPI.HasComponent<UnitCombatComponent>(entity)
                && SystemAPI.GetComponent<UnitCombatComponent>(entity).target != Entity.Null;
            _bodies.Add((p, (int)team.ValueRO.value, entity.Index, squad.Index, spacing, engaged));
            var key = new int2((int)math.floor(p.x), (int)math.floor(p.z));
            if (!_grid.TryGetValue(key, out var list)) _grid[key] = list = new List<int>();
            list.Add(index);
        }

        _overlapNow.Clear();
        float limitSq = OverlapDistance * OverlapDistance;
        for (int i = 0; i < _bodies.Count; i++)
        {
            var a = _bodies[i];
            int cx = (int)math.floor(a.position.x), cz = (int)math.floor(a.position.z);
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (!_grid.TryGetValue(new int2(cx + dx, cz + dz), out var list)) continue;
                foreach (int j in list)
                {
                    if (j <= i) continue;
                    var b = _bodies[j];
                    float distSq = math.distancesq(a.position.xz, b.position.xz);
                    if (distSq >= limitSq) continue;
                    if (a.squad == b.squad && distSq >= math.square(SquadmateOverlapFraction * math.min(a.spacing, b.spacing)))
                        continue;
                    bool involvesFocus = _focusUnits.Count == 0 || _focusUnits.Contains(a.index) || _focusUnits.Contains(b.index);
                    if (!involvesFocus) continue;
                    if (a.team == b.team) { ally++; continue; }
                    enemy++;
                    long key = ((long)math.min(a.index, b.index) << 32) | (uint)math.max(a.index, b.index);
                    _overlapNow.Add(key);
                    if (!EnemyOverlapSince.TryGetValue(key, out float since)) EnemyOverlapSince[key] = since = time;
                    if (time - since > MovementMetricsAnalyzer.EnemyOverlapPersistence)
                    {
                        persistentEnemy++;
                        if (a.engaged || b.engaged) persistentEngaged++;
                    }
                }
            }
        }
        // Forget pairs that are no longer overlapping.
        var stale = new List<long>();
        foreach (var key in EnemyOverlapSince.Keys) if (!_overlapNow.Contains(key)) stale.Add(key);
        foreach (var key in stale) EnemyOverlapSince.Remove(key);
    }

    static string WriteFiles(MovementMetricsSummary summary)
    {
        var c = CultureInfo.InvariantCulture;
        string stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss", c);
        string directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "MovementMetrics");
        Directory.CreateDirectory(directory);
        string prefix = Path.Combine(directory, $"{summary.label}_{stamp}");

        var frames = new StringBuilder("frame,time,dt,squad,anchorMoving,anchorX,anchorZ,forwardX,forwardZ,setDestination,projections,formationStateMs,navMeshMs,followFormationMs,avoidanceMs,allyOverlaps,enemyOverlaps,persistentEnemyOverlaps,persistentEnemyOverlapsEngaged,stackedDestinations,detourDestinations,relocatedSlots,unresolvedSlots,slotResolveQueries,templateIndex,alliedSharedCells\n");
        foreach (var f in FrameSamples)
            frames.AppendLine(string.Join(",", f.frame, f.time.ToString("F4", c), f.deltaTime.ToString("F4", c), f.squad,
                f.anchorMoving ? 1 : 0, f.anchorX.ToString("F3", c), f.anchorZ.ToString("F3", c),
                f.forwardX.ToString("F3", c), f.forwardZ.ToString("F3", c),
                f.setDestinationCalls, f.projectionRequests,
                f.formationStateMs.ToString("F4", c), f.navMeshMs.ToString("F4", c),
                f.followFormationMs.ToString("F4", c), f.avoidanceMs.ToString("F4", c),
                f.allyOverlaps, f.enemyOverlaps, f.persistentEnemyOverlaps, f.persistentEnemyOverlapsEngaged, f.stackedDestinations, f.detourDestinations,
                f.relocatedSlots, f.unresolvedSlots, f.slotResolveQueries, f.templateIndex, f.alliedSharedCells));
        File.WriteAllText(prefix + "_frames.csv", frames.ToString());

        var units = new StringBuilder("frame,time,squad,unit,state,slotDistance,x,z,yaw,team,target,targetDistance,attacking,reacting,gear,speed,destX,destZ,row\n");
        foreach (var u in UnitSamples)
            units.AppendLine(string.Join(",", u.frame, u.time.ToString("F4", c), u.squad, u.unit, u.state,
                u.slotDistance.ToString("F4", c), u.x.ToString("F3", c), u.z.ToString("F3", c), u.yaw.ToString("F4", c),
                u.team, u.target, u.targetDistance.ToString("F3", c), u.attacking ? 1 : 0, u.reacting ? 1 : 0,
                u.gear, u.speed.ToString("F3", c), u.destX.ToString("F3", c), u.destZ.ToString("F3", c), u.row));
        File.WriteAllText(prefix + "_units.csv", units.ToString());
        File.WriteAllText(prefix + "_summary.txt", summary.ToString());
        File.WriteAllText(prefix + "_summary.json", UnityEngine.JsonUtility.ToJson(summary, true)); // For medians (M10).
        return directory;
    }
}
#endif

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
    readonly List<(float3 position, int team, int index)> _bodies = new();
    readonly Dictionary<int2, List<int>> _grid = new();
    readonly HashSet<long> _overlapNow = new();
    readonly HashSet<int> _focusUnits = new();

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
    }

    ProfilerRecorder Recorder(string system) =>
        ProfilerRecorder.StartNew(ProfilerCategory.Scripts, $"{World.Name} {system}");

    protected override void OnDestroy()
    {
        _formationStateTime.Dispose();
        _navMeshTime.Dispose();
        _followFormationTime.Dispose();
        _avoidanceTime.Dispose();
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

        FillFocusUnits();
        CountOverlaps(time, out int allyOverlaps, out int enemyOverlaps, out int persistentEnemyOverlaps);

        foreach (var (units, squad) in SystemAPI.Query<DynamicBuffer<SquadUnitElement>>()
                     .WithAll<SquadFormationAnchorComponent>().WithEntityAccess())
        {
            if (_squadFilter != Entity.Null && squad != _squadFilter) continue;
            bool anchorMoving = SystemAPI.HasComponent<SquadAnchorMovingTag>(squad)
                && SystemAPI.IsComponentEnabled<SquadAnchorMovingTag>(squad);
            bool reacting = SystemAPI.HasComponent<SquadResolvedOrderComponent>(squad)
                && SystemAPI.GetComponent<SquadResolvedOrderComponent>(squad).source == OrderSource.CombatReaction;
            float3 anchor = SystemAPI.GetComponent<SquadFormationAnchorComponent>(squad).position;
            FrameSamples.Add(new MovementFrameSample
            {
                frame = frame, time = time, deltaTime = dt, squad = squad.Index,
                anchorMoving = anchorMoving, anchorX = anchor.x, anchorZ = anchor.z,
                setDestinationCalls = setDestination, projectionRequests = projections,
                formationStateMs = Ms(_formationStateTime), navMeshMs = Ms(_navMeshTime),
                followFormationMs = Ms(_followFormationTime), avoidanceMs = Ms(_avoidanceTime),
                allyOverlaps = allyOverlaps, enemyOverlaps = enemyOverlaps,
                persistentEnemyOverlaps = persistentEnemyOverlaps
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

    MovementUnitSample SampleUnit(Entity unit, int bufferIndex, int squadIndex, int frame, float time, bool reacting)
    {
        float3 requested = SystemAPI.GetComponent<UnitTargetPositionComponent>(unit).position;
        float3 slot = SquadNavigationSystem.GetEffectiveFormationDestination(EntityManager, unit, requested);
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
            unit = SystemAPI.HasComponent<UnitGridSlotComponent>(unit)
                ? SystemAPI.GetComponent<UnitGridSlotComponent>(unit).slotIndex : bufferIndex,
            state = SystemAPI.GetComponent<UnitFormationStateComponent>(unit).State,
            slotDistance = math.distance(position.xz, slot.xz),
            x = position.x, z = position.z, yaw = math.atan2(forward.x, forward.z),
            team = SystemAPI.HasComponent<TeamComponent>(unit) ? (int)SystemAPI.GetComponent<TeamComponent>(unit).value : -1,
            target = target, targetDistance = targetDistance, attacking = attacking, reacting = reacting,
            gear = -1, speed = speed
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
    void CountOverlaps(float time, out int ally, out int enemy, out int persistentEnemy)
    {
        ally = enemy = persistentEnemy = 0;
        _bodies.Clear();
        foreach (var cell in _grid.Values) cell.Clear();
        foreach (var (team, transform, entity) in SystemAPI.Query<RefRO<TeamComponent>, RefRO<LocalTransform>>()
                     .WithAll<UnitFormationStateComponent>().WithEntityAccess())
        {
            float3 p = transform.ValueRO.Position;
            int index = _bodies.Count;
            _bodies.Add((p, (int)team.ValueRO.value, entity.Index));
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
                    if (math.distancesq(a.position.xz, b.position.xz) >= limitSq) continue;
                    bool involvesFocus = _focusUnits.Count == 0 || _focusUnits.Contains(a.index) || _focusUnits.Contains(b.index);
                    if (!involvesFocus) continue;
                    if (a.team == b.team) { ally++; continue; }
                    enemy++;
                    long key = ((long)math.min(a.index, b.index) << 32) | (uint)math.max(a.index, b.index);
                    _overlapNow.Add(key);
                    if (!EnemyOverlapSince.TryGetValue(key, out float since)) EnemyOverlapSince[key] = since = time;
                    if (time - since > MovementMetricsAnalyzer.EnemyOverlapPersistence) persistentEnemy++;
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

        var frames = new StringBuilder("frame,time,dt,squad,anchorMoving,anchorX,anchorZ,setDestination,projections,formationStateMs,navMeshMs,followFormationMs,avoidanceMs,allyOverlaps,enemyOverlaps,persistentEnemyOverlaps\n");
        foreach (var f in FrameSamples)
            frames.AppendLine(string.Join(",", f.frame, f.time.ToString("F4", c), f.deltaTime.ToString("F4", c), f.squad,
                f.anchorMoving ? 1 : 0, f.anchorX.ToString("F3", c), f.anchorZ.ToString("F3", c),
                f.setDestinationCalls, f.projectionRequests,
                f.formationStateMs.ToString("F4", c), f.navMeshMs.ToString("F4", c),
                f.followFormationMs.ToString("F4", c), f.avoidanceMs.ToString("F4", c),
                f.allyOverlaps, f.enemyOverlaps, f.persistentEnemyOverlaps));
        File.WriteAllText(prefix + "_frames.csv", frames.ToString());

        var units = new StringBuilder("frame,time,squad,unit,state,slotDistance,x,z,yaw,team,target,targetDistance,attacking,reacting,gear,speed\n");
        foreach (var u in UnitSamples)
            units.AppendLine(string.Join(",", u.frame, u.time.ToString("F4", c), u.squad, u.unit, u.state,
                u.slotDistance.ToString("F4", c), u.x.ToString("F3", c), u.z.ToString("F3", c), u.yaw.ToString("F4", c),
                u.team, u.target, u.targetDistance.ToString("F3", c), u.attacking ? 1 : 0, u.reacting ? 1 : 0,
                u.gear, u.speed.ToString("F3", c)));
        File.WriteAllText(prefix + "_units.csv", units.ToString());
        File.WriteAllText(prefix + "_summary.txt", summary.ToString());
        return directory;
    }
}
#endif

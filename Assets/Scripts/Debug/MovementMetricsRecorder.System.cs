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

/// <summary>
/// Samples formation state, slot error and navigation counters every frame while recording.
/// Disabled until <see cref="Begin"/>; <see cref="End"/> writes CSV files under Logs/MovementMetrics
/// and returns the analysed summary. Used by the MovementBaseline PlayMode scenarios.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class MovementMetricsRecorderSystem : SystemBase
{
    static bool _recording;
    static string _label;
    static Entity _squadFilter;
    static readonly List<MovementUnitSample> UnitSamples = new();
    static readonly List<MovementFrameSample> FrameSamples = new();

    ProfilerRecorder _formationStateTime;
    ProfilerRecorder _navMeshTime;

    public static bool IsRecording => _recording;

    /// <summary>Starts recording. With a squad filter only that squad is sampled.</summary>
    public static void Begin(string label, Entity squadFilter = default)
    {
        _label = label;
        _squadFilter = squadFilter;
        UnitSamples.Clear();
        FrameSamples.Clear();
        _recording = true;
    }

    /// <summary>Stops recording, writes the CSV files and returns the summary.</summary>
    public static MovementMetricsSummary End(out string directory)
    {
        _recording = false;
        var summary = MovementMetricsAnalyzer.Analyze(_label, FrameSamples, UnitSamples);
        directory = WriteFiles(summary);
        return summary;
    }

    protected override void OnCreate()
    {
        // Entities names each system's marker "<world name> <system name>".
        _formationStateTime = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, $"{World.Name} {nameof(UnitFormationStateSystem)}");
        _navMeshTime = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, $"{World.Name} {nameof(UnitNavMeshSystem)}");
    }

    protected override void OnDestroy()
    {
        _formationStateTime.Dispose();
        _navMeshTime.Dispose();
    }

    protected override void OnUpdate()
    {
        if (!_recording) return;
        int frame = UnityEngine.Time.frameCount;
        float time = (float)SystemAPI.Time.ElapsedTime;
        float dt = SystemAPI.Time.DeltaTime;
        int setDestination = UnitNavMeshSystem.DebugSetDestinationCallsThisFrame;
        int projections = UnitNavMeshSystem.DebugProjectionRequestsThisFrame;
        double stateMs = _formationStateTime.Valid ? _formationStateTime.LastValue * 1e-6 : -1;
        double navMs = _navMeshTime.Valid ? _navMeshTime.LastValue * 1e-6 : -1;

        foreach (var (units, squad) in SystemAPI.Query<DynamicBuffer<SquadUnitElement>>()
                     .WithAll<SquadFormationAnchorComponent>().WithEntityAccess())
        {
            if (_squadFilter != Entity.Null && squad != _squadFilter) continue;
            bool anchorMoving = SystemAPI.HasComponent<SquadAnchorMovingTag>(squad)
                && SystemAPI.IsComponentEnabled<SquadAnchorMovingTag>(squad);
            float3 anchor = SystemAPI.GetComponent<SquadFormationAnchorComponent>(squad).position;
            FrameSamples.Add(new MovementFrameSample
            {
                frame = frame, time = time, deltaTime = dt, squad = squad.Index,
                anchorMoving = anchorMoving, anchorX = anchor.x, anchorZ = anchor.z,
                setDestinationCalls = setDestination,
                projectionRequests = projections, formationStateMs = stateMs, navMeshMs = navMs
            });

            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (!SystemAPI.HasComponent<UnitFormationStateComponent>(unit)
                    || !SystemAPI.HasComponent<LocalTransform>(unit)
                    || !SystemAPI.HasComponent<UnitTargetPositionComponent>(unit))
                    continue;
                float3 requested = SystemAPI.GetComponent<UnitTargetPositionComponent>(unit).position;
                float3 slot = SquadNavigationSystem.GetEffectiveFormationDestination(EntityManager, unit, requested);
                float3 position = SystemAPI.GetComponent<LocalTransform>(unit).Position;
                int slotIndex = SystemAPI.HasComponent<UnitGridSlotComponent>(unit)
                    ? SystemAPI.GetComponent<UnitGridSlotComponent>(unit).slotIndex : i;
                UnitSamples.Add(new MovementUnitSample
                {
                    frame = frame, time = time, squad = squad.Index, unit = slotIndex,
                    state = SystemAPI.GetComponent<UnitFormationStateComponent>(unit).State,
                    slotDistance = math.distance(position.xz, slot.xz)
                });
            }
        }
    }

    static string WriteFiles(MovementMetricsSummary summary)
    {
        var c = CultureInfo.InvariantCulture;
        string stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss", c);
        string directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "MovementMetrics");
        Directory.CreateDirectory(directory);
        string prefix = Path.Combine(directory, $"{summary.label}_{stamp}");

        var frames = new StringBuilder("frame,time,dt,squad,anchorMoving,anchorX,anchorZ,setDestination,projections,formationStateMs,navMeshMs\n");
        foreach (var f in FrameSamples)
            frames.AppendLine(string.Join(",", f.frame, f.time.ToString("F4", c), f.deltaTime.ToString("F4", c), f.squad,
                f.anchorMoving ? 1 : 0, f.anchorX.ToString("F3", c), f.anchorZ.ToString("F3", c),
                f.setDestinationCalls, f.projectionRequests,
                f.formationStateMs.ToString("F4", c), f.navMeshMs.ToString("F4", c)));
        File.WriteAllText(prefix + "_frames.csv", frames.ToString());

        var units = new StringBuilder("frame,time,squad,unit,state,slotDistance\n");
        foreach (var u in UnitSamples)
            units.AppendLine(string.Join(",", u.frame, u.time.ToString("F4", c), u.squad, u.unit, u.state,
                u.slotDistance.ToString("F4", c)));
        File.WriteAllText(prefix + "_units.csv", units.ToString());
        File.WriteAllText(prefix + "_summary.txt", summary.ToString());
        return directory;
    }
}
#endif

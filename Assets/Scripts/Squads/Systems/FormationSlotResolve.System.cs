using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.AI;

/// <summary>
/// Single writer of <see cref="UnitResolvedSlotComponent"/>: checks each squad's formation slots against the NavMesh
/// (C13, L3-01, decision D9) with <see cref="FormationSlotResolver"/>. A blocked or taken slot moves to the nearest
/// free reachable cell; the template and the order are never rejected. The NavMesh is queried once per squad when a
/// template slot has moved more than <c>slotResolveRevalidateDistance</c> since the last validation (anchor, formation
/// or slot change), never per unit or per frame; in between, each slot carries its last correction.
///
/// Pipeline: FormationSystem → GridFormationUpdateSystem (template) → [FormationSlotResolveSystem] →
/// UnitFormationStateSystem / SquadNavigationSystem / UnitNavMeshSystem (read the resolved slot).
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(GridFormationUpdateSystem))]
[UpdateBefore(typeof(UnitFormationStateSystem))]
[UpdateBefore(typeof(SquadNavigationSystem))]
public partial class FormationSlotResolveSystem : SystemBase
{
    readonly FormationSlotResolver _resolver = new();
    readonly List<FormationSlotRequest> _requests = new();
    readonly List<FormationSlotResolution> _results = new();
    readonly List<Entity> _units = new();

    static int _queryFrame = -1;
    static int _queriesThisFrame;
    /// <summary>Debug-only count of NavMesh queries made by slot validation (movement metrics).</summary>
    public static long DebugTotalQueries { get; private set; }

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCounters()
    {
        _queryFrame = -1;
        _queriesThisFrame = 0;
        DebugTotalQueries = 0;
    }

    public static int DebugQueriesThisFrame
    {
        get { return _queryFrame == UnityEngine.Time.frameCount ? _queriesThisFrame : 0; }
    }

    static void CountQueries(int count)
    {
        int frame = UnityEngine.Time.frameCount;
        if (_queryFrame != frame)
        {
            _queryFrame = frame;
            _queriesThisFrame = 0;
        }
        _queriesThisFrame += count;
        DebugTotalQueries += count;
    }

    /// <summary>Answers the resolver with the NavMesh of the squad's agent type.</summary>
    struct NavMeshProbe : IFormationNavProbe
    {
        /// <summary>Height difference below which a projection counts as vertical (NavMesh voxel scale), m.</summary>
        const float SurfaceHeightTolerance = 0.05f;

        public NavMeshQueryFilter filter;
        public int queries;

        public bool TrySnap(float3 point, float radius, out float3 snapped)
        {
            queries++;
            bool found = NavMesh.SamplePosition(point, out NavMeshHit hit, radius, filter);
            snapped = hit.position;
            // The nearest point is nearest in 3D: with the NavMesh above or below the ground point it also slides
            // sideways. Asking again from the surface height gives the point straight above or below.
            if (found && math.abs(snapped.y - point.y) > SurfaceHeightTolerance)
            {
                queries++;
                if (NavMesh.SamplePosition(new float3(point.x, snapped.y, point.z), out hit, radius, filter))
                    snapped = hit.position;
            }
            return found;
        }

        public bool Raycast(float3 from, float3 to, out float3 hit)
        {
            queries++;
            bool blocked = NavMesh.Raycast(from, to, out NavMeshHit navHit, filter);
            hit = navHit.position;
            return blocked;
        }
    }

    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
    }

    protected override void OnUpdate()
    {
        var tuning = SystemAPI.HasSingleton<MovementTuningComponent>()
            ? SystemAPI.GetSingleton<MovementTuningComponent>() : MovementTuningComponent.Default;
        // The wide projection radius places the formation origin (anchor or hero) on the NavMesh.
        float originRadius = SystemAPI.HasSingleton<SquadSpawnConfigComponent>()
            ? math.max(0.01f, SystemAPI.GetSingleton<SquadSpawnConfigComponent>().navMeshDestinationSampleRadius) : 2f;
        float revalidateSq = math.square(math.max(0.01f, tuning.slotResolveRevalidateDistance));

        foreach (var (units, formation, anchor, squadState, squad) in SystemAPI
                     .Query<DynamicBuffer<SquadUnitElement>, RefRO<FormationComponent>,
                            RefRO<SquadFormationAnchorComponent>, RefRO<SquadStateComponent>>()
                     .WithEntityAccess())
        {
            _units.Clear();
            bool revalidate = false;
            NavMeshAgent agent = null;
            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (!SystemAPI.Exists(unit) || !SystemAPI.HasComponent<UnitTargetPositionComponent>(unit)
                    || !SystemAPI.HasComponent<UnitResolvedSlotComponent>(unit)
                    || !SystemAPI.HasComponent<UnitGridSlotComponent>(unit))
                    continue;
                _units.Add(unit);
                var resolved = SystemAPI.GetComponent<UnitResolvedSlotComponent>(unit);
                float3 template = SystemAPI.GetComponent<UnitTargetPositionComponent>(unit).position;
                revalidate |= resolved.status == FormationSlotResolveStatus.Pending
                              || math.distancesq(template, resolved.validatedTemplate) > revalidateSq;
                if (agent == null && SystemAPI.ManagedAPI.HasComponent<NavMeshAgent>(unit))
                {
                    var candidate = SystemAPI.ManagedAPI.GetComponent<NavMeshAgent>(unit);
                    if (candidate != null && candidate.enabled && candidate.isOnNavMesh) agent = candidate;
                }
            }
            if (_units.Count == 0) continue;

            if (!tuning.slotResolveEnabled || agent == null)
                PassThrough();
            else if (revalidate)
                Validate(agent, squad, formation.ValueRO.spacing, anchor.ValueRO, squadState.ValueRO, tuning, originRadius);
            else
                CarryCorrections();
        }
    }

    /// <summary>Not validated: the slot is the template and navigation projects it as before C13.</summary>
    void PassThrough()
    {
        foreach (var unit in _units)
        {
            float3 template = SystemAPI.GetComponent<UnitTargetPositionComponent>(unit).position;
            SystemAPI.SetComponent(unit, new UnitResolvedSlotComponent
            {
                position = template, validatedTemplate = template, status = FormationSlotResolveStatus.Pending
            });
        }
    }

    /// <summary>Between validations each slot follows its template with the correction found last time.</summary>
    void CarryCorrections()
    {
        foreach (var unit in _units)
        {
            var resolved = SystemAPI.GetComponentRW<UnitResolvedSlotComponent>(unit);
            float3 position = SystemAPI.GetComponent<UnitTargetPositionComponent>(unit).position + resolved.ValueRO.correction;
            if (!math.all(resolved.ValueRO.position == position)) resolved.ValueRW.position = position;
        }
    }

    void Validate(NavMeshAgent agent, Entity squad, float spacing, in SquadFormationAnchorComponent anchor,
        in SquadStateComponent state, in MovementTuningComponent tuning, float originRadius)
    {
        var probe = new NavMeshProbe
        {
            filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask }
        };
        SquadHoldPositionComponent? hold = SystemAPI.HasComponent<SquadHoldPositionComponent>(squad)
            ? SystemAPI.GetComponent<SquadHoldPositionComponent>(squad) : null;
        float3 center = FormationPositionCalculator.GetSquadCenter(state, hold, anchor.position);
        // While the formation follows the hero, reach its centre from the hero so a centre inside a wall stays on
        // the hero's side; holding or retreating formations are not tied to the hero.
        bool following = state.currentOrder != SquadOrderType.HoldPosition
                         && state.currentState != SquadFSMState.Retreating
                         && SystemAPI.HasComponent<HeroWorldPositionComponent>(squad);
        float3 leader = following ? SystemAPI.GetComponent<HeroWorldPositionComponent>(squad).position : float3.zero;

        _requests.Clear();
        foreach (var unit in _units)
            _requests.Add(new FormationSlotRequest
            {
                template = SystemAPI.GetComponent<UnitTargetPositionComponent>(unit).position,
                localOffset = SystemAPI.GetComponent<UnitGridSlotComponent>(unit).localOffset.xz,
                previousShift = SystemAPI.GetComponent<UnitResolvedSlotComponent>(unit).shift,
                unitPosition = SystemAPI.HasComponent<LocalTransform>(unit)
                    ? SystemAPI.GetComponent<LocalTransform>(unit).Position
                    : SystemAPI.GetComponent<UnitTargetPositionComponent>(unit).position,
            });

        if (FormationSlotResolver.TryOrigin(ref probe, center, following, leader, originRadius, out float3 origin))
            _resolver.Resolve(ref probe, origin, anchor.rotation, new FormationSlotResolver.Settings
            {
                spacing = spacing,
                snapRadius = tuning.slotSnapRadiusMax,
                searchCells = tuning.slotResolveSearchCells,
            }, _requests, _results);
        else
        {
            // No NavMesh near the formation: every slot falls back to the template.
            _results.Clear();
            foreach (var request in _requests)
                _results.Add(new FormationSlotResolution
                    { position = request.template, status = FormationSlotResolveStatus.Unresolved });
        }
        CountQueries(probe.queries);

        for (int i = 0; i < _units.Count; i++)
        {
            float3 template = _requests[i].template;
            var result = _results[i];
            // A slot that passed keeps its template exactly, so open ground moves as before C13. A relocated slot takes
            // the new cell at ground height, like a template. Navigation puts either on the NavMesh within its cell.
            float3 position = result.status == FormationSlotResolveStatus.Relocated
                ? new float3(result.position.x,
                    FormationPositionCalculator.calculateTerraindHeight(new float3(result.position.x, template.y, result.position.z)),
                    result.position.z)
                : template;
            SystemAPI.SetComponent(_units[i], new UnitResolvedSlotComponent
            {
                position = position,
                validatedTemplate = template,
                correction = position - template,
                shift = result.shift,
                status = result.status,
            });
        }
    }
}

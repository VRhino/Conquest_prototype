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
/// Squads run in a fixed order, the lowest entity index first (F17): the cells of an earlier allied squad count as
/// occupied, and a squad validates again whenever an earlier ally close to it has validated. Next to an ally that moves
/// differently, both validate more often and its cells take in how far apart they can drift in between, so no cell is
/// shared between validations. Allies marching together have nothing to drift: they keep the plain distances.
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
    readonly List<Entity> _squads = new();
    readonly List<ResolvedSquad> _entries = new();
    readonly List<float2> _occupied = new();
    readonly Dictionary<Entity, float3> _lastAnchors = new();

    /// <summary>Below this a squad counts as still, so anchor noise never reads as motion, m/s.</summary>
    const float StillSpeed = 0.1f;

    /// <summary>A squad this frame: what its allies need to keep off its cells (F17).</summary>
    struct ResolvedSquad
    {
        public Entity squad;
        public Team team;
        public float spacing;
        public float3 velocity;
        public FormationSlotFootprint footprint;
        public bool validated;
    }

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

        _squads.Clear();
        foreach (var (_, squad) in SystemAPI.Query<RefRO<FormationComponent>>()
                     .WithAll<SquadUnitElement, SquadFormationAnchorComponent, SquadStateComponent>().WithEntityAccess())
            _squads.Add(squad);
        _squads.Sort();
        _entries.Clear();
        foreach (Entity squad in _squads) _entries.Add(DescribeSquad(squad));

        for (int s = 0; s < _squads.Count; s++)
        {
            Entity squad = _squads[s];
            var entry = _entries[s];
            float limitSq = math.min(revalidateSq, math.square(NearestAllyDrift(s, tuning)));
            var units = SystemAPI.GetBuffer<SquadUnitElement>(squad);
            _units.Clear();
            bool revalidate = false;
            NavMeshAgent agent = null;
            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (!HasSlot(unit)) continue;
                _units.Add(unit);
                var resolved = SystemAPI.GetComponent<UnitResolvedSlotComponent>(unit);
                float3 template = SystemAPI.GetComponent<UnitTargetPositionComponent>(unit).position;
                revalidate |= resolved.status == FormationSlotResolveStatus.Pending
                              || math.distancesq(template, resolved.validatedTemplate) > limitSq;
                if (agent == null && SystemAPI.ManagedAPI.HasComponent<NavMeshAgent>(unit))
                {
                    var candidate = SystemAPI.ManagedAPI.GetComponent<NavMeshAgent>(unit);
                    if (candidate != null && candidate.enabled && candidate.isOnNavMesh) agent = candidate;
                }
            }
            if (_units.Count == 0) continue;

            revalidate |= AllyNearbyValidated(s, tuning);
            if (!tuning.slotResolveEnabled || agent == null)
                PassThrough();
            else if (revalidate)
            {
                Validate(agent, s, ref entry, SystemAPI.GetComponent<SquadFormationAnchorComponent>(squad),
                    SystemAPI.GetComponent<SquadStateComponent>(squad), tuning, originRadius);
                entry.validated = true;
            }
            else
                CarryCorrections();
            _entries[s] = entry;
        }
    }

    bool HasSlot(Entity unit) =>
        SystemAPI.Exists(unit) && SystemAPI.HasComponent<UnitTargetPositionComponent>(unit)
        && SystemAPI.HasComponent<UnitResolvedSlotComponent>(unit) && SystemAPI.HasComponent<UnitGridSlotComponent>(unit);

    ResolvedSquad DescribeSquad(Entity squad)
    {
        var entry = new ResolvedSquad
        {
            squad = squad,
            team = SystemAPI.HasComponent<TeamComponent>(squad) ? SystemAPI.GetComponent<TeamComponent>(squad).value : Team.None,
            spacing = SystemAPI.GetComponent<FormationComponent>(squad).spacing,
            footprint = FormationSlotFootprint.Empty,
        };
        float3 anchor = SystemAPI.GetComponent<SquadFormationAnchorComponent>(squad).position;
        float deltaTime = SystemAPI.Time.DeltaTime;
        if (deltaTime > 0f && _lastAnchors.TryGetValue(squad, out float3 last)) entry.velocity = (anchor - last) / deltaTime;
        _lastAnchors[squad] = anchor;
        var units = SystemAPI.GetBuffer<SquadUnitElement>(squad);
        for (int i = 0; i < units.Length; i++)
        {
            if (!HasSlot(units[i].Value)) continue;
            entry.footprint.Include(SystemAPI.GetComponent<UnitTargetPositionComponent>(units[i].Value).position);
            entry.footprint.Include(SystemAPI.GetComponent<UnitResolvedSlotComponent>(units[i].Value).position);
        }
        return entry;
    }

    /// <summary>How far apart two squads drift per metre the faster one walks: 0 together, 1 past a still one, 2 head on.</summary>
    static float RelativeDrift(in ResolvedSquad a, in ResolvedSquad b)
    {
        float speed = math.max(math.length(a.velocity.xz), math.length(b.velocity.xz));
        return speed < StillSpeed ? 0f : math.length((a.velocity - b.velocity).xz) / speed;
    }

    /// <summary>Distance a squad may walk between validations so these two drift apart no more than the ally budget.</summary>
    static float AllowedDrift(in ResolvedSquad a, in ResolvedSquad b, in MovementTuningComponent tuning)
    {
        float budget = 2f * tuning.slotResolveAllyRevalidateDistance;
        float relative = RelativeDrift(a, b);
        return relative * tuning.slotResolveRevalidateDistance <= budget ? tuning.slotResolveRevalidateDistance : budget / relative;
    }

    /// <summary>
    /// How close two squads' cells may stand at a validation: a shared cell, plus how far apart they drift before the next.
    /// </summary>
    static float OccupiedRadius(in ResolvedSquad a, in ResolvedSquad b, in MovementTuningComponent tuning) =>
        FormationSlotResolver.SharedCellFraction * math.max(a.spacing, b.spacing)
        + RelativeDrift(a, b) * AllowedDrift(a, b, tuning);

    /// <summary>
    /// How close two footprints must be for their squads to matter to each other: an occupied cell, plus the distance a
    /// squad can drift between validations, so the last check before they part sees them clear and restores the slots.
    /// </summary>
    static float NeighbourMargin(in ResolvedSquad a, in ResolvedSquad b, in MovementTuningComponent tuning) =>
        OccupiedRadius(a, b, tuning) + tuning.slotResolveRevalidateDistance;

    bool IsNeighbour(in ResolvedSquad other, in ResolvedSquad squad, in MovementTuningComponent tuning) =>
        tuning.slotOccupancyEnabled && squad.team != Team.None && other.team == squad.team
        && other.footprint.Near(squad.footprint, NeighbourMargin(other, squad, tuning));

    /// <summary>The tightest drift this squad's neighbouring allies allow between validations.</summary>
    float NearestAllyDrift(int index, in MovementTuningComponent tuning)
    {
        float drift = tuning.slotResolveRevalidateDistance;
        for (int i = 0; i < _entries.Count; i++)
            if (i != index && IsNeighbour(_entries[i], _entries[index], tuning))
                drift = math.min(drift, AllowedDrift(_entries[i], _entries[index], tuning));
        return drift;
    }

    /// <summary>True when an earlier ally next to this squad moved its cells this frame.</summary>
    bool AllyNearbyValidated(int index, in MovementTuningComponent tuning)
    {
        for (int i = 0; i < index; i++)
            if (_entries[i].validated && IsNeighbour(_entries[i], _entries[index], tuning)) return true;
        return false;
    }

    /// <summary>Fills <see cref="_occupied"/> with the cells of the earlier allies next to this squad; returns the cell radius.</summary>
    float CollectOccupiedCells(int index, in MovementTuningComponent tuning)
    {
        _occupied.Clear();
        float radius = 0f;
        for (int e = 0; e < index; e++)
        {
            if (!IsNeighbour(_entries[e], _entries[index], tuning)) continue;
            radius = math.max(radius, OccupiedRadius(_entries[e], _entries[index], tuning));
            var units = SystemAPI.GetBuffer<SquadUnitElement>(_entries[e].squad);
            for (int i = 0; i < units.Length; i++)
                if (SystemAPI.HasComponent<UnitResolvedSlotComponent>(units[i].Value))
                    _occupied.Add(SystemAPI.GetComponent<UnitResolvedSlotComponent>(units[i].Value).position.xz);
        }
        return radius;
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

    void Validate(NavMeshAgent agent, int index, ref ResolvedSquad entry, in SquadFormationAnchorComponent anchor,
        in SquadStateComponent state, in MovementTuningComponent tuning, float originRadius)
    {
        Entity squad = entry.squad;
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

        float occupiedRadius = CollectOccupiedCells(index, tuning);
        if (FormationSlotResolver.TryOrigin(ref probe, center, following, leader, originRadius, out float3 origin))
            _resolver.Resolve(ref probe, origin, anchor.rotation, new FormationSlotResolver.Settings
            {
                spacing = entry.spacing,
                snapRadius = tuning.slotSnapRadiusMax,
                searchCells = tuning.slotResolveSearchCells,
                occupiedRadius = occupiedRadius,
            }, _requests, _results, _occupied);
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
            entry.footprint.Include(position);
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

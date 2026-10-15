using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Aplica repulsión física per-frame entre entidades de equipos distintos usando agent.Move().
/// Cubre unidades vs unidades y héroes remotos vs unidades.
/// No modifica el NavMesh, no usa carving, no usa CharacterController.
///
/// Para el héroe local (que usa CharacterController en lugar de NavMeshAgent activo),
/// el bloqueo se logra agregando un CapsuleCollider a los prefabs visuales de unidades:
/// el CharacterController del héroe queda bloqueado físicamente sin código adicional.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitNavMeshSystem))]
[UpdateBefore(typeof(NavMeshPositionSyncSystem))]
public partial class UnitBodyblockSystem : SystemBase
{
    // ── Formaciones que actúan como muro sólido ───────────────────────────────
    // Line / Testudo / Wedge / Square / ShieldWall forman una pared continua; Dispersed y Column no.
    private static bool IsWallFormation(FormationType f) =>
        f == FormationType.Line       ||
        f == FormationType.Testudo    ||
        f == FormationType.Wedge      ||
        f == FormationType.Square     ||
        f == FormationType.ShieldWall;

    // ── Internal structs & collections ───────────────────────────────────────
    private struct AgentData
    {
        public NavMeshAgent       agent;
        public Entity             entity;
        public float3             position;
        public UnitFormationState state;   // héroes = siempre Moving
        public Team               team;
        public bool               isWall;      // Formed + formación muro
        public bool               isEngaging;  // en combate cuerpo a cuerpo
    }

    private readonly List<AgentData>                    _agents         = new();
    private readonly Dictionary<Entity, bool>           _unitIsWall     = new();
    private readonly Dictionary<(int, int), List<int>>  _grid           = new();
    private readonly List<List<int>>                    _bucketPool     = new();
    private NativeArray<RepulsionAgent> _nativeAgents;
    private NativeArray<float3> _nativeOffsets;
    private NativeParallelMultiHashMap<int2, int> _nativeGrid;
    private NativeArray<int2> _pairCounts;
    private int _usedBucketCount;

    public int DebugBucketPoolSize => _bucketPool.Count;
    public int DebugCandidatePairs { get; private set; }
    public int DebugContactPairs { get; private set; }
    private static readonly Unity.Profiling.ProfilerMarker BodyblockMarker = new("Conquest.Bodyblock");

    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
        RequireForUpdate<SquadSpawnConfigComponent>();
    }

    protected override void OnUpdate()
    {
        using var sample = BodyblockMarker.Auto();
        DebugCandidatePairs = 0;
        DebugContactPairs = 0;
        if (SystemAPI.GetSingleton<MatchStateComponent>().currentState != MatchState.InBattle) return;
        float dt = SystemAPI.Time.DeltaTime;
        if (dt <= 0f) return;
        var config = SystemAPI.GetSingleton<SquadSpawnConfigComponent>();
        float radius = math.max(0.01f, config.bodyblockRadius);
        float repulsionStrength = math.max(0f, config.bodyblockRepulsionStrength);
        float wallStrength = math.max(0f, config.bodyblockWallStrength);
        float maxPushSpeed = math.max(0f, config.bodyblockMaxPushSpeed);
        float engagingRadius = math.clamp(config.bodyblockEngagingRadius, 0f, radius);
        float engagingStrength = math.max(0f, config.bodyblockEngagingStrength);

        // ── 0. Build unit→isWall map from squad formation data ────────────────
        // Iteramos todos los squads: si su formación es muro, marcamos cada
        // unidad del buffer. Coste: O(squads × units_per_squad).
        _unitIsWall.Clear();
        foreach (var (formationComp, buffer) in
            SystemAPI.Query<RefRO<FormationComponent>, DynamicBuffer<SquadUnitElement>>())
        {
            bool wall = IsWallFormation(formationComp.ValueRO.currentFormation);
            foreach (var elem in buffer)
                _unitIsWall[elem.Value] = wall;
        }

        // ── 1. Collect: unidades ──────────────────────────────────────────────
        _agents.Clear();
        foreach (var (teamComp, stateComp, entity) in
            SystemAPI.Query<RefRO<TeamComponent>, RefRO<UnitFormationStateComponent>>()
                     .WithAll<NavAgentComponent>()
                     .WithEntityAccess())
        {
            var agent = SystemAPI.ManagedAPI.GetComponent<NavMeshAgent>(entity);
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) continue;

            var pos = agent.transform.position;
            var formState = stateComp.ValueRO.State;  // estado real, sin override

            bool engaging = SystemAPI.HasComponent<IsEngagingTag>(entity)
                         && SystemAPI.IsComponentEnabled<IsEngagingTag>(entity);

            bool formed  = formState != UnitFormationState.Moving;
            bool isWall  = formed && _unitIsWall.TryGetValue(entity, out var w) && w;

            _agents.Add(new AgentData
            {
                agent      = agent,
                entity     = entity,
                position   = new float3(pos.x, pos.y, pos.z),
                state      = formState,
                team       = teamComp.ValueRO.value,
                isWall     = isWall,
                isEngaging = engaging,
            });
        }

        // ── 1b. Collect: héroes remotos (tratados como Moving, sin isWall) ────
        // Los héroes remotos sí tienen NavMeshAgent activo en el NavMesh.
        // El héroe local usa CharacterController → su agente no pasa isOnNavMesh
        // y queda excluido automáticamente; su bloqueo es físico (CapsuleCollider).
        foreach (var (teamComp, entity) in
            SystemAPI.Query<RefRO<TeamComponent>>()
                     .WithAll<NavAgentComponent, HeroSpawnComponent>()
                     .WithEntityAccess())
        {
            var agent = SystemAPI.ManagedAPI.GetComponent<NavMeshAgent>(entity);
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) continue;

            var pos = agent.transform.position;
            _agents.Add(new AgentData
            {
                agent    = agent,
                entity   = entity,
                position = new float3(pos.x, pos.y, pos.z),
                state    = UnitFormationState.Moving,   // héroe = siempre activo
                team     = teamComp.ValueRO.value,
                isWall   = false,
            });
        }

        int count = _agents.Count;
        if (count < 2) return;

        // ── 2. Ensure offset buffer ───────────────────────────────────────────


        // ── 3. Build spatial grid ─────────────────────────────────────────────
        _grid.Clear();
        _usedBucketCount = 0;
        for (int i = 0; i < count; i++)
        {
            var cell = ToCell(_agents[i].position, radius);
            if (!_grid.TryGetValue(cell, out var bucket))
            {
                bucket = AcquireBucket();
                _grid[cell] = bucket;
            }
            bucket.Add(i);
        }

        // ── 4. Repulsion ──────────────────────────────────────────────────────
        EnsureNativeCapacity(count);
        _nativeGrid.Clear();
        for (int i = 0; i < count; i++)
        {
            var source = _agents[i];
            _nativeAgents[i] = new RepulsionAgent
            {
                entity = source.entity, position = source.position, state = source.state,
                team = source.team, isWall = source.isWall, isEngaging = source.isEngaging
            };
            _nativeGrid.Add(new int2((int)math.floor(source.position.x / radius),
                (int)math.floor(source.position.z / radius)), i);
        }
        var job = new RepulsionJob
        {
            Agents = _nativeAgents, Offsets = _nativeOffsets, Grid = _nativeGrid, Counts = _pairCounts,
            radius = radius, dt = dt, repulsionStrength = repulsionStrength,
            wallStrength = wallStrength, engagingRadius = engagingRadius, engagingStrength = engagingStrength,
            overlapEpsilonSq = math.max(math.EPSILON, config.bodyblockOverlapEpsilonSq)
        };
        if (count >= math.max(1, config.bodyblockParallelThreshold))
            job.Schedule(count, math.max(1, config.bodyblockJobBatchSize)).Complete();
        else job.Run(count);

        // ── 5. Apply ──────────────────────────────────────────────────────────
        for (int i = 0; i < count; i++)
        {
            DebugCandidatePairs += _pairCounts[i].x;
            DebugContactPairs += _pairCounts[i].y;
            var offset = (Vector3)_nativeOffsets[i];
            if (offset == Vector3.zero) continue;

            offset = ClampOffsetPerSecond(offset, maxPushSpeed, dt);

            _agents[i].agent.Move(offset);
        }
        // Each pair was examined from both agents; publish unique-pair counts.
        DebugCandidatePairs /= 2;
        DebugContactPairs /= 2;
    }

    private struct RepulsionAgent
    {
        public Entity entity;
        public float3 position;
        public UnitFormationState state;
        public Team team;
        public bool isWall, isEngaging;
    }

    [BurstCompile]
    private struct RepulsionJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<RepulsionAgent> Agents;
        [ReadOnly] public NativeParallelMultiHashMap<int2, int> Grid;
        [WriteOnly] public NativeArray<float3> Offsets;
        [WriteOnly] public NativeArray<int2> Counts;
        public float radius, dt, repulsionStrength, wallStrength, engagingRadius, engagingStrength, overlapEpsilonSq;

        public void Execute(int i)
        {
            float3 offset = float3.zero;
            int candidates = 0, contacts = 0;
            var current = Agents[i];
            var cell = new int2((int)math.floor(current.position.x / radius),
                (int)math.floor(current.position.z / radius));
            float radiusSq = radius * radius;
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (!Grid.TryGetFirstValue(cell + new int2(dx, dz), out int j, out var iterator)) continue;
                do
                {
                    if (j == i) continue;
                    var other = Agents[j];
                    if (current.team == other.team) continue;
                    candidates++;
                    float3 diff = current.position - other.position;
                    diff.y = 0f;
                    float distSq = math.lengthsq(diff);
                    if (distSq >= radiusSq) continue;
                    contacts++;
                    float dist = math.sqrt(distSq);
                    // Canonical pair ordering preserves the old direction at exact overlap.
                    float3 dir = distSq < overlapEpsilonSq
                        ? (i < j ? GetOverlapDirection(current.entity, other.entity)
                            : -GetOverlapDirection(other.entity, current.entity))
                        : diff / dist;
                    if (current.isEngaging && other.isEngaging)
                    {
                        if (distSq >= engagingRadius * engagingRadius) continue;
                        offset += dir * ((engagingRadius - dist) * engagingStrength * dt * 0.5f);
                        continue;
                    }
                    bool moving = current.state == UnitFormationState.Moving && !current.isEngaging;
                    bool otherMoving = other.state == UnitFormationState.Moving && !other.isEngaging;
                    if (!moving) continue;
                    float strength = otherMoving ? repulsionStrength * 0.5f
                        : other.isWall ? wallStrength : repulsionStrength;
                    offset += dir * ((radius - dist) * strength * dt);
                } while (Grid.TryGetNextValue(out j, ref iterator));
            }
            Offsets[i] = offset;
            Counts[i] = new int2(candidates, contacts);
        }
    }

    private void EnsureNativeCapacity(int count)
    {
        if (_nativeAgents.IsCreated && _nativeAgents.Length >= count) return;
        if (_nativeAgents.IsCreated) _nativeAgents.Dispose();
        if (_nativeOffsets.IsCreated) _nativeOffsets.Dispose();
        if (_pairCounts.IsCreated) _pairCounts.Dispose();
        if (_nativeGrid.IsCreated) _nativeGrid.Dispose();
        _nativeAgents = new NativeArray<RepulsionAgent>(count, Allocator.Persistent);
        _nativeOffsets = new NativeArray<float3>(count, Allocator.Persistent);
        _pairCounts = new NativeArray<int2>(count, Allocator.Persistent);
        _nativeGrid = new NativeParallelMultiHashMap<int2, int>(count, Allocator.Persistent);
    }

    protected override void OnDestroy()
    {
        if (_nativeAgents.IsCreated) _nativeAgents.Dispose();
        if (_nativeOffsets.IsCreated) _nativeOffsets.Dispose();
        if (_nativeGrid.IsCreated) _nativeGrid.Dispose();
        if (_pairCounts.IsCreated) _pairCounts.Dispose();
    }

    public static Vector3 ClampOffsetPerSecond(Vector3 offset, float maxSpeed, float deltaTime)
    {
        float maxDistance = math.max(0f, maxSpeed) * math.max(0f, deltaTime);
        float len = offset.magnitude;
        return len > maxDistance && len > 0f ? offset * (maxDistance / len) : offset;
    }

    public static float3 GetOverlapDirection(Entity first, Entity second)
    {
        uint hash = math.hash(new uint4((uint)first.Index, (uint)first.Version,
            (uint)second.Index, (uint)second.Version));
        float angle = (hash / (float)uint.MaxValue) * math.PI * 2f;
        return new float3(math.cos(angle), 0f, math.sin(angle));
    }

    private static (int, int) ToCell(float3 pos, float cellSize) =>
        ((int)math.floor(pos.x / cellSize), (int)math.floor(pos.z / cellSize));

    private List<int> AcquireBucket()
    {
        if (_usedBucketCount == _bucketPool.Count)
            _bucketPool.Add(new List<int>(4));
        var bucket = _bucketPool[_usedBucketCount++];
        bucket.Clear();
        return bucket;
    }
}

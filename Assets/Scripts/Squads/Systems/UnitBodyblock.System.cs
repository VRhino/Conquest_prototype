using System.Collections.Generic;
using Unity.Entities;
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
public partial class UnitBodyblockSystem : SystemBase
{
    // ── Tuneable constants ────────────────────────────────────────────────────
    private const float BodyblockRadius   = 0.8f;   // radio de colisión per-entidad
    private const float RepulsionStrength = 8f;     // fuerza Moving vs Moving
    private const float WallStrength      = 60f;    // fuerza cuando una formación-muro bloquea
    private const float MaxPushPerFrame   = 0.3f;   // clamp de desplazamiento por frame
    private const float EngagingRadius    = 0.35f;  // solo previene overlap físico entre engaging
    private const float EngagingStrength  = 3f;     // suave — no empuja fuera de rango de ataque
    private const float CellSize          = BodyblockRadius;

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
        public float3             position;
        public UnitFormationState state;   // héroes = siempre Moving
        public Team               team;
        public bool               isWall;      // Formed + formación muro
        public bool               isEngaging;  // en combate cuerpo a cuerpo
    }

    private readonly List<AgentData>                    _agents         = new();
    private readonly Dictionary<Entity, bool>           _unitIsWall     = new();
    private readonly Dictionary<(int, int), List<int>>  _grid           = new();
    private          Vector3[]                          _offsets        = new Vector3[256];

    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;
        if (dt <= 0f) return;

        // ── 0. Build unit→isWall map from squad formation data ────────────────
        // Iteramos todos los squads: si su formación es muro, marcamos cada
        // unidad del buffer. Coste: O(squads × units_per_squad).
        _unitIsWall.Clear();
        foreach (var (state, buffer) in
            SystemAPI.Query<RefRO<SquadStateComponent>, DynamicBuffer<SquadUnitElement>>())
        {
            bool wall = IsWallFormation(state.ValueRO.currentFormation);
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
                position = new float3(pos.x, pos.y, pos.z),
                state    = UnitFormationState.Moving,   // héroe = siempre activo
                team     = teamComp.ValueRO.value,
                isWall   = false,
            });
        }

        int count = _agents.Count;
        if (count < 2) return;

        // ── 2. Ensure offset buffer ───────────────────────────────────────────
        if (_offsets.Length < count)
            _offsets = new Vector3[count * 2];
        for (int i = 0; i < count; i++)
            _offsets[i] = Vector3.zero;

        // ── 3. Build spatial grid ─────────────────────────────────────────────
        _grid.Clear();
        for (int i = 0; i < count; i++)
        {
            var cell = ToCell(_agents[i].position);
            if (!_grid.TryGetValue(cell, out var bucket))
            {
                bucket = new List<int>(4);
                _grid[cell] = bucket;
            }
            bucket.Add(i);
        }

        // ── 4. Repulsion ──────────────────────────────────────────────────────
        float radiusSq = BodyblockRadius * BodyblockRadius;

        for (int i = 0; i < count; i++)
        {
            var cellI = ToCell(_agents[i].position);

            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                var neighborCell = (cellI.Item1 + dx, cellI.Item2 + dz);
                if (!_grid.TryGetValue(neighborCell, out var bucket)) continue;

                foreach (int j in bucket)
                {
                    if (j <= i) continue;
                    if (_agents[i].team == _agents[j].team) continue;

                    float3 diff = _agents[i].position - _agents[j].position;
                    diff.y = 0f;
                    float distSq = math.lengthsq(diff);
                    if (distSq >= radiusSq || distSq < 1e-6f) continue;

                    float dist    = math.sqrt(distSq);
                    float overlap = BodyblockRadius - dist;
                    float3 dir    = diff / dist;  // i → dirección alejándose de j

                    bool iMoving = _agents[i].state == UnitFormationState.Moving;
                    bool jMoving = _agents[j].state == UnitFormationState.Moving;
                    bool iEng    = _agents[i].isEngaging;
                    bool jEng    = _agents[j].isEngaging;

                    // Engaging vs Engaging → repulsión suave solo para prevenir overlap
                    if (iEng && jEng)
                    {
                        float engRadSq = EngagingRadius * EngagingRadius;
                        if (distSq >= engRadSq) continue;
                        float eOverlap = EngagingRadius - dist;
                        float eMag     = eOverlap * EngagingStrength * dt;
                        var   ePush    = new Vector3(dir.x * eMag * 0.5f, 0f, dir.z * eMag * 0.5f);
                        _offsets[i] += ePush;
                        _offsets[j] -= ePush;
                        continue;
                    }

                    // Engaging vs non-engaging → engaging actúa como muro estático
                    if (iEng) iMoving = false;
                    if (jEng) jMoving = false;

                    // Formed vs Formed → sin push (dos paredes estáticas, evita vibración)
                    if (!iMoving && !jMoving) continue;

                    if (!iMoving && jMoving)
                    {
                        // i es muro, j se mueve → solo j recibe push (lejos de i)
                        float str  = _agents[i].isWall ? WallStrength : RepulsionStrength;
                        float mag  = overlap * str * dt;
                        // dir apunta de j hacia i → negarlo para alejar j de i
                        _offsets[j] -= new Vector3(dir.x * mag, 0f, dir.z * mag);
                    }
                    else if (iMoving && !jMoving)
                    {
                        // j es muro, i se mueve → solo i recibe push (lejos de j)
                        float str  = _agents[j].isWall ? WallStrength : RepulsionStrength;
                        float mag  = overlap * str * dt;
                        // dir apunta de j hacia i → aplicarlo para alejar i de j
                        _offsets[i] += new Vector3(dir.x * mag, 0f, dir.z * mag);
                    }
                    else
                    {
                        // Ambos Moving → 50/50
                        float mag  = overlap * RepulsionStrength * dt;
                        var   push = new Vector3(dir.x * mag * 0.5f, 0f, dir.z * mag * 0.5f);
                        _offsets[i] += push;
                        _offsets[j] -= push;
                    }
                }
            }
        }

        // ── 5. Apply ──────────────────────────────────────────────────────────
        for (int i = 0; i < count; i++)
        {
            var offset = _offsets[i];
            if (offset == Vector3.zero) continue;

            float len = offset.magnitude;
            if (len > MaxPushPerFrame)
                offset *= MaxPushPerFrame / len;

            _agents[i].agent.Move(offset);
        }
    }

    private static (int, int) ToCell(float3 pos) =>
        ((int)math.floor(pos.x / CellSize), (int)math.floor(pos.z / CellSize));
}

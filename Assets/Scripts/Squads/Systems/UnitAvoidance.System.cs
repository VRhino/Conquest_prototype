using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.AI;

/// <summary>
/// Allied units dodge each other by velocity (C14, decision D1), with <see cref="AllyAvoidance"/>.
///
/// Reads the agent's steering (desiredVelocity, capped at the speed UnitFollowFormationSystem set) and writes
/// agent.velocity for units that are following their slot or closing on a target. Formed, still units and
/// heroes are obstacles the movers avoid alone. Enemies are not avoided but slid along: with enemyContactEnabled the
/// final velocity loses its approach to the enemies in front (<see cref="EnemyContact"/>, M2), unless either unit has the
/// enemy collision off (<see cref="EnemyCollision.PassesEnemies"/>, P2). The enemy that stops a unit and is not its target
/// goes to <see cref="UnitBlockedComponent"/> (single writer, P4). Runs after the speed is
/// set and before the bodyblock and the position sync.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitFollowFormationSystem))]
[UpdateBefore(typeof(UnitBodyblockSystem))]
[UpdateBefore(typeof(NavMeshPositionSyncSystem))]
public partial class UnitAvoidanceSystem : SystemBase
{
    /// <summary>Squadmates keep this fraction of the grid spacing apart, so formed slots stay reachable.</summary>
    const float SquadmateSpacingFraction = 0.9f;

    readonly List<NavMeshAgent> _navAgents = new();
    readonly List<Entity> _entities = new();
    NativeList<AllyAvoidance.Agent> _agents;
    // Persistent and grow-only (M9): a warm frame allocates nothing.
    NativeParallelMultiHashMap<int2, int> _grid;
    NativeList<float2> _velocities;
    NativeList<int> _blockers;
    // Hero positions of the last frame; rebuilt into the other map each frame so heroes that left drop out.
    NativeHashMap<Entity, float2> _heroPreviousPositions, _heroCurrentPositions;

    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
        _agents = new NativeList<AllyAvoidance.Agent>(256, Allocator.Persistent);
        _heroPreviousPositions = new NativeHashMap<Entity, float2>(16, Allocator.Persistent);
        _heroCurrentPositions = new NativeHashMap<Entity, float2>(16, Allocator.Persistent);
        _grid = new NativeParallelMultiHashMap<int2, int>(256, Allocator.Persistent);
        _velocities = new NativeList<float2>(256, Allocator.Persistent);
        _blockers = new NativeList<int>(256, Allocator.Persistent);
    }

    protected override void OnDestroy()
    {
        if (_agents.IsCreated) _agents.Dispose();
        if (_heroPreviousPositions.IsCreated) _heroPreviousPositions.Dispose();
        if (_heroCurrentPositions.IsCreated) _heroCurrentPositions.Dispose();
        if (_grid.IsCreated) _grid.Dispose();
        if (_velocities.IsCreated) _velocities.Dispose();
        if (_blockers.IsCreated) _blockers.Dispose();
        base.OnDestroy();
    }

    protected override void OnUpdate()
    {
        if (SystemAPI.GetSingleton<MatchStateComponent>().currentState != MatchState.InBattle) return;
        var tuning = SystemAPI.HasSingleton<MovementTuningComponent>()
            ? SystemAPI.GetSingleton<MovementTuningComponent>() : MovementTuningComponent.Default;
        if (!tuning.avoidanceEnabled) return;
        float dt = SystemAPI.Time.DeltaTime;
        if (dt <= 0f) return;

        _agents.Clear();
        _navAgents.Clear();
        _entities.Clear();

        // Units: movers steer, everyone else is an obstacle.
        foreach (var (team, formation, slot, transform, entity) in SystemAPI
                     .Query<RefRO<TeamComponent>, RefRO<UnitFormationStateComponent>,
                            RefRO<UnitResolvedSlotComponent>, RefRO<LocalTransform>>()
                     .WithAll<NavAgentComponent>()
                     .WithEntityAccess())
        {
            var agent = SystemAPI.ManagedAPI.GetComponent<NavMeshAgent>(entity);
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) continue;

            bool hasTarget = SystemAPI.HasComponent<UnitCombatComponent>(entity)
                && SystemAPI.GetComponent<UnitCombatComponent>(entity).target != Entity.Null;
            bool steered = !agent.isStopped && agent.speed > 0f
                && (formation.ValueRO.State == UnitFormationState.Moving || hasTarget);
            float2 position = transform.ValueRO.Position.xz;
            float2 preferred = ((float3)agent.desiredVelocity).xz;
            float maxSpeed = agent.speed;
            if (math.lengthsq(preferred) > maxSpeed * maxSpeed) preferred = math.normalize(preferred) * maxSpeed;
            Entity squadEntity = SystemAPI.HasComponent<UnitOwnerComponent>(entity)
                ? SystemAPI.GetComponent<UnitOwnerComponent>(entity).squad : Entity.Null;
            float squadSpacing = squadEntity != Entity.Null && SystemAPI.HasComponent<FormationComponent>(squadEntity)
                ? math.max(FormationDefinition.MinSpacing, SystemAPI.GetComponent<FormationComponent>(squadEntity).spacing)
                : MovementTuningComponent.ReferenceSpacing;
            bool nearSlot = math.distance(position, slot.ValueRO.position.xz) < squadSpacing;

            _agents.Add(new AllyAvoidance.Agent
            {
                position = position,
                velocity = ((float3)agent.velocity).xz,
                preferred = preferred,
                maxSpeed = maxSpeed,
                timeHorizon = nearSlot ? tuning.avoidanceTimeHorizonNear : tuning.avoidanceTimeHorizon,
                team = (int)team.ValueRO.value,
                squad = squadEntity != Entity.Null ? squadEntity.Index : -1,
                squadSpacing = squadSpacing,
                steered = steered,
                passesEnemies = EnemyCollision.PassesEnemies(EntityManager, entity),
                radius = SystemAPI.HasComponent<UnitBodyRadiusComponent>(entity)
                    ? BodyRadius.Resolve(SystemAPI.GetComponent<UnitBodyRadiusComponent>(entity).radius, tuning.bodyRadius)
                    : tuning.bodyRadius,
            });
            _navAgents.Add(steered ? agent : null);
            _entities.Add(entity);
        }

        // Heroes: obstacles moving at the velocity estimated from their last position.
        _heroCurrentPositions.Clear();
        foreach (var (team, transform, entity) in SystemAPI
                     .Query<RefRO<TeamComponent>, RefRO<LocalTransform>>()
                     .WithAll<HeroSpawnComponent>()
                     .WithEntityAccess())
        {
            float2 position = transform.ValueRO.Position.xz;
            float2 velocity = _heroPreviousPositions.TryGetValue(entity, out float2 previous)
                ? (position - previous) / dt : float2.zero;
            _heroCurrentPositions[entity] = position;
            _agents.Add(new AllyAvoidance.Agent
            {
                position = position, velocity = velocity, team = (int)team.ValueRO.value, squad = -1, steered = false,
            });
            _navAgents.Add(null);
            _entities.Add(entity);
        }

        (_heroPreviousPositions, _heroCurrentPositions) = (_heroCurrentPositions, _heroPreviousPositions);

        if (_agents.Length == 0) return;
        _velocities.ResizeUninitialized(_agents.Length);
        var velocities = _velocities.AsArray();
        AllyAvoidance.Run(_agents.AsArray(), velocities, _grid, new AllyAvoidance.Settings
        {
            neighborRadius = tuning.avoidanceNeighborRadius,
            maxNeighbors = (int)tuning.avoidanceMaxNeighbors,
            combinedRadius = 2f * tuning.bodyRadius,
            squadmateSpacingFraction = SquadmateSpacingFraction,
            collisionResolveTime = tuning.avoidanceCollisionResolveTime,
        });

        for (int i = 0; i < _navAgents.Count; i++)
        {
            var agent = _navAgents[i];
            if (agent == null) continue;
            // Respect the agent's acceleration: writing the solved velocity directly bypassed it (spikes > 20 m/s²).
            float2 current = ((float3)agent.velocity).xz;
            velocities[i] = AllyAvoidance.LimitAcceleration(current, velocities[i], agent.acceleration * dt,
                agent.acceleration * tuning.brakingFactor * dt);
        }

        // Last, so neither ORCA nor the acceleration limit can put the approach back (M2, P1).
        _blockers.ResizeUninitialized(_agents.Length);
        var blockers = _blockers.AsArray();
        if (tuning.enemyContactEnabled)
            EnemyContact.Run(_agents.AsArray(), velocities, _grid, AllyAvoidance.CellSize(tuning.avoidanceNeighborRadius),
                2f * tuning.bodyRadius, math.max(dt, tuning.enemyContactLookaheadTime), blockers);
        else
            for (int i = 0; i < blockers.Length; i++) blockers[i] = -1;
        UpdateBlocked(blockers, SystemAPI.Time.ElapsedTime);

        for (int i = 0; i < _navAgents.Count; i++)
        {
            var agent = _navAgents[i];
            if (agent == null) continue;
            agent.velocity = new UnityEngine.Vector3(velocities[i].x, agent.velocity.y, velocities[i].y);
        }
    }

    /// <summary>Records who blocks each unit; its own target in the way is no block, it is the fight (P4).</summary>
    void UpdateBlocked(NativeArray<int> blockers, double now)
    {
        for (int i = 0; i < _entities.Count; i++)
        {
            Entity entity = _entities[i];
            if (!SystemAPI.HasComponent<UnitBlockedComponent>(entity)) continue;
            Entity blocker = blockers[i] >= 0 ? _entities[blockers[i]] : Entity.Null;
            if (blocker != Entity.Null && SystemAPI.HasComponent<UnitCombatComponent>(entity)
                && SystemAPI.GetComponent<UnitCombatComponent>(entity).target == blocker) blocker = Entity.Null;
            var blocked = SystemAPI.GetComponentRW<UnitBlockedComponent>(entity);
            if (blocked.ValueRO.blocker == blocker) continue;
            blocked.ValueRW = new UnitBlockedComponent { blocker = blocker, since = now };
        }
    }
}

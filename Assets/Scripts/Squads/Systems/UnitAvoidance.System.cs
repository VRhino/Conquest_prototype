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
/// heroes are obstacles the movers avoid alone; enemies are ignored (D2). Runs after the speed is set and
/// before the bodyblock and the position sync.
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
    NativeList<AllyAvoidance.Agent> _agents;
    NativeHashMap<Entity, float2> _heroPreviousPositions;

    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
        _agents = new NativeList<AllyAvoidance.Agent>(256, Allocator.Persistent);
        _heroPreviousPositions = new NativeHashMap<Entity, float2>(16, Allocator.Persistent);
    }

    protected override void OnDestroy()
    {
        if (_agents.IsCreated) _agents.Dispose();
        if (_heroPreviousPositions.IsCreated) _heroPreviousPositions.Dispose();
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

        // Units: movers steer, everyone else is an obstacle.
        foreach (var (team, formation, slot, transform, entity) in SystemAPI
                     .Query<RefRO<TeamComponent>, RefRO<UnitFormationStateComponent>,
                            RefRO<UnitTargetPositionComponent>, RefRO<LocalTransform>>()
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
            bool nearSlot = math.distance(position, slot.ValueRO.position.xz) < FormationGridSystem.CELL_WIDTH;

            _agents.Add(new AllyAvoidance.Agent
            {
                position = position,
                velocity = ((float3)agent.velocity).xz,
                preferred = preferred,
                maxSpeed = maxSpeed,
                timeHorizon = nearSlot ? tuning.avoidanceTimeHorizonNear : tuning.avoidanceTimeHorizon,
                team = (int)team.ValueRO.value,
                squad = SystemAPI.HasComponent<UnitOwnerComponent>(entity)
                    ? SystemAPI.GetComponent<UnitOwnerComponent>(entity).squad.Index : -1,
                steered = steered,
            });
            _navAgents.Add(steered ? agent : null);
        }

        // Heroes: obstacles moving at the velocity estimated from their last position.
        foreach (var (team, transform, entity) in SystemAPI
                     .Query<RefRO<TeamComponent>, RefRO<LocalTransform>>()
                     .WithAll<HeroSpawnComponent>()
                     .WithEntityAccess())
        {
            float2 position = transform.ValueRO.Position.xz;
            float2 velocity = _heroPreviousPositions.TryGetValue(entity, out float2 previous)
                ? (position - previous) / dt : float2.zero;
            _heroPreviousPositions[entity] = position;
            _agents.Add(new AllyAvoidance.Agent
            {
                position = position, velocity = velocity, team = (int)team.ValueRO.value, squad = -1, steered = false,
            });
            _navAgents.Add(null);
        }

        if (_agents.Length == 0) return;
        using var velocities = new NativeArray<float2>(_agents.Length, Allocator.TempJob);
        AllyAvoidance.Run(_agents.AsArray(), velocities, new AllyAvoidance.Settings
        {
            neighborRadius = tuning.avoidanceNeighborRadius,
            maxNeighbors = (int)tuning.avoidanceMaxNeighbors,
            combinedRadius = 2f * tuning.bodyRadius,
            squadmateCombinedRadius = math.min(2f * tuning.bodyRadius,
                SquadmateSpacingFraction * FormationGridSystem.CELL_WIDTH),
            collisionResolveTime = tuning.avoidanceCollisionResolveTime,
        });

        for (int i = 0; i < _navAgents.Count; i++)
        {
            var agent = _navAgents[i];
            if (agent == null) continue;
            float2 v = velocities[i];
            agent.velocity = new UnityEngine.Vector3(v.x, agent.velocity.y, v.y);
        }
    }
}

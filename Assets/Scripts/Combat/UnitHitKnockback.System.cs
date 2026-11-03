using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.AI;

/// <summary>
/// Hit knock-back (pattern of the reference's <c>hit_curve</c>): a hit that reaches a unit's body pushes it away from
/// the attacker along <see cref="HitKnockback"/>'s curve, <c>hitKnockbackDistance</c> in <c>hitKnockbackDuration</c>.
/// Starts from <see cref="UnitHitImpactComponent"/> (consumed here) and moves the agent with <c>agent.Move</c>, which keeps
/// it on the NavMesh; a new hit restarts the push. Single writer of <see cref="UnitHitKnockbackComponent"/>.
/// With enemyContactEnabled the push slides along enemy bodies like any other movement (M2, P3): it never closes the gap to
/// an enemy below the contact distance (<see cref="EnemyContact.ClipStep"/>), unless either of the two has the enemy collision
/// off (<see cref="EnemyCollision.PassesEnemies"/>, P2; a heavy hit turns it off for the pushed unit while the push lasts).
/// Runs after the avoidance and before the bodyblock, which undoes what is left the same frame, and before the position
/// sync, so the push lands in ECS the same frame.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitAvoidanceSystem))]
[UpdateBefore(typeof(UnitBodyblockSystem))]
[UpdateBefore(typeof(NavMeshPositionSyncSystem))]
public partial class UnitHitKnockbackSystem : SystemBase
{
    readonly List<(float2 position, Team team, bool passesEnemies, float radius)> _bodies = new();
    bool _bodiesCollected;

    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
    }

    protected override void OnUpdate()
    {
        var tuning = SystemAPI.HasSingleton<MovementTuningComponent>()
            ? SystemAPI.GetSingleton<MovementTuningComponent>() : MovementTuningComponent.Default;
        float dt = SystemAPI.Time.DeltaTime;
        float distance = tuning.hitKnockbackDistance, duration = tuning.hitKnockbackDuration;
        bool enabled = distance > 0f && duration > 0f && dt > 0f;
        _bodiesCollected = false;

        foreach (var (impact, knockback, entity) in SystemAPI
                     .Query<RefRO<UnitHitImpactComponent>, RefRW<UnitHitKnockbackComponent>>()
                     .WithAll<NavAgentComponent>()
                     .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                     .WithEntityAccess())
        {
            if (SystemAPI.IsComponentEnabled<UnitHitImpactComponent>(entity))
            {
                SystemAPI.SetComponentEnabled<UnitHitImpactComponent>(entity, false);
                if (enabled && math.lengthsq(impact.ValueRO.direction) > 1e-6f)
                    knockback.ValueRW = new UnitHitKnockbackComponent
                    {
                        direction = math.normalize(impact.ValueRO.direction), elapsed = 0f, active = true,
                        passesEnemies = impact.ValueRO.passesEnemies,
                    };
            }
            if (!knockback.ValueRO.active) continue;
            if (!enabled || SystemAPI.HasComponent<IsDeadComponent>(entity))
            {
                knockback.ValueRW.active = false;
                continue;
            }

            var agent = SystemAPI.ManagedAPI.GetComponent<NavMeshAgent>(entity);
            float step = HitKnockback.Step(knockback.ValueRO.elapsed, dt, duration, distance);
            if (agent != null && agent.enabled && agent.isOnNavMesh && step > 0f)
            {
                float2 move = knockback.ValueRO.direction * step;
                if (tuning.enemyContactEnabled && SystemAPI.HasComponent<TeamComponent>(entity)
                    && !EnemyCollision.PassesEnemies(EntityManager, entity))
                    move = ClipAgainstEnemies(move, entity, tuning.bodyRadius);
                agent.Move(new UnityEngine.Vector3(move.x, 0f, move.y));
            }
            knockback.ValueRW.elapsed += dt;
            if (knockback.ValueRO.elapsed >= duration) knockback.ValueRW.active = false;
        }
    }

    /// <summary>The push step left after the enemies around the unit. Bodies are gathered once, on the first push of the frame.</summary>
    float2 ClipAgainstEnemies(float2 step, Entity entity, float defaultRadius)
    {
        float selfRadius = BodyOf(entity, defaultRadius);
        if (!_bodiesCollected)
        {
            _bodies.Clear();
            foreach (var (team, transform, body) in SystemAPI.Query<RefRO<TeamComponent>, RefRO<LocalTransform>>()
                         .WithAny<UnitFormationStateComponent, HeroSpawnComponent>()
                         .WithEntityAccess())
                _bodies.Add((transform.ValueRO.Position.xz, team.ValueRO.value, EnemyCollision.PassesEnemies(EntityManager, body),
                    BodyOf(body, defaultRadius)));
            _bodiesCollected = true;
        }
        float2 position = SystemAPI.GetComponent<LocalTransform>(entity).Position.xz;
        Team own = SystemAPI.GetComponent<TeamComponent>(entity).value;
        for (int pass = 0; pass < EnemyContact.Passes; pass++)
            foreach (var body in _bodies)
            {
                // Both bodies of the pair touch (L5-5).
                float contactDistance = selfRadius + body.radius;
                float reach = contactDistance + math.length(step);
                if (body.team == own || body.passesEnemies || math.distancesq(body.position, position) > reach * reach) continue;
                step = EnemyContact.ClipStep(step, body.position - position, contactDistance);
            }
        return step;
    }

    float BodyOf(Entity entity, float fallback) => SystemAPI.HasComponent<UnitBodyRadiusComponent>(entity)
        ? BodyRadius.Resolve(SystemAPI.GetComponent<UnitBodyRadiusComponent>(entity).radius, fallback) : fallback;
}

using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.AI;

/// <summary>
/// Hit knock-back (pattern of the reference's <c>hit_curve</c>): a hit that reaches a unit's body pushes it away from
/// the attacker along <see cref="HitKnockback"/>'s curve, <c>hitKnockbackDistance</c> in <c>hitKnockbackDuration</c>.
/// Starts from <see cref="UnitHitImpactComponent"/> (consumed here) and moves the agent with <c>agent.Move</c>, which keeps
/// it on the NavMesh; a new hit restarts the push. Single writer of <see cref="UnitHitKnockbackComponent"/>.
/// Runs after the bodyblock and before the position sync, so the push lands in ECS the same frame.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitBodyblockSystem))]
[UpdateBefore(typeof(NavMeshPositionSyncSystem))]
public partial class UnitHitKnockbackSystem : SystemBase
{
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
                agent.Move(new UnityEngine.Vector3(move.x, 0f, move.y));
            }
            knockback.ValueRW.elapsed += dt;
            if (knockback.ValueRO.elapsed >= duration) knockback.ValueRW.active = false;
        }
    }
}

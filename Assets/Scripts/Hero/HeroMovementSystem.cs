using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;

/// <summary>
/// Handles physical movement and orientation of the local hero based on
/// <see cref="HeroInputComponent"/> data.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class HeroMovementSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (input, stats, life, velocity, mass, transform, entity) in
                 SystemAPI.Query<RefRO<HeroInputComponent>,
                                 RefRO<HeroStatsComponent>,
                                 RefRO<HeroLifeComponent>,
                                 RefRW<PhysicsVelocity>,
                                 RefRO<PhysicsMass>,
                                 RefRW<LocalTransform>>()
                        .WithAll<IsLocalPlayer>()
                        .WithEntityAccess())
        {
            if (!life.ValueRO.isAlive)
                continue;

            float3 move = new float3(input.ValueRO.moveInput.x, 0f, input.ValueRO.moveInput.y);
            float speed = stats.ValueRO.baseSpeed;
            if (input.ValueRO.isSprinting)
                speed *= stats.ValueRO.sprintMultiplier;

            float3 desired = math.normalizesafe(move) * speed;

            var vel = velocity.ValueRW;
            vel.Linear.x = desired.x;
            vel.Linear.z = desired.z;

            if (input.ValueRO.isJumping)
            {
                if (SystemAPI.HasComponent<HeroGroundState>(entity) &&
                    SystemAPI.GetComponent<HeroGroundState>(entity).isGrounded)
                {
                    vel.Linear.y = stats.ValueRO.jumpForce;
                    var ground = SystemAPI.GetComponentRW<HeroGroundState>(entity);
                    ground.ValueRW.isGrounded = false;
                }
            }

            velocity.ValueRW = vel;

            if (!math.all(move == float3.zero))
            {
                quaternion targetRot = quaternion.LookRotationSafe(move, math.up());
                transform.ValueRW.Rotation = math.slerp(transform.ValueRW.Rotation, targetRot, deltaTime * 10f);
            }
        }
    }
}

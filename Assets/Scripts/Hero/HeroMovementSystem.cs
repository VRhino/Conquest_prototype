using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Handles deterministic movement and rotation of the local hero based on
/// <see cref="HeroInputComponent"/> data.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class HeroMovementSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (input, movement, life, transform) in
                 SystemAPI.Query<RefRO<HeroInputComponent>,
                                 RefRO<HeroMovementComponent>,
                                 RefRO<HeroLifeComponent>,
                                 RefRW<LocalTransform>>()
                        .WithAll<IsLocalPlayer>())
        {
            if (!life.ValueRO.isAlive)
                continue;

            float2 moveInput = input.ValueRO.moveInput;
            float3 forward = math.forward(transform.ValueRO.Rotation);
            float3 right = math.normalizesafe(math.cross(math.up(), forward));

            float3 desired = forward * moveInput.y + right * moveInput.x;
            float magnitude = math.length(desired);

            if (magnitude > 0f)
            {
                float3 direction = desired / magnitude;
                transform.ValueRW.Position += direction * movement.ValueRO.movementSpeed * deltaTime;

                quaternion targetRot = quaternion.LookRotationSafe(direction, math.up());
                transform.ValueRW.Rotation = math.slerp(transform.ValueRW.Rotation, targetRot, deltaTime * 10f);
            }
        }
    }
}

using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Handles deterministic movement and rotation of the local hero based on
/// <see cref="HeroInputComponent"/> and <see cref="HeroStatsComponent"/> data.
/// Applies sprint multiplier when sprinting and stamina is available.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class HeroMovementSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        int i = 0;
        foreach (var (input, stats, stamina, life, transform) in SystemAPI.Query<RefRO<HeroInputComponent>,
                                   RefRO<HeroStatsComponent>,
                                   RefRO<StaminaComponent>,
                                   RefRO<HeroLifeComponent>,
                                   RefRW<LocalTransform>>()
                        .WithAll<IsLocalPlayer>())
        {
            // If you need the entity, you can get it from the query's Entity array:
            // var entity = query.GetEntityArray(Allocator.Temp)[i];
            // But for most single-player hero systems, there is only one entity.
            // If you need to log the entity, you can skip it or log index.

            if (!life.ValueRO.isAlive)
            {
                i++;
                continue;
            }

            float2 moveInput = input.ValueRO.MoveInput;

            var cam = Camera.main;
            if (cam == null)
            {
                i++;
                return;
            }

            Vector3 camForward = cam.transform.forward;
            camForward.y = 0f;
            camForward.Normalize();
            Vector3 camRight = cam.transform.right;
            camRight.y = 0f;
            camRight.Normalize();

            float3 desired = (float3)(camForward * moveInput.y + camRight * moveInput.x);
            float magnitude = math.length(desired);

            if (magnitude > 0f)
            {
                float3 direction = desired / magnitude;

                float currentSpeed = stats.ValueRO.baseSpeed;

                if (input.ValueRO.IsSprintPressed && !stamina.ValueRO.isExhausted && stamina.ValueRO.currentStamina > 0f)
                {
                    currentSpeed *= stats.ValueRO.sprintMultiplier;
                }

                transform.ValueRW.Position += direction * currentSpeed * deltaTime;
                Debug.Log($"[HeroMovementSystem.cs] Set LocalTransform.Position += {direction * currentSpeed * deltaTime} (new: {transform.ValueRW.Position})");

                quaternion targetRot = quaternion.LookRotationSafe((float3)camForward, math.up());
                transform.ValueRW.Rotation = math.slerp(transform.ValueRW.Rotation, targetRot, deltaTime * 10f);
            }
            i++;
        }
    }
}

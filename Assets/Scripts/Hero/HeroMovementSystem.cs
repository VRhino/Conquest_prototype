using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

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

            // Obtén la referencia a la cámara principal
            var cam = Camera.main;
            if (cam == null)
                return;

            // Calcula forward y right de la cámara, ignorando el eje Y para movimiento plano
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
                transform.ValueRW.Position += direction * movement.ValueRO.movementSpeed * deltaTime;

                // Rota el héroe siempre hacia el forward de la cámara
                quaternion targetRot = quaternion.LookRotationSafe((float3)camForward, math.up());
                transform.ValueRW.Rotation = math.slerp(transform.ValueRW.Rotation, targetRot, deltaTime * 10f);
            }
        }
    }
}

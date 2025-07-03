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

        foreach (var (input, stats, stamina, life, transform) in
                 SystemAPI.Query<RefRO<HeroInputComponent>,
                                 RefRO<HeroStatsComponent>,
                                 RefRO<StaminaComponent>,
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
                
                // Calcular velocidad actual considerando sprint
                float currentSpeed = stats.ValueRO.baseSpeed;
                
                // Si está haciendo sprint y tiene stamina suficiente, aplicar multiplicador
                if (input.ValueRO.isSprinting && !stamina.ValueRO.isExhausted && stamina.ValueRO.currentStamina > 0f)
                {
                    currentSpeed *= stats.ValueRO.sprintMultiplier;
                }
                
                transform.ValueRW.Position += direction * currentSpeed * deltaTime;

                // Rota el héroe siempre hacia el forward de la cámara
                quaternion targetRot = quaternion.LookRotationSafe((float3)camForward, math.up());
                transform.ValueRW.Rotation = math.slerp(transform.ValueRW.Rotation, targetRot, deltaTime * 10f);
            }
        }
    }
}

using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Manages stamina consumption and regeneration for the local hero based on input.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class HeroStaminaSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (input, stats, life, stamina, entity) in
                 SystemAPI.Query<RefRO<HeroInputComponent>,
                                 RefRO<HeroStatsComponent>,
                                 RefRO<HeroLifeComponent>,
                                 RefRW<StaminaComponent>>()
                        .WithAll<IsLocalPlayer>()
                        .WithEntityAccess())
        {
            if (!life.ValueRO.isAlive)
                continue;

            var data = stamina.ValueRW;

            bool performedAction = false;

            if (!data.isExhausted)
            {
                if (input.ValueRO.isSprinting)
                {
                    data.currentStamina -= 10f * deltaTime;
                    performedAction = true;
                }

                if (input.ValueRO.isAttacking)
                {
                    data.currentStamina -= 15f;
                    performedAction = true;
                }

                if (input.ValueRO.useSkill1)
                {
                    data.currentStamina -= 20f;
                    performedAction = true;
                }

                if (input.ValueRO.useSkill2)
                {
                    data.currentStamina -= 25f;
                    performedAction = true;
                }

                if (input.ValueRO.useUltimate)
                {
                    data.currentStamina -= 40f;
                    performedAction = true;
                }
            }

            // Regeneración de stamina solo si no se realizaron acciones
            if (!performedAction)
            {
                data.currentStamina += data.regenRate * deltaTime;
            }

            data.currentStamina = math.clamp(data.currentStamina, 0f, data.maxStamina);

            // Sistema de exhaustion más granular
            if (data.currentStamina <= 0f)
            {
                data.isExhausted = true;
            }
            else if (data.currentStamina > data.maxStamina * 0.2f) // Recupera cuando tiene más del 20%
            {
                data.isExhausted = false;
            }

            stamina.ValueRW = data;
        }
    }
}


using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Manages stamina consumption and regeneration for the local hero based on input.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class HeroStaminaSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<HeroGameplayConfigComponent>();
    }

    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        var cfg = SystemAPI.GetSingleton<HeroGameplayConfigComponent>();

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
                if (input.ValueRO.IsSprintPressed)
                {
                    data.currentStamina -= cfg.sprintStaminaCostPerSecond * deltaTime;
                    performedAction = true;
                }

                if (input.ValueRO.IsAttackPressed)
                {
                    data.currentStamina -= cfg.attackStaminaCost;
                    performedAction = true;
                }

                if (input.ValueRO.UseSkill1)
                {
                    data.currentStamina -= cfg.skill1StaminaCost;
                    performedAction = true;
                }

                if (input.ValueRO.UseSkill2)
                {
                    data.currentStamina -= cfg.skill2StaminaCost;
                    performedAction = true;
                }

                if (input.ValueRO.UseUltimate)
                {
                    data.currentStamina -= cfg.ultimateStaminaCost;
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
            else if (data.currentStamina > data.maxStamina * cfg.exhaustionRecoveryThreshold)
            {
                data.isExhausted = false;
            }

            stamina.ValueRW = data;
        }
    }
}


using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Regenerates hero stamina and charges only continuous sprint movement; on horseback (M19) the horse's gallop wears
/// the horse's own stamina instead.
/// One-shot actions charge stamina when their execution starts.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(SkillExecutionSystem))]
[UpdateAfter(typeof(HeroMovementSystem))]
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

        foreach (var (stats, life, stamina, entity) in
                 SystemAPI.Query<RefRO<HeroStatsComponent>,
                                 RefRO<HeroLifeComponent>,
                                 RefRW<StaminaComponent>>()
                        .WithEntityAccess())
        {
            if (!life.ValueRO.isAlive)
                continue;

            var data = stamina.ValueRW;

            bool sprinting = false;
            bool riding = SystemAPI.HasComponent<HeroMountComponent>(entity) && SystemAPI.GetComponent<HeroMountComponent>(entity).Riding;
            if (!data.isExhausted && !riding && SystemAPI.HasComponent<HeroMoveIntent>(entity))
            {
                var movement = SystemAPI.GetComponent<HeroMoveIntent>(entity);
                sprinting = math.lengthsq(movement.Direction) > 0f
                    && movement.Speed > stats.ValueRO.baseSpeed + 0.001f;
            }

            if (sprinting)
                data.currentStamina -= cfg.sprintStaminaCostPerSecond * deltaTime;
            else if (!SystemAPI.HasComponent<HeroCombatComponent>(entity)
                     || !SystemAPI.GetComponent<HeroCombatComponent>(entity).isAttacking)
                data.currentStamina += data.regenRate * deltaTime;

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

        // The hero's horse (M19): galloping (faster than the trot) wears its own stamina down; otherwise it recovers.
        var mount = cfg.mount.OrDefault();
        foreach (var (horse, stamina) in SystemAPI.Query<RefRO<HeroHorseComponent>, RefRW<StaminaComponent>>())
        {
            var data = stamina.ValueRW;
            Entity rider = horse.ValueRO.rider;
            bool galloping = SystemAPI.HasComponent<HeroMoveIntent>(rider)
                && SystemAPI.GetComponent<HeroMoveIntent>(rider).Speed > mount.trotSpeed + 0.01f;
            data.currentStamina = math.clamp(
                data.currentStamina + (galloping ? -mount.gallopStaminaPerSecond : data.regenRate) * deltaTime, 0f, data.maxStamina);
            if (data.currentStamina <= 0f) data.isExhausted = true;
            else if (data.currentStamina > data.maxStamina * cfg.exhaustionRecoveryThreshold) data.isExhausted = false;
            stamina.ValueRW = data;
        }
    }
}


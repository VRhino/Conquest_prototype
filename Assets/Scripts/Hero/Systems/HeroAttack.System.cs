using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Manages hero attack state: input/AI decision → attack initiation, cooldown,
/// animation timer, and WeaponHitboxActiveTag gating.
/// Hit detection and PendingDamageEvent creation are handled by WeaponHitboxBehaviour.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HeroAIExecutionSystem))]
public partial class HeroAttackSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
        RequireForUpdate<HeroGameplayConfigComponent>();
    }

    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        var cfg = SystemAPI.GetSingleton<HeroGameplayConfigComponent>();

        // Process attack input and start animations
        foreach (var (input, combat, stamina, life, anim, entity) in
                 SystemAPI.Query<RefRO<HeroInputComponent>,
                                 RefRW<HeroCombatComponent>,
                                 RefRW<StaminaComponent>,
                                 RefRO<HeroLifeComponent>,
                                 RefRW<HeroAnimationComponent>>()
                        .WithAll<IsLocalPlayer>()
                        .WithEntityAccess())
        {
            if (!life.ValueRO.isAlive)
                continue;

            var c = combat.ValueRW;
            c.attackCooldown = math.max(0f, c.attackCooldown - deltaTime);

            if (input.ValueRO.IsAttackPressed && !c.isAttacking && c.attackCooldown <= 0f &&
                !stamina.ValueRO.isExhausted && stamina.ValueRO.currentStamina >= cfg.attackStaminaCost)
            {
                c.isAttacking = true;
                c.attackCooldown = cfg.attackCooldown;
                c.attackAnimationTimer = 0f;
                anim.ValueRW.triggerAttack = true;
                stamina.ValueRW.currentStamina -= cfg.attackStaminaCost;
            }

            combat.ValueRW = c;
        }

        // AI hero attack processing — reads HeroAIDecision.shouldAttack instead of player input
        foreach (var (decision, combat, stamina, life, anim, entity) in
                 SystemAPI.Query<RefRO<HeroAIDecision>,
                                 RefRW<HeroCombatComponent>,
                                 RefRW<StaminaComponent>,
                                 RefRO<HeroLifeComponent>,
                                 RefRW<HeroAnimationComponent>>()
                          .WithAll<HeroAITag>()
                          .WithEntityAccess())
        {
            if (!life.ValueRO.isAlive)
                continue;

            var c = combat.ValueRW;
            c.attackCooldown = math.max(0f, c.attackCooldown - deltaTime);

            if (decision.ValueRO.shouldAttack && !c.isAttacking && c.attackCooldown <= 0f &&
                !stamina.ValueRO.isExhausted && stamina.ValueRO.currentStamina >= cfg.attackStaminaCost)
            {
                c.isAttacking = true;
                c.attackCooldown = cfg.attackCooldown;
                c.attackAnimationTimer = 0f;
                anim.ValueRW.triggerAttack = true;
                stamina.ValueRW.currentStamina -= cfg.attackStaminaCost;
            }

            combat.ValueRW = c;
        }

        // Phase 2+3: tick attack animation timer, gate WeaponHitboxActiveTag, reset when done
        foreach (var (combat, entity) in
                 SystemAPI.Query<RefRW<HeroCombatComponent>>().WithEntityAccess())
        {
            if (!combat.ValueRO.isAttacking) continue;

            var c = combat.ValueRW;
            c.attackAnimationTimer += deltaTime;

            bool inWindow = c.attackAnimationTimer >= c.strikeWindowStart &&
                            c.attackAnimationTimer <= c.strikeWindowStart + c.strikeWindowDuration;
            SystemAPI.SetComponentEnabled<WeaponHitboxActiveTag>(entity, inWindow && !c.hitboxFired);

            if (c.attackAnimationTimer >= c.attackAnimationDuration)
            {
                c.isAttacking = false;
                c.attackAnimationTimer = 0f;
                c.hitboxFired = false;
                SystemAPI.SetComponentEnabled<WeaponHitboxActiveTag>(entity, false);
            }

            combat.ValueRW = c;
        }

    }
}

using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// CB-style unit attack state machine — three phases per swing:
///
///   Phase 1 — Decision:
///     target valid + in detectionRange AABB + !isAttacking + cooldown=0
///     → isAttacking=true, reset timer and hitboxFired
///
///   Phase 2 — Strike window:
///     timer in [strikeWindowStart, strikeWindowStart+strikeWindowDuration]
///     → SetComponentEnabled WeaponHitboxActiveTag = true (HitboxCollisionSystem detects overlap)
///     → outside window → disable tag
///
///   Phase 3 — End of animation:
///     timer >= attackAnimationDuration
///     → isAttacking=false, apply attackInterval cooldown, reset for next swing
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class UnitAttackSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;
        var hitboxTagLookup = GetComponentLookup<WeaponHitboxActiveTag>();

        foreach (var (combat, weapon, transform, entity) in
                 SystemAPI.Query<RefRW<UnitCombatComponent>,
                                  RefRO<UnitWeaponComponent>,
                                  RefRO<LocalTransform>>()
                          .WithEntityAccess())
        {
            ref var c = ref combat.ValueRW;

            // Always tick cooldown
            c.attackCooldown = math.max(0f, c.attackCooldown - dt);

            // ── Phase 2 & 3 — animation in progress ──────────────────────────
            if (c.isAttacking)
            {
                c.attackAnimationTimer += dt;

                bool inWindow = c.attackAnimationTimer >= weapon.ValueRO.strikeWindowStart
                             && c.attackAnimationTimer <  weapon.ValueRO.strikeWindowStart
                                                        + weapon.ValueRO.strikeWindowDuration;

                // Toggle hitbox tag for HitboxCollisionSystem
                if (SystemAPI.HasComponent<WeaponHitboxRef>(entity))
                {
                    Entity hbe = SystemAPI.GetComponent<WeaponHitboxRef>(entity).hitboxEntity;
                    if (hbe != Entity.Null && hitboxTagLookup.HasComponent(hbe))
                        hitboxTagLookup.SetComponentEnabled(hbe, inWindow);
                }

                // Phase 3 — animation done
                if (c.attackAnimationTimer >= weapon.ValueRO.attackAnimationDuration)
                {
                    c.isAttacking          = false;
                    c.attackAnimationTimer = 0f;
                    c.hitboxFired          = false;
                    c.attackCooldown       = weapon.ValueRO.attackInterval;

                    // Ensure hitbox is disabled when animation ends
                    if (SystemAPI.HasComponent<WeaponHitboxRef>(entity))
                    {
                        Entity hbe = SystemAPI.GetComponent<WeaponHitboxRef>(entity).hitboxEntity;
                        if (hbe != Entity.Null && hitboxTagLookup.HasComponent(hbe))
                            hitboxTagLookup.SetComponentEnabled(hbe, false);
                    }
                }
                continue;
            }

            // ── Phase 1 — decision ────────────────────────────────────────────
            Entity target = c.target;
            if (target == Entity.Null || !SystemAPI.Exists(target))
            {
                c.target = Entity.Null;
                continue;
            }

            // Target alive check
            bool alive = true;
            if (SystemAPI.HasComponent<HealthComponent>(target))
                alive = SystemAPI.GetComponent<HealthComponent>(target).currentHealth > 0f;
            else if (SystemAPI.HasComponent<HeroLifeComponent>(target))
                alive = SystemAPI.GetComponent<HeroLifeComponent>(target).isAlive;
            if (!alive) { c.target = Entity.Null; continue; }

            // Range check: directional AABB matching the weapon damage box
            if (!SystemAPI.HasComponent<LocalTransform>(target)) continue;
            float3 targetPos = SystemAPI.GetComponent<LocalTransform>(target).Position;

            float3 forward = math.forward(transform.ValueRO.Rotation);
            float  halfD   = math.max(0.01f,
                (weapon.ValueRO.attackRange - weapon.ValueRO.damageZoneStart) * 0.5f);
            float3 center  = transform.ValueRO.Position
                           + forward * (weapon.ValueRO.damageZoneStart + halfD)
                           + new float3(0f, weapon.ValueRO.damageZoneYOffset, 0f);
            float3 halfExt = new float3(
                weapon.ValueRO.damageZoneHalfWidth,
                weapon.ValueRO.damageZoneHalfHeight,
                halfD);
            bool inRange = math.all(targetPos >= center - halfExt & targetPos <= center + halfExt);

            if (inRange && c.attackCooldown <= 0f)
            {
                c.isAttacking          = true;
                c.attackAnimationTimer = 0f;
                c.hitboxFired          = false;
            }
        }
    }
}

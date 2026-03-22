using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// CB-style unit attack state machine — three phases per swing:
///
///   Phase 1 — Decision:
///     target valid + in OBB range + !isAttacking + cooldown=0
///     → isAttacking=true, reset timer and hitboxFired
///
///   Phase 2 — Strike window:
///     timer in [strikeWindowStart, strikeWindowStart+strikeWindowDuration]
///     → WeaponHitboxActiveTag enabled on the unit (WeaponHitboxBehaviour reads this gate)
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

                // Gate WeaponHitboxBehaviour — tag lives on the unit entity itself
                if (SystemAPI.HasComponent<WeaponHitboxActiveTag>(entity))
                {
                    bool wasInWindow = SystemAPI.IsComponentEnabled<WeaponHitboxActiveTag>(entity);
                    SystemAPI.SetComponentEnabled<WeaponHitboxActiveTag>(entity, inWindow);

                }

                // Phase 3 — animation done
                if (c.attackAnimationTimer >= weapon.ValueRO.attackAnimationDuration)
                {
                    c.isAttacking          = false;
                    c.attackAnimationTimer = 0f;
                    c.hitboxFired          = false;
                    c.attackCooldown       = weapon.ValueRO.attackInterval;

                    if (SystemAPI.HasComponent<WeaponHitboxActiveTag>(entity))
                        SystemAPI.SetComponentEnabled<WeaponHitboxActiveTag>(entity, false);
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

            // Range check: simple 2D distance (XZ plane) — actual hit detection is
            // handled by the designer-placed WeaponHitbox BoxCollider on the unit prefab.
            if (!SystemAPI.HasComponent<LocalTransform>(target)) continue;
            float3 targetPos = SystemAPI.GetComponent<LocalTransform>(target).Position;

            float3 toTarget = targetPos - transform.ValueRO.Position;
            float  dist2D   = math.sqrt(toTarget.x * toTarget.x + toTarget.z * toTarget.z);
            bool   inRange  = dist2D <= weapon.ValueRO.attackRange;

            if (inRange && c.attackCooldown <= 0f)
            {
                c.isAttacking          = true;
                c.attackAnimationTimer = 0f;
                c.hitboxFired          = false;

            }
            else
            {

            }
        }
    }
}

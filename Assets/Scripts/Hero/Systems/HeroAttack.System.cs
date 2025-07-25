using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// System that handles hero attack input, manages cooldowns and enables the
/// weapon collider during the impact frames. When a collision is detected a
/// <see cref="PendingDamageEvent"/> is created on the hero.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class HeroAttackSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;

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
                !stamina.ValueRO.isExhausted && stamina.ValueRO.currentStamina >= 15f)
            {
                c.isAttacking = true;
                c.attackCooldown = 0.7f;
                anim.ValueRW.triggerAttack = true;
                stamina.ValueRW.currentStamina -= 15f;

                if (SystemAPI.Exists(c.activeWeapon) &&
                    SystemAPI.HasComponent<WeaponColliderComponent>(c.activeWeapon))
                {
                    var weapon = SystemAPI.GetComponentRW<WeaponColliderComponent>(c.activeWeapon);
                    weapon.ValueRW.owner = entity;
                    weapon.ValueRW.isActive = true;
                }
            }

            combat.ValueRW = c;
        }

        // Check for collisions while weapon colliders are active
        foreach ((RefRW<WeaponColliderComponent> weapon,
                  RefRO<PhysicsCollider> collider,
                  RefRO<LocalTransform> transform,
                  RefRO<UnitWeaponComponent> weaponData,
                  Entity entity) in
                 SystemAPI.Query<RefRW<WeaponColliderComponent>,
                                 RefRO<PhysicsCollider>,
                                 RefRO<LocalTransform>,
                                 RefRO<UnitWeaponComponent>>()
                        .WithEntityAccess())
        {
            if (!weapon.ValueRO.isActive)
                continue;

            var rigidTransform = new RigidTransform(transform.ValueRO.Rotation, transform.ValueRO.Position);
            var aabb = collider.ValueRO.Value.Value.CalculateAabb(rigidTransform);

            var input = new OverlapAabbInput
            {
                Aabb = aabb,
                Filter = CollisionFilter.Default
            };

            var hits = new NativeList<int>(Allocator.Temp);
            physicsWorld.CollisionWorld.OverlapAabb(input, ref hits);

            for (int i = 0; i < hits.Length; i++)
            {
                Entity hitEntity = physicsWorld.Bodies[hits[i]].Entity;
                if (hitEntity == weapon.ValueRO.owner)
                    continue;

                if (!SystemAPI.HasComponent<PendingDamageEvent>(weapon.ValueRO.owner))
                {
                    bool crit = UnityEngine.Random.value <= weaponData.ValueRO.criticalChance;
                    Team team = Team.None;
                    if (SystemAPI.HasComponent<TeamComponent>(weapon.ValueRO.owner))
                        team = SystemAPI.GetComponent<TeamComponent>(weapon.ValueRO.owner).value;

                    EntityManager.AddComponentData(weapon.ValueRO.owner, new PendingDamageEvent
                    {
                        target = hitEntity,
                        damageSource = weapon.ValueRO.owner,
                        damageProfile = weaponData.ValueRO.damageProfile,
                        sourceTeam = team,
                        category = crit ? DamageCategory.Critical : DamageCategory.Normal,
                        multiplier = crit ? 1.5f : 1f
                    });
                }
            }

            hits.Dispose();
            weapon.ValueRW.isActive = false;
        }
    }
}

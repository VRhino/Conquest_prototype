using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using ConquestTactics.Animation;

/// <summary>
/// Detects weapon hitbox overlaps with enemy hurtboxes each frame using AABB queries
/// against the Unity Physics collision world.
///
/// Only processes units whose WeaponHitboxActiveTag is currently enabled (i.e. inside
/// the strike window). One PendingDamageEvent is created per swing (hitboxFired flag).
///
/// Physics layer requirements (configure in Unity Physics Category Names):
///   Layer 6 = Hurtbox   (unit capsules)
///   Layer 7 = Hitbox    (weapon boxes)
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class HitboxCollisionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        if (!SystemAPI.HasSingleton<PhysicsWorldSingleton>()) return;

        var collisionWorld  = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
        var teamLookup      = GetComponentLookup<TeamComponent>(true);
        var animLookup      = GetComponentLookup<UnitAnimationMovementComponent>(true);
        var activeTagLookup = GetComponentLookup<WeaponHitboxActiveTag>(true);
        var ecb             = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (combat, weapon, weaponRef, transform, entity) in
                 SystemAPI.Query<RefRW<UnitCombatComponent>,
                                  RefRO<UnitWeaponComponent>,
                                  RefRO<WeaponHitboxRef>,
                                  RefRO<LocalTransform>>()
                          .WithEntityAccess())
        {
            if (!combat.ValueRO.isAttacking) continue;
            if (combat.ValueRO.hitboxFired)  continue;

            Entity hitboxEntity = weaponRef.ValueRO.hitboxEntity;
            if (!activeTagLookup.HasComponent(hitboxEntity))           continue;
            if (!activeTagLookup.IsComponentEnabled(hitboxEntity)) continue;

            // Build AABB matching the weapon damage box
            float3 pos     = transform.ValueRO.Position;
            float3 forward = math.forward(transform.ValueRO.Rotation);
            float  halfD   = math.max(0.01f,
                (weapon.ValueRO.attackRange - weapon.ValueRO.damageZoneStart) * 0.5f);
            float3 center  = pos
                           + forward * (weapon.ValueRO.damageZoneStart + halfD)
                           + new float3(0f, weapon.ValueRO.damageZoneYOffset, 0f);
            float3 halfExt = new float3(
                weapon.ValueRO.damageZoneHalfWidth,
                weapon.ValueRO.damageZoneHalfHeight,
                halfD);

            var overlapInput = new OverlapAabbInput
            {
                Aabb   = new Aabb { Min = center - halfExt, Max = center + halfExt },
                Filter = new CollisionFilter
                {
                    BelongsTo    = PhysicsLayers.HitboxMask,
                    CollidesWith = PhysicsLayers.HurtboxMask,
                    GroupIndex   = 0
                }
            };

            var hits = new NativeList<int>(8, Allocator.Temp);
            if (collisionWorld.OverlapAabb(overlapInput, ref hits))
            {
                Team sourceTeam = Team.None;
                bool hasTeam    = teamLookup.HasComponent(entity);
                if (hasTeam) sourceTeam = teamLookup[entity].value;

                float attackerSpeed = 0f;
                if (animLookup.HasComponent(entity))
                    attackerSpeed = animLookup[entity].CurrentSpeed;

                for (int h = 0; h < hits.Length; h++)
                {
                    Entity hitEntity = collisionWorld.Bodies[hits[h]].Entity;
                    if (hitEntity == entity || hitEntity == Entity.Null) continue;

                    if (hasTeam && teamLookup.HasComponent(hitEntity))
                        if (teamLookup[hitEntity].value == sourceTeam) continue;

                    if (!SystemAPI.HasComponent<PendingDamageEvent>(entity))
                    {
                        bool crit = UnityEngine.Random.value <= weapon.ValueRO.criticalChance;
                        ecb.AddComponent(entity, new PendingDamageEvent
                        {
                            target           = hitEntity,
                            damageSource     = entity,
                            damageProfile    = weapon.ValueRO.damageProfile,
                            sourceTeam       = sourceTeam,
                            category         = crit ? DamageCategory.Critical : DamageCategory.Normal,
                            multiplier       = crit ? weapon.ValueRO.criticalMultiplier : 1f,
                            attackDirection  = forward,
                            attackerSpeed    = attackerSpeed,
                            attackerPosition = pos
                        });
                    }

                    combat.ValueRW.hitboxFired = true;
                    break; // one target per swing
                }
            }
            hits.Dispose();
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}

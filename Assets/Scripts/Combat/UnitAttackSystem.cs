using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Executes melee or ranged attacks for each unit when their target is within
/// range and the attack cooldown has elapsed.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class UnitAttackSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (combat, weapon, transform, entity) in
                 SystemAPI.Query<RefRW<UnitCombatComponent>,
                                 RefRO<UnitWeaponComponent>,
                                 RefRO<LocalTransform>>()
                        .WithEntityAccess())
        {
            Entity target = combat.ValueRO.target;
            if (target == Entity.Null || !SystemAPI.Exists(target))
            {
                combat.ValueRW.target = Entity.Null;
                continue;
            }

            bool targetAlive = true;
            if (SystemAPI.HasComponent<HealthComponent>(target))
            {
                var health = SystemAPI.GetComponent<HealthComponent>(target);
                targetAlive = health.currentHealth > 0f;
            }
            else if (SystemAPI.HasComponent<HeroLifeComponent>(target))
            {
                targetAlive = SystemAPI.GetComponent<HeroLifeComponent>(target).isAlive;
            }
            if (!targetAlive)
            {
                combat.ValueRW.target = Entity.Null;
                continue;
            }

            float3 targetPos = float3.zero;
            if (SystemAPI.HasComponent<LocalTransform>(target))
            {
                targetPos = SystemAPI.GetComponent<LocalTransform>(target).Position;
            }
            else
            {
                continue;
            }

            float distSq = math.distancesq(transform.ValueRO.Position, targetPos);
            bool inRange = distSq <= weapon.ValueRO.attackRange * weapon.ValueRO.attackRange;

            var c = combat.ValueRW;
            c.attackCooldown = math.max(0f, c.attackCooldown - deltaTime);

            if (inRange && c.attackCooldown <= 0f)
            {
                if (!SystemAPI.HasComponent<PendingDamageEvent>(entity))
                {
                    bool crit = UnityEngine.Random.value <= weapon.ValueRO.criticalChance;
                    Team team = Team.None;
                    if (SystemAPI.HasComponent<TeamComponent>(entity))
                        team = SystemAPI.GetComponent<TeamComponent>(entity).value;

                    SystemAPI.AddComponent(entity, new PendingDamageEvent
                    {
                        target = target,
                        damageSource = entity,
                        damageProfile = weapon.ValueRO.damageProfile,
                        sourceTeam = team,
                        category = crit ? DamageCategory.Critical : DamageCategory.Normal,
                        multiplier = crit ? 1.5f : 1f
                    });
                }

                c.attackCooldown = weapon.ValueRO.attackInterval;
            }

            combat.ValueRW = c;
        }
    }
}

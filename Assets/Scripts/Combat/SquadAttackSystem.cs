using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Executes automatic squad attacks against nearby enemies at fixed intervals.
/// The system does not rely on per-unit colliders, instead applying damage
/// events directly when enemies are within range.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SquadAttackSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;

        var weaponLookup = GetComponentLookup<UnitWeaponComponent>(true);
        var transformLookup = GetComponentLookup<LocalTransform>(true);
        var healthLookup = GetComponentLookup<HealthComponent>(true);
        var heroLifeLookup = GetComponentLookup<HeroLifeComponent>(true);

        foreach (var (combat, ai, units, targets, entity) in SystemAPI
                     .Query<RefRW<SquadCombatComponent>,
                            RefRO<SquadAIComponent>,
                            DynamicBuffer<SquadUnitElement>,
                            DynamicBuffer<SquadTargetEntity>>()
                     .WithEntityAccess())
        {
            if (ai.ValueRO.state != SquadAIState.Attacking)
                continue;

            var c = combat.ValueRW;
            c.attackTimer += dt;

            if (c.attackTimer >= c.attackInterval &&
                units.Length > 0 && targets.Length > 0)
            {
                int targetCount = targets.Length;
                for (int i = 0; i < units.Length; i++)
                {
                    Entity unit = units[i].Value;
                    if (!SystemAPI.Exists(unit) ||
                        !weaponLookup.HasComponent(unit) ||
                        !transformLookup.HasComponent(unit))
                        continue;

                    var weapon = weaponLookup[unit];
                    float3 unitPos = transformLookup[unit].Position;
                    float rangeSq = weapon.attackRange * weapon.attackRange;

                    Entity chosen = Entity.Null;
                    float bestDist = float.MaxValue;
                    for (int j = 0; j < targetCount; j++)
                    {
                        Entity enemy = targets[j].Value;
                        if (!SystemAPI.Exists(enemy) ||
                            !transformLookup.HasComponent(enemy))
                            continue;

                        float3 enemyPos = transformLookup[enemy].Position;
                        float distSq = math.distancesq(unitPos, enemyPos);
                        if (distSq <= rangeSq && distSq < bestDist)
                        {
                            bool alive = true;
                            if (healthLookup.HasComponent(enemy))
                                alive = healthLookup[enemy].currentHealth > 0f;
                            else if (heroLifeLookup.HasComponent(enemy))
                                alive = heroLifeLookup[enemy].isAlive;

                            if (!alive)
                                continue;

                            bestDist = distSq;
                            chosen = enemy;
                        }
                    }

                    if (chosen != Entity.Null &&
                        !SystemAPI.HasComponent<PendingDamageEvent>(unit))
                    {
                        bool crit = Random.value <= weapon.criticalChance;
                        Team team = Team.None;
                        if (SystemAPI.HasComponent<TeamComponent>(unit))
                            team = SystemAPI.GetComponent<TeamComponent>(unit).value;

                        SystemAPI.AddComponent(unit, new PendingDamageEvent
                        {
                            target = chosen,
                            damageSource = unit,
                            damageProfile = weapon.damageProfile,
                            sourceTeam = team,
                            category = crit ? DamageCategory.Critical : DamageCategory.Normal,
                            multiplier = crit ? 1.5f : 1f
                        });
                    }
                }

                c.attackTimer = 0f;
            }

            combat.ValueRW = c;
        }
    }
}

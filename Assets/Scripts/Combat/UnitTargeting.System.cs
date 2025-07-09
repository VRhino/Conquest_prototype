using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// System that assigns a target enemy to each unit of a squad based on the
/// current squad state and player order.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class UnitTargetingSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach (var (ai, state, units, squadEntity) in SystemAPI
                     .Query<RefRO<SquadAIComponent>,
                            RefRO<SquadStateComponent>,
                            DynamicBuffer<SquadUnitElement>>()
                     .WithEntityAccess())
        {
            bool allow = ai.ValueRO.state == SquadAIState.Attacking &&
                         (state.ValueRO.currentOrder == SquadOrderType.Attack ||
                          state.ValueRO.currentOrder == SquadOrderType.FollowHero ||
                          (state.ValueRO.currentOrder == SquadOrderType.HoldPosition));

            // Temporary map to track how many units are attacking each enemy
            var enemyCounts = new NativeParallelHashMap<Entity, int>(16, Allocator.Temp);

            // First pass: choose closest target for each unit
            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (!SystemAPI.Exists(unit) || !SystemAPI.HasComponent<UnitCombatComponent>(unit))
                    continue;

                var combat = SystemAPI.GetComponentRW<UnitCombatComponent>(unit);

                if (!allow)
                {
                    combat.ValueRW.target = Entity.Null;
                    continue;
                }

                if (!SystemAPI.HasBuffer<UnitDetectedEnemy>(unit))
                {
                    combat.ValueRW.target = Entity.Null;
                    continue;
                }

                var detected = SystemAPI.GetBuffer<UnitDetectedEnemy>(unit);
                if (detected.Length == 0)
                {
                    combat.ValueRW.target = Entity.Null;
                    continue;
                }

                bool currentValid = false;
                for (int j = 0; j < detected.Length; j++)
                {
                    if (detected[j].Value == combat.ValueRO.target)
                    {
                        currentValid = true;
                        break;
                    }
                }

                if (!currentValid)
                {
                    float3 unitPos = SystemAPI.GetComponent<LocalTransform>(unit).Position;
                    float bestDist = float.MaxValue;
                    Entity bestEnemy = Entity.Null;
                    for (int j = 0; j < detected.Length; j++)
                    {
                        Entity enemy = detected[j].Value;
                        if (!SystemAPI.Exists(enemy) || !SystemAPI.HasComponent<LocalTransform>(enemy))
                            continue;
                        float3 enemyPos = SystemAPI.GetComponent<LocalTransform>(enemy).Position;
                        float dist = math.distancesq(unitPos, enemyPos);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestEnemy = enemy;
                        }
                    }
                    combat.ValueRW.target = bestEnemy;
                }

                if (combat.ValueRW.target != Entity.Null)
                {
                    enemyCounts.TryAdd(combat.ValueRW.target, 0);
                    enemyCounts[combat.ValueRW.target] = enemyCounts[combat.ValueRW.target] + 1;
                }
            }

            // Second pass: redistribute if too many units target the same enemy
            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (!SystemAPI.Exists(unit) || !SystemAPI.HasComponent<UnitCombatComponent>(unit))
                    continue;

                var combat = SystemAPI.GetComponentRW<UnitCombatComponent>(unit);
                if (combat.ValueRO.target == Entity.Null)
                    continue;

                if (!enemyCounts.TryGetValue(combat.ValueRO.target, out int count) || count <= 3)
                    continue;

                if (!SystemAPI.HasBuffer<UnitDetectedEnemy>(unit))
                    continue;
                var detected = SystemAPI.GetBuffer<UnitDetectedEnemy>(unit);

                for (int j = 0; j < detected.Length; j++)
                {
                    Entity candidate = detected[j].Value;
                    if (candidate == combat.ValueRO.target)
                        continue;

                    int current = 0;
                    enemyCounts.TryGetValue(candidate, out current);
                    if (current < 3)
                    {
                        enemyCounts[combat.ValueRO.target] = count - 1;
                        combat.ValueRW.target = candidate;
                        enemyCounts.TryAdd(candidate, 0);
                        enemyCounts[candidate] = current + 1;
                        break;
                    }
                }
            }

            enemyCounts.Dispose();
        }
    }
}

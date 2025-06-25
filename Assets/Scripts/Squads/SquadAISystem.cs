using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Decides the tactical AI state for each squad based on the current order,
/// detected enemies and squad cohesion. Movement or attacks are handled by
/// other systems.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SquadAISystem : SystemBase
{
    protected override void OnUpdate()
    {
        const float cohesionRadiusSq = 25f; // Distance squared to consider units scattered

        foreach (var (ai, state, units, entity) in SystemAPI
                     .Query<RefRW<SquadAIComponent>,
                            RefRO<SquadStateComponent>,
                            DynamicBuffer<SquadUnitElement>>()
                     .WithEntityAccess())
        {
            bool enemiesDetected = false;
            if (SystemAPI.HasBuffer<DetectedEnemy>(entity))
            {
                var detected = SystemAPI.GetBuffer<DetectedEnemy>(entity);
                enemiesDetected = detected.Length > 0;
            }

            bool dispersed = false;
            if (units.Length > 0)
            {
                Entity leader = units[0].Value;
                if (SystemAPI.Exists(leader))
                {
                    float3 leaderPos = SystemAPI.GetComponent<LocalTransform>(leader).Position;
                    for (int i = 0; i < units.Length; i++)
                    {
                        Entity unit = units[i].Value;
                        if (!SystemAPI.Exists(unit))
                            continue;

                        float3 pos = SystemAPI.GetComponent<LocalTransform>(unit).Position;
                        if (math.distancesq(pos, leaderPos) > cohesionRadiusSq)
                        {
                            dispersed = true;
                            break;
                        }
                    }
                }
            }

            SquadAIState desiredState;
            if (dispersed)
            {
                desiredState = SquadAIState.Regrouping;
            }
            else if (state.ValueRO.currentOrder == SquadOrderType.FollowHero ||
                     state.ValueRO.currentOrder == SquadOrderType.Attack)
            {
                desiredState = enemiesDetected ? SquadAIState.Attacking : SquadAIState.Idle;
            }
            else if (state.ValueRO.currentOrder == SquadOrderType.HoldPosition)
            {
                desiredState = enemiesDetected ? SquadAIState.Attacking : SquadAIState.Defending;
            }
            else
            {
                desiredState = SquadAIState.Idle;
            }

            ai.ValueRW.state = desiredState;
            ai.ValueRW.isInCombat = enemiesDetected;
        }
    }
}

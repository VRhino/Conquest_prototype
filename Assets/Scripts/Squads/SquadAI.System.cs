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

        var dataLookup = GetComponentLookup<SquadDataComponent>(true);

        foreach (var (ai, state, dataRef, units, entity) in SystemAPI
                     .Query<RefRW<SquadAIComponent>,
                            RefRW<SquadStateComponent>,
                            RefRO<SquadDataReference>,
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

            BehaviorProfile profile = BehaviorProfile.Versatile;
            if (dataLookup.TryGetComponent(dataRef.ValueRO.dataEntity, out var data))
                profile = data.behaviorProfile;

            SquadAIState desiredState;
            switch (profile)
            {
                case BehaviorProfile.Defensive:
                    desiredState = dispersed ? SquadAIState.Regrouping
                                             : enemiesDetected ? SquadAIState.Defending
                                                               : SquadAIState.Idle;
                    break;
                case BehaviorProfile.Harassing:
                    desiredState = dispersed ? SquadAIState.Regrouping
                                             : enemiesDetected ? SquadAIState.Attacking
                                                               : SquadAIState.Idle;
                    break;
                case BehaviorProfile.AntiCharge:
                    desiredState = dispersed ? SquadAIState.Regrouping
                                             : SquadAIState.Defending;
                    break;
                default: // Versatile
                    if (dispersed)
                        desiredState = SquadAIState.Regrouping;
                    else if (enemiesDetected)
                        desiredState = SquadAIState.Attacking;
                    else if (state.ValueRO.currentOrder == SquadOrderType.HoldPosition)
                        desiredState = SquadAIState.Defending;
                    else
                        desiredState = SquadAIState.Idle;
                    break;
            }

            ai.ValueRW.state = desiredState;
            ai.ValueRW.isInCombat = enemiesDetected;
            state.ValueRW.isInCombat = enemiesDetected;
        }
    }
}

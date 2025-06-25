using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Automatically adjusts squad formations when traversing narrow spaces
/// or when obstacles are detected. The player's chosen formation is stored
/// in <see cref="FormationComponent"/> and restored when space allows.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class FormationAdaptationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach (var (env, input, state, formation, units) in SystemAPI
                     .Query<RefRW<EnvironmentAwarenessComponent>,
                            RefRW<SquadInputComponent>,
                            RefRO<SquadStateComponent>,
                            RefRO<FormationComponent>,
                            DynamicBuffer<SquadUnitElement>>())
        {
            if (state.ValueRO.isInCombat || units.Length == 0)
                continue;

            Entity leader = units[0].Value;
            if (!SystemAPI.Exists(leader))
                continue;

            float3 leaderPos = SystemAPI.GetComponent<LocalTransform>(leader).Position;
            var envData = env.ValueRW;
            envData.obstacleDetected = Physics.CheckSphere(leaderPos, envData.detectionRadius);
            env.ValueRW = envData;

            bool narrow = envData.terrainType != TerrainType.Abierto || envData.obstacleDetected;
            FormationType desired = narrow ? FormationType.Line : formation.ValueRO.currentFormation;

            if (input.ValueRO.desiredFormation != desired)
            {
                input.ValueRW.desiredFormation = desired;
            }
        }
    }
}

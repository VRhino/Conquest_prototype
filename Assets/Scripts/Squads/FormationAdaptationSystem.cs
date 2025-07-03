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
[UpdateAfter(typeof(SquadControlSystem))]
public partial class FormationAdaptationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ownerLookup = GetComponentLookup<SquadOwnerComponent>(true);
        var transformLookup = GetComponentLookup<LocalTransform>(true);
        
        foreach (var (env, input, state, formation, units, squadEntity) in SystemAPI
                     .Query<RefRW<EnvironmentAwarenessComponent>,
                            RefRW<SquadInputComponent>,
                            RefRO<SquadStateComponent>,
                            RefRO<FormationComponent>,
                            DynamicBuffer<SquadUnitElement>>()
                     .WithEntityAccess())
        {
            if (state.ValueRO.isInCombat || units.Length == 0)
                continue;

            // Usar la posición del héroe para detectar obstáculos
            if (!ownerLookup.TryGetComponent(squadEntity, out var squadOwner))
                continue;
            if (!transformLookup.TryGetComponent(squadOwner.hero, out var heroTransform))
                continue;

            float3 heroPos = heroTransform.Position;
            var envData = env.ValueRW;
            envData.obstacleDetected = Physics.CheckSphere(heroPos, envData.detectionRadius);
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

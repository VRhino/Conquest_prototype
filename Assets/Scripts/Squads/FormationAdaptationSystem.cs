using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Detects environmental obstacles and terrain conditions for individual unit navigation.
/// Units can use this information to adapt their pathfinding and movement behavior.
/// Does NOT modify squad formations - that's the player's choice.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(SquadControlSystem))]
public partial class FormationAdaptationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ownerLookup = GetComponentLookup<SquadOwnerComponent>(true);
        var transformLookup = GetComponentLookup<LocalTransform>(true);
        
        foreach (var (env, state, units, squadEntity) in SystemAPI
                     .Query<RefRW<EnvironmentAwarenessComponent>,
                            RefRO<SquadStateComponent>,
                            DynamicBuffer<SquadUnitElement>>()
                     .WithEntityAccess())
        {
            if (state.ValueRO.isInCombat || units.Length == 0)
                continue;

            // Usar la posición del héroe para detectar obstáculos
            if (!HeroPositionUtility.TryGetHeroPosition(squadEntity, ownerLookup, transformLookup, out float3 heroPos))
                continue;
            var envData = env.ValueRW;
            
            // Detectar obstáculos para que las unidades puedan usar esta información
            envData.obstacleDetected = Physics.CheckSphere(heroPos, envData.detectionRadius);
            
            // Detectar el tipo de terreno en la zona del escuadrón
            // Este información será utilizada por las unidades individuales para su navegación
            bool narrowSpace = envData.terrainType != TerrainType.Abierto || envData.obstacleDetected;
            envData.requiresAdaptation = narrowSpace;
            
            env.ValueRW = envData;
            
            // Las unidades individuales pueden leer EnvironmentAwarenessComponent 
            // para ajustar su comportamiento de movimiento y navegación
        }
    }
}

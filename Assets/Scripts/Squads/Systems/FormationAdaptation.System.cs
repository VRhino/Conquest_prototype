using Unity.Entities;
using Unity.Mathematics;
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
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
    }

    protected override void OnUpdate()
    {
        foreach (var (env, ai, heroWorldPos, units) in SystemAPI
                     .Query<RefRW<EnvironmentAwarenessComponent>,
                            RefRO<SquadAIComponent>,
                            RefRO<HeroWorldPositionComponent>,
                            DynamicBuffer<SquadUnitElement>>())
        {
            if (ai.ValueRO.isInCombat || units.Length == 0)
                continue;

            float3 heroPos = heroWorldPos.ValueRO.position;
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

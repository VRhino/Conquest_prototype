using Unity.Entities;
using UnityEngine;

/// <summary>
/// Authoring component para crear prefabs ECS-only de squad.
/// Este prefab solo contiene la lógica ECS, sin componentes visuales.
/// Los squads no tienen visuales propios, solo las unidades individuales.
/// </summary>
public class SquadEntityAuthoring : MonoBehaviour
{
    [Header("Squad ECS Configuration")]
    [Tooltip("Referencia al SquadData que define las características del squad")]
    public SquadDataAuthoring squadData;
}

/// <summary>
/// Baker para SquadEntityAuthoring. Solo agrega componentes ECS de lógica.
/// </summary>
public class SquadEntityBaker : Baker<SquadEntityAuthoring>
{
    public override void Bake(SquadEntityAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        
        // Componentes básicos del squad
        AddComponent<SquadAIComponent>(entity);
        AddComponent<SquadStateComponent>(entity);
        AddComponent<SquadInputComponent>(entity);
        AddComponent<FormationComponent>(entity);
        AddComponent<SquadProgressComponent>(entity);
        
        // Buffer para las unidades del squad
        AddBuffer<SquadUnitElement>(entity);
        AddBuffer<DetectedEnemy>(entity);
        
        // Los squads no tienen visuales propios, solo las unidades individuales
        // No se agrega SquadVisualReference
        
        // Referencia al SquadData si está asignado
        if (authoring.squadData != null)
        {
            var squadDataEntity = GetEntity(authoring.squadData, TransformUsageFlags.None);
            AddComponent(entity, new SquadDataReference
            {
                dataEntity = squadDataEntity
            });
        }
        
        // ECS-only squad entity baked
    }
}

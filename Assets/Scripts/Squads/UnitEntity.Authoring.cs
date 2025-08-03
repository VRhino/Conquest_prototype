using Unity.Entities;
using UnityEngine;

/// <summary>
/// Authoring component para crear prefabs ECS-only de unidad.
/// Este prefab solo contiene la lógica ECS, sin componentes visuales.
/// </summary>
public class UnitEntityAuthoring : MonoBehaviour
{
    [Header("Unit ECS Configuration")]
    [Tooltip("Velocidad base de movimiento")]
    public float baseSpeed = 3.5f;

    [Tooltip("Radio mínimo para evitar colisiones")]
    public float minDistance = 1.5f;

    [Tooltip("Fuerza de repulsión contra otras unidades")]
    public float repelForce = 1f;
    [Tooltip("data del escuadrón al que pertenece esta unidad")]
    public SquadData squadData;
}

/// <summary>
/// Baker para UnitEntityAuthoring. Solo agrega componentes ECS de lógica.
/// </summary>
public class UnitEntityBaker : Baker<UnitEntityAuthoring>
{
    public override void Bake(UnitEntityAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        
        // Componentes básicos de la unidad
        AddComponent(entity, new UnitEquipmentComponent
        {
            armorPercent = 100f,
            hasDebuff = false,
            isDeployable = true
        });
        
        AddComponent<UnitCombatComponent>(entity);
        
        AddComponent(entity, new UnitSpacingComponent
        {
            minDistance = authoring.minDistance,
            repelForce = authoring.repelForce
        });
        
        AddComponent<UnitTargetPositionComponent>(entity);
        AddComponent<UnitFormationStateComponent>(entity);
        
        // Orientación básica (puede ser modificada por el squad data)
        AddComponent(entity, new UnitOrientationComponent
        {
            orientationType = UnitOrientationType.MatchHeroDirection,
            rotationSpeed = 5f
        });
        
        // Referencia al visual prefab
        AddComponent(entity, new UnitVisualReference
        {
            visualPrefabName = authoring.squadData.visualPrefabName
        });
        
        // ECS-only unit entity baked
    }
}

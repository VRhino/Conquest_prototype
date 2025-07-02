using Unity.Entities;
using UnityEngine;

/// <summary>
/// Authoring component para configurar la orientación de unidades en formación.
/// </summary>
public class UnitOrientationAuthoring : MonoBehaviour
{
    [Header("Orientación")]
    [Tooltip("Tipo de orientación que debe usar esta unidad")]
    public UnitOrientationType orientationType = UnitOrientationType.FaceHero;
    
    [Tooltip("Velocidad de rotación (mayor = rotación más rápida)")]
    [Range(0.1f, 20f)]
    public float rotationSpeed = 5f;
}

/// <summary>
/// Baker para convertir UnitOrientationAuthoring a componente ECS.
/// </summary>
public class UnitOrientationBaker : Baker<UnitOrientationAuthoring>
{
    public override void Bake(UnitOrientationAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(entity, new UnitOrientationComponent
        {
            orientationType = authoring.orientationType,
            rotationSpeed = authoring.rotationSpeed
        });
    }
}

using Unity.Entities;

/// <summary>
/// Componente que define cómo debe orientarse una unidad cuando sigue al héroe.
/// </summary>
public struct UnitOrientationComponent : IComponentData
{
    /// <summary>Tipo de orientación a aplicar</summary>
    public UnitOrientationType orientationType;
    
    /// <summary>Velocidad de rotación (multiplicador para interpolación)</summary>
    public float rotationSpeed;
}

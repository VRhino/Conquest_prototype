using Unity.Entities;

/// <summary>
/// Configuración para el comportamiento de orientación de las unidades en formación.
/// </summary>
public enum UnitOrientationType
{
    /// <summary>Las unidades mantienen su orientación original</summary>
    None,
    /// <summary>Las unidades miran hacia el héroe</summary>
    FaceHero,
    /// <summary>Las unidades miran en la misma dirección que el héroe</summary>
    MatchHeroDirection,
    /// <summary>Las unidades miran hacia la dirección de movimiento</summary>
    FaceMovementDirection
}

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

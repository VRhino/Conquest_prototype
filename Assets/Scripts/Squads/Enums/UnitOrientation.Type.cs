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
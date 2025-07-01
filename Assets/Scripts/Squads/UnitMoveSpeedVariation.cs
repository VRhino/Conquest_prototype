using Unity.Entities;

/// <summary>
/// Componente para velocidad individual de cada unidad.
/// </summary>
public struct UnitMoveSpeedVariation : IComponentData
{
    public float speedMultiplier;
}

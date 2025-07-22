using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Guarda la posición previa del líder para cada unidad, para detectar inicio de movimiento.
/// </summary>
public struct UnitPrevLeaderPosComponent : IComponentData
{
    public float3 value;
}

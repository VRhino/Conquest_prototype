using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Componente que almacena la posición de una unidad en la cuadrícula local de su formación.
/// Reemplaza el sistema de localOffsets por posiciones discretas en cuadrícula.
/// </summary>
public struct UnitGridSlotComponent : IComponentData
{
    /// <summary>
    /// Posición en la cuadrícula local (enteros)
    /// (0,0) representa el centro de la formación (primera unidad)
    /// </summary>
    public int2 gridPosition;
    
    /// <summary>
    /// Índice de la unidad en el buffer del squad
    /// </summary>
    public int slotIndex;
    
    /// <summary>
    /// Posición en el mundo calculada desde la cuadrícula
    /// (para caché, se actualiza cuando cambia la formación)
    /// </summary>
    public float3 worldOffset;
}

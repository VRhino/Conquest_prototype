using UnityEngine;
using Unity.Mathematics;

/// <summary>
/// Define un patrón de formación basado en posiciones de cuadrícula.
/// Reemplaza los localOffsets por posiciones discretas en una cuadrícula 1x1.
/// </summary>
[CreateAssetMenu(menuName = "Formations/Grid Formation Pattern")]
public class GridFormationScriptableObject : ScriptableObject
{
    /// <summary>Tipo de formación que representa este patrón.</summary>
    public FormationType formationType;

    public Sprite formationIcon;

    /// <summary>
    /// Posiciones en la cuadrícula para las unidades del squad (sin incluir el héroe).
    /// Cada posición incluye el borde de 2 celdas alrededor de la formación.
    /// Cada celda es de 1x1 metros.
    /// </summary>
    public Vector2Int[] gridPositions;

    /// <summary>
    /// Convierte las posiciones de cuadrícula a offsets del mundo para compatibilidad.
    /// </summary>
    public Vector3[] GetWorldOffsets()
    {
        Vector3[] offsets = new Vector3[gridPositions.Length];
        for (int i = 0; i < gridPositions.Length; i++)
        {
            float3 worldPos = FormationGridSystem.GridToRelativeWorld(new int2(gridPositions[i].x, gridPositions[i].y));
            offsets[i] = new Vector3(worldPos.x, 0f, worldPos.z);
        }
        return offsets;
    }

    /// <summary>
    /// Convierte las posiciones de cuadrícula a offsets del mundo usando las posiciones absolutas.
    /// Mantiene compatibilidad con código existente que usa posiciones absolutas.
    /// </summary>
    public Vector3[] GetAbsoluteWorldOffsets()
    {
        Vector3[] offsets = new Vector3[gridPositions.Length];
        
        for (int i = 0; i < gridPositions.Length; i++)
        {
            float3 worldPos = FormationGridSystem.GridToRelativeWorld(new int2(gridPositions[i].x, gridPositions[i].y));
            offsets[i] = new Vector3(worldPos.x, 0f, worldPos.z);
        }
        
        return offsets;
    }

    /// <summary>
    /// Calcula el área total que ocupa esta formación incluyendo márgenes.
    /// </summary>
    public Vector2 GetFormationArea()
    {
        if (gridPositions.Length == 0) return Vector2.zero;

        int minX = int.MaxValue, maxX = int.MinValue;
        int minZ = int.MaxValue, maxZ = int.MinValue;

        foreach (var pos in gridPositions)
        {
            minX = math.min(minX, pos.x);
            maxX = math.max(maxX, pos.x);
            minZ = math.min(minZ, pos.y);
            maxZ = math.max(maxZ, pos.y);
        }

        float2 area = FormationGridSystem.GetTotalGridArea(new int2(minX, minZ), new int2(maxX, maxZ));
        return new Vector2(area.x, area.y);
    }

    private void OnValidate()
    {
        // Validation removed - hero is no longer part of the grid
        // All positions are now for squad units only
    }

    /// <summary>
    /// Note: Formation validation removed because hero is no longer part of the grid.
    /// All grid positions are now exclusively for squad units.
    /// </summary>
    [System.Obsolete("Formation validation removed - hero not in grid anymore")]
    public void ValidateFormation()
    {
        // No validation needed - all positions are valid squad unit positions
    }

    /// <summary>
    /// Convierte las posiciones de cuadrícula a offsets del mundo usando posiciones absolutas.
    /// Sin centrado - usa las coordenadas de grid tal como fueron diseñadas.
    /// </summary>
    public Vector3[] GetCenteredWorldOffsets()
    {
        if (gridPositions.Length == 0) return new Vector3[0];
        
        Vector3[] offsets = new Vector3[gridPositions.Length];
        
        for (int i = 0; i < gridPositions.Length; i++)
        {
            // Usar posición de grid directamente sin centrado
            float3 worldPos = FormationGridSystem.GridToRelativeWorld(new int2(gridPositions[i].x, gridPositions[i].y));
            offsets[i] = new Vector3(worldPos.x, 0f, worldPos.z);
        }
        
        return offsets;
    }
}

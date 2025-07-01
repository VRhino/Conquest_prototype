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
    /// Calcula el centro real de la formación basándose en las posiciones de las unidades.
    /// Para grillas con dimensiones pares, redondea hacia el centro de masa.
    /// </summary>
    /// <returns>Posición del centro de la formación en coordenadas de grid</returns>
    public Vector2Int GetFormationCenter()
    {
        if (gridPositions.Length == 0) return Vector2Int.zero;

        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;

        foreach (var pos in gridPositions)
        {
            minX = math.min(minX, pos.x);
            maxX = math.max(maxX, pos.x);
            minY = math.min(minY, pos.y);
            maxY = math.max(maxY, pos.y);
        }

        // Para dimensiones pares, usar el centro geométrico real
        // Para dimensiones impares, usar la celda central
        float centerX = (minX + maxX) / 2.0f;
        float centerY = (minY + maxY) / 2.0f;
        
        // Redondear al entero más cercano
        int finalCenterX = Mathf.RoundToInt(centerX);
        int finalCenterY = Mathf.RoundToInt(centerY);

        return new Vector2Int(finalCenterX, finalCenterY);
    }

    /// <summary>
    /// Obtiene las posiciones de las unidades relativas al centro de la formación.
    /// El héroe siempre estará en el centro (0,0) relativo.
    /// </summary>
    /// <returns>Posiciones ajustadas donde el centro de la formación es (0,0)</returns>
    public Vector2Int[] GetCenteredGridPositions()
    {
        if (gridPositions.Length == 0) return new Vector2Int[0];

        Vector2Int center = GetFormationCenter();
        Vector2Int[] centeredPositions = new Vector2Int[gridPositions.Length];

        for (int i = 0; i < gridPositions.Length; i++)
        {
            centeredPositions[i] = new Vector2Int(
                gridPositions[i].x - center.x,
                gridPositions[i].y - center.y
            );
        }

        return centeredPositions;
    }

    /// <summary>
    /// Convierte las posiciones centradas de cuadrícula a offsets del mundo.
    /// Usa las posiciones relativas al centro de la formación.
    /// </summary>
    public Vector3[] GetCenteredWorldOffsets()
    {
        Vector2Int[] centeredPositions = GetCenteredGridPositions();
        Vector3[] offsets = new Vector3[centeredPositions.Length];
        
        for (int i = 0; i < centeredPositions.Length; i++)
        {
            float3 worldPos = FormationGridSystem.GridToRelativeWorld(new int2(centeredPositions[i].x, centeredPositions[i].y));
            offsets[i] = new Vector3(worldPos.x, 0f, worldPos.z);
        }
        
        return offsets;
    }
}

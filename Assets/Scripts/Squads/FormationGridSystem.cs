using Unity.Mathematics;

/// <summary>
/// Sistema de cuadrícula local para formaciones de squad.
/// Cada celda es de 1x1 metros, con unidades distribuidas uniformemente.
/// </summary>
public static class FormationGridSystem
{
    public const float CELL_WIDTH = 1.0f;   // Ancho de celda en X
    public const float CELL_DEPTH = 1.0f;   // Profundidad de celda en Z
    public const float MARGIN = 5.0f;       // Margen alrededor de la formación

    /// <summary>Public properties for external access</summary>
    public static float CellWidth => CELL_WIDTH;
    public static float CellDepth => CELL_DEPTH;

    /// <summary>
    /// Convierte una posición de cuadrícula a coordenadas del mundo relativas al centro de la formación.
    /// </summary>
    /// <param name="gridPos">Posición en la cuadrícula (entero)</param>
    /// <returns>Posición en el mundo relativa al centro</returns>
    public static float3 GridToRelativeWorld(int2 gridPos)
    {
        return new float3(
            gridPos.x * CELL_WIDTH,
            0f,
            gridPos.y * CELL_DEPTH
        );
    }

    /// <summary>
    /// Convierte una posición del mundo a coordenadas de cuadrícula.
    /// </summary>
    /// <param name="worldPos">Posición en el mundo</param>
    /// <returns>Posición en la cuadrícula</returns>
    public static int2 WorldToGrid(float3 worldPos)
    {
        return new int2(
            (int)math.round(worldPos.x / CELL_WIDTH),
            (int)math.round(worldPos.z / CELL_DEPTH)
        );
    }

    /// <summary>
    /// Ajusta una posición del mundo al centro de la celda más cercana.
    /// </summary>
    /// <param name="worldPos">Posición original</param>
    /// <returns>Posición ajustada al centro de la celda</returns>
    public static float3 SnapToGrid(float3 worldPos)
    {
        int2 gridPos = WorldToGrid(worldPos);
        return GridToRelativeWorld(gridPos);
    }

    /// <summary>
    /// Calcula el área total de la cuadrícula para una formación dada.
    /// </summary>
    /// <param name="minGrid">Coordenada mínima de la cuadrícula</param>
    /// <param name="maxGrid">Coordenada máxima de la cuadrícula</param>
    /// <returns>Área total incluyendo márgenes</returns>
    public static float2 GetTotalGridArea(int2 minGrid, int2 maxGrid)
    {
        float width = (maxGrid.x - minGrid.x + 1) * CELL_WIDTH + (MARGIN * 2);
        float depth = (maxGrid.y - minGrid.y + 1) * CELL_DEPTH + (MARGIN * 2);
        return new float2(width, depth);
    }

    /// <summary>
    /// Convierte una posición del mundo relativa a coordenadas de cuadrícula.
    /// Similar a WorldToGrid pero especifica que es relativo al centro de formación.
    /// </summary>
    /// <param name="relativeWorldPos">Posición relativa al centro de formación</param>
    /// <returns>Posición en la cuadrícula</returns>
    public static int2 RelativeWorldToGrid(float3 relativeWorldPos)
    {
        return WorldToGrid(relativeWorldPos);
    }
}

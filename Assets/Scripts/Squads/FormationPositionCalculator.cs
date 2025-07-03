using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Calculadora centralizada para posiciones de formación.
/// Unifica toda la lógica de cálculo de posiciones deseadas para las unidades.
/// </summary>
public static class FormationPositionCalculator
{
    
    public static bool IsUnitInSlot(float3 unitPosition, float3 desiredPosition, float thresholdSq)
    {
        float distSq = math.lengthsq(desiredPosition - unitPosition);
        return distSq <= thresholdSq;
    }

    public static float calculateTerraindHeight(float3 position)
    {
        if (UnityEngine.Terrain.activeTerrain != null)
        {
            float terrainHeight = UnityEngine.Terrain.activeTerrain.SampleHeight(
                new UnityEngine.Vector3(position.x, 0, position.z));
            terrainHeight += UnityEngine.Terrain.activeTerrain.GetPosition().y; // Adjust for terrain position
            return terrainHeight;
        }
        return position.y; // Return original Y if no terrain
    }

    public static float3 CalculateDesiredPosition(
        Entity unit,
        ref BlobArray<int2> gridPositions,
        float3 squadCenter,
        int unitIndex,
        out int2 originalGridPos,
        out float3 gridOffset,
        out float3 worldPos,
        bool adjustForTerrain
        )
    {
        var squadOrigin = squadCenter;
        originalGridPos = gridPositions[unitIndex];
                            
        //calculo la poscicion central ubicando al heroe en el centro de la formacion
        int2 minGrid = new int2(int.MaxValue, int.MaxValue);
        int2 maxGrid = new int2(int.MinValue, int.MinValue);

        //busco los limites del grid
        for (int j = 0; j < gridPositions.Length; j++)
        {
            minGrid.x = math.min(minGrid.x, gridPositions[j].x);
            minGrid.y = math.min(minGrid.y, gridPositions[j].y);
            maxGrid.x = math.max(maxGrid.x, gridPositions[j].x);
            maxGrid.y = math.max(maxGrid.y, gridPositions[j].y);
        }
        //calculo el centro del grid
        int2 formationCenter = new int2(
            (int)math.round((minGrid.x + maxGrid.x) / 2.0f),
            (int)math.round((minGrid.y + maxGrid.y) / 2.0f)
        );

        // Calculo la posicion central relativa al centro de la formacion
        int2 centeredGridPos = new int2(
            originalGridPos.x - formationCenter.x,
            originalGridPos.y - formationCenter.y
        );

        //convierto la posicion central a world offset
        gridOffset = FormationGridSystem.GridToRelativeWorld(centeredGridPos);

        float3 baseXZ = squadOrigin + new float3(gridOffset.x, 0, gridOffset.z);

        // obtengo Unity terrain height
        float y = baseXZ.y;
        if (adjustForTerrain && UnityEngine.Terrain.activeTerrain != null)
        {
           y = calculateTerraindHeight(baseXZ);
        }
        worldPos = new float3(baseXZ.x, y, baseXZ.z);
        
        return worldPos;
    }

    public static bool isHeroInRange(DynamicBuffer<Entity> units, ComponentLookup<LocalTransform> transformLookup, float3 heroPosition, float range)
    {
        float3 squadCenter = float3.zero;
        int squadCount = 0;
        foreach (var unit in units)
        {
            if (transformLookup.HasComponent(unit))
            {
                squadCenter += transformLookup[unit].Position;
                squadCount++;
            }
        }

        if (squadCount > 0)
            squadCenter /= squadCount;

        float heroDistSq = math.lengthsq(heroPosition - squadCenter);
        return heroDistSq <= range; // range al cuadrado
    }

    public static bool isHeroInRange(DynamicBuffer<SquadUnitElement> units, ComponentLookup<LocalTransform> transformLookup, float3 heroPosition, float range)
    {
        float3 squadCenter = float3.zero;
        int squadCount = 0;
        foreach (var unitElement in units)
        {
            Entity unit = unitElement.Value;
            if (transformLookup.HasComponent(unit))
            {
                squadCenter += transformLookup[unit].Position;
                squadCount++;
            }
        }

        if (squadCount > 0)
            squadCenter /= squadCount;

        float heroDistSq = math.lengthsq(heroPosition - squadCenter);
        return heroDistSq <= range; // range al cuadrado
    }
}

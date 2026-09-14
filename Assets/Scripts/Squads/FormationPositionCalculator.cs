using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Calculadora centralizada para posiciones de formación.
/// Unifica toda la lógica de cálculo de posiciones deseadas para las unidades.
/// </summary>
public static class FormationPositionCalculator
{
    public static float3 GetSquadCenter(
        in SquadStateComponent squadState,
        SquadHoldPositionComponent? holdComponent,
        float3 heroPos)
    {
        return (squadState.currentOrder == SquadOrderType.HoldPosition
                && squadState.currentState != SquadFSMState.Retreating && holdComponent.HasValue)
            ? holdComponent.Value.holdCenter
            : heroPos;
    }

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
        int unitIndex,
        in SquadStateComponent squadState,
        SquadHoldPositionComponent? holdComponent,
        float3 heroPos,
        out int2 originalGridPos,
        out float3 gridOffset,
        out float3 worldPos,
        bool adjustForTerrain,
        quaternion formationRotation = default
        )
    {
        float2 formationCenter = CalculateFormationCenter(ref gridPositions);
        return CalculateDesiredPosition(unit, ref gridPositions, unitIndex, formationCenter,
            squadState, holdComponent, heroPos, out originalGridPos, out gridOffset,
            out worldPos, adjustForTerrain, formationRotation);
    }

    public static float3 CalculateDesiredPosition(
        Entity unit,
        ref BlobArray<int2> gridPositions,
        int unitIndex,
        float2 formationCenter,
        in SquadStateComponent squadState,
        SquadHoldPositionComponent? holdComponent,
        float3 heroPos,
        out int2 originalGridPos,
        out float3 gridOffset,
        out float3 worldPos,
        bool adjustForTerrain,
        quaternion formationRotation = default)
    {
        float3 squadCenter = GetSquadCenter(squadState, holdComponent, heroPos);
        var squadOrigin = squadCenter;
        originalGridPos = gridPositions[unitIndex];
        float2 centeredGridPos = new float2(originalGridPos) - formationCenter;
        gridOffset = FormationGridSystem.GridToRelativeWorld(centeredGridPos);
        if (math.lengthsq(formationRotation.value) > 1e-6f)
            gridOffset = math.mul(math.normalize(formationRotation), gridOffset);

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

    public static float2 CalculateFormationCenter(ref BlobArray<int2> gridPositions)
    {
        if (gridPositions.Length == 0) return float2.zero;
        int2 minGrid = gridPositions[0];
        int2 maxGrid = gridPositions[0];
        for (int i = 1; i < gridPositions.Length; i++)
        {
            minGrid = math.min(minGrid, gridPositions[i]);
            maxGrid = math.max(maxGrid, gridPositions[i]);
        }
        return (new float2(minGrid) + new float2(maxGrid)) * 0.5f;
    }

}

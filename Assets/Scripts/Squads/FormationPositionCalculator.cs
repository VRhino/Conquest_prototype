using Unity.Entities;
using Unity.Mathematics;
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

    /// <summary>
    /// World position of a slot: squad centre (hold centre or anchor) plus the slot offset rotated by the formation.
    /// </summary>
    public static float3 DesiredPosition(float3 localOffset, in SquadStateComponent squadState,
        SquadHoldPositionComponent? holdComponent, float3 anchorPosition, quaternion formationRotation,
        bool adjustForTerrain)
    {
        float3 center = GetSquadCenter(squadState, holdComponent, anchorPosition);
        float3 offset = math.lengthsq(formationRotation.value) > 1e-6f
            ? math.mul(math.normalize(formationRotation), localOffset) : localOffset;
        float3 position = center + new float3(offset.x, 0f, offset.z);
        if (adjustForTerrain && UnityEngine.Terrain.activeTerrain != null)
            position.y = calculateTerraindHeight(position);
        return position;
    }

    /// <summary>
    /// Slot component for slot <paramref name="slotIndex"/> of the current layout. False when the layout has no such slot.
    /// </summary>
    public static bool TryBuildSlot(ref FormationDataBlob formation, int templateIndex, int slotIndex, float spacing,
        uint seed, out UnitGridSlotComponent slot)
    {
        slot = default;
        if (!FormationLayout.TryGetSlot(ref formation, templateIndex, slotIndex, seed, out var data))
            return false;
        float3 offset = FormationLayout.LocalOffset(data, spacing, formation.noiseRadius, formation.noiseAngle,
            seed, slotIndex, out float heading);
        slot = new UnitGridSlotComponent
        {
            slotIndex = slotIndex,
            localPosition = data.position,
            row = data.row,
            role = data.role,
            headingLocal = heading,
            localOffset = offset
        };
        return true;
    }
}

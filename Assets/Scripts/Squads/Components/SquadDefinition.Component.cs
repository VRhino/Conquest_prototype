using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Identity and configuration fields that systems read every frame from a squad entity.
/// Written once at spawn by SquadSpawningSystem from SquadDataComponent.
/// Replaces the direct runtime reads of SquadDataComponent for: squadType, behaviorProfile,
/// formationLibrary, unitCount, GridSize, leadershipCost, detectionRange.
/// </summary>
public struct SquadDefinitionComponent : IComponentData
{
    public SquadType squadType;
    /// <summary>Infantry, cavalry or ranged (<see cref="SquadData.unitType"/>), for conditions (L1-02).</summary>
    public UnitType unitType;
    public BehaviorProfile behaviorProfile;
    public BlobAssetReference<FormationLibraryBlob> formationLibrary;
    public int unitCount;
    public int2 GridSize;
    public int leadershipCost;
    public float detectionRange;
}

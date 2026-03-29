using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Identity and configuration fields that systems read every frame from a squad entity.
/// Written once at spawn by SquadSpawningSystem from SquadDataComponent.
/// Replaces the direct runtime reads of SquadDataComponent for: squadType, behaviorProfile,
/// formationLibrary, unitCount, GridSize, unitPrefab, leadershipCost, detectionRange.
/// </summary>
public struct SquadDefinitionComponent : IComponentData
{
    public SquadType squadType;
    public BehaviorProfile behaviorProfile;
    public BlobAssetReference<FormationLibraryBlob> formationLibrary;
    public int unitCount;
    public int2 GridSize;
    public Entity unitPrefab;
    public int leadershipCost;
    public float detectionRange;
}

using Unity.Entities;

/// <summary>
/// References the formation patterns available to a squad.
/// </summary>
public struct SquadFormationDataComponent : IComponentData
{
    /// <summary>Blob asset with all available formations.</summary>
    public BlobAssetReference<FormationLibraryBlob> formationLibrary;
}

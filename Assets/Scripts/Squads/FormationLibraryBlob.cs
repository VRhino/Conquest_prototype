using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Blob asset containing all formation patterns available to a squad.
/// </summary>
public struct FormationLibraryBlob
{
    /// <summary>Array of formations contained in this library.</summary>
    public BlobArray<FormationDataBlob> formations;
}

/// <summary>
/// Blob representation of a single formation.
/// </summary>
public struct FormationDataBlob
{
    /// <summary>Formation type.</summary>
    public FormationType formationType;

    /// <summary>Offsets relative to the leader.</summary>
    public BlobArray<float3> localOffsets;
}

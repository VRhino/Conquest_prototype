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
/// All formations are now grid-based.
/// </summary>
public struct FormationDataBlob
{
    /// <summary>Formation type.</summary>
    public FormationType formationType;
    
    /// <summary>Grid positions for each unit.</summary>
    public BlobArray<int2> gridPositions;
}

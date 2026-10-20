using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Blob asset containing all formation patterns available to a squad (baked from <see cref="FormationDefinition"/>).
/// </summary>
public struct FormationLibraryBlob
{
    /// <summary>Array of formations contained in this library.</summary>
    public BlobArray<FormationDataBlob> formations;
}

/// <summary>Blob representation of a single formation.</summary>
public struct FormationDataBlob
{
    public FormationType formationType;
    public FormationLayoutKind layout;
    /// <summary>Distance between neighbouring slots in metres (Scatter: cell size). At least 0.3.</summary>
    public float spacing;
    /// <summary>Organic position noise per slot, metres (Scatter: jitter).</summary>
    public float noiseRadius;
    /// <summary>Organic facing noise per slot, radians.</summary>
    public float noiseAngle;
    /// <summary>Templates sorted by minimum soldiers alive, largest first. Empty for Scatter.</summary>
    public BlobArray<FormationTemplateBlob> templates;
    /// <summary>Posture per slot role; roles not listed use Normal.</summary>
    public BlobArray<FormationPostureBlob> postures;
}

/// <summary>Posture of one slot role.</summary>
public struct FormationPostureBlob
{
    public FormationSlotRole role;
    public UnitStance formed;
    public UnitStance moving;
}

/// <summary>Layout used while at least <see cref="minUnits"/> soldiers are alive.</summary>
public struct FormationTemplateBlob
{
    public int minUnits;
    public BlobArray<FormationSlotBlob> slots;
}

/// <summary>One slot, in spacing units relative to the anchor (+y = forward).</summary>
public struct FormationSlotBlob
{
    public float2 position;
    /// <summary>Rank from the front (0 = front row).</summary>
    public int row;
    public FormationSlotRole role;
    /// <summary>Local facing, radians clockwise from forward.</summary>
    public float heading;
}

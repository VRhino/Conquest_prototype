using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Blob structure storing progression multipliers per squad level.
/// Each array has 30 elements representing levels 1 through 30.
/// </summary>
public struct SquadProgressionCurveBlob
{
    public BlobArray<float> vida;
    public BlobArray<float> dano;
    public BlobArray<float> defensa;
    public BlobArray<float> velocidad;
}


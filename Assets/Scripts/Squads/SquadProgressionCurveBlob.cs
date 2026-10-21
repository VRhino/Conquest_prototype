using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Blob structure storing progression per squad level (Aprendizajes_Referencia.md L7-5).
/// The multiplier arrays have 30 elements representing levels 1 through 30; <see cref="xpToNextLevel"/> has the XP to
/// go from each level to the next, and <see cref="abilityUnlockLevel"/> the level each ability (in order) unlocks at.
/// </summary>
public struct SquadProgressionCurveBlob
{
    public BlobArray<float> health;
    public BlobArray<float> damage;
    public BlobArray<float> defense;
    public BlobArray<float> speed;
    public BlobArray<float> xpToNextLevel;
    public BlobArray<int> abilityUnlockLevel;
}

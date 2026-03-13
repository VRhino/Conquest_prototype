using Unity.Entities;

/// <summary>
/// Attached to a hero entity while channeling a squad swap.
/// The swap completes when <see cref="timer"/> reaches <see cref="duration"/>.
/// </summary>
public struct SquadSwapChannelingComponent : IComponentData
{
    /// <summary>Int ID of the target squad (from selectedSquads).</summary>
    public int targetSquadId;

    /// <summary>Zone where the channeling started.</summary>
    public int zoneId;

    /// <summary>Time elapsed since channeling started.</summary>
    public float timer;

    /// <summary>Total channeling duration required (1.0f).</summary>
    public float duration;
}

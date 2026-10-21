using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Seeded randomness of combat (Aprendizajes_Referencia.md L1-05): every roll comes from (simulation tick, attacker,
/// purpose), so the same hit rolls the same in a replay or on a server, whatever the order systems iterate in.
/// Replaces <c>UnityEngine.Random</c> and the per-frame seed indexed by query order.
/// </summary>
public static class CombatRandom
{
    /// <summary>What a roll is for, so two rolls of the same hit are independent.</summary>
    public const uint CriticalSalt = 0x9e3779b9u;
    public const uint SpreadSalt = 0x85ebca6bu;

    /// <summary>Simulation tick in milliseconds of elapsed time (stable under a fixed step).</summary>
    public static uint Tick(double elapsedTime) => (uint)math.max(0.0, math.round(elapsedTime * 1000.0));

    /// <summary>Generator for one roll of <paramref name="attacker"/> at <paramref name="tick"/>.</summary>
    public static Random ForHit(uint tick, Entity attacker, uint salt)
        => Random.CreateFromIndex(math.hash(new uint4(tick, (uint)attacker.Index, (uint)attacker.Version, salt)));

    /// <summary>Critical roll: true with probability <paramref name="chance"/>.</summary>
    public static bool RollCritical(uint tick, Entity attacker, float chance)
        => ForHit(tick, attacker, CriticalSalt).NextFloat() < chance;
}

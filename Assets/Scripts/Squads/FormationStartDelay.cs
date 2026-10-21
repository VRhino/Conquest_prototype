using Unity.Mathematics;

/// <summary>
/// Deterministic start delay per unit (Movimiento_Formaciones.md L-4): the front row leaves first and each
/// row behind waits <c>rowDelay</c> more, plus a small per-unit jitter from a hash of its slot. Replaces the
/// random 0.5-1.5 s delay that produced the accordion start; reproducible, so it is also network-safe.
/// </summary>
public static class FormationStartDelay
{
    /// <param name="rowRank">Slot row: 0 for the front row, 1 for the next one, and so on.</param>
    public static float Compute(int rowRank, int slotIndex, float rowDelay, float jitterDelay)
    {
        uint hash = math.hash(new int2(slotIndex, rowRank));
        float jitter01 = (hash & 0xFFFFFF) / (float)0x1000000; // [0, 1)
        return math.max(0, rowRank) * rowDelay + jitter01 * jitterDelay;
    }
}

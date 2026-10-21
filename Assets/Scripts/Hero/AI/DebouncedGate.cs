/// <summary>
/// Condition with hysteresis in time (Aprendizajes_Referencia.md L4-02): it opens only after the raw condition has
/// held for <c>openTime</c> and closes only after it has been false for <c>closeTime</c>, so a bot does not switch
/// branch on a single tick of proximity. Pattern of the reference's precondition decorator.
/// </summary>
public struct DebouncedGate
{
    public bool open;
    /// <summary>Seconds the raw condition has disagreed with <see cref="open"/>.</summary>
    public float pending;

    /// <summary>Advances the gate by <paramref name="deltaTime"/> with the raw condition of this step.</summary>
    public static DebouncedGate Step(DebouncedGate gate, bool raw, float deltaTime, float openTime, float closeTime)
    {
        if (raw == gate.open)
        {
            gate.pending = 0f;
            return gate;
        }
        gate.pending += deltaTime > 0f ? deltaTime : 0f;
        if (gate.pending >= (gate.open ? closeTime : openTime))
        {
            gate.open = raw;
            gate.pending = 0f;
        }
        return gate;
    }
}

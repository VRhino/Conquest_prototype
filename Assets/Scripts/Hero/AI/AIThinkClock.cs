using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Decision clock of the hero AI (Aprendizajes_Referencia.md L4-01, T2): each AI hero thinks every
/// <c>interval</c> seconds of simulation time, at a phase of its own (hash of its entity) so bots do not all think on the
/// same frame, independent of the frame rate. Replaces <c>Time.frameCount % 5</c>.
/// </summary>
public static class AIThinkClock
{
    /// <summary>Offset of the first think of <paramref name="entity"/> within one interval, in [0, interval).</summary>
    public static double Phase(Entity entity, float interval)
    {
        uint hash = math.hash(new int2(entity.Index, entity.Version));
        return (hash / (double)uint.MaxValue) * math.max(0f, interval) * 0.999;
    }

    /// <summary>
    /// Next think after one that was due at <paramref name="due"/> and ran at <paramref name="now"/>: keeps the phase
    /// (due + interval) unless it fell a whole interval behind, then restarts from now.
    /// </summary>
    public static double Next(double due, double now, float interval)
    {
        double next = due + math.max(1e-3f, interval);
        return next <= now ? now + math.max(1e-3f, interval) : next;
    }
}

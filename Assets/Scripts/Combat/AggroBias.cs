using Unity.Entities;

/// <summary>
/// A pull of a squad's melee targeting towards one enemy, until it expires (Aprendizajes_Referencia.md L4-05; ref.: a
/// per-squad bias added to the target score). Lives on the squad entity; written by <see cref="SkillEffectSystem"/>
/// (taunt), read by <see cref="UnitTargetingSystem"/>.
/// </summary>
public struct AggroBiasElement : IBufferElementData
{
    /// <summary>The enemy it pulls towards: a hero, a soldier or a whole squad (matches a candidate or its squad).</summary>
    public Entity toward;
    public float bias;
    /// <summary>Skill that set it, so a recast refreshes instead of stacking.</summary>
    public int sourceId;
    /// <summary>Simulation time at which it stops; 0 or less = never.</summary>
    public double expiresAt;
}

/// <summary>Pure rules of the aggro bias (L4-05).</summary>
public static class AggroBias
{
    public static bool IsExpired(in AggroBiasElement a, double now) => a.expiresAt > 0 && a.expiresAt <= now;

    /// <summary>Sum of the live biases towards <paramref name="candidate"/> or its squad <paramref name="owner"/>.</summary>
    public static float For(DynamicBuffer<AggroBiasElement> biases, Entity candidate, Entity owner, double now)
    {
        float sum = 0f;
        for (int i = 0; i < biases.Length; i++)
        {
            var a = biases[i];
            if (IsExpired(a, now) || (a.toward != candidate && a.toward != owner)) continue;
            sum += a.bias;
        }
        return sum;
    }

    /// <summary>Sets the bias of (<paramref name="toward"/>, <paramref name="sourceId"/>), replacing its own and dropping expired ones.</summary>
    public static void Apply(DynamicBuffer<AggroBiasElement> biases, Entity toward, float bias, int sourceId,
        double expiresAt, double now)
    {
        for (int i = biases.Length - 1; i >= 0; i--)
        {
            var a = biases[i];
            if ((a.toward == toward && a.sourceId == sourceId) || IsExpired(a, now)) biases.RemoveAt(i);
        }
        biases.Add(new AggroBiasElement { toward = toward, bias = bias, sourceId = sourceId, expiresAt = expiresAt });
    }
}

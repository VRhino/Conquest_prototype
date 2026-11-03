using Unity.Mathematics;

/// <summary>
/// The health floor (Aprendizajes_Referencia.md L1-07), as in the reference: <c>max(health − damage, floor · max)</c>.
/// A floor above the current health never heals: the hit just takes nothing.
/// </summary>
public static class HealthFloor
{
    public static float Apply(float currentHealth, float damage, float maxHealth, float floorRatio)
    {
        float floor = math.min(currentHealth, math.saturate(floorRatio) * maxHealth);
        return math.max(0f, math.max(currentHealth - damage, floor));
    }
}

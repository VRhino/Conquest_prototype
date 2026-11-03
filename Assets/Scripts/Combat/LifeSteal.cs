using Unity.Mathematics;

/// <summary>
/// Life steal (Aprendizajes_Referencia.md L1-13), as in the reference: one more stat of the effects. The attacker heals a
/// fraction of the health the hit really took (after shields, absorb and floor), up to its maximum.
/// </summary>
public static class LifeSteal
{
    public static float Heal(float currentHealth, float maxHealth, float healthTaken, float ratio)
    {
        if (healthTaken <= 0f || ratio <= 0f) return currentHealth;
        return math.min(maxHealth, currentHealth + healthTaken * ratio);
    }
}

using Unity.Entities;

/// <summary>
/// Timed effects expressed as modifiers on the stack (Aprendizajes_Referencia.md L5-3): an effect is an override or
/// ratio with an expiry, so every movement system sees it through the resolved speed and it ends on its own.
/// </summary>
public static class StatEffects
{
    /// <summary><see cref="StatModifier.sourceId"/> of the shield-break stun (source <see cref="ModifierSource.Effect"/>).</summary>
    public const int ShieldBreakStun = 1;

    /// <summary>
    /// Speed overridden to 0 until <paramref name="until"/> (simulation time), replacing an earlier one of the same
    /// effect. The caller marks <see cref="ModifiersDirty"/>.
    /// </summary>
    /// <summary>
    /// A timed modifier of a skill (<paramref name="effectId"/> = skill id) on one stat; recasting refreshes it instead of
    /// stacking, and the skill's modifiers on other stats are left alone.
    /// </summary>
    public static void ApplyTimed(DynamicBuffer<StatModifier> modifiers, StatId stat, ModifierOp op, float value, double until, int effectId)
    {
        for (int i = modifiers.Length - 1; i >= 0; i--)
        {
            var m = modifiers[i];
            if (m.source == ModifierSource.Effect && m.sourceId == effectId && m.stat == stat) modifiers.RemoveAt(i);
        }
        modifiers.Add(new StatModifier
        {
            stat = stat, op = op, value = value, source = ModifierSource.Effect, sourceId = effectId, expiresAt = until,
        });
    }

    public static void Immobilize(DynamicBuffer<StatModifier> modifiers, double until, int effectId)
    {
        StatMath.RemoveSource(modifiers, ModifierSource.Effect, effectId);
        modifiers.Add(new StatModifier
        {
            stat = StatId.MoveSpeed, op = ModifierOp.Override, value = 0f,
            source = ModifierSource.Effect, sourceId = effectId, expiresAt = until,
        });
    }
}

using Unity.Entities;
using Unity.Mathematics;

/// <summary>What reapplying a live effect does (L2-10).</summary>
public enum EffectReapply : byte { Refresh, Stack, Ignore }

/// <summary>
/// Timed effects expressed as modifiers on the stack (Aprendizajes_Referencia.md L5-3): an effect is an override or
/// ratio with an expiry, so every movement system sees it through the resolved speed and it ends on its own.
/// </summary>
public static class StatEffects
{
    /// <summary><see cref="StatModifier.sourceId"/> of the shield-break stun (source <see cref="ModifierSource.Effect"/>).</summary>
    public const int ShieldBreakStun = 1;
    /// <summary><see cref="StatModifier.sourceId"/> of the health floor given on respawn (L1-07, source <see cref="ModifierSource.Effect"/>).</summary>
    public const int RespawnProtection = 2;
    /// <summary><see cref="StatModifier.sourceId"/> of the stun of a resilience break (L1-16, source <see cref="ModifierSource.Effect"/>).</summary>
    public const int ResilienceBreakStun = 3;

    /// <summary>
    /// Speed overridden to 0 until <paramref name="until"/> (simulation time), replacing an earlier one of the same
    /// effect. The caller marks <see cref="ModifiersDirty"/>.
    /// </summary>
    /// <summary>
    /// A timed modifier of a skill (<paramref name="effectId"/> = skill id) on one stat; recasting refreshes it instead of
    /// stacking, and the skill's modifiers on other stats are left alone.
    /// </summary>
    public static void ApplyTimed(DynamicBuffer<StatModifier> modifiers, StatId stat, ModifierOp op, float value, double until, int effectId)
        => ApplyTimed(modifiers, stat, op, value, until, ModifierSource.Effect, effectId);

    /// <summary>Same, for another source (a perk aura refreshes its own modifier this way).</summary>
    public static void ApplyTimed(DynamicBuffer<StatModifier> modifiers, StatId stat, ModifierOp op, float value, double until,
        ModifierSource source, int sourceId)
    {
        for (int i = modifiers.Length - 1; i >= 0; i--)
        {
            var m = modifiers[i];
            if (m.source == source && m.sourceId == sourceId && m.stat == stat) modifiers.RemoveAt(i);
        }
        modifiers.Add(new StatModifier
        {
            stat = stat, op = op, value = value, source = source, sourceId = sourceId, expiresAt = until,
        });
    }

    /// <summary>
    /// A timed modifier of a skill with stacks (L2-10; ref.: max stacks and a mode on reapplying). Refresh resets the
    /// time and keeps one stack; Stack adds one up to <paramref name="maxStacks"/> and resets the time; Ignore leaves a
    /// live one as it is. With <paramref name="maxStacks"/> ≤ 1 it behaves as a refresh.
    /// </summary>
    public static void ApplyStacked(DynamicBuffer<StatModifier> modifiers, StatId stat, ModifierOp op, float value, double until,
        int effectId, int maxStacks, EffectReapply reapply, double now)
    {
        for (int i = 0; i < modifiers.Length; i++)
        {
            var m = modifiers[i];
            if (m.source != ModifierSource.Effect || m.sourceId != effectId || m.stat != stat || StatMath.IsExpired(m, now)) continue;
            if (reapply == EffectReapply.Ignore) return;
            int stacks = reapply == EffectReapply.Stack ? math.min(StatMath.Stacks(m) + 1, math.max(1, maxStacks)) : 1;
            m.stacks = (byte)math.min(stacks, byte.MaxValue);
            m.value = value;
            m.op = op;
            m.expiresAt = until;
            modifiers[i] = m;
            return;
        }
        ApplyTimed(modifiers, stat, op, value, until, effectId);
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

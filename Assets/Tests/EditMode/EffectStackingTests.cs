using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

/// <summary>Stacks of effects and effect duration stats on both sides (Aprendizajes_Referencia.md L2-10).</summary>
public class EffectStackingTests
{
    static float Resolve(DynamicBuffer<StatModifier> modifiers, double now)
    {
        using var array = modifiers.ToNativeArray(Allocator.Temp);
        return StatMath.Resolve(100f, array, StatId.MoveSpeed, now);
    }

    [Test]
    public void StackAddsUpToTheMaximumRefreshKeepsOneAndIgnoreLeavesItAlone()
    {
        using var world = new World("Stacks");
        var em = world.EntityManager;
        var e = em.CreateEntity();
        var m = em.AddBuffer<StatModifier>(e);
        for (int i = 0; i < 4; i++)
            StatEffects.ApplyStacked(m, StatId.MoveSpeed, ModifierOp.Ratio, -0.1f, 10.0 + i, 5, 3, EffectReapply.Stack, i);
        Assert.That(m.Length, Is.EqualTo(1));
        Assert.That(m[0].stacks, Is.EqualTo(3), "Capped at three.");
        Assert.That(m[0].expiresAt, Is.EqualTo(13.0), "Each reapply resets the time.");
        Assert.That(Resolve(m, 4), Is.EqualTo(70f).Within(1e-3f), "Three stacks of −10 %.");

        StatEffects.ApplyStacked(m, StatId.MoveSpeed, ModifierOp.Ratio, -0.1f, 20.0, 5, 3, EffectReapply.Ignore, 5);
        Assert.That(m[0].expiresAt, Is.EqualTo(13.0), "Ignore leaves the live one as it is.");
        StatEffects.ApplyStacked(m, StatId.MoveSpeed, ModifierOp.Ratio, -0.1f, 20.0, 5, 3, EffectReapply.Refresh, 5);
        Assert.That(m[0].stacks, Is.EqualTo(1));
        Assert.That(m[0].expiresAt, Is.EqualTo(20.0));
        Assert.That(Resolve(m, 6), Is.EqualTo(90f).Within(1e-3f));
    }

    [Test]
    public void OldModifiersWithoutStacksCountOnce()
    {
        using var world = new World("Old stacks");
        var em = world.EntityManager;
        var m = em.AddBuffer<StatModifier>(em.CreateEntity());
        m.Add(new StatModifier { stat = StatId.MoveSpeed, op = ModifierOp.Flat, value = 5f });
        Assert.That(Resolve(m, 0), Is.EqualTo(105f));
    }

    [Test]
    public void BothSidesScaleTheDuration()
    {
        var caster = new CombatStatsComponent { effectDurationOut = 1.5f };
        var target = new CombatStatsComponent { effectDurationIn = 0.5f };
        Assert.That(CombatStatsComponent.EffectDuration(4f, true, caster, true, target), Is.EqualTo(3f).Within(1e-5f));
        Assert.That(CombatStatsComponent.EffectDuration(4f, false, default, false, default), Is.EqualTo(4f));
        Assert.That(CombatStatsComponent.EffectDuration(4f, true, default, true, default), Is.EqualTo(4f),
            "Not yet resolved (0) counts as 1.");
    }
}

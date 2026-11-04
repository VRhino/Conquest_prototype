using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

/// <summary>Attack speed shortens only the phases marked for it (Aprendizajes_Referencia.md L2-12).</summary>
public class AttackSpeedTests
{
    [Test]
    public void TheBasicAttackRunsAtTheAttackSpeed()
    {
        var combat = new HeroCombatComponent { strikeWindowStart = 0.2f, strikeWindowDuration = 0.1f, attackAnimationDuration = 0.5f };
        using var blob = SkillBlobBuilder.BuildBasicAttack(combat, 0f, 0f);
        using var fired = new NativeList<SkillEvent>(8, Allocator.Temp);
        var inst = new SkillInstanceComponent();
        SkillTimeline.Start(ref blob.Value, ref inst, 0, fired);
        fired.Clear();
        Assert.That(SkillTimeline.Advance(ref blob.Value, ref inst, 0.1f, fired, 2f), Is.False);
        Assert.That(inst.phase, Is.EqualTo(1), "Twice as fast: the 0.2 s wind-up is over in 0.1 s.");
        Assert.That(fired.Length, Is.EqualTo(1));
        Assert.That(fired[0].type, Is.EqualTo(SkillEventType.HitboxOpen));
        Assert.That(SkillTimeline.Advance(ref blob.Value, ref inst, 0.149f, fired, 2f), Is.False);
        Assert.That(SkillTimeline.Advance(ref blob.Value, ref inst, 0.002f, fired, 2f), Is.True,
            "The 0.5 s swing ends at 0.25 s.");
    }

    [Test]
    public void UnmarkedPhasesKeepTheirDuration()
    {
        var ability = ScriptableObject.CreateInstance<HeroAbility>();
        ability.abilityName = "Mixed";
        ability.phases = new[]
        {
            new SkillPhase { duration = 0.4f, scalesWithAttackSpeed = true },
            new SkillPhase { duration = 0.4f, final = true },
        };
        using var blob = SkillBlobBuilder.Build(ability);
        Object.DestroyImmediate(ability);
        using var fired = new NativeList<SkillEvent>(8, Allocator.Temp);
        var inst = new SkillInstanceComponent();
        SkillTimeline.Start(ref blob.Value, ref inst, 0, fired);
        Assert.That(SkillTimeline.Advance(ref blob.Value, ref inst, 0.3f, fired, 4f), Is.False);
        Assert.That(inst.phase, Is.EqualTo(1));
        Assert.That(inst.phaseTime, Is.EqualTo(0.2f).Within(1e-5f), "0.1 s for the fast phase, the other 0.2 s at 1×.");
        Assert.That(SkillTimeline.Advance(ref blob.Value, ref inst, 0.25f, fired, 4f), Is.True);
    }

    [Test]
    public void AnUnresolvedAttackSpeedCountsAsOne()
    {
        Assert.That(CombatStatsComponent.AttackSpeedOf(false, default), Is.EqualTo(1f));
        Assert.That(CombatStatsComponent.AttackSpeedOf(true, default), Is.EqualTo(1f));
        Assert.That(CombatStatsComponent.AttackSpeedOf(true, new CombatStatsComponent { attackSpeed = 1.3f }), Is.EqualTo(1.3f));
    }
}

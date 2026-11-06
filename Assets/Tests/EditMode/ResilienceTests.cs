using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>Resilience: hits wear it down, it recovers on its own and breaking it stuns (Aprendizajes_Referencia.md L1-16).</summary>
public class ResilienceTests
{
    static readonly ResilienceTuning Tuning = ResilienceTuning.Default;

    [Test]
    public void ItRecoversAfterTheDelayUpToTheMaximum()
    {
        var r = new ResilienceComponent { max = 100f, current = 20f, lastHitTime = 0 };
        Assert.That(Resilience.Current(r, Tuning.recoveryDelay - 0.5, Tuning), Is.EqualTo(20f), "Still within the delay.");
        Assert.That(Resilience.Current(r, Tuning.recoveryDelay + 2, Tuning),
            Is.EqualTo(20f + 100f * Tuning.recoveryPerSecond * 2f).Within(1e-3f));
        Assert.That(Resilience.Current(r, 1000, Tuning), Is.EqualTo(100f), "Never above the maximum.");
    }

    [Test]
    public void CriticalFlankBackAndAmbushTakeMore()
    {
        Assert.That(Resilience.HitDamage(10f, false, HitDirection.Front, false, Tuning), Is.EqualTo(10f));
        Assert.That(Resilience.HitDamage(10f, true, HitDirection.Back, true, Tuning),
            Is.EqualTo(10f * (1f + Tuning.criticalRatio + Tuning.backRatio + Tuning.ambushRatio)).Within(1e-4f));
        Assert.That(Resilience.HitDamage(10f, false, HitDirection.Flank, false, Tuning),
            Is.EqualTo(10f * (1f + Tuning.flankRatio)).Within(1e-4f));
        Assert.That(Resilience.HitDamage(0f, true, HitDirection.Back, true, Tuning), Is.EqualTo(0f), "A weapon without it takes none.");
    }

    [Test]
    public void BreakingItStunsRefillsAndIgnoresHitsWhileStunned()
    {
        var r = new ResilienceComponent { max = 30f, current = 30f };
        Assert.That(Resilience.Hit(ref r, 20f, 1.0, Tuning), Is.False);
        Assert.That(r.current, Is.EqualTo(10f));
        Assert.That(Resilience.Hit(ref r, 20f, 1.5, Tuning), Is.True, "Emptied: it breaks.");
        Assert.That(r.current, Is.EqualTo(30f), "Refilled.");
        Assert.That(Resilience.IsStunned(r, 1.5 + Tuning.stunDuration * 0.5), Is.True);
        Assert.That(Resilience.Hit(ref r, 50f, 1.6, Tuning), Is.False, "Hits during the stun do not wear it down.");
        Assert.That(r.current, Is.EqualTo(30f));
        Assert.That(Resilience.IsStunned(r, 1.5 + Tuning.stunDuration), Is.False);

        var none = new ResilienceComponent();
        Assert.That(Resilience.Hit(ref none, 1000f, 1.0, Tuning), Is.False, "No resilience: it never breaks.");
    }

    [Test]
    public void TheStackWritesTheMaximumKeepingTheFraction()
    {
        using var world = new World("Resilience stat");
        var em = world.EntityManager;
        var e = em.CreateEntity();
        em.AddComponentData(e, new ResilienceComponent { max = 40f, current = 20f });
        StatStack.Ensure(em, e);
        StatStack.SetBase(em, e, StatId.MaxResilience, 100f);
        StatStack.MarkDirty(em, e);
        world.GetOrCreateSystemManaged<StatResolveSystem>().Update();
        var r = em.GetComponentData<ResilienceComponent>(e);
        Assert.That(r.max, Is.EqualTo(100f));
        Assert.That(r.current, Is.EqualTo(50f).Within(1e-4f), "Half full before, half full after.");
    }

    [Test]
    public void HitsToHealthWearItDownAndTheBreakImmobilises()
    {
        using var world = new World("Resilience break");
        var em = world.EntityManager;
        world.SetTime(new TimeData(10.0, 0.02f));
        var target = Soldier(em);
        var system = world.GetOrCreateSystemManaged<DamageCalculationSystem>();

        Hit(em, target, 20f, periodic: false);
        system.Update();
        Assert.That(em.GetComponentData<ResilienceComponent>(target).current, Is.EqualTo(10f).Within(1e-4f));
        Assert.That(HasStun(em, target), Is.False);

        Hit(em, target, 20f, periodic: true);
        system.Update();
        Assert.That(em.GetComponentData<ResilienceComponent>(target).current, Is.EqualTo(10f).Within(1e-4f),
            "A tick of an effect over time does not wear it down.");

        Hit(em, target, 20f, periodic: false);
        system.Update();
        var r = em.GetComponentData<ResilienceComponent>(target);
        Assert.That(r.stunnedUntil, Is.EqualTo(10.0 + Tuning.stunDuration).Within(1e-4));
        Assert.That(r.current, Is.EqualTo(30f));
        Assert.That(HasStun(em, target), Is.True, "Speed overridden to 0 until the stun ends.");
        Assert.That(em.IsComponentEnabled<ModifiersDirty>(target), Is.True);
        Assert.That(StunnedTexts(em), Is.EqualTo(1), "One \"stunned\" text, only for the break.");
    }

    static int StunnedTexts(EntityManager em)
    {
        using var query = em.CreateEntityQuery(typeof(DamageFeedbackEvent));
        using var events = query.ToComponentDataArray<DamageFeedbackEvent>(Unity.Collections.Allocator.Temp);
        int count = 0;
        foreach (var e in events)
            if (e.category == DamageCategory.Stunned) count++;
        return count;
    }

    static Entity Soldier(EntityManager em)
    {
        var e = em.CreateEntity();
        em.AddComponentData(e, LocalTransform.FromPosition(float3.zero));
        em.AddComponentData(e, new HealthComponent { maxHealth = 1000f, currentHealth = 1000f });
        em.AddComponentData(e, new ResilienceComponent { max = 30f, current = 30f });
        StatStack.Ensure(em, e);
        return e;
    }

    static void Hit(EntityManager em, Entity target, float resilienceDamage, bool periodic)
    {
        var hit = em.CreateEntity(typeof(PendingDamageEvent));
        em.SetComponentData(hit, new PendingDamageEvent
        {
            target = target, hasInlineDamageProfile = true, multiplier = 1f, destroyEntityAfterProcessing = true,
            attackerPosition = new float3(0f, 0f, 2f), periodic = periodic,
            inlineDamageProfile = new DamageProfileComponent
            {
                bluntDamage = 5f, traits = new HitTraits { resilienceDamage = resilienceDamage },
            },
        });
    }

    static bool HasStun(EntityManager em, Entity e)
    {
        var modifiers = em.GetBuffer<StatModifier>(e);
        for (int i = 0; i < modifiers.Length; i++)
            if (modifiers[i].source == ModifierSource.Effect && modifiers[i].sourceId == StatEffects.ResilienceBreakStun
                && modifiers[i].stat == StatId.MoveSpeed && modifiers[i].op == ModifierOp.Override)
                return true;
        return false;
    }
}

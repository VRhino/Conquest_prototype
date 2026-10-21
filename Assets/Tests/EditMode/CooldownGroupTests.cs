using NUnit.Framework;
using Unity.Entities;

/// <summary>Cooldowns per group with absolute end time (Aprendizajes_Referencia.md L2-5).</summary>
public class CooldownGroupTests
{
    [Test]
    public void GroupsRunOnAbsoluteTimeShareAcrossSkillsAndScaleWithACap()
    {
        using var world = new World("Cooldowns");
        var em = world.EntityManager;
        var hero = em.CreateEntity();
        var groups = em.AddBuffer<CooldownGroupElement>(hero);

        Assert.That(CooldownUtility.IsReady(groups, 3, 10.0), Is.True, "Never started: ready.");
        CooldownUtility.Start(groups, 3, 8f, 1f, 10.0);
        Assert.That(CooldownUtility.IsReady(groups, 3, 17.9), Is.False);
        Assert.That(CooldownUtility.Remaining(groups, 3, 14.0), Is.EqualTo(4f).Within(1e-5f));
        Assert.That(CooldownUtility.Fraction(groups, 3, 14.0), Is.EqualTo(0.5f).Within(1e-5f));
        Assert.That(CooldownUtility.IsReady(groups, 3, 18.0), Is.True);
        Assert.That(CooldownUtility.IsReady(groups, 4, 12.0), Is.True, "Other groups are independent.");

        CooldownUtility.Start(groups, 3, 8f, 0.75f, 20.0);
        Assert.That(groups.Length, Is.EqualTo(1), "Restarting a group reuses its entry.");
        Assert.That(CooldownUtility.Remaining(groups, 3, 20.0), Is.EqualTo(6f).Within(1e-5f));
        CooldownUtility.Start(groups, 5, 8f, 0.1f, 20.0);
        Assert.That(CooldownUtility.Remaining(groups, 5, 20.0), Is.EqualTo(8f * CooldownUtility.MinScale).Within(1e-5f), "Capped.");

        CooldownUtility.Start(groups, 0, 8f, 1f, 20.0);
        Assert.That(CooldownUtility.IsReady(groups, 0, 20.0), Is.True, "Group 0 has no cooldown.");
    }

    [Test]
    public void ResolverWritesTheCooldownScaleFromTheStack()
    {
        using var world = new World("Cooldown scale");
        var em = world.EntityManager;
        var hero = em.CreateEntity();
        em.AddComponentData(hero, new CooldownScaleComponent { value = 1f });
        StatStack.Ensure(em, hero);
        StatStack.SetBase(em, hero, StatId.CooldownScale, 1f);
        StatStack.Add(em, hero, StatId.CooldownScale, ModifierOp.Ratio, -0.3f, ModifierSource.Perk);
        StatStack.MarkDirty(em, hero);
        world.GetOrCreateSystemManaged<StatResolveSystem>().Update();
        Assert.That(em.GetComponentData<CooldownScaleComponent>(hero).value, Is.EqualTo(0.7f).Within(1e-5f));
    }
}

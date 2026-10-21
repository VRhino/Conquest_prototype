using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

/// <summary>Modifier stack (Aprendizajes_Referencia.md T1).</summary>
public class StatStackTests
{
    static StatModifier Mod(StatId stat, ModifierOp op, float value, double expiresAt = 0,
        ModifierSource source = ModifierSource.Effect, int sourceId = 0)
        => new StatModifier { stat = stat, op = op, value = value, expiresAt = expiresAt, source = source, sourceId = sourceId };

    [Test]
    public void RatiosAddUpAndMultiplyOnceThenFlatsAdd()
    {
        using var mods = new NativeArray<StatModifier>(new[]
        {
            Mod(StatId.MaxHealth, ModifierOp.Ratio, 0.2f),
            Mod(StatId.MaxHealth, ModifierOp.Ratio, 0.3f),
            Mod(StatId.MaxHealth, ModifierOp.Flat, 10f),
            Mod(StatId.MoveSpeed, ModifierOp.Ratio, 5f), // Other stat: ignored.
        }, Allocator.Temp);
        // (1 + 0.2 + 0.3) * 100 + 10, not 100 * 1.2 * 1.3 + 10.
        Assert.That(StatMath.Resolve(100f, mods, StatId.MaxHealth, 0), Is.EqualTo(160f).Within(1e-4f));
    }

    [Test]
    public void OverrideWinsAndExpiredModifiersDoNotCount()
    {
        using var mods = new NativeArray<StatModifier>(new[]
        {
            Mod(StatId.MoveSpeed, ModifierOp.Ratio, 0.5f),
            Mod(StatId.MoveSpeed, ModifierOp.Override, 0f, expiresAt: 2.0), // e.g. a stun until t = 2 s
        }, Allocator.Temp);
        Assert.That(StatMath.Resolve(4f, mods, StatId.MoveSpeed, 1.0), Is.EqualTo(0f));
        Assert.That(StatMath.Resolve(4f, mods, StatId.MoveSpeed, 2.0), Is.EqualTo(6f).Within(1e-5f));
        Assert.That(StatMath.NextExpiry(mods, 1.0), Is.EqualTo(2.0));
        Assert.That(StatMath.NextExpiry(mods, 2.5), Is.EqualTo(0));
    }

    [Test]
    public void ResolverWritesTargetsKeepsTheHealthFractionAndHonoursExpiry()
    {
        using var world = new World("Stat stack");
        var em = world.EntityManager;
        var unit = em.CreateEntity(typeof(HealthComponent), typeof(DefenseComponent), typeof(UnitStatsComponent),
            typeof(PenetrationComponent), typeof(StatModifierExpiry), typeof(ModifiersDirty));
        em.SetComponentData(unit, new HealthComponent { maxHealth = 100f, currentHealth = 50f });
        var bases = em.AddBuffer<StatBaseElement>(unit);
        for (int i = 0; i < (int)StatId.Count; i++) bases.Add(default);
        bases[(int)StatId.MaxHealth] = new StatBaseElement { value = 100f };
        bases[(int)StatId.SlashDefense] = new StatBaseElement { value = 20f };
        bases[(int)StatId.MoveSpeed] = new StatBaseElement { value = 3f };
        var mods = em.AddBuffer<StatModifier>(unit);
        mods.Add(Mod(StatId.MaxHealth, ModifierOp.Ratio, 1f, source: ModifierSource.Level));
        mods.Add(Mod(StatId.SlashDefense, ModifierOp.Flat, 5f, expiresAt: 1.0));
        var system = world.GetOrCreateSystemManaged<StatResolveSystem>();

        world.SetTime(new Unity.Core.TimeData(0.5, 0.1f));
        system.Update();
        var health = em.GetComponentData<HealthComponent>(unit);
        Assert.That(health.maxHealth, Is.EqualTo(200f));
        Assert.That(health.currentHealth, Is.EqualTo(100f), "Half health stays half when the maximum doubles.");
        Assert.That(em.GetComponentData<DefenseComponent>(unit).slashDefense, Is.EqualTo(25f));
        Assert.That(em.GetComponentData<UnitStatsComponent>(unit).speed, Is.EqualTo(3f));
        Assert.That(em.IsComponentEnabled<ModifiersDirty>(unit), Is.False);

        // Nothing changes until the defence bonus expires; then it is dropped on its own.
        world.SetTime(new Unity.Core.TimeData(1.2, 0.1f));
        system.Update();
        Assert.That(em.GetComponentData<DefenseComponent>(unit).slashDefense, Is.EqualTo(20f));
        Assert.That(em.GetBuffer<StatModifier>(unit).Length, Is.EqualTo(1));
    }

    [Test]
    public void ASourceRemovesOnlyItsOwnModifiers()
    {
        using var world = new World("Stat sources");
        var e = world.EntityManager.CreateEntity();
        var mods = world.EntityManager.AddBuffer<StatModifier>(e);
        mods.Add(Mod(StatId.MaxHealth, ModifierOp.Ratio, 0.1f, source: ModifierSource.Level));
        mods.Add(Mod(StatId.Armor, ModifierOp.Flat, 3f, source: ModifierSource.Equipment, sourceId: 7));
        mods.Add(Mod(StatId.Armor, ModifierOp.Flat, 2f, source: ModifierSource.Equipment, sourceId: 8));
        Assert.That(StatMath.RemoveSource(mods, ModifierSource.Equipment, 7), Is.EqualTo(1));
        Assert.That(StatMath.RemoveSource(mods, ModifierSource.Equipment), Is.EqualTo(1));
        Assert.That(mods.Length, Is.EqualTo(1));
        Assert.That(mods[0].source, Is.EqualTo(ModifierSource.Level));
    }
}

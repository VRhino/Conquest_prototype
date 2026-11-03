using NUnit.Framework;
using Unity.Entities;

/// <summary>Modifiers by the map's terrain, declared by the squad type (Aprendizajes_Referencia.md L1-14).</summary>
public class TerrainModifierTests
{
    static TerrainModifier Mod(BattleTerrain terrain, StatId stat, float value)
        => new TerrainModifier { terrain = terrain, stat = stat, op = ModifierOp.Ratio, value = value };

    [Test]
    public void AModifierCountsOnItsTerrainOrOnAnyIfDeclaredSo()
    {
        Assert.That(TerrainModifiers.Applies(Mod(BattleTerrain.Forest, StatId.MaxHealth, 0.1f), BattleTerrain.Forest), Is.True);
        Assert.That(TerrainModifiers.Applies(Mod(BattleTerrain.Forest, StatId.MaxHealth, 0.1f), BattleTerrain.Snow), Is.False);
        Assert.That(TerrainModifiers.Applies(Mod(BattleTerrain.Forest, StatId.MaxHealth, 0.1f), BattleTerrain.Any), Is.False,
            "A map without terrain only gets the modifiers for any terrain.");
        Assert.That(TerrainModifiers.Applies(Mod(BattleTerrain.Any, StatId.MaxHealth, 0.1f), BattleTerrain.Snow), Is.True);
        Assert.That(TerrainModifiers.OnUnit(StatId.SlashDamage) && TerrainModifiers.OnProfile(StatId.SlashDamage), Is.True);
        Assert.That(TerrainModifiers.OnUnit(StatId.PiercePenetration), Is.False, "Penetration only on the profile.");
        Assert.That(TerrainModifiers.OnProfile(StatId.MoveSpeed), Is.False);
    }

    [Test]
    public void UnitsAndTheirProfileGetTheModifiersOfTheBattleTerrain()
    {
        using var world = new World("Terrain modifiers");
        var em = world.EntityManager;
        var profile = em.CreateEntity(typeof(DamageProfileComponent));
        StatStack.Ensure(em, profile);
        StatStack.SetBase(em, profile, StatId.PierceDamage, 100f);
        var unit = em.CreateEntity(typeof(HealthComponent));
        em.SetComponentData(unit, new HealthComponent { maxHealth = 100f, currentHealth = 100f });
        StatStack.Ensure(em, unit);
        StatStack.SetBase(em, unit, StatId.MaxHealth, 100f);
        var squad = em.CreateEntity();
        em.AddComponentData(squad, new SquadDamageProfileReference { Value = profile });
        em.AddBuffer<SquadUnitElement>(squad).Add(new SquadUnitElement { Value = unit });
        var declared = em.AddBuffer<SquadTerrainModifierElement>(squad);
        declared.Add(new SquadTerrainModifierElement { value = Mod(BattleTerrain.Forest, StatId.MaxHealth, 0.2f) });
        declared.Add(new SquadTerrainModifierElement { value = Mod(BattleTerrain.Forest, StatId.PierceDamage, -0.1f) });
        declared.Add(new SquadTerrainModifierElement { value = Mod(BattleTerrain.Snow, StatId.MaxHealth, -0.5f) });

        UnitStatsUtility.ApplySquadTypeModifiers(squad, BattleTerrain.Forest, em);
        UnitStatsUtility.ApplySquadTypeModifiers(squad, BattleTerrain.Forest, em); // Again on level-up: no stacking.
        world.GetOrCreateSystemManaged<StatResolveSystem>().Update();

        Assert.That(em.GetComponentData<HealthComponent>(unit).maxHealth, Is.EqualTo(120f).Within(1e-3f));
        Assert.That(em.GetComponentData<DamageProfileComponent>(profile).piercingDamage, Is.EqualTo(90f).Within(1e-3f));
        foreach (var m in em.GetBuffer<StatModifier>(unit))
            Assert.That(m.source, Is.EqualTo(ModifierSource.Terrain));
        Assert.That(em.GetBuffer<StatModifier>(unit).Length, Is.EqualTo(2), "Health on the unit, damage shown on it too.");
    }
}

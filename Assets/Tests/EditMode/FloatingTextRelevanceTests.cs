using System.Linq;
using NUnit.Framework;
using Unity.Entities;
using UnityEditor;

/// <summary>Which floating texts a player sees in a battle of many squads (Docs/Por_Abordar/Combate.md C12).</summary>
public class FloatingTextRelevanceTests
{
    [Test]
    public void ThePlayersHitsAndTheImportantOnesShowUnderTheCap()
    {
        Assert.That(FloatingTextRelevance.Shows(true, false, 0, 30), Is.True, "Its own.");
        Assert.That(FloatingTextRelevance.Shows(false, true, 0, 30), Is.True, "Important.");
        Assert.That(FloatingTextRelevance.Shows(false, false, 0, 30), Is.False, "Someone else's plain hit.");
        Assert.That(FloatingTextRelevance.Shows(true, true, 30, 30), Is.False, "The cap is full.");
    }

    [Test]
    public void AHitIsThePlayersWhenItsHeroOrSoldiersDealOrTakeIt()
    {
        using var world = new World("Floating text relevance");
        var em = world.EntityManager;
        var hero = em.CreateEntity(typeof(IsLocalPlayer));
        var squad = em.CreateEntity();
        em.AddComponentData(squad, new SquadOwnerComponent { hero = hero });
        var soldier = em.CreateEntity();
        em.AddComponentData(soldier, new UnitOwnerComponent { squad = squad, hero = hero });
        var enemyHero = em.CreateEntity();
        var enemy = em.CreateEntity();
        em.AddComponentData(enemy, new UnitOwnerComponent { hero = enemyHero });
        var otherEnemy = em.CreateEntity();
        em.AddComponentData(otherEnemy, new UnitOwnerComponent { hero = enemyHero });

        Assert.That(Involves(em, hero, enemy), Is.True, "The hero deals it.");
        Assert.That(Involves(em, squad, enemy), Is.True, "Its squad's ability.");
        Assert.That(Involves(em, enemy, soldier), Is.True, "Its soldier takes it: where the damage comes from.");
        Assert.That(Involves(em, enemyHero, hero), Is.True, "Its hero takes it.");
        Assert.That(Involves(em, enemy, otherEnemy), Is.False, "Someone else's fight.");
        Assert.That(Involves(em, Entity.Null, otherEnemy), Is.False);
    }

    [Test]
    public void OnlyDeathShieldBreakAndStunShowForOthers()
    {
        var config = AssetDatabase.LoadAssetAtPath<FCTCategoryConfig>("Assets/ScriptableObjects/UI/Battle/FCTCategoryConfig.asset");
        Assert.That(config, Is.Not.Null);
        var shown = config.entries.Where(e => e.showForOthers).Select(e => e.category).OrderBy(c => c).ToArray();
        Assert.That(shown, Is.EqualTo(new[] { DamageCategory.Death, DamageCategory.ShieldBreak, DamageCategory.Stunned }));
    }

    static bool Involves(EntityManager em, Entity source, Entity target)
        => FloatingTextRelevance.InvolvesLocal(em, new DamageFeedbackEvent { source = source, target = target });
}

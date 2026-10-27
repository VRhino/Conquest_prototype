using NUnit.Framework;
using Unity.Entities;
using UnityEngine;

/// <summary>The skill bar shows the hero's skills, its active perks and its squad's abilities with their cooldowns (H6, H10).</summary>
public class SkillBarDataTests
{
    [Test]
    public void BarDataCarriesHeroPerkAndSquadSlotsWithTheirCooldowns()
    {
        var ability = ScriptableObject.CreateInstance<HeroAbility>();
        ability.abilityName = "Bar Strike";
        ability.category = HeroAbilityCategory.E;
        ability.cooldown = 10f;
        var perk = ScriptableObject.CreateInstance<HeroPerk>();
        perk.perkID = 77;
        perk.cooldown = 20f;
        var squadAbility = ScriptableObject.CreateInstance<AbilityData>();
        squadAbility.abilityName = "Bar Volley";
        squadAbility.cooldown = 30f;
        var strike = SkillBlobBuilder.Build(ability);
        var perkSkill = SkillBlobBuilder.BuildPerk(perk, SkillSlot.ActivePerk2);
        var volley = SkillBlobBuilder.BuildSquadAbility(squadAbility, SkillSlot.Squad1);
        try
        {
            using var world = new World("Skill bar data");
            var em = world.EntityManager;
            em.CreateEntity(typeof(MatchStateComponent));
            var hero = em.CreateEntity(typeof(IsLocalPlayer));
            var squad = em.CreateEntity(typeof(IsLocalSquadActive));
            var heroSkills = em.AddBuffer<HeroSkillElement>(hero);
            heroSkills.Add(new HeroSkillElement { slot = SkillSlot.E, skillId = strike.Value.id, skill = strike });
            heroSkills.Add(new HeroSkillElement { slot = SkillSlot.ActivePerk2, skillId = perkSkill.Value.id, skill = perkSkill });
            em.AddBuffer<HeroSkillElement>(squad).Add(new HeroSkillElement { slot = SkillSlot.Squad1, skillId = volley.Value.id, skill = volley });
            // The clock starts at 0: groups ending later are still cooling down.
            em.AddBuffer<CooldownGroupElement>(hero).Add(new CooldownGroupElement
                { groupId = strike.Value.cooldownGroup, endTime = 5, duration = 10 });
            em.AddBuffer<CooldownGroupElement>(squad).Add(new CooldownGroupElement
                { groupId = volley.Value.cooldownGroup, endTime = 15, duration = 30 });

            world.GetOrCreateSystemManaged<UIBattleDataSystem>().Update();

            using var query = em.CreateEntityQuery(typeof(UIHeroBattleDataComponent));
            var data = query.GetSingleton<UIHeroBattleDataComponent>();
            Assert.That(data.skills.Length, Is.EqualTo(UIHeroBattleDataComponent.SkillSlotCount));
            var e = data.Skill(SkillSlot.E);
            Assert.That(e.skillId, Is.EqualTo(strike.Value.id));
            Assert.That(e.cooldown, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(e.remaining, Is.EqualTo(5f).Within(1e-4f));
            var activePerk = data.Skill(SkillSlot.ActivePerk2);
            Assert.That(activePerk.skillId, Is.EqualTo(SkillBlobBuilder.PerkId(perk)));
            Assert.That(activePerk.cooldown, Is.EqualTo(0f), "Never started: ready.");
            var squadSlot = data.Skill(SkillSlot.Squad1);
            Assert.That(squadSlot.skillId, Is.EqualTo(volley.Value.id));
            Assert.That(squadSlot.cooldown, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(data.Skill(SkillSlot.Q).skillId, Is.EqualTo(0), "Empty slot.");
            Assert.That(data.Skill(SkillSlot.Squad2).skillId, Is.EqualTo(0), "Ability not unlocked.");
        }
        finally
        {
            strike.Dispose(); perkSkill.Dispose(); volley.Dispose();
            Object.DestroyImmediate(ability); Object.DestroyImmediate(perk); Object.DestroyImmediate(squadAbility);
        }
    }

    [Test]
    public void CatalogFindsIconsByTheSkillIds()
    {
        var icon = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.zero);
        var perk = ScriptableObject.CreateInstance<HeroPerk>();
        perk.perkID = 5; perk.icon = icon;
        var definition = ScriptableObject.CreateInstance<HeroClassDefinition>();
        definition.validClassPerks = new System.Collections.Generic.List<HeroPerk> { perk };
        var squadAbility = ScriptableObject.CreateInstance<AbilityData>();
        squadAbility.abilityName = "Catalog Volley"; squadAbility.icon = icon;
        var squad = ScriptableObject.CreateInstance<SquadData>();
        squad.abilitiesByLevel = new System.Collections.Generic.List<AbilityData> { squadAbility };
        try
        {
            var catalog = new SkillIconCatalog();
            catalog.AddClass(definition);
            catalog.AddSquad(squad);
            Assert.That(catalog.Find(SkillBlobBuilder.PerkId(perk)), Is.SameAs(icon));
            Assert.That(catalog.Find(SkillBlobBuilder.IdOf(squadAbility)), Is.SameAs(icon));
            Assert.That(catalog.Find(0), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(perk); Object.DestroyImmediate(definition); Object.DestroyImmediate(squadAbility);
            Object.DestroyImmediate(squad); Object.DestroyImmediate(icon);
        }
    }
}

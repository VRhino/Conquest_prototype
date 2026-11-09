#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

/// <summary>Squad abilities in the battle scene (L2-9): baked from the squad's data, unlocked by level, launched by key.</summary>
public partial class BattleSceneIntegrationTests
{
    [UnityTest]
    public IEnumerator BattleSceneSquadLaunchesItsAbilityAtItsUnlockLevel()
    {
        Assert.That(em.HasBuffer<HeroSkillElement>(squad), Is.True, "The spawned squad is a launcher.");
        Assert.That(HasSquadSkill(SkillSlot.Squad1), Is.False, "The first ability is still locked at level 1.");
        Assert.That(em.GetBuffer<AbilityByLevelElement>(squad).Length, Is.GreaterThan(0), "The squad's abilities were baked.");

        var progress = em.GetComponentData<SquadProgressComponent>(squad);
        progress.level = SquadProgressionCurves.UnlockLevel(em.GetComponentData<SquadDataComponent>(squad).curves, 0);
        em.SetComponentData(squad, progress);
        em.SetComponentData(em.CreateEntity(typeof(SquadLevelUpEvent)), new SquadLevelUpEvent { squad = squad });
        yield return Until(() => HasSquadSkill(SkillSlot.Squad1), 5f, "The first ability did not unlock at its level.");
        var skill = SquadSkill(SkillSlot.Squad1);
        Assert.That(skill.slot, Is.EqualTo(SkillSlot.Squad1));

        yield return Until(() => em.GetComponentData<MatchStateComponent>(GetMatchEntity()).currentState == MatchState.InBattle,
            60f, "The battle did not start.");
        yield return Press(Key.Digit1);
        yield return Until(() => UnitsWithModifier(skill.skillId) > 0, 5f, "Key 1 did not launch the squad's ability on its units.");
        Assert.That(CooldownUtility.IsReady(em.GetBuffer<CooldownGroupElement>(squad), skill.skill.Value.cooldownGroup,
            World.DefaultGameObjectInjectionWorld.Time.ElapsedTime), Is.False, "The squad cools down on its own.");
    }

    /// <summary>The squad's unlocked ability in a slot: abilities unlock in level order, not in slot order (the squires'
    /// Advance, slot 3, is there from level 1).</summary>
    HeroSkillElement SquadSkill(SkillSlot slot)
    {
        foreach (var skill in em.GetBuffer<HeroSkillElement>(squad))
            if (skill.slot == slot) return skill;
        return default;
    }

    bool HasSquadSkill(SkillSlot slot) => SquadSkill(slot).skill.IsCreated;

    Entity GetMatchEntity()
    {
        using var query = em.CreateEntityQuery(typeof(MatchStateComponent));
        return query.GetSingletonEntity();
    }

    int UnitsWithModifier(int skillId)
    {
        int count = 0;
        foreach (var unit in em.GetBuffer<SquadUnitElement>(squad))
        {
            if (!em.HasBuffer<StatModifier>(unit.Value)) continue;
            foreach (var m in em.GetBuffer<StatModifier>(unit.Value))
                if (m.source == ModifierSource.Effect && m.sourceId == skillId) { count++; break; }
        }
        return count;
    }
}
#endif

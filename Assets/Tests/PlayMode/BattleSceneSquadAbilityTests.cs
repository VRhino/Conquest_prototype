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
        Assert.That(em.GetBuffer<HeroSkillElement>(squad).Length, Is.EqualTo(0), "Nothing is unlocked at level 1.");
        Assert.That(em.GetBuffer<AbilityByLevelElement>(squad).Length, Is.GreaterThan(0), "The squad's abilities were baked.");

        var progress = em.GetComponentData<SquadProgressComponent>(squad);
        progress.level = SquadProgressionCurves.UnlockLevel(em.GetComponentData<SquadDataComponent>(squad).curves, 0);
        em.SetComponentData(squad, progress);
        em.SetComponentData(em.CreateEntity(typeof(SquadLevelUpEvent)), new SquadLevelUpEvent { squad = squad });
        yield return Until(() => em.GetBuffer<HeroSkillElement>(squad).Length == 1, 5f, "The first ability did not unlock at its level.");
        var skill = em.GetBuffer<HeroSkillElement>(squad)[0];
        Assert.That(skill.slot, Is.EqualTo(SkillSlot.Squad1));

        yield return Until(() => em.GetComponentData<MatchStateComponent>(GetMatchEntity()).currentState == MatchState.InBattle,
            60f, "The battle did not start.");
        yield return Press(Key.Digit1);
        yield return Until(() => UnitsWithModifier(skill.skillId) > 0, 5f, "Key 1 did not launch the squad's ability on its units.");
        Assert.That(CooldownUtility.IsReady(em.GetBuffer<CooldownGroupElement>(squad), skill.skill.Value.cooldownGroup,
            World.DefaultGameObjectInjectionWorld.Time.ElapsedTime), Is.False, "The squad cools down on its own.");
    }

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

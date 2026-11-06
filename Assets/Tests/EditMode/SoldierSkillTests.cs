using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>A squad ability run soldier by soldier: staggered, each from where it stands (Aprendizajes_Referencia.md L2-17).</summary>
public class SoldierSkillTests
{
    static readonly SoldierOrderTuning Tuning = SoldierOrderTuning.Default;

    [Test]
    public void EachSoldierStartsWithinTheStaggerWindow()
    {
        Assert.That(SoldierOrders.StartAt(10.0, 0f, Tuning), Is.EqualTo(10.0));
        Assert.That(SoldierOrders.StartAt(10.0, 1f, Tuning), Is.EqualTo(10.0 + Tuning.staggerWindow).Within(1e-6));
        Assert.That(SoldierOrders.StartAt(10.0, 0.5f, Tuning), Is.EqualTo(10.0 + Tuning.staggerWindow * 0.5).Within(1e-6));
    }

    [Test]
    public void ASoldierWaitsForItsTimeAndToBeFreeThenGivesUp()
    {
        Assert.That(SoldierOrders.Step(0.9, 1.0, 2.5, true), Is.EqualTo(SoldierOrderStep.Wait));
        Assert.That(SoldierOrders.Step(1.0, 1.0, 2.5, true), Is.EqualTo(SoldierOrderStep.Start));
        Assert.That(SoldierOrders.Step(2.0, 1.0, 2.5, false), Is.EqualTo(SoldierOrderStep.Wait), "Busy: it waits.");
        Assert.That(SoldierOrders.Step(2.5, 1.0, 2.5, false), Is.EqualTo(SoldierOrderStep.GiveUp));
    }

    [Test]
    public void EachFreeSoldierHitsTheEnemyInFrontOfItAndABusyOneSkipsTheOrder()
    {
        using var world = new World("Soldier skills");
        var em = world.EntityManager;
        em.SetComponentData(em.CreateEntity(typeof(MatchStateComponent)), new MatchStateComponent { currentState = MatchState.InBattle });

        var ability = ScriptableObject.CreateInstance<AbilityData>();
        ability.abilityName = "Test Bash";
        ability.cooldown = 30f;
        ability.perSoldier = true;
        ability.events = new[]
        {
            new SkillEvent { type = SkillEventType.AreaDamage, radius = 1.8f, angle = 90f, maxTargets = 1, value = 1f },
        };
        using var blob = SkillBlobBuilder.BuildSquadAbility(ability, SkillBlobBuilder.SquadSlot(0));
        Object.DestroyImmediate(ability);
        Assert.That(blob.Value.perSoldier, Is.True, "The mark reaches the blob.");

        var profile = em.CreateEntity();
        em.AddComponentData(profile, new DamageProfileComponent { bluntDamage = 10f });
        var squad = em.CreateEntity(typeof(TeamComponent), typeof(SkillRequestComponent), typeof(SkillInstanceComponent));
        em.SetComponentData(squad, new TeamComponent { value = Team.TeamA });
        em.AddComponentData(squad, new SquadDamageProfileReference { Value = profile });
        em.AddBuffer<HeroSkillElement>(squad).Add(new HeroSkillElement { slot = SkillBlobBuilder.SquadSlot(0), skillId = blob.Value.id, skill = blob });
        em.AddBuffer<CooldownGroupElement>(squad);
        em.AddBuffer<SquadUnitElement>(squad);

        // Three soldiers 4 m apart facing +Z, an enemy 1 m in front of each; the last one is mid-swing all along.
        var enemies = new Entity[3];
        for (int i = 0; i < 3; i++)
        {
            var soldier = Body(em, new float3(i * 4f, 0f, 0f), Team.TeamA);
            em.AddComponentData(soldier, new UnitCombatComponent { isAttacking = i == 2 });
            em.GetBuffer<SquadUnitElement>(squad).Add(new SquadUnitElement { Value = soldier });
            enemies[i] = Body(em, new float3(i * 4f, 0f, 1f), Team.TeamB);
            // Facing the soldiers, so the bash is frontal (no back bonus, L1-15).
            em.SetComponentData(enemies[i], LocalTransform.FromPositionRotation(new float3(i * 4f, 0f, 1f),
                quaternion.LookRotationSafe(new float3(0f, 0f, -1f), math.up())));
        }

        var execution = world.GetOrCreateSystemManaged<SkillExecutionSystem>();
        var effects = world.GetOrCreateSystemManaged<SkillEffectSystem>();
        var damage = world.GetOrCreateSystemManaged<DamageCalculationSystem>();
        for (int step = 0; step <= 30; step++)
        {
            double t = 1.0 + step * 0.1;
            world.SetTime(new Unity.Core.TimeData(t, 0.1f));
            bool press = step == 0;
            em.SetComponentEnabled<SkillRequestComponent>(squad, press);
            if (press) em.SetComponentData(squad, new SkillRequestComponent { slot = SkillBlobBuilder.SquadSlot(0), requestedAt = t });
            execution.Update();
            effects.Update();
            damage.Update();
        }

        Assert.That(Health(em, enemies[0]), Is.EqualTo(90f).Within(1e-3f), "Hit once by the soldier in front of it.");
        Assert.That(Health(em, enemies[1]), Is.EqualTo(90f).Within(1e-3f));
        Assert.That(Health(em, enemies[2]), Is.EqualTo(100f), "Its soldier was busy and gave up.");
        Assert.That(em.GetComponentData<SkillInstanceComponent>(squad).active, Is.False, "The squad itself runs nothing.");
        Assert.That(CooldownUtility.IsReady(em.GetBuffer<CooldownGroupElement>(squad), blob.Value.cooldownGroup, 4.0), Is.False,
            "The squad cools down.");
    }

    [Test]
    public void ASoldierFightsUntilItsStartThenHoldsItsAttacksUntilTheOrderEnds()
    {
        var order = new SoldierSkillComponent { startAt = 2.0 };
        Assert.That(SoldierOrders.HoldsAttacks(order, 1.9), Is.False, "Before its stagger start it keeps fighting.");
        Assert.That(SoldierOrders.HoldsAttacks(order, 2.0), Is.True);
        order.instance.active = true;
        Assert.That(SoldierOrders.HoldsAttacks(order, 1.0), Is.True, "Running.");
    }

    [Test]
    public void ASoldierWithADueOrderStartsNoSwing()
    {
        using var world = new World("Soldier order holds swings");
        var em = world.EntityManager;
        var target = em.CreateEntity(typeof(LocalTransform));
        em.SetComponentData(target, LocalTransform.FromPosition(new float3(0f, 0f, 1f)));
        var ordered = Swordsman(em, target);
        em.AddComponentData(ordered, new SoldierSkillComponent { startAt = 0.5 });
        var waiting = Swordsman(em, target);
        em.AddComponentData(waiting, new SoldierSkillComponent { startAt = 5.0 });
        var finished = Swordsman(em, target);
        em.AddComponentData(finished, new SoldierSkillComponent { startAt = 0.5 });
        em.SetComponentEnabled<SoldierSkillComponent>(finished, false);

        world.SetTime(new Unity.Core.TimeData(1d, 0.1f));
        world.GetOrCreateSystemManaged<UnitAttackSystem>().Update();
        Assert.That(em.GetComponentData<UnitCombatComponent>(ordered).isAttacking, Is.False, "Free for its bash.");
        Assert.That(em.GetComponentData<UnitCombatComponent>(waiting).isAttacking, Is.True, "Its start has not come.");
        Assert.That(em.GetComponentData<UnitCombatComponent>(finished).isAttacking, Is.True, "The order is over.");
    }

    [Test]
    public void AResilienceBreakCutsASoldiersRun()
    {
        using var world = new World("Soldier stunned mid-bash");
        var em = world.EntityManager;
        em.SetComponentData(em.CreateEntity(typeof(MatchStateComponent)), new MatchStateComponent { currentState = MatchState.InBattle });
        var ability = ScriptableObject.CreateInstance<AbilityData>();
        ability.abilityName = "Test Bash";
        ability.cooldown = 30f;
        ability.perSoldier = true;
        ability.events = new[] { new SkillEvent { type = SkillEventType.AreaDamage, radius = 1.8f, angle = 90f, maxTargets = 1, value = 1f } };
        using var blob = SkillBlobBuilder.BuildSquadAbility(ability, SkillBlobBuilder.SquadSlot(0));
        Object.DestroyImmediate(ability);

        var squad = em.CreateEntity(typeof(TeamComponent));
        var soldier = Body(em, float3.zero, Team.TeamA);
        var order = new SoldierSkillComponent { skill = blob, skillId = blob.Value.id, squad = squad, startAt = 0.0, giveUpAt = 10.0 };
        order.instance.active = true;
        em.AddComponentData(soldier, order);
        em.AddComponentData(soldier, new ResilienceComponent { max = 10f, current = 0f, stunnedUntil = 5.0 });

        world.SetTime(new Unity.Core.TimeData(1d, 0.1f));
        world.GetOrCreateSystemManaged<SkillExecutionSystem>().Update();
        Assert.That(em.IsComponentEnabled<SoldierSkillComponent>(soldier), Is.False, "Its run is cut.");
        Assert.That(em.GetComponentData<SoldierSkillComponent>(soldier).instance.active, Is.False);
    }

    static Entity Swordsman(EntityManager em, Entity target)
    {
        var unit = em.CreateEntity(typeof(UnitWeaponComponent), typeof(LocalTransform));
        em.SetComponentData(unit, new UnitWeaponComponent
        {
            attackRange = 2f, strikeWindowStart = 0.8f, strikeWindowDuration = 0.1f, attackAnimationDuration = 1f, attackInterval = 1f,
        });
        em.SetComponentData(unit, LocalTransform.Identity);
        em.AddComponentData(unit, new UnitCombatComponent { target = target });
        return unit;
    }

    static Entity Body(EntityManager em, float3 position, Team team)
    {
        var e = em.CreateEntity();
        em.AddComponentData(e, LocalTransform.FromPosition(position));
        em.AddComponentData(e, new TeamComponent { value = team });
        em.AddComponentData(e, new HealthComponent { maxHealth = 100f, currentHealth = 100f });
        return e;
    }

    static float Health(EntityManager em, Entity e) => em.GetComponentData<HealthComponent>(e).currentHealth;
}

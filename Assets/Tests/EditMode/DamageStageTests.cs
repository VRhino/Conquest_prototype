using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>Damage stages added for NvN: range falloff (L1-17), immunities (L1-18) and percentage relief (L1-19).</summary>
public class DamageStageTests
{
    static readonly HitTraits Bow = new HitTraits { falloffStart = 15f, falloffEnd = 40f, falloffMinFactor = 0.6f };

    [Test]
    public void ARangedHitKeepsFullDamageThenLosesItLinearlyDownToTheMinimum()
    {
        Assert.That(HitStages.Falloff(5f, Bow), Is.EqualTo(1f));
        Assert.That(HitStages.Falloff(15f, Bow), Is.EqualTo(1f));
        Assert.That(HitStages.Falloff(27.5f, Bow), Is.EqualTo(0.8f).Within(1e-5f), "Half way: half the loss.");
        Assert.That(HitStages.Falloff(40f, Bow), Is.EqualTo(0.6f).Within(1e-5f));
        Assert.That(HitStages.Falloff(80f, Bow), Is.EqualTo(0.6f).Within(1e-5f), "Never below the minimum.");
        Assert.That(HitStages.Falloff(80f, default), Is.EqualTo(1f), "A weapon without falloff keeps everything.");
    }

    [Test]
    public void ReliefTakesItsShareAfterTheFlatDefenseAndIsCapped()
    {
        var tuning = CombatTuning.Default;
        var input = new DamageInput
        {
            profile = new DamageProfileComponent { piercingDamage = 100f, slashingDamage = 100f },
            multiplier = 1f,
            defense = new DefenseComponent { pierceDefense = 20f, pierceRelief = 0.25f },
        };
        var r = DamageFormula.Compute(input, tuning);
        Assert.That(r.piercing, Is.EqualTo((100f - 20f) * 0.75f).Within(1e-3f));
        Assert.That(r.slashing, Is.EqualTo(100f), "Each type has its own relief.");

        input.defense.pierceRelief = 2f;
        Assert.That(DamageFormula.Compute(input, tuning).piercing, Is.EqualTo(80f * (1f - tuning.maxRelief)).Within(1e-3f),
            "Capped.");
        input.defense.pierceRelief = -1f;
        Assert.That(DamageFormula.Compute(input, tuning).piercing, Is.EqualTo(80f).Within(1e-3f), "Never negative.");
    }

    [Test]
    public void TheStackWritesTheRelief()
    {
        using var world = new World("Relief stat");
        var em = world.EntityManager;
        var e = em.CreateEntity(typeof(DefenseComponent));
        StatStack.Ensure(em, e);
        StatStack.Add(em, e, StatId.SlashRelief, ModifierOp.Flat, 0.3f, ModifierSource.Perk);
        world.GetOrCreateSystemManaged<StatResolveSystem>().Update();
        Assert.That(em.GetComponentData<DefenseComponent>(e).slashRelief, Is.EqualTo(0.3f).Within(1e-5f));
    }

    /// <summary>L1-19 data, approved 2026-11-07: relief by armour; heroes have none of their own.</summary>
    [TestCase("Squires/Squires", 0f, 0.1f, 0.2f)]
    [TestCase("Pikemen/Pikemen", 0f, 0.1f, 0f)]
    [TestCase("Spearmen/Spearmen", 0f, 0.1f, 0f)]
    [TestCase("Levy_Archers/Levy Archers", 0f, 0f, 0f)]
    public void EachSquadHasTheReliefOfItsArmour(string asset, float blunt, float slash, float pierce)
    {
        var data = UnityEditor.AssetDatabase.LoadAssetAtPath<SquadData>("Assets/Resources/Squads/" + asset + ".asset");
        Assert.That(data, Is.Not.Null, asset);
        Assert.That(data.bluntRelief, Is.EqualTo(blunt).Within(1e-5f));
        Assert.That(data.slashRelief, Is.EqualTo(slash).Within(1e-5f));
        Assert.That(data.pierceRelief, Is.EqualTo(pierce).Within(1e-5f));
    }

    [Test]
    public void ASoldiersReliefComesFromItsSquadAndDoesNotScaleWithLevel()
    {
        using var world = new World("Squad relief");
        var em = world.EntityManager;
        var unit = em.CreateEntity(typeof(HealthComponent), typeof(DefenseComponent));
        UnitStatsUtility.ApplyStatsToUnit(unit, new SquadDataComponent
            { baseHealth = 100, baseSpeed = 5, slashRelief = 0.1f, pierceRelief = 0.2f }, 1, 1f, 1f, 3f, 1f, em);
        world.GetOrCreateSystemManaged<StatResolveSystem>().Update();
        var defense = em.GetComponentData<DefenseComponent>(unit);
        Assert.That(defense.slashRelief, Is.EqualTo(0.1f).Within(1e-5f));
        Assert.That(defense.pierceRelief, Is.EqualTo(0.2f).Within(1e-5f), "The defense multiplier is for the flat defense only.");
        Assert.That(defense.bluntRelief, Is.Zero);
    }

    [Test]
    public void FalloffCountsOnlyForRangedHitsFromWhereTheyWereFired()
    {
        using var world = new World("Falloff");
        var em = world.EntityManager;
        var profile = new DamageProfileComponent { bluntDamage = 100f, traits = Bow };

        var far = Target(em, new float3(0f, 0f, 40f));
        Hit(em, far, profile, HitForm.Ranged);
        var melee = Target(em, new float3(0f, 0f, 40f));
        Hit(em, melee, profile, HitForm.Melee);
        world.GetOrCreateSystemManaged<DamageCalculationSystem>().Update();

        Assert.That(Health(em, far), Is.EqualTo(1000f - 60f).Within(1e-3f), "40 m: 60 %.");
        Assert.That(Health(em, melee), Is.EqualTo(1000f - 100f).Within(1e-3f), "Not ranged: no falloff.");
    }

    [Test]
    public void AHitThatSparesAClassLeavesItUntouched()
    {
        using var world = new World("Immunity");
        var em = world.EntityManager;
        var spareHeroes = new DamageProfileComponent { bluntDamage = 100f, traits = new HitTraits { spares = HitTargets.Heroes } };

        var hero = Target(em, new float3(0f, 0f, 2f));
        em.AddComponentData(hero, new HeroLifeComponent { isAlive = true });
        Hit(em, hero, spareHeroes, HitForm.Ability);
        var soldier = Target(em, new float3(0f, 0f, 2f));
        em.AddComponentData(soldier, new UnitOwnerComponent());
        Hit(em, soldier, spareHeroes, HitForm.Ability);
        world.GetOrCreateSystemManaged<DamageCalculationSystem>().Update();

        Assert.That(Health(em, hero), Is.EqualTo(1000f), "Heroes are spared.");
        Assert.That(Health(em, soldier), Is.EqualTo(900f), "Soldiers are not.");
        Assert.That(HitStages.Spares(HitTargets.Soldiers, false, true), Is.True);
        Assert.That(HitStages.Spares(HitTargets.Heroes | HitTargets.Soldiers, true, false), Is.True);
        Assert.That(HitStages.Spares(HitTargets.None, true, true), Is.False);
    }

    /// <summary>A body at <paramref name="position"/> facing the origin, so hits from there are frontal.</summary>
    static Entity Target(EntityManager em, float3 position)
    {
        var e = em.CreateEntity();
        em.AddComponentData(e, LocalTransform.FromPositionRotation(position,
            quaternion.LookRotationSafe(-math.normalizesafe(position), math.up())));
        em.AddComponentData(e, new HealthComponent { maxHealth = 1000f, currentHealth = 1000f });
        return e;
    }

    static void Hit(EntityManager em, Entity target, DamageProfileComponent profile, HitForm form)
    {
        var hit = em.CreateEntity(typeof(PendingDamageEvent));
        em.SetComponentData(hit, new PendingDamageEvent
        {
            target = target, hasInlineDamageProfile = true, inlineDamageProfile = profile, multiplier = 1f,
            attackerPosition = float3.zero, hitForm = form, destroyEntityAfterProcessing = true,
        });
    }

    static float Health(EntityManager em, Entity e) => em.GetComponentData<HealthComponent>(e).currentHealth;
}

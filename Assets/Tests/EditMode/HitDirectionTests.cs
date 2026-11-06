using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>Flank, back and ambush hits (Aprendizajes_Referencia.md L1-15).</summary>
public class HitDirectionTests
{
    static readonly HitDirectionTuning Tuning = HitDirectionTuning.Default;
    static readonly float3 Forward = new float3(0f, 0f, 1f);

    static HitDirection Classify(float x, float z) => HitDirectionRules.Classify(Forward, float3.zero, new float3(x, 0f, z), Tuning);

    [Test]
    public void TheDirectionIsTheAngleFromTheTargetsFront()
    {
        Assert.That(Classify(0f, 5f), Is.EqualTo(HitDirection.Front));
        Assert.That(Classify(3f, 5f), Is.EqualTo(HitDirection.Front), "31° off the front.");
        Assert.That(Classify(5f, 0f), Is.EqualTo(HitDirection.Flank), "90°.");
        Assert.That(Classify(-5f, -1f), Is.EqualTo(HitDirection.Flank), "101°, from either side.");
        Assert.That(Classify(1f, -5f), Is.EqualTo(HitDirection.Back), "169°.");
        Assert.That(Classify(0f, 0f), Is.EqualTo(HitDirection.Front), "No direction: frontal.");
        Assert.That(HitDirectionRules.Classify(Forward, float3.zero, new float3(0f, 3f, -5f), Tuning),
            Is.EqualTo(HitDirection.Back), "Height does not count.");
    }

    [Test]
    public void DirectionAndAmbushRatiosAddUp()
    {
        Assert.That(HitDirectionRules.DamageRatio(HitDirection.Front, false, Tuning), Is.EqualTo(0f));
        Assert.That(HitDirectionRules.DamageRatio(HitDirection.Flank, false, Tuning), Is.EqualTo(Tuning.flankDamageRatio));
        Assert.That(HitDirectionRules.DamageRatio(HitDirection.Back, true, Tuning),
            Is.EqualTo(Tuning.backDamageRatio + Tuning.ambushDamageRatio).Within(1e-6f));
        Assert.That(HitDirectionRules.DamageRatio(HitDirection.Front, true, Tuning), Is.EqualTo(Tuning.ambushDamageRatio));
    }

    [Test]
    public void ZeroTuningFallsBackToTheDefaults()
        => Assert.That(default(HitDirectionTuning).OrDefault().backAngle, Is.EqualTo(Tuning.backAngle));

    [Test]
    public void TheFormulaMultipliesByOnePlusTheDirectionRatio()
    {
        var input = new DamageInput { profile = new DamageProfileComponent { bluntDamage = 40f }, multiplier = 1f };
        float plain = DamageFormula.Compute(input, CombatTuning.Default).effective;
        input.directionRatio = 0.3f;
        var r = DamageFormula.Compute(input, CombatTuning.Default);
        Assert.That(r.directionFactor, Is.EqualTo(1.3f).Within(1e-6f));
        Assert.That(r.effective, Is.EqualTo(plain * 1.3f).Within(1e-4f));
    }

    [Test]
    public void ConditionsCanAskForTheDirectionAndTheAmbush()
    {
        var self = new HitParty { isHero = true, healthFraction = 1f };
        var other = new HitParty { isSoldier = true, healthFraction = 1f };
        var back = new HitCondition { directions = HitDirections.Back | HitDirections.Flank };
        Assert.That(HitConditions.Matches(back, self, other, new HitFacts { direction = HitDirection.Back }, false, false, 0f), Is.True);
        Assert.That(HitConditions.Matches(back, self, other, new HitFacts { direction = HitDirection.Front }, false, false, 0f), Is.False);
        var ambush = new HitCondition { ambush = PerkTristate.Must };
        Assert.That(HitConditions.Matches(ambush, self, other, new HitFacts { ambush = true }, false, false, 0f), Is.True);
        Assert.That(HitConditions.Matches(ambush, self, other, new HitFacts(), false, false, 0f), Is.False);
        Assert.That(HitConditions.Matches(default, self, other, new HitFacts { direction = HitDirection.Back }, false, false, 0f),
            Is.True, "No direction asked: any.");
    }

    [Test]
    public void TheFloatingTextShowsDeathCriticalThenAmbushBackAndFlank()
    {
        Assert.That(DamageFeedbackCategory.Of(true, true, DamageCategory.Normal, HitDirection.Back, true), Is.EqualTo(DamageCategory.Death));
        Assert.That(DamageFeedbackCategory.Of(false, true, DamageCategory.Normal, HitDirection.Back, true), Is.EqualTo(DamageCategory.Critical));
        Assert.That(DamageFeedbackCategory.Of(false, false, DamageCategory.Normal, HitDirection.Back, true), Is.EqualTo(DamageCategory.Ambush));
        Assert.That(DamageFeedbackCategory.Of(false, false, DamageCategory.Normal, HitDirection.Back, false), Is.EqualTo(DamageCategory.Back));
        Assert.That(DamageFeedbackCategory.Of(false, false, DamageCategory.Normal, HitDirection.Flank, false), Is.EqualTo(DamageCategory.Flank));
        Assert.That(DamageFeedbackCategory.Of(false, false, DamageCategory.Normal, HitDirection.Front, false), Is.EqualTo(DamageCategory.Normal));
        Assert.That(DamageFeedbackCategory.Of(false, false, DamageCategory.Ability, HitDirection.Back, true), Is.EqualTo(DamageCategory.Ability),
            "A skill's hidden text stays hidden.");
    }

    [Test]
    public void ABackHitShowsItsOwnText()
    {
        using var world = new World("Back text");
        var em = world.EntityManager;
        var (target, attacker, _) = Setup(em, new float3(0f, 0f, -2f), LineOfSight.AllClear);
        Hit(em, target, attacker, new float3(0f, 0f, -2f));
        world.GetOrCreateSystemManaged<DamageCalculationSystem>().Update();
        using var query = em.CreateEntityQuery(typeof(DamageFeedbackEvent));
        using var events = query.ToComponentDataArray<DamageFeedbackEvent>(Unity.Collections.Allocator.Temp);
        Assert.That(events.Length, Is.EqualTo(1));
        Assert.That(events[0].category, Is.EqualTo(DamageCategory.Back));
    }

    [Test]
    public void ABackHitAndAnAmbushDoMoreDamage()
    {
        using var world = new World("Hit direction");
        var em = world.EntityManager;

        // A frontal hit seen by the target's squad: plain damage.
        var (target, attacker, _) = Setup(em, new float3(0f, 0f, 2f), LineOfSight.AllClear);
        Hit(em, target, attacker, new float3(0f, 0f, 2f));
        world.GetOrCreateSystemManaged<DamageCalculationSystem>().Update();
        Assert.That(Health(em, target), Is.EqualTo(100f - 40f).Within(1e-3f));

        // From behind: +30 %.
        (target, attacker, _) = Setup(em, new float3(0f, 0f, -2f), LineOfSight.AllClear);
        Hit(em, target, attacker, new float3(0f, 0f, -2f));
        world.GetOrCreateSystemManaged<DamageCalculationSystem>().Update();
        Assert.That(Health(em, target), Is.EqualTo(100f - 40f * (1f + Tuning.backDamageRatio)).Within(1e-3f));

        // From the front, but the target's squad has the attacker's squad in range and cannot see it: ambush.
        (target, attacker, _) = Setup(em, new float3(0f, 0f, 2f), 0);
        Hit(em, target, attacker, new float3(0f, 0f, 2f));
        world.GetOrCreateSystemManaged<DamageCalculationSystem>().Update();
        Assert.That(Health(em, target), Is.EqualTo(100f - 40f * (1f + Tuning.ambushDamageRatio)).Within(1e-3f));
    }

    [Test]
    public void AnAttackerOutOfRangeIsNoAmbush()
    {
        using var world = new World("No pair, no ambush");
        var em = world.EntityManager;
        var (target, attacker, targetSquad) = Setup(em, new float3(0f, 0f, 2f), 0);
        em.GetBuffer<SquadVisibilityElement>(targetSquad).Clear();
        Hit(em, target, attacker, new float3(0f, 0f, 2f));
        world.GetOrCreateSystemManaged<DamageCalculationSystem>().Update();
        Assert.That(Health(em, target), Is.EqualTo(60f).Within(1e-3f));
    }

    /// <summary>A soldier at the origin facing +Z, and an enemy soldier; the target's squad sees the enemy squad with <paramref name="clearMask"/>.</summary>
    static (Entity target, Entity attacker, Entity targetSquad) Setup(EntityManager em, float3 attackerPosition, byte clearMask)
    {
        var targetSquad = em.CreateEntity();
        var attackerSquad = em.CreateEntity();
        var target = em.CreateEntity();
        em.AddComponentData(target, LocalTransform.FromPositionRotation(float3.zero, quaternion.LookRotationSafe(Forward, math.up())));
        em.AddComponentData(target, new HealthComponent { maxHealth = 100f, currentHealth = 100f });
        em.AddComponentData(target, new UnitOwnerComponent { squad = targetSquad });
        var attacker = em.CreateEntity();
        em.AddComponentData(attacker, LocalTransform.FromPosition(attackerPosition));
        em.AddComponentData(attacker, new UnitOwnerComponent { squad = attackerSquad });
        em.AddBuffer<SquadVisibilityElement>(targetSquad).Add(new SquadVisibilityElement { enemy = attackerSquad, clearMask = clearMask });
        return (target, attacker, targetSquad);
    }

    static void Hit(EntityManager em, Entity target, Entity attacker, float3 from)
    {
        var hit = em.CreateEntity(typeof(PendingDamageEvent));
        em.SetComponentData(hit, new PendingDamageEvent
        {
            target = target, damageSource = attacker, hasInlineDamageProfile = true,
            inlineDamageProfile = new DamageProfileComponent { bluntDamage = 40f },
            multiplier = 1f, attackerPosition = from, destroyEntityAfterProcessing = true,
        });
    }

    static float Health(EntityManager em, Entity e) => em.GetComponentData<HealthComponent>(e).currentHealth;
}

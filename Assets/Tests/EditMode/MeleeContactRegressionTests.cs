using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public class MeleeContactRegressionTests
{
    [TearDown]
    public void ClearContacts()
    {
        while (MeleeContactQueue.Pending.TryDequeue(out _)) { }
    }

    [Test]
    public void TriggerContactsAreQueuedOnlyDuringAnUnusedStrikeWindow()
    {
        using var world = new World("Melee contact admission regression");
        var em = world.EntityManager;
        var attacker = em.CreateEntity(typeof(HeroCombatComponent), typeof(WeaponHitboxActiveTag));
        var target = em.CreateEntity();
        var contact = new MeleeContact { world = world, attacker = attacker, target = target };

        em.SetComponentEnabled<WeaponHitboxActiveTag>(attacker, false);
        Assert.That(MeleeContactQueue.TryEnqueue(contact), Is.False);
        Assert.That(MeleeContactQueue.Pending.IsEmpty, Is.True);

        em.SetComponentEnabled<WeaponHitboxActiveTag>(attacker, true);
        Assert.That(MeleeContactQueue.TryEnqueue(contact), Is.True);
        Assert.That(MeleeContactQueue.Pending.Count, Is.EqualTo(1));

        var combat = em.GetComponentData<HeroCombatComponent>(attacker);
        combat.hitboxFired = true;
        em.SetComponentData(attacker, combat);
        Assert.That(MeleeContactQueue.TryEnqueue(contact), Is.False);
        Assert.That(MeleeContactQueue.Pending.Count, Is.EqualTo(1));

        em.DestroyEntity(target);
        Assert.That(MeleeContactQueue.TryEnqueue(contact), Is.False);
        Assert.That(MeleeContactQueue.Pending.Count, Is.EqualTo(1));
    }

    [Test]
    public void FirstValidTargetGetsOneDamageEventAndShieldTakesPriority()
    {
        using var world = new World("Melee contact regression");
        var em = world.EntityManager;
        var attacker = em.CreateEntity(typeof(HeroCombatComponent),
            typeof(WeaponHitboxActiveTag), typeof(DamageProfileComponent), typeof(TeamComponent));
        em.SetComponentData(attacker, new TeamComponent { value = Team.TeamA });
        em.SetComponentEnabled<WeaponHitboxActiveTag>(attacker, true);
        var friendly = em.CreateEntity(typeof(TeamComponent));
        em.SetComponentData(friendly, new TeamComponent { value = Team.TeamA });
        var enemy = em.CreateEntity(typeof(TeamComponent));
        em.SetComponentData(enemy, new TeamComponent { value = Team.TeamB });

        MeleeContactQueue.Pending.Enqueue(new MeleeContact
        { world = world, attacker = attacker, target = friendly, hitType = HitType.Body });
        MeleeContactQueue.Pending.Enqueue(new MeleeContact
        { world = world, attacker = attacker, target = enemy, hitType = HitType.Body });
        MeleeContactQueue.Pending.Enqueue(new MeleeContact
        { world = world, attacker = attacker, target = enemy, hitType = HitType.Shield });

        world.GetOrCreateSystemManaged<MeleeContactResolutionSystem>().Update();

        using var events = em.CreateEntityQuery(typeof(PendingDamageEvent)).ToEntityArray(Allocator.Temp);
        Assert.That(events.Length, Is.EqualTo(1));
        var pending = em.GetComponentData<PendingDamageEvent>(events[0]);
        Assert.That(pending.target, Is.EqualTo(enemy));
        Assert.That(pending.hitType, Is.EqualTo(HitType.Shield));
        Assert.That(pending.destroyEntityAfterProcessing, Is.True);
        Assert.That(pending.hasInlineDamageProfile, Is.True);
        Assert.That(em.GetComponentData<HeroCombatComponent>(attacker).hitboxFired, Is.True);
    }

    [Test]
    public void DestroyedAttackerCannotProduceDamageFromQueuedContact()
    {
        using var world = new World("Destroyed melee attacker regression");
        var em = world.EntityManager;
        var attacker = em.CreateEntity();
        var enemy = em.CreateEntity();
        MeleeContactQueue.Pending.Enqueue(new MeleeContact
        { world = world, attacker = attacker, target = enemy, attackerPosition = float3.zero });
        em.DestroyEntity(attacker);

        Assert.DoesNotThrow(() => world.GetOrCreateSystemManaged<MeleeContactResolutionSystem>().Update());
        Assert.That(em.CreateEntityQuery(typeof(PendingDamageEvent)).CalculateEntityCount(), Is.Zero);
    }

    [Test]
    public void DeadAttackerCannotQueueOrResolveAnAlreadyQueuedContact()
    {
        using var world = new World("Dead melee attacker regression");
        var em = world.EntityManager;
        var attacker = em.CreateEntity(typeof(HeroCombatComponent),
            typeof(HeroLifeComponent), typeof(WeaponHitboxActiveTag), typeof(DamageProfileComponent));
        var target = em.CreateEntity();
        em.SetComponentEnabled<WeaponHitboxActiveTag>(attacker, true);
        em.SetComponentData(attacker, new HeroLifeComponent { isAlive = true });
        var contact = new MeleeContact { world = world, attacker = attacker, target = target };

        Assert.That(MeleeContactQueue.TryEnqueue(contact), Is.True);
        em.SetComponentData(attacker, new HeroLifeComponent { isAlive = false });
        Assert.That(MeleeContactQueue.TryEnqueue(contact), Is.False);

        world.GetOrCreateSystemManaged<MeleeContactResolutionSystem>().Update();
        Assert.That(em.CreateEntityQuery(typeof(PendingDamageEvent)).CalculateEntityCount(), Is.Zero);
    }
}

using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public class MeleeContactRegressionTests
{
    [Test]
    public void BraceUsesCommittedFormationPolicyAndDisablesWhenFormationDoesNotSupportIt()
    {
        using var world = new World("Brace activation policy");
        var em = world.EntityManager;
        var squad = em.CreateEntity(typeof(SquadStateComponent), typeof(FormationComponent), typeof(SquadHoldPositionComponent));
        em.SetComponentData(squad, new SquadStateComponent { currentOrder = SquadOrderType.HoldPosition });
        em.SetComponentData(squad, new FormationComponent { supportsBrace = true, braceHalfAngleRadians = math.radians(20f) });
        em.SetComponentData(squad, new SquadHoldPositionComponent { holdRotation = quaternion.RotateY(math.PI / 2f) });
        var system = world.GetOrCreateSystemManaged<BraceWeaponActivationSystem>();
        system.Update();
        var brace = em.GetComponentData<SquadCombatModeComponent>(squad);
        Assert.That(brace.mode, Is.EqualTo(SquadCombatMode.Brace));
        Assert.That(SquadCombatModeComponent.InsideArc(brace.forward, new float3(1, 0, 0), brace.halfAngleRadians), Is.True);
        Assert.That(SquadCombatModeComponent.InsideArc(brace.forward, new float3(0, 0, 1), brace.halfAngleRadians), Is.False);
        em.SetComponentData(squad, new FormationComponent { supportsBrace = false });
        system.Update();
        Assert.That(em.GetComponentData<SquadCombatModeComponent>(squad).mode, Is.EqualTo(SquadCombatMode.Normal));
    }

    [Test]
    public void BraceRejectsRearAndOutOfReachTargetsAndFollowReleasesIt()
    {
        using var world = new World("Brace regression");
        var em = world.EntityManager;
        var squad = em.CreateEntity(typeof(SquadCombatModeComponent), typeof(SquadStateComponent));
        em.SetComponentData(squad, new SquadCombatModeComponent {
            mode = SquadCombatMode.Brace, forward = new float3(0, 0, 1), halfAngleRadians = math.radians(30f) });
        var unit = em.CreateEntity(typeof(UnitOwnerComponent), typeof(UnitWeaponComponent), typeof(Unity.Transforms.LocalTransform));
        em.SetComponentData(unit, new UnitOwnerComponent { squad = squad });
        em.SetComponentData(unit, new UnitWeaponComponent { attackRange = 2f });
        em.SetComponentData(unit, Unity.Transforms.LocalTransform.Identity);
        var target = em.CreateEntity(typeof(Unity.Transforms.LocalTransform));
        em.SetComponentData(target, Unity.Transforms.LocalTransform.FromPosition(new float3(0, 0, 1)));
        Assert.That(SquadCombatModeComponent.AllowsTarget(em, unit, target), Is.True);
        em.SetComponentData(target, Unity.Transforms.LocalTransform.FromPosition(new float3(1, 0, 0)));
        Assert.That(SquadCombatModeComponent.AllowsTarget(em, unit, target), Is.False);
        em.AddComponentData(unit, new UnitCombatComponent { target = target, attackCooldown = 1f });
        world.SetTime(new Unity.Core.TimeData(1d, 0.5f));
        world.GetOrCreateSystemManaged<UnitAttackSystem>().Update();
        Assert.That(em.GetComponentData<UnitCombatComponent>(unit).attackCooldown, Is.EqualTo(0.5f));
        em.SetComponentData(target, Unity.Transforms.LocalTransform.FromPosition(new float3(0, 0, -1)));
        Assert.That(SquadCombatModeComponent.AllowsTarget(em, unit, target), Is.False);
        em.SetComponentData(target, Unity.Transforms.LocalTransform.FromPosition(new float3(0, 0, 3)));
        Assert.That(SquadCombatModeComponent.AllowsTarget(em, unit, target), Is.False);
        em.SetComponentData(squad, new SquadStateComponent { currentOrder = SquadOrderType.FollowHero });
        world.GetOrCreateSystemManaged<BraceWeaponActivationSystem>().Update();
        Assert.That(em.GetComponentData<SquadCombatModeComponent>(squad).mode, Is.EqualTo(SquadCombatMode.Normal));
        Assert.That(SquadCombatModeComponent.AllowsTarget(em, unit, target), Is.True);
    }

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

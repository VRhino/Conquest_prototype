using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>The hero's mount (M19): rules, the mount system, its gate, stamina and hit routing.</summary>
public class HeroMountTests
{
    static readonly MountTuning Tuning = MountTuning.Default;

    // ---------------------------------------------------------------- rules

    [Test]
    public void TheHorseStandsOnTheHeroRightAndTheRiderGetsOffOnItsLeft()
    {
        var facing = quaternion.RotateY(math.radians(90f));      // facing +X: the hero's right is -Z
        float3 feet = new(10f, 2f, 5f);
        float3 horse = HeroMountRules.HorseFor(feet, facing);
        Assert.That(horse.z, Is.LessThan(feet.z - 0.5f), "The horse comes on the hero's right.");
        Assert.That(math.distance(HeroMountRules.GroundBeside(horse, facing), feet), Is.LessThan(1e-4f));
    }

    [Test]
    public void TheHorseTurnsAtMostItsTurnRate()
    {
        float3 forward = new(0f, 0f, 1f);
        float3 heading = HeroMountRules.Steer(forward, new float3(1f, 0f, 0f), 120f, 0.25f);
        float turned = math.degrees(math.acos(math.dot(forward, heading)));
        Assert.That(turned, Is.EqualTo(30f).Within(0.01f));
        Assert.That(heading.x, Is.GreaterThan(0f), "It turns towards the input.");
        var reached = HeroMountRules.Steer(forward, new float3(0.1f, 0f, 1f), 120f, 1f);
        Assert.That(math.dot(reached, math.normalize(new float3(0.1f, 0f, 1f))), Is.EqualTo(1f).Within(1e-4f), "A small turn is done at once.");
    }

    [Test]
    public void GaitSpeedsAndBraking()
    {
        float3 forward = new(0f, 0f, 1f);
        Assert.That(HeroMountRules.TargetSpeed(Tuning, false, true, forward, forward), Is.Zero);
        Assert.That(HeroMountRules.TargetSpeed(Tuning, true, false, forward, forward), Is.EqualTo(Tuning.trotSpeed));
        Assert.That(HeroMountRules.TargetSpeed(Tuning, true, true, forward, forward), Is.EqualTo(Tuning.gallopSpeed));
        Assert.That(HeroMountRules.TargetSpeed(Tuning, true, true, forward, -forward), Is.EqualTo(Tuning.gallopSpeed * 0.5f),
            "Turning round, it slows to half.");
        Assert.That(HeroMountRules.Approach(0f, 11f, Tuning, 0.5f), Is.EqualTo(Tuning.acceleration * 0.5f).Within(1e-5f));
        Assert.That(HeroMountRules.Approach(11f, 0f, Tuning, 0.5f), Is.EqualTo(11f - Tuning.deceleration * 0.5f).Within(1e-5f));
        Assert.That(HeroMountRules.Approach(5.9f, 6f, Tuning, 1f), Is.EqualTo(6f), "It does not overshoot.");
    }

    [Test]
    public void OnlyKnockDownsAndLaunchesThrowTheRiderOff()
    {
        Assert.That(HeroMountRules.ThrowsOff(HitReaction.Knockdown), Is.True);
        Assert.That(HeroMountRules.ThrowsOff(HitReaction.Launch), Is.True);
        Assert.That(HeroMountRules.ThrowsOff(HitReaction.Stagger), Is.False);
        Assert.That(HeroMountRules.ThrowsOff(HitReaction.GuardBreak), Is.False);
    }

    // ---------------------------------------------------------------- the mount system

    sealed class Scene : System.IDisposable
    {
        public readonly World World = new("Hero mount");
        public EntityManager Em => World.EntityManager;
        public readonly Entity Hero;
        readonly HeroMountSystem _system;
        double _time;

        public Scene()
        {
            Em.SetComponentData(Em.CreateEntity(typeof(MatchStateComponent)), new MatchStateComponent { currentState = MatchState.InBattle });
            Em.SetComponentData(Em.CreateEntity(typeof(HeroGameplayConfigComponent)),
                new HeroGameplayConfigComponent { mount = Tuning, exhaustionRecoveryThreshold = 0.2f });
            Hero = Em.CreateEntity(typeof(HeroLifeComponent), typeof(LocalTransform), typeof(HeroHealthComponent), typeof(TeamComponent),
                typeof(HeroInputComponent), typeof(ResilienceComponent));
            Em.SetComponentData(Hero, new HeroLifeComponent { isAlive = true });
            Em.SetComponentData(Hero, LocalTransform.FromPositionRotation(new float3(4f, 0f, 4f), quaternion.identity));
            Em.SetComponentData(Hero, new HeroHealthComponent { currentHealth = 500f, maxHealth = 500f });
            Em.SetComponentData(Hero, new TeamComponent { value = Team.TeamA });
            _system = World.GetOrCreateSystemManaged<HeroMountSystem>();
        }

        public HeroMountComponent Mount => Em.GetComponentData<HeroMountComponent>(Hero);

        public void Step(float dt = 0.1f, bool pressG = false)
        {
            var input = Em.GetComponentData<HeroInputComponent>(Hero);
            input.MountPressed = pressG;
            Em.SetComponentData(Hero, input);
            _time += dt;
            World.SetTime(new TimeData(_time, dt));
            _system.Update();
        }

        /// <summary>Plays the motor's part: the body jumps where the mount asks.</summary>
        public void ApplyJump()
        {
            var m = Mount;
            Em.SetComponentData(Hero, LocalTransform.FromPositionRotation(m.poseJumpPosition, m.poseJumpRotation));
        }

        public void Ride()
        {
            Step();
            Step(pressG: true);
            ApplyJump();
            for (float t = 0f; t < Tuning.mountSeconds + 0.05f; t += 0.1f) Step();
            Assert.That(Mount.state, Is.EqualTo(HeroMountState.Mounted));
        }

        public void React(HitReaction reaction)
        {
            var r = Em.GetComponentData<ResilienceComponent>(Hero);
            r.reaction = reaction;
            r.reactionRevision++;
            Em.SetComponentData(Hero, r);
        }

        public void Dispose() => World.Dispose();
    }

    [Test]
    public void GCallsTheHorseAndTheHeroGetsOnThenOff()
    {
        using var s = new Scene();
        s.Step();
        Assert.That(s.Mount.state, Is.EqualTo(HeroMountState.OnFoot), "The system gives every hero a mount state.");
        float3 feet = s.Em.GetComponentData<LocalTransform>(s.Hero).Position;

        s.Step(pressG: true);
        var m = s.Mount;
        Assert.That(m.state, Is.EqualTo(HeroMountState.Mounting));
        Assert.That(s.Em.Exists(m.horse), Is.True);
        Assert.That(s.Em.GetComponentData<HealthComponent>(m.horse).currentHealth, Is.EqualTo(Tuning.horseHealth));
        Assert.That(s.Em.GetComponentData<TeamComponent>(m.horse).value, Is.EqualTo(Team.TeamA));
        Assert.That(s.Em.GetComponentData<StaminaComponent>(m.horse).currentStamina, Is.EqualTo(Tuning.stamina));
        Assert.That(s.Em.GetComponentData<HeroHorseComponent>(m.horse).rider, Is.EqualTo(s.Hero));
        Assert.That(m.poseJumpRevision, Is.EqualTo(1));
        Assert.That(m.poseJumpPosition, Is.EqualTo(s.Em.GetComponentData<LocalTransform>(m.horse).Position),
            "The body goes to the horse's centre.");
        Assert.That(math.distance(HeroMountRules.GroundBeside(m.poseJumpPosition, m.poseJumpRotation), feet), Is.LessThan(1e-4f),
            "The clip starts where the hero stood.");

        s.ApplyJump();
        s.Step(Tuning.mountSeconds * 0.5f, pressG: true);
        Assert.That(s.Mount.state, Is.EqualTo(HeroMountState.Mounting), "G while getting on does nothing.");
        s.Step(Tuning.mountSeconds * 0.6f);
        Assert.That(s.Mount.state, Is.EqualTo(HeroMountState.Mounted));

        // The horse carries its rider.
        var moved = LocalTransform.FromPositionRotation(new float3(20f, 0f, 9f), quaternion.RotateY(1f));
        s.Em.SetComponentData(s.Hero, moved);
        s.Step();
        Assert.That(s.Em.GetComponentData<LocalTransform>(s.Mount.horse).Position, Is.EqualTo(moved.Position));

        Entity horse = s.Mount.horse;
        s.Step(pressG: true);
        Assert.That(s.Mount.state, Is.EqualTo(HeroMountState.Dismounting));
        s.Step(Tuning.dismountSeconds + 0.01f);
        m = s.Mount;
        Assert.That(m.state, Is.EqualTo(HeroMountState.OnFoot));
        Assert.That(m.lastEnd, Is.EqualTo(HeroMountEnd.Dismounted));
        Assert.That(s.Em.Exists(horse), Is.False, "The horse goes.");
        Assert.That(m.horse, Is.EqualTo(Entity.Null));
        Assert.That(m.poseJumpRevision, Is.EqualTo(2));
        Assert.That(math.distance(m.poseJumpPosition, HeroMountRules.GroundBeside(moved.Position, m.poseJumpRotation)), Is.LessThan(1e-4f),
            "The rider lands beside the horse.");
    }

    [Test]
    public void NoHorseForADeadStunnedOrAttackingHero()
    {
        using var s = new Scene();
        s.Em.AddComponentData(s.Hero, new MoveSpeedComponent { value = 0f });
        s.Step(pressG: true);
        Assert.That(s.Mount.state, Is.EqualTo(HeroMountState.OnFoot), "Stunned.");
        s.Em.SetComponentData(s.Hero, new MoveSpeedComponent { value = 5f });
        s.Em.AddComponentData(s.Hero, new HeroCombatComponent { isAttacking = true });
        s.Step(pressG: true);
        Assert.That(s.Mount.state, Is.EqualTo(HeroMountState.OnFoot), "Attacking.");
        s.Em.SetComponentData(s.Hero, new HeroCombatComponent());
        s.Em.SetComponentData(s.Hero, new HeroLifeComponent { isAlive = false });
        s.Step(pressG: true);
        Assert.That(s.Mount.state, Is.EqualTo(HeroMountState.OnFoot), "Dead.");
        s.Em.SetComponentData(s.Hero, new HeroLifeComponent { isAlive = true });
        s.Step(pressG: true);
        Assert.That(s.Mount.state, Is.EqualTo(HeroMountState.Mounting));
    }

    [Test]
    public void AHitWhileGettingOnInterruptsIt()
    {
        using var s = new Scene();
        s.Step();
        s.Step(pressG: true);
        Entity horse = s.Mount.horse;
        s.ApplyJump();
        var hp = s.Em.GetComponentData<HeroHealthComponent>(s.Hero);
        hp.currentHealth -= 20f;
        s.Em.SetComponentData(s.Hero, hp);
        s.Step();
        Assert.That(s.Mount.state, Is.EqualTo(HeroMountState.OnFoot));
        Assert.That(s.Mount.lastEnd, Is.EqualTo(HeroMountEnd.Interrupted));
        Assert.That(s.Em.Exists(horse), Is.False);
        Assert.That(s.Mount.poseJumpRevision, Is.EqualTo(2), "Back on the ground where it stood.");
    }

    [Test]
    public void AStaggerDoesNotThrowTheRiderOffButAKnockDownDoes()
    {
        using var s = new Scene();
        s.Ride();
        s.React(HitReaction.Stagger);
        s.Step();
        Assert.That(s.Mount.state, Is.EqualTo(HeroMountState.Mounted));
        s.React(HitReaction.Knockdown);
        s.Step();
        Assert.That(s.Mount.state, Is.EqualTo(HeroMountState.OnFoot));
        Assert.That(s.Mount.lastEnd, Is.EqualTo(HeroMountEnd.ThrownOff));
    }

    [Test]
    public void TheHorseDyingDropsTheHeroOnFoot()
    {
        using var s = new Scene();
        s.Ride();
        Entity horse = s.Mount.horse;
        uint jumps = s.Mount.poseJumpRevision;
        s.Em.SetComponentData(horse, new HealthComponent { maxHealth = Tuning.horseHealth, currentHealth = 0f });
        s.Em.AddComponent<IsDeadComponent>(horse);
        s.Step();
        Assert.That(s.Mount.state, Is.EqualTo(HeroMountState.OnFoot));
        Assert.That(s.Mount.lastEnd, Is.EqualTo(HeroMountEnd.HorseKilled));
        Assert.That(s.Em.Exists(horse), Is.False);
        Assert.That(s.Mount.poseJumpRevision, Is.EqualTo(jumps + 1), "It falls beside the horse.");
        Assert.That(s.Em.GetComponentData<HeroLifeComponent>(s.Hero).isAlive, Is.True, "The hero goes on.");
    }

    [Test]
    public void ADeadHeroIsOnFootWhereItFell()
    {
        using var s = new Scene();
        s.Ride();
        uint jumps = s.Mount.poseJumpRevision;
        s.Em.SetComponentData(s.Hero, new HeroLifeComponent { isAlive = false });
        s.Step();
        Assert.That(s.Mount.state, Is.EqualTo(HeroMountState.OnFoot));
        Assert.That(s.Mount.lastEnd, Is.EqualTo(HeroMountEnd.HeroDied));
        Assert.That(s.Mount.poseJumpRevision, Is.EqualTo(jumps), "The body is not moved.");
    }

    [Test]
    public void ANewHorseOnlyLongAfterTheLastOneDied()
    {
        using var s = new Scene();
        s.Ride();
        Entity horse = s.Mount.horse;
        s.Em.SetComponentData(horse, new HealthComponent { maxHealth = Tuning.horseHealth, currentHealth = 0f });
        s.Step();
        Assert.That(s.Mount.lastEnd, Is.EqualTo(HeroMountEnd.HorseKilled));
        s.Step(pressG: true);
        Assert.That(s.Mount.state, Is.EqualTo(HeroMountState.OnFoot), "No horse right after the last one died.");
        s.Step(Tuning.recallSeconds);
        s.Step(pressG: true);
        Assert.That(s.Mount.state, Is.EqualTo(HeroMountState.Mounting));
    }

    [Test]
    public void AnAIHeroRidesFarAndFightsOnFoot()
    {
        Assert.That(HeroMountRules.AIWantsToRide(false, 60f, false, float.MaxValue), Is.True, "A long way, no enemy: it rides.");
        Assert.That(HeroMountRules.AIWantsToRide(false, 20f, false, float.MaxValue), Is.False, "A short way: on foot.");
        Assert.That(HeroMountRules.AIWantsToRide(true, 20f, false, float.MaxValue), Is.True, "Riding, it keeps on until close.");
        Assert.That(HeroMountRules.AIWantsToRide(true, 8f, false, float.MaxValue), Is.False);
        Assert.That(HeroMountRules.AIWantsToRide(true, 60f, true, float.MaxValue), Is.False, "Under attack it gets off.");
        Assert.That(HeroMountRules.AIWantsToRide(false, 60f, false, 15f), Is.False, "An enemy hero near: it fights on foot.");

        using var s = new Scene();
        s.Em.AddComponentData(s.Hero, new HeroMountRequestComponent { ride = true });
        s.Step();
        Assert.That(s.Mount.state, Is.EqualTo(HeroMountState.Mounting), "Its request calls the horse.");
        s.ApplyJump();
        s.Step(Tuning.mountSeconds + 0.01f);
        s.Step();
        Assert.That(s.Mount.state, Is.EqualTo(HeroMountState.Mounted), "And it stays on while it asks to ride.");
        s.Em.SetComponentData(s.Hero, new HeroMountRequestComponent { ride = false });
        s.Step();
        Assert.That(s.Mount.state, Is.EqualTo(HeroMountState.Dismounting));
    }

    // ---------------------------------------------------------------- gate, stamina, hits

    [Test]
    public void OnHorsebackOnlyTheBasicAttack()
    {
        var riding = new SkillGateInput { inBattle = true, alive = true, hasSkill = true, cooldownReady = true, mount = SkillMountGate.Riding };
        Assert.That(SkillGate.Evaluate(riding), Is.EqualTo(SkillRejection.Mounted));
        riding.basic = true;
        Assert.That(SkillGate.Evaluate(riding), Is.EqualTo(SkillRejection.None));
        riding.mount = SkillMountGate.Transition;
        Assert.That(SkillGate.Evaluate(riding), Is.EqualTo(SkillRejection.Mounted), "Nothing while getting on or off.");
        riding.mount = SkillMountGate.OnFoot;
        riding.basic = false;
        Assert.That(SkillGate.Evaluate(riding), Is.EqualTo(SkillRejection.None));
    }

    [Test]
    public void TheGallopWearsTheHorseNotTheHero()
    {
        using var world = new World("Horse stamina");
        var em = world.EntityManager;
        em.SetComponentData(em.CreateEntity(typeof(HeroGameplayConfigComponent)),
            new HeroGameplayConfigComponent { mount = Tuning, sprintStaminaCostPerSecond = 10f, exhaustionRecoveryThreshold = 0.2f });
        var hero = em.CreateEntity(typeof(HeroStatsComponent), typeof(HeroLifeComponent), typeof(StaminaComponent), typeof(HeroMoveIntent),
            typeof(HeroMountComponent));
        em.SetComponentData(hero, new HeroStatsComponent { baseSpeed = 5f });
        em.SetComponentData(hero, new HeroLifeComponent { isAlive = true });
        em.SetComponentData(hero, new StaminaComponent { maxStamina = 100f, currentStamina = 50f, regenRate = 4f });
        em.SetComponentData(hero, new HeroMoveIntent { Direction = new float3(0f, 0f, 1f), Speed = Tuning.gallopSpeed });
        em.SetComponentData(hero, new HeroMountComponent { state = HeroMountState.Mounted });
        var horse = em.CreateEntity(typeof(HeroHorseComponent), typeof(StaminaComponent));
        em.SetComponentData(horse, new HeroHorseComponent { rider = hero });
        em.SetComponentData(horse, new StaminaComponent { maxStamina = 100f, currentStamina = 50f, regenRate = Tuning.staminaRegenPerSecond });
        var system = world.GetOrCreateSystemManaged<HeroStaminaSystem>();
        world.SetTime(new TimeData(1, 1f));
        system.Update();
        Assert.That(em.GetComponentData<StaminaComponent>(horse).currentStamina, Is.EqualTo(50f - Tuning.gallopStaminaPerSecond).Within(1e-4f));
        Assert.That(em.GetComponentData<StaminaComponent>(hero).currentStamina, Is.EqualTo(54f).Within(1e-4f),
            "Riding, the hero is not sprinting: it recovers.");
        em.SetComponentData(hero, new HeroMoveIntent { Direction = new float3(0f, 0f, 1f), Speed = Tuning.trotSpeed });
        system.Update();
        Assert.That(em.GetComponentData<StaminaComponent>(horse).currentStamina,
            Is.EqualTo(50f - Tuning.gallopStaminaPerSecond + Tuning.staminaRegenPerSecond).Within(1e-4f), "At the trot it recovers.");
    }

    [Test]
    public void BlowsBelowTheSaddleLandOnTheHorse()
    {
        using var world = new World("Mounted hits");
        var em = world.EntityManager;
        var horse = em.CreateEntity(typeof(HealthComponent));
        var hero = em.CreateEntity(typeof(LocalTransform), typeof(HeroMountComponent));
        em.SetComponentData(hero, LocalTransform.FromPosition(new float3(0f, 3f, 0f)));
        em.SetComponentData(hero, new HeroMountComponent { state = HeroMountState.Mounted, horse = horse });
        Assert.That(HeroMountRules.HitTarget(em, hero, new float3(0.4f, 3f + 1.0f, 0f), 1.45f), Is.EqualTo(horse));
        Assert.That(HeroMountRules.HitTarget(em, hero, new float3(0.4f, 3f + 2.0f, 0f), 1.45f), Is.EqualTo(hero));
        em.SetComponentData(hero, new HeroMountComponent { state = HeroMountState.OnFoot });
        Assert.That(HeroMountRules.HitTarget(em, hero, new float3(0.4f, 3f + 1.0f, 0f), 1.45f), Is.EqualTo(hero), "On foot, all on the hero.");
        var soldier = em.CreateEntity(typeof(LocalTransform));
        Assert.That(HeroMountRules.HitTarget(em, soldier, float3.zero, 1.45f), Is.EqualTo(soldier));
    }

    [Test]
    public void TheMountedHeroMovesLikeAHorse()
    {
        var camera = new GameObject("MainCamera", typeof(Camera)) { tag = "MainCamera" };
        try
        {
            using var world = new World("Mounted movement");
            var em = world.EntityManager;
            em.SetComponentData(em.CreateEntity(typeof(HeroGameplayConfigComponent)), new HeroGameplayConfigComponent { mount = Tuning });
            var horse = em.CreateEntity(typeof(StaminaComponent));
            em.SetComponentData(horse, new StaminaComponent { maxStamina = 100f, currentStamina = 100f });
            var hero = em.CreateEntity(typeof(IsLocalPlayer), typeof(HeroInputComponent), typeof(HeroStatsComponent), typeof(StaminaComponent),
                typeof(HeroLifeComponent), typeof(LocalTransform), typeof(HeroMountComponent), typeof(HeroMotorStateComponent));
            em.SetComponentData(hero, new HeroStatsComponent { baseSpeed = 5f, sprintMultiplier = 1.5f });
            em.SetComponentData(hero, new HeroLifeComponent { isAlive = true });
            em.SetComponentData(hero, LocalTransform.Identity);                                         // facing +Z
            em.SetComponentData(hero, new HeroMountComponent { state = HeroMountState.Mounted, horse = horse });
            em.SetComponentData(hero, new HeroMotorStateComponent { velocity = new float3(0f, 0f, 4f) });
            camera.transform.SetPositionAndRotation(new Vector3(0f, 5f, -10f), Quaternion.identity);       // looking along +Z
            em.SetComponentData(hero, new HeroInputComponent { MoveInput = new float2(1f, 0f), IsSprintPressed = true });   // D: to the right
            var system = world.GetOrCreateSystemManaged<HeroMovementSystem>();
            world.SetTime(new TimeData(1, 0.25f));
            system.Update();
            var intent = em.GetComponentData<HeroMoveIntent>(hero);
            Assert.That(math.degrees(math.acos(math.dot(intent.Direction, new float3(0f, 0f, 1f)))), Is.EqualTo(30f).Within(0.05f),
                "It turns at its rate, not at once.");
            Assert.That(intent.Direction.x, Is.GreaterThan(0f));
            Assert.That(intent.Speed, Is.EqualTo(4f + Tuning.acceleration * 0.25f).Within(1e-4f), "It speeds up towards the gallop.");

            em.SetComponentData(hero, new HeroMountComponent { state = HeroMountState.Mounting, horse = horse });
            system.Update();
            Assert.That(em.GetComponentData<HeroMoveIntent>(hero).Speed, Is.Zero, "Getting on, it stands still.");
        }
        finally
        {
            Object.DestroyImmediate(camera);
        }
    }
}

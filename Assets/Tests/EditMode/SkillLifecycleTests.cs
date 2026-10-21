using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>Launch gate, phases and the shared execution of hero skills (Aprendizajes_Referencia.md L2-1, L2-2).</summary>
public class SkillLifecycleTests
{
    [Test]
    public void GateChecksInTheReferenceOrder()
    {
        var ok = new SkillGateInput { inBattle = true, alive = true, hasSkill = true, cooldownReady = true, stamina = 50f, staminaCost = 10f };
        Assert.That(SkillGate.Evaluate(ok), Is.EqualTo(SkillRejection.None));
        var dead = ok; dead.alive = false; dead.cooldownReady = false;
        Assert.That(SkillGate.Evaluate(dead), Is.EqualTo(SkillRejection.Dead), "Life is checked before cooldown.");
        var stunned = ok; stunned.immobilised = true;
        Assert.That(SkillGate.Evaluate(stunned), Is.EqualTo(SkillRejection.Immobilised));
        var locked = ok; locked.busy = true; locked.currentInterruptible = false;
        Assert.That(SkillGate.Evaluate(locked), Is.EqualTo(SkillRejection.CurrentNotInterruptible));
        var free = ok; free.busy = true; free.currentInterruptible = true;
        Assert.That(SkillGate.Evaluate(free), Is.EqualTo(SkillRejection.None));
        var cooling = ok; cooling.cooldownReady = false;
        Assert.That(SkillGate.Evaluate(cooling), Is.EqualTo(SkillRejection.OnCooldown));
        var tired = ok; tired.stamina = 5f;
        Assert.That(SkillGate.Evaluate(tired), Is.EqualTo(SkillRejection.NotEnoughStamina));
    }

    [Test]
    public void TimelineFiresEachEventOnceCarriesTimeAndEndlessPhasesWait()
    {
        var combat = new HeroCombatComponent { strikeWindowStart = 0.2f, strikeWindowDuration = 0.1f, attackAnimationDuration = 0.5f };
        using var blob = SkillBlobBuilder.BuildBasicAttack(combat, 0f, 0f);
        using var fired = new NativeList<SkillEvent>(8, Allocator.Temp);
        var inst = new SkillInstanceComponent();
        SkillTimeline.Start(ref blob.Value, ref inst, 0, fired);
        Assert.That(fired.Length, Is.EqualTo(1), "Animation at time 0 of phase 0.");
        fired.Clear();
        Assert.That(SkillTimeline.Advance(ref blob.Value, ref inst, 0.25f, fired), Is.False);
        Assert.That(inst.phase, Is.EqualTo(1));
        Assert.That(inst.phaseTime, Is.EqualTo(0.05f).Within(1e-5f), "Leftover time carries into the strike.");
        Assert.That(fired.Length, Is.EqualTo(1));
        Assert.That(fired[0].type, Is.EqualTo(SkillEventType.HitboxOpen));
        fired.Clear();
        Assert.That(SkillTimeline.Advance(ref blob.Value, ref inst, 1f, fired), Is.True, "A long frame still ends it.");
        Assert.That(fired.Length, Is.EqualTo(1));
        Assert.That(fired[0].type, Is.EqualTo(SkillEventType.HitboxClose));

        var ability = ScriptableObject.CreateInstance<HeroAbility>();
        ability.abilityName = "Hold";
        ability.phases = new[] { new SkillPhase { duration = -1f, interruptible = true, charge = true }, new SkillPhase { duration = 0.2f, final = true } };
        using var hold = SkillBlobBuilder.Build(ability);
        Object.DestroyImmediate(ability);
        var waiting = new SkillInstanceComponent();
        SkillTimeline.Start(ref hold.Value, ref waiting, 0, fired);
        Assert.That(SkillTimeline.Advance(ref hold.Value, ref waiting, 100f, fired), Is.False, "An endless phase never expires.");
        Assert.That(waiting.phase, Is.EqualTo(0));
    }

    [Test]
    public void ASkillReplacesAnInterruptiblePhaseButNotTheStrikeAndChargesAtStart()
    {
        using var world = new World("Skill execution");
        var em = world.EntityManager;
        em.SetComponentData(em.CreateEntity(typeof(MatchStateComponent)), new MatchStateComponent { currentState = MatchState.InBattle });
        var combat = new HeroCombatComponent { strikeWindowStart = 0.2f, strikeWindowDuration = 0.3f, attackAnimationDuration = 1f };
        using var basic = SkillBlobBuilder.BuildBasicAttack(combat, 10f, 0.5f);
        var ability = ScriptableObject.CreateInstance<HeroAbility>();
        ability.abilityName = "Bash";
        ability.category = HeroAbilityCategory.Q;
        ability.cooldown = 8f;
        ability.staminaCost = 25f;
        using var bash = SkillBlobBuilder.Build(ability);
        Object.DestroyImmediate(ability);

        var hero = em.CreateEntity(typeof(HeroLifeComponent), typeof(HeroCombatComponent), typeof(StaminaComponent),
            typeof(HeroAnimationComponent), typeof(WeaponHitboxActiveTag), typeof(SkillInstanceComponent), typeof(SkillRequestComponent));
        em.SetComponentData(hero, new HeroLifeComponent { isAlive = true });
        em.SetComponentData(hero, combat);
        em.SetComponentData(hero, new StaminaComponent { currentStamina = 100f, maxStamina = 100f });
        em.AddBuffer<CooldownGroupElement>(hero);
        var skills = em.AddBuffer<HeroSkillElement>(hero);
        skills.Add(new HeroSkillElement { slot = SkillSlot.Basic, skillId = basic.Value.id, skill = basic });
        skills.Add(new HeroSkillElement { slot = SkillSlot.Q, skillId = bash.Value.id, skill = bash });
        var system = world.GetOrCreateSystemManaged<SkillExecutionSystem>();

        void Step(double t, SkillSlot? slot)
        {
            world.SetTime(new Unity.Core.TimeData(t, 0.1f));
            em.SetComponentEnabled<SkillRequestComponent>(hero, slot.HasValue);
            if (slot.HasValue) em.SetComponentData(hero, new SkillRequestComponent { slot = slot.Value, requestedAt = t });
            system.Update();
        }

        Step(0.0, SkillSlot.Basic);
        Assert.That(em.GetComponentData<StaminaComponent>(hero).currentStamina, Is.EqualTo(90f));
        Assert.That(em.GetComponentData<HeroCombatComponent>(hero).isAttacking, Is.True);
        Assert.That(em.GetComponentData<HeroAnimationComponent>(hero).triggerAttack, Is.True);

        Step(0.1, SkillSlot.Q); // Still in the windup (interruptible): Q replaces the basic attack.
        var inst = em.GetComponentData<SkillInstanceComponent>(hero);
        Assert.That(inst.skillIndex, Is.EqualTo(1));
        Assert.That(em.GetComponentData<StaminaComponent>(hero).currentStamina, Is.EqualTo(65f));

        Step(0.2, null);
        Step(0.3, null);
        Step(0.4, null); // Q in its active phase (not interruptible).
        Step(0.5, SkillSlot.Basic);
        Assert.That(em.GetComponentData<SkillInstanceComponent>(hero).skillIndex, Is.EqualTo(1), "The active phase protects itself.");

        for (double t = 0.6; t < 2.0; t += 0.1) Step(t, null);
        Assert.That(em.GetComponentData<SkillInstanceComponent>(hero).active, Is.False);
        Step(2.0, SkillSlot.Q);
        Assert.That(em.GetComponentData<SkillInstanceComponent>(hero).active, Is.False, "Q is cooling down.");
        Assert.That(em.GetComponentData<StaminaComponent>(hero).currentStamina, Is.EqualTo(65f));

        em.AddComponentData(hero, new MoveSpeedComponent { value = 0f });
        Step(9.0, SkillSlot.Q);
        Assert.That(em.GetComponentData<SkillInstanceComponent>(hero).active, Is.False, "Stunned: no skills.");
    }
}

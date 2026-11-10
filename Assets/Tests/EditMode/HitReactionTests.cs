using System.Linq;
using ConquestTactics.Visual;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Hit reactions (Aprendizajes_Referencia.md L1-09, Por_Abordar/Combate.md C4): the reaction a resilience break or a
/// broken shield causes, its stun and push, the bridge to the Animator and the "Hit Reaction" layer of every controller.
/// </summary>
public class HitReactionTests
{
    static readonly ResilienceTuning Tuning = ResilienceTuning.Default;
    const string ModelPath = "Assets/Synty/PolygonFantasyHeroCharacters/Models/ModularCharacters.fbx";

    // ---------------------------------------------------------------- pure rules

    [Test]
    public void ABreakCausesTheHitsReactionAndAStaggerByDefault()
    {
        Assert.That(HitReactions.OfBreak(HitReaction.None, HitTargets.None, false, true, 0f), Is.EqualTo(HitReaction.Stagger));
        Assert.That(HitReactions.OfBreak(HitReaction.Knockdown, HitTargets.None, false, true, 0f), Is.EqualTo(HitReaction.Knockdown));
        Assert.That(HitReactions.OfBreak(HitReaction.Launch, HitTargets.None, true, false, 0f), Is.EqualTo(HitReaction.Launch));
        Assert.That(HitReactions.OfBreak(HitReaction.GuardBreak, HitTargets.None, false, true, 0f), Is.EqualTo(HitReaction.Stagger),
            "A guard break only comes from a broken shield.");
    }

    [Test]
    public void AHeavyReactionStaysAStaggerOnTheClassesItIsLimitedOn()
    {
        Assert.That(HitReactions.OfBreak(HitReaction.Launch, HitTargets.Heroes, true, false, 0f), Is.EqualTo(HitReaction.Stagger));
        Assert.That(HitReactions.OfBreak(HitReaction.Launch, HitTargets.Heroes, false, true, 0f), Is.EqualTo(HitReaction.Launch));
        Assert.That(HitReactions.OfBreak(HitReaction.Knockdown, HitTargets.Soldiers, false, true, 0f), Is.EqualTo(HitReaction.Stagger));
    }

    [Test]
    public void ControlResistanceDropsWholeLevelsDownToNothing()
    {
        Assert.That(HitReactions.OfBreak(HitReaction.Launch, HitTargets.None, false, true, 1f), Is.EqualTo(HitReaction.Knockdown));
        Assert.That(HitReactions.OfBreak(HitReaction.Knockdown, HitTargets.None, false, true, 1.9f), Is.EqualTo(HitReaction.Stagger),
            "Whole levels only.");
        Assert.That(HitReactions.OfBreak(HitReaction.None, HitTargets.None, false, true, 1f), Is.EqualTo(HitReaction.None),
            "A resisted stagger is nothing.");
        Assert.That(HitReactions.OfBreak(HitReaction.Launch, HitTargets.None, false, true, 3f), Is.EqualTo(HitReaction.None));
        Assert.That(HitReactions.OfBreak(HitReaction.Launch, HitTargets.None, false, true, -2f), Is.EqualTo(HitReaction.Launch),
            "A negative resistance does not raise it.");
    }

    [Test]
    public void EachReactionStunsAsLongAsItsClipAndOnlyHeavyOnesPushFurther()
    {
        Assert.That(HitReactions.StunDuration(HitReaction.Stagger, Tuning), Is.EqualTo(Tuning.stunDuration));
        Assert.That(HitReactions.StunDuration(HitReaction.None, Tuning), Is.EqualTo(0f));
        Assert.That(Tuning.stunDuration, Is.EqualTo(HitReactionAnimatorSetup.Clip("Hit_Stagger").length).Within(0.05f));
        Assert.That(HitReactions.StunDuration(HitReaction.Knockdown, Tuning),
            Is.EqualTo(HitReactionAnimatorSetup.Clip("Hit_Knockdown").length).Within(0.05f), "Stunned until it is up again.");
        Assert.That(HitReactions.StunDuration(HitReaction.Launch, Tuning),
            Is.EqualTo(HitReactionAnimatorSetup.Clip("Hit_Launch").length).Within(0.05f));
        Assert.That(HitReactions.Push(HitReaction.Stagger, Tuning), Is.EqualTo(float2.zero), "The push of every hit.");
        Assert.That(HitReactions.Push(HitReaction.Knockdown, Tuning).x, Is.GreaterThan(0f));
        Assert.That(HitReactions.Push(HitReaction.Launch, Tuning).x, Is.GreaterThan(HitReactions.Push(HitReaction.Knockdown, Tuning).x));
    }

    [Test]
    public void AConfigBakedBeforeTheReactionsGetsTheirDefaults()
    {
        var old = Tuning;
        old.stunDuration = 1.2f;
        old.knockdownDuration = old.launchDuration = 0f;
        old.knockdownPushDistance = old.knockdownPushDuration = old.launchPushDistance = old.launchPushDuration = 0f;
        var t = old.OrDefault();
        Assert.That(t.stunDuration, Is.EqualTo(1.2f), "What was set stays.");
        Assert.That(t.knockdownDuration, Is.EqualTo(Tuning.knockdownDuration));
        Assert.That(t.launchDuration, Is.EqualTo(Tuning.launchDuration));
        Assert.That(t.knockdownPushDistance, Is.EqualTo(Tuning.knockdownPushDistance));
        Assert.That(t.launchPushDuration, Is.EqualTo(Tuning.launchPushDuration));
    }

    // ---------------------------------------------------------------- damage system

    [Test]
    public void AKnockdownBreakPublishesItStunsForItAndPushesTheSoldierFurther()
    {
        using var world = NewWorld(out var em, out var system);
        var soldier = Soldier(em);
        Hit(em, soldier, new HitTraits { resilienceDamage = 40f, reaction = HitReaction.Knockdown });
        system.Update();

        var r = em.GetComponentData<ResilienceComponent>(soldier);
        Assert.That(r.reaction, Is.EqualTo(HitReaction.Knockdown));
        Assert.That(r.reactionRevision, Is.EqualTo(1u));
        Assert.That(r.stunnedUntil, Is.EqualTo(10.0 + Tuning.knockdownDuration).Within(1e-4));
        Assert.That(HasStun(em, soldier), Is.True);
        Assert.That(em.IsComponentEnabled<UnitHitImpactComponent>(soldier), Is.True);
        var impact = em.GetComponentData<UnitHitImpactComponent>(soldier);
        Assert.That(impact.distance, Is.EqualTo(Tuning.knockdownPushDistance));
        Assert.That(impact.duration, Is.EqualTo(Tuning.knockdownPushDuration));
        Assert.That(impact.passesEnemies, Is.True, "A heavy push goes through enemy bodies (P2).");
        Assert.That(impact.direction.y, Is.LessThan(-0.99f), "Away from the attacker, who is in front (+z).");
    }

    [Test]
    public void ADefaultBreakIsAStaggerWithTheUsualPush()
    {
        using var world = NewWorld(out var em, out var system);
        var soldier = Soldier(em);
        Hit(em, soldier, new HitTraits { resilienceDamage = 40f });
        system.Update();

        var r = em.GetComponentData<ResilienceComponent>(soldier);
        Assert.That(r.reaction, Is.EqualTo(HitReaction.Stagger));
        Assert.That(r.stunnedUntil, Is.EqualTo(10.0 + Tuning.stunDuration).Within(1e-4));
        var impact = em.GetComponentData<UnitHitImpactComponent>(soldier);
        Assert.That(impact.distance, Is.EqualTo(0f), "The tuning's push.");
        Assert.That(impact.passesEnemies, Is.False);
    }

    [Test]
    public void ControlResistanceCancelsTheReactionAndTheStunButTheBarRefills()
    {
        using var world = NewWorld(out var em, out var system);
        var soldier = Soldier(em);
        var r0 = em.GetComponentData<ResilienceComponent>(soldier);
        r0.controlResistance = 1f;
        em.SetComponentData(soldier, r0);
        Hit(em, soldier, new HitTraits { resilienceDamage = 40f });
        system.Update();

        var r = em.GetComponentData<ResilienceComponent>(soldier);
        Assert.That(r.reactionRevision, Is.EqualTo(0u), "Nothing to play.");
        Assert.That(Resilience.IsStunned(r, 10.0), Is.False);
        Assert.That(HasStun(em, soldier), Is.False);
        Assert.That(r.current, Is.EqualTo(r.max), "The break refills it anyway.");
        Assert.That(Texts(em, DamageCategory.Stunned), Is.EqualTo(0));
    }

    [Test]
    public void TheStackWritesTheControlResistance()
    {
        using var world = new World("Control resistance");
        var em = world.EntityManager;
        var e = em.CreateEntity();
        em.AddComponentData(e, new ResilienceComponent { max = 10f, current = 10f });
        StatStack.Ensure(em, e);
        StatStack.SetBase(em, e, StatId.MaxResilience, 10f);
        StatStack.SetBase(em, e, StatId.ControlResistance, 2f);
        StatStack.MarkDirty(em, e);
        world.GetOrCreateSystemManaged<StatResolveSystem>().Update();
        Assert.That(em.GetComponentData<ResilienceComponent>(e).controlResistance, Is.EqualTo(2f));
    }

    [Test]
    public void ABrokenShieldBreaksTheGuardEvenWithControlResistance()
    {
        using var world = NewWorld(out var em, out var system);
        var soldier = Soldier(em);
        em.AddComponentData(soldier, new UnitShieldComponent { currentBlock = 1f, maxBlock = 50f, breakStunDuration = 2f });
        var r0 = em.GetComponentData<ResilienceComponent>(soldier);
        r0.controlResistance = 5f;
        em.SetComponentData(soldier, r0);
        Hit(em, soldier, new HitTraits { resilienceDamage = 40f }, HitType.Shield);
        system.Update();

        var r = em.GetComponentData<ResilienceComponent>(soldier);
        Assert.That(r.reaction, Is.EqualTo(HitReaction.GuardBreak));
        Assert.That(r.reactionRevision, Is.EqualTo(1u));
        Assert.That(r.current, Is.EqualTo(30f), "A blocked hit does not wear resilience down.");
    }

    // ---------------------------------------------------------------- animator

    [Test]
    public void TheBridgeFiresOncePerNewReactionButNotForALateVisualOrADeadBody()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var animator = Character(scene, HitReactionAnimatorSetup.Controllers[0]);
            int layer = animator.GetLayerIndex(HitReactionAnimatorSetup.LayerName);
            var bridge = new HitReactionAnimationBridge();
            var unit = new Entity { Index = 7, Version = 1 };

            bridge.Apply(unit, 3, HitReaction.Knockdown, false, animator);
            Step(animator, 0.3f);
            Assert.That(InRest(animator, layer), Is.True, "Bound after the reaction: it does not replay it.");

            bridge.Apply(unit, 4, HitReaction.Knockdown, false, animator);
            Step(animator, 0.3f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(layer).IsName(Full("Knockdown")), Is.True);
            bridge.Apply(unit, 4, HitReaction.Knockdown, false, animator);
            Assert.That(animator.GetInteger(AnimationHashes.HitReactionType), Is.EqualTo((int)HitReaction.Knockdown));

            Step(animator, Tuning.knockdownDuration + 0.4f);
            Assert.That(InRest(animator, layer), Is.True, "Back to rest when the clip ends.");

            bridge.Apply(unit, 5, HitReaction.Stagger, true, animator);
            Step(animator, 0.3f);
            Assert.That(InRest(animator, layer), Is.True, "A dead body does not react.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    static readonly string[] ControllerPaths = HitReactionAnimatorSetup.Controllers;

    [Test, TestCaseSource(nameof(ControllerPaths))]
    public void EveryControllerPlaysEachReactionOnTopAndLetsTheLayersBelowThroughAtRest(string controllerPath)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        Assert.That(controller.layers.Last().name, Is.EqualTo(HitReactionAnimatorSetup.LayerName), "On top of every other layer.");
        Assert.That(controller.layers.Last().avatarMask, Is.Null, "Full body.");

        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var animator = Character(scene, controllerPath);
            int layer = animator.GetLayerIndex(HitReactionAnimatorSetup.LayerName);
            Step(animator, 0.5f);
            var restHand = animator.GetBoneTransform(HumanBodyBones.RightHand).position;
            animator.SetLayerWeight(layer, 0f);
            Step(animator, 0.1f);
            Assert.That(Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.RightHand).position, restHand), Is.LessThan(0.01f),
                "At rest the layer leaves the pose to the layers below.");
            animator.SetLayerWeight(layer, 1f);

            foreach (var (reaction, clip) in HitReactionAnimatorSetup.Reactions)
            {
                animator.SetInteger(AnimationHashes.HitReactionType, (int)reaction);
                animator.SetTrigger(AnimationHashes.TriggerHitReaction);
                Step(animator, 0.3f);
                var info = animator.GetCurrentAnimatorStateInfo(layer);
                Assert.That(info.IsName(Full(reaction.ToString())), Is.True, $"{reaction} plays in the layer.");
                Step(animator, HitReactionAnimatorSetup.Clip(clip).length + 0.4f);
                Assert.That(InRest(animator, layer), Is.True, $"{reaction} goes back to rest.");
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    // ---------------------------------------------------------------- helpers

    static string Full(string state) => HitReactionAnimatorSetup.LayerName + "." + state;

    static bool InRest(Animator animator, int layer) =>
        animator.GetCurrentAnimatorStateInfo(layer).IsName(Full(HitReactionAnimatorSetup.RestState)) && !animator.IsInTransition(layer);

    static void Step(Animator animator, float seconds)
    {
        for (float t = 0f; t < seconds; t += 1f / 30) animator.Update(1f / 30);
    }

    static Animator Character(Scene scene, string controllerPath)
    {
        var go = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
        SceneManager.MoveGameObjectToScene(go, scene);
        var animator = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
        animator.avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().First();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();
        animator.SetBool(AnimationHashes.IsGrounded, true);
        animator.SetBool(AnimationHashes.IsStopped, true);
        return animator;
    }

    static World NewWorld(out EntityManager em, out DamageCalculationSystem system)
    {
        var world = new World("Hit reactions");
        em = world.EntityManager;
        world.SetTime(new TimeData(10.0, 0.02f));
        system = world.GetOrCreateSystemManaged<DamageCalculationSystem>();
        return world;
    }

    static Entity Soldier(EntityManager em)
    {
        var e = em.CreateEntity();
        em.AddComponentData(e, LocalTransform.FromPosition(float3.zero));
        em.AddComponentData(e, new HealthComponent { maxHealth = 1000f, currentHealth = 1000f });
        em.AddComponentData(e, new ResilienceComponent { max = 30f, current = 30f });
        em.AddComponentData(e, new UnitHitImpactComponent());
        em.SetComponentEnabled<UnitHitImpactComponent>(e, false);
        StatStack.Ensure(em, e);
        return e;
    }

    static void Hit(EntityManager em, Entity target, HitTraits traits, HitType hitType = default)
    {
        var hit = em.CreateEntity(typeof(PendingDamageEvent));
        em.SetComponentData(hit, new PendingDamageEvent
        {
            target = target, hasInlineDamageProfile = true, multiplier = 1f, destroyEntityAfterProcessing = true,
            attackerPosition = new float3(0f, 0f, 2f), hitType = hitType,
            inlineDamageProfile = new DamageProfileComponent { bluntDamage = 5f, traits = traits },
        });
    }

    static bool HasStun(EntityManager em, Entity e)
    {
        var modifiers = em.GetBuffer<StatModifier>(e);
        for (int i = 0; i < modifiers.Length; i++)
            if (modifiers[i].source == ModifierSource.Effect && modifiers[i].sourceId == StatEffects.ResilienceBreakStun
                && modifiers[i].stat == StatId.MoveSpeed && modifiers[i].op == ModifierOp.Override)
                return true;
        return false;
    }

    static int Texts(EntityManager em, DamageCategory category)
    {
        using var query = em.CreateEntityQuery(typeof(DamageFeedbackEvent));
        using var events = query.ToComponentDataArray<DamageFeedbackEvent>(Unity.Collections.Allocator.Temp);
        return events.Count(e => e.category == category);
    }
}

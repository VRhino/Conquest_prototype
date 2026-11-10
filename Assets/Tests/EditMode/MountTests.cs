using System.Linq;
using ConquestTactics.Visual;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Cavalry (Por_Abordar/Movimiento.md M17, Aprendizajes_Referencia.md L5-8): the rider's attack arc around the horse, the
/// horse's turn rate, its gaits on screen, the shot from horseback and the authored squads, visuals and Animators.
/// </summary>
public class MountTests
{
    // ---------------------------------------------------------------- pure rules

    [Test]
    public void TheArcIsAroundTheHorsesFront()
    {
        float arc90 = MountRules.ArcCos(90f);
        Assert.That(MountRules.InArc(new float3(0, 0, 1), float3.zero, new float3(0.5f, 0, 2), arc90), Is.True, "In front.");
        Assert.That(MountRules.InArc(new float3(0, 0, 1), float3.zero, new float3(2, 0, 2), arc90), Is.True, "On the edge (45°).");
        Assert.That(MountRules.InArc(new float3(0, 0, 1), float3.zero, new float3(2, 0, 0.5f), arc90), Is.False, "At the side.");
        Assert.That(MountRules.InArc(new float3(0, 0, 1), float3.zero, new float3(0, 0, -2), MountRules.ArcCos(300f)), Is.False,
            "Right behind is out even of a wide arc.");
        Assert.That(MountRules.ArcCos(0f), Is.EqualTo(-1f), "0 = no limit.");
        Assert.That(MountRules.InArc(new float3(0, 0, 1), float3.zero, new float3(0, 0, -2), MountRules.ArcCos(0f)), Is.True);
        Assert.That(MountRules.TurnRate(0f, 450f), Is.EqualTo(450f));
        Assert.That(MountRules.TurnRate(120f, 450f), Is.EqualTo(120f));
    }

    [Test]
    public void TheHorseWalksTrotsAndGallopsWithTheSquadsPaceWithoutSliding()
    {
        const float walk = 5.5f, run = 11f;
        Assert.That(MountGait.Blend(0f, walk, run), Is.EqualTo(0f), "Still: idle.");
        Assert.That(MountGait.Blend(MountGait.WalkFraction * walk, walk, run), Is.EqualTo(1f).Within(1e-4f), "Slow: walk.");
        Assert.That(MountGait.Blend(walk, walk, run), Is.EqualTo(2f).Within(1e-4f), "The march: trot.");
        Assert.That(MountGait.Blend(run, walk, run), Is.EqualTo(3f).Within(1e-4f), "The run: gallop.");
        Assert.That(MountGait.Blend(run * 2f, walk, run), Is.EqualTo(3f), "Never past the gallop.");
        float previous = 0f;
        for (float s = 0.1f; s < run; s += 0.1f)
        {
            float b = MountGait.Blend(s, walk, run);
            Assert.That(b, Is.GreaterThanOrEqualTo(previous - 1e-5f), "The gait rises with the speed.");
            previous = b;
            // The clips' ground speed times the playback speed is the real speed: the hooves do not slide.
            Assert.That(MountGait.ClipSpeed(b) * MountGait.Scale(s, b), Is.EqualTo(s).Within(0.01f).Or.LessThan(0.3f));
        }
        Assert.That(MountGait.ClipSpeed(2f), Is.EqualTo(MountGait.TrotClipSpeed));
        Assert.That(MountGait.ClipSpeed(3f), Is.EqualTo(MountGait.GallopClipSpeed));
    }

    // ---------------------------------------------------------------- systems

    [Test]
    public void AMountedSoldierOnlyStrikesInsideItsArc()
    {
        using var world = new World("Mounted melee");
        var em = world.EntityManager;
        var front = Target(em, new float3(0f, 0f, 2f));
        var side = Target(em, new float3(2.5f, 0f, 0f));
        var lancer = Melee(em, front, mounted: true);
        var lancerAtSide = Melee(em, side, mounted: true);
        var onFoot = Melee(em, side, mounted: false);
        world.GetOrCreateSystemManaged<UnitAttackSystem>().Update();
        Assert.That(em.GetComponentData<UnitCombatComponent>(lancer).isAttacking, Is.True, "Target in front: it strikes.");
        Assert.That(em.GetComponentData<UnitCombatComponent>(lancerAtSide).isAttacking, Is.False, "Target out of the arc: no strike.");
        Assert.That(em.GetComponentData<UnitCombatComponent>(onFoot).isAttacking, Is.True, "On foot there is no arc.");
    }

    [Test]
    public void AHorseArcherShootsFromHorsebackAndOnlyInsideItsArc()
    {
        using var world = new World("Mounted shot");
        var em = world.EntityManager;
        var ahead = Target(em, new float3(0f, 0f, 10f));
        var behind = Target(em, new float3(0f, 0f, -10f));
        var archer = Archer(em, ahead);
        var turned = Archer(em, behind);
        world.GetOrCreateSystemManaged<RangedAttackSystem>().Update();
        world.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().Update();
        using var query = em.CreateEntityQuery(typeof(ProjectileSpawnRequest));
        var requests = query.ToComponentDataArray<ProjectileSpawnRequest>(Unity.Collections.Allocator.Temp);
        Assert.That(requests.Length, Is.EqualTo(1), "Only the archer facing its target shoots.");
        Assert.That(requests[0].shooter, Is.EqualTo(archer));
        Assert.That(requests[0].spawnPosition.y, Is.EqualTo(2.3f).Within(1e-4f), "The arrow leaves from the rider's hands.");
        Assert.That(em.GetComponentData<RangedAttackStateComponent>(turned).currentAmmo, Is.EqualTo(3));
        requests.Dispose();
    }

    // ---------------------------------------------------------------- authored assets

    static readonly string[] Kinds = CavalrySetup.Kinds.Select(k => k.key).ToArray();

    [Test, TestCaseSource(nameof(Kinds))]
    public void TheCavalrySquadIsRegisteredMountedAndSpacedForItsBody(string key)
    {
        var kind = CavalrySetup.Kinds.Single(k => k.key == key);
        var data = Resources.Load<SquadDatabase>("Squads/SquadDatabase").allSquads.Single(d => d && d.id == kind.id);
        Assert.That(data.unitType, Is.EqualTo(UnitType.Cavalry));
        Assert.That(data.type, Is.EqualTo(kind.type));
        Assert.That(data.bodyRadius, Is.GreaterThan(0.5f), "A horse is bigger than a soldier on foot.");
        Assert.That(data.mountAttackArc, Is.InRange(1f, 359f));
        Assert.That(data.mountTurnRate, Is.GreaterThan(0f));
        Assert.That(kind.ranged ? data.rangedData != null && data.rangedData.launchHeight > 2f : data.meleeData != null, Is.True);
        foreach (var formation in data.formations)
        {
            Assert.That(formation.supportsBrace, Is.False, $"{formation.name}: cavalry does not brace.");
            Assert.That(formation.postures, Is.Empty, $"{formation.name}: no infantry postures.");
            Assert.That(formation.EffectiveSpacing, Is.GreaterThanOrEqualTo(2f * data.bodyRadius), $"{formation.name}: horses do not overlap.");
        }
        var config = AssetDatabase.LoadAssetAtPath<VisualPrefabConfiguration>("Assets/ScriptableObjects/VisualPrefabConfig.asset");
        var entry = config.UnitPrefabs.Single(e => e.squadType == kind.type);
        Assert.That(entry.unitId, Is.EqualTo(kind.VisualId));
        Assert.That(entry.visualPrefab, Is.EqualTo(AssetDatabase.LoadAssetAtPath<GameObject>(kind.Prefab)));
    }

    [Test, TestCaseSource(nameof(Kinds))]
    public void TheVisualIsAHorseWithItsRiderOnTheSaddle(string key)
    {
        var kind = CavalrySetup.Kinds.Single(k => k.key == key);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(kind.Prefab);
        var mirror = prefab.GetComponent<RiderAnimatorMirror>();
        Assert.That(mirror, Is.Not.Null);
        Assert.That(mirror.Horse, Is.EqualTo(prefab.GetComponent<Animator>()), "The horse's Animator is the root's.");
        Assert.That(mirror.Horse.runtimeAnimatorController.name, Is.EqualTo("AC_Horse"));
        Assert.That(mirror.Rider.transform.parent.name, Is.EqualTo("Saddle"));
        Assert.That(mirror.Rider.avatar != null && mirror.Rider.avatar.isHuman, Is.True);
        Assert.That(mirror.Rider.runtimeAnimatorController.name, Is.EqualTo("AC_Rider_" + kind.rider));
        Assert.That(prefab.GetComponent<UnityEngine.AI.NavMeshAgent>(), Is.Not.Null);
        Assert.That(prefab.GetComponentsInChildren<ShieldHitboxBehaviour>(true), Is.Empty, "No shield on horseback.");
        Assert.That(prefab.GetComponentsInChildren<WeaponHitboxBehaviour>(true).Length, Is.EqualTo(kind.ranged ? 0 : 1));
    }

    [Test, TestCaseSource(nameof(Kinds))]
    public void HorseAndRiderGaitsLastTheSameSoTheyPlayInStep(string key)
    {
        var kind = CavalrySetup.Kinds.Single(k => k.key == key);
        var horse = Tree(CavalrySetup.HorseController);
        var rider = Tree(kind.RiderController);
        Assert.That(rider.children.Length, Is.EqualTo(horse.children.Length));
        for (int i = 0; i < horse.children.Length; i++)
        {
            Assert.That(rider.children[i].threshold, Is.EqualTo(horse.children[i].threshold));
            Assert.That(rider.children[i].motion.averageDuration, Is.EqualTo(horse.children[i].motion.averageDuration).Within(1e-3f),
                $"{rider.children[i].motion.name} lasts as long as {horse.children[i].motion.name}.");
        }
    }

    // ---------------------------------------------------------------- helpers

    static BlendTree Tree(string controller)
        => (BlendTree)AssetDatabase.LoadAssetAtPath<AnimatorController>(controller).layers[0].stateMachine.defaultState.motion;

    static Entity Target(EntityManager em, float3 position)
    {
        var e = em.CreateEntity(typeof(LocalTransform), typeof(HealthComponent));
        em.SetComponentData(e, LocalTransform.FromPosition(position));
        em.SetComponentData(e, new HealthComponent { currentHealth = 100, maxHealth = 100 });
        return e;
    }

    static Entity Melee(EntityManager em, Entity target, bool mounted)
    {
        var profile = em.CreateEntity(typeof(DamageProfileComponent));
        var e = em.CreateEntity(typeof(LocalTransform), typeof(UnitCombatComponent), typeof(UnitWeaponComponent));
        em.SetComponentData(e, LocalTransform.Identity);
        em.SetComponentData(e, new UnitCombatComponent { target = target });
        em.SetComponentData(e, new UnitWeaponComponent { attackRange = 5, damageProfile = profile, attackAnimationDuration = 1f });
        if (mounted) em.AddComponentData(e, new MountComponent { attackArcCos = MountRules.ArcCos(90f), turnRate = 120f });
        return e;
    }

    static Entity Archer(EntityManager em, Entity target)
    {
        var profile = em.CreateEntity(typeof(DamageProfileComponent));
        var e = em.CreateEntity(typeof(LocalTransform), typeof(UnitCombatComponent), typeof(UnitWeaponComponent),
            typeof(UnitRangedStatsComponent), typeof(RangedAttackStateComponent));
        em.SetComponentData(e, LocalTransform.Identity);
        em.SetComponentData(e, new UnitCombatComponent { target = target });
        em.SetComponentData(e, new UnitWeaponComponent { damageProfile = profile });
        em.SetComponentData(e, new UnitRangedStatsComponent { range = 30, accuracy = 100, fireRate = 1, totalAmmo = 3, launchHeight = 2.3f });
        em.SetComponentData(e, new RangedAttackStateComponent { currentAmmo = 3 });
        em.AddComponentData(e, new MountComponent { attackArcCos = MountRules.ArcCos(300f), turnRate = 140f });
        return e;
    }
}

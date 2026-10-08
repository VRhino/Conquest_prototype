using System.Collections;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;
using ConquestTactics.Animation;
using ConquestTactics.Visual;

[DefaultExecutionOrder(0)]
public sealed class LocalHeroRotationTestWriter : MonoBehaviour
{
    private void Update()
    {
        transform.Rotate(0f, 15f, 0f, Space.World);
    }
}

public class HeroVisualRespawnRegressionTests
{
    [Test]
    public void VerticalDisplacementDoesNotAnimateUnitLocomotion()
    {
        using var world = new World("Horizontal animation regression");
        var em = world.EntityManager;
        em.CreateEntity(typeof(MatchStateComponent));
        var config = em.CreateEntity(typeof(SquadSpawnConfigComponent));
        em.SetComponentData(config, new SquadSpawnConfigComponent {
            animationMovingSpeedThreshold = 0.05f, animationRunningSpeedRatio = 0.6f
        });
        var unit = em.CreateEntity(typeof(LocalTransform), typeof(UnitStatsComponent),
            typeof(UnitAnimationMovementComponent));
        em.SetComponentData(unit, LocalTransform.FromPosition(new float3(0, 4, 0)));
        em.SetComponentData(unit, new UnitStatsComponent { speed = 3.5f });
        world.SetTime(new Unity.Core.TimeData(1d, 0.1f));
        world.GetOrCreateSystemManaged<UnitAnimationSystem>().Update();
        var animation = em.GetComponentData<UnitAnimationMovementComponent>(unit);
        Assert.That(animation.IsMoving, Is.False);
        Assert.That(animation.CurrentSpeed, Is.Zero);
        Assert.That(animation.PreviousPosition.y, Is.EqualTo(4f));
    }

    [Test]
    public void BlendCoordinatesTrackAcceptedGaitAcrossGameplaySpeeds()
    {
        var settings = new LocomotionAnimationSettings();
        foreach (float baseSpeed in new[] { 2f, 3.5f, 5f })
        {
            Assert.That(settings.GetGait(baseSpeed, baseSpeed, false), Is.EqualTo(LocomotionGait.Run));
            Assert.That(settings.GetBlendSpeed(baseSpeed, baseSpeed, baseSpeed * 2f, false), Is.EqualTo(5f));
            Assert.That(settings.GetGait(baseSpeed * 2f, baseSpeed, true), Is.EqualTo(LocomotionGait.Sprint));
            Assert.That(settings.GetBlendSpeed(baseSpeed * 2f, baseSpeed, baseSpeed * 2f, true), Is.EqualTo(12f));
            Assert.That(settings.GetGait(0f, baseSpeed, true), Is.EqualTo(LocomotionGait.Idle));
            Assert.That(settings.GetBlendSpeed(0f, baseSpeed, baseSpeed * 2f, true), Is.Zero);
        }
    }

    [UnityTest]
    public IEnumerator LocalMotorPublishesFinalVisualRotationInTheSameFrame()
    {
        using var world = new World("Local hero final rotation regression");
        var visual = new GameObject("Local hero rotation fixture");
        try
        {
            var em = world.EntityManager;
            var hero = em.CreateEntity(typeof(LocalTransform), typeof(IsLocalPlayer));
            em.SetComponentData(hero, LocalTransform.Identity);
            visual.AddComponent<CharacterController>();
            var motor = visual.AddComponent<LocalHeroCharacterMotor>();
            motor.Bind(world, hero, true, true);
            visual.AddComponent<LocalHeroRotationTestWriter>();

            yield return null;
            // Batchmode does not invoke WaitForEndOfFrame. FixedUpdate resumes
            // after the prior frame's LateUpdate and before the next Update.
            yield return new WaitForFixedUpdate();

            var published = em.GetComponentData<LocalTransform>(hero);
            Assert.That(Quaternion.Angle(published.Rotation, visual.transform.rotation),
                Is.LessThan(0.01f));
        }
        finally
        {
            Object.DestroyImmediate(visual);
        }
    }

    [Test]
    public void SpawnRevisionTeleportsOnceAndDeadHeroIgnoresStaleMovement()
    {
        var previousWorld = World.DefaultGameObjectInjectionWorld;
        using var world = new World("Hero visual respawn regression");
        World.DefaultGameObjectInjectionWorld = world;
        var visual = new GameObject("Hero respawn fixture");
        try
        {
            var em = world.EntityManager;
            var hero = em.CreateEntity(typeof(LocalTransform), typeof(HeroSpawnComponent),
                typeof(HeroMoveIntent), typeof(HeroLifeComponent), typeof(IsLocalPlayer));
            em.SetComponentData(hero, LocalTransform.Identity);
            em.SetComponentData(hero, new HeroLifeComponent { isAlive = true });
            var pose = new HeroSpawnComponent { hasSpawned = true, positionRevision = 1,
                spawnPosition = new float3(12, 3, 8), spawnRotation = quaternion.identity };
            em.SetComponentData(hero, pose);
            em.SetComponentData(hero, new HeroMoveIntent { Direction = math.forward(), Speed = 20 });
            visual.AddComponent<CharacterController>();
            var sync = visual.AddComponent<EntityVisualSync>();
            sync.DebugLogging = false;
            sync.IsLocalHero = true;
            sync.SetHeroEntity(hero);
            var motor = visual.GetComponent<LocalHeroCharacterMotor>();
            Assert.That(motor, Is.Not.Null);
            motor.Tick(0f);
            Assert.That((float3)visual.transform.position, Is.EqualTo(pose.spawnPosition));
            Assert.That(em.GetComponentData<LocalTransform>(hero).Position, Is.EqualTo(pose.spawnPosition));
            Assert.That(em.HasComponent<HeroMotorStateComponent>(hero), Is.True);

            motor.Tick(0.1f);
            Assert.That(visual.transform.position.z, Is.GreaterThan(pose.spawnPosition.z));
            Assert.That(em.GetComponentData<LocalTransform>(hero).Position,
                Is.EqualTo((float3)visual.transform.position));
            Assert.That(em.GetComponentData<HeroMotorStateComponent>(hero).velocity.z,
                Is.GreaterThan(0f));

            em.SetComponentData(hero, new HeroLifeComponent { isAlive = false });
            var movedPosition = new Vector3(15, 3, 8);
            visual.transform.position = movedPosition;
            motor.Tick(0.1f);
            Assert.That(visual.transform.position, Is.EqualTo(movedPosition));

            pose.positionRevision++;
            pose.spawnPosition = new float3(-7, 4, 2);
            em.SetComponentData(hero, pose);
            em.SetComponentData(hero, new HeroLifeComponent { isAlive = true });
            motor.Tick(0.1f);
            Assert.That((float3)visual.transform.position, Is.EqualTo(pose.spawnPosition));
            Assert.That(em.GetComponentData<LocalTransform>(hero).Position, Is.EqualTo(pose.spawnPosition));
            Assert.That(visual.GetComponent<CharacterController>().enabled, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(visual);
            World.DefaultGameObjectInjectionWorld = previousWorld;
        }
    }

    [Test]
    public void RemoteVisualCannotKeepCharacterControllerAuthority()
    {
        var previousWorld = World.DefaultGameObjectInjectionWorld;
        using var world = new World("Remote visual authority regression");
        World.DefaultGameObjectInjectionWorld = world;
        var visual = new GameObject("Remote hero fixture");
        try
        {
            var em = world.EntityManager;
            var hero = em.CreateEntity(typeof(LocalTransform), typeof(HeroMoveIntent));
            em.SetComponentData(hero, LocalTransform.Identity);
            var controller = visual.AddComponent<CharacterController>();
            var sync = visual.AddComponent<EntityVisualSync>();
            sync.DebugLogging = false;
            sync.SetHeroEntity(hero);

            Assert.That(sync.IsLocalHero, Is.False);
            Assert.That(controller.enabled, Is.False);
            Assert.That(visual.GetComponent<LocalHeroCharacterMotor>(), Is.Null);
            Assert.That(visual.GetComponent<RemoteHeroAnimationDriver>(), Is.Not.Null);
            Assert.That(visual.GetComponent<RemoteHeroAnimationDriver>().IsBound, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(visual);
            World.DefaultGameObjectInjectionWorld = previousWorld;
        }
    }

    [Test]
    public void LocalAnimationUsesConfirmedMotorMovementInsteadOfRawInput()
    {
        var previousWorld = World.DefaultGameObjectInjectionWorld;
        using var world = new World("Local animation motor-state regression");
        World.DefaultGameObjectInjectionWorld = world;
        var visual = new GameObject("Local animation fixture");
        try
        {
            var em = world.EntityManager;
            var hero = em.CreateEntity(typeof(HeroInputComponent), typeof(HeroMotorStateComponent),
                typeof(IsLocalPlayer));
            em.SetComponentData(hero, new HeroInputComponent { MoveInput = new float2(0, 1) });
            em.SetComponentData(hero, new HeroMotorStateComponent
            {
                velocity = float3.zero,
                isGrounded = true
            });

            var adapter = visual.AddComponent<EcsAnimationInputAdapter>();
            adapter.SendMessage("Start");
            adapter.SendMessage("Update");
            Assert.That(adapter._movementInputDetected, Is.False,
                "Raw input must not animate locomotion while the motor is physically blocked.");
            Assert.That(adapter._moveComposite, Is.EqualTo(Vector2.zero));

            em.SetComponentData(hero, new HeroMotorStateComponent
            {
                velocity = new float3(0, 0, 2),
                isGrounded = false
            });
            adapter.SendMessage("Update");
            Assert.That(adapter._movementInputDetected, Is.True);
            Assert.That(adapter._moveComposite.y, Is.EqualTo(1f));
            Assert.That(adapter._isGrounded, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(visual);
            World.DefaultGameObjectInjectionWorld = previousWorld;
        }
    }

    [Test]
    public void UnitVisualDoesNotReceiveRemoteHeroAnimationDriver()
    {
        var previousWorld = World.DefaultGameObjectInjectionWorld;
        using var world = new World("Unit visual animation ownership regression");
        World.DefaultGameObjectInjectionWorld = world;
        var visual = new GameObject("Unit visual fixture");
        try
        {
            var unit = world.EntityManager.CreateEntity(typeof(LocalTransform));
            world.EntityManager.SetComponentData(unit, LocalTransform.Identity);
            var sync = visual.AddComponent<EntityVisualSync>();
            sync.DebugLogging = false;
            sync.SetHeroEntity(unit);

            Assert.That(visual.GetComponent<RemoteHeroAnimationDriver>(), Is.Null);
            Assert.That(visual.GetComponent<LocalHeroCharacterMotor>(), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(visual);
            World.DefaultGameObjectInjectionWorld = previousWorld;
        }
    }
}

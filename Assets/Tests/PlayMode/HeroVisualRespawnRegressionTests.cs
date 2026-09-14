using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using ConquestTactics.Visual;

public class HeroVisualRespawnRegressionTests
{
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
            sync.SendMessage("Update");
            Assert.That((float3)visual.transform.position, Is.EqualTo(pose.spawnPosition));
            Assert.That(em.GetComponentData<LocalTransform>(hero).Position, Is.EqualTo(pose.spawnPosition));

            em.SetComponentData(hero, new HeroLifeComponent { isAlive = false });
            var movedPosition = new Vector3(15, 3, 8);
            visual.transform.position = movedPosition;
            sync.SendMessage("Update");
            Assert.That(visual.transform.position, Is.EqualTo(movedPosition));

            pose.positionRevision++;
            pose.spawnPosition = new float3(-7, 4, 2);
            em.SetComponentData(hero, pose);
            em.SetComponentData(hero, new HeroLifeComponent { isAlive = true });
            sync.SendMessage("Update");
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
}

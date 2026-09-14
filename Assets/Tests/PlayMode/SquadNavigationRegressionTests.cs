using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AI;

public class SquadNavigationRegressionTests
{
    [Test]
    public void RemoteHeroRejectsOffMeshDecisionWithoutLeavingMovementIntent()
    {
        using var world = new World("Remote hero invalid destination");
        var em = world.EntityManager;
        var sources = new System.Collections.Generic.List<NavMeshBuildSource>
        {
            new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                size = new Vector3(20, 0.2f, 20), transform = Matrix4x4.Translate(new Vector3(0, -0.1f, 0)), area = 0 }
        };
        var settings = NavMesh.GetSettingsByIndex(0);
        var data = NavMeshBuilder.BuildNavMeshData(settings, sources,
            new Bounds(Vector3.zero, new Vector3(25, 5, 25)), Vector3.zero, Quaternion.identity);
        var navData = NavMesh.AddNavMeshData(data);
        var visual = new GameObject("Remote hero nav agent");
        try
        {
            var agent = visual.AddComponent<NavMeshAgent>();
            agent.agentTypeID = settings.agentTypeID;
            Assert.That(agent.Warp(Vector3.zero), Is.True);
            var hero = em.CreateEntity(typeof(HeroAITag), typeof(LocalTransform),
                typeof(HeroStatsComponent), typeof(HeroLifeComponent), typeof(HeroAIDecision),
                typeof(HeroMoveIntent), typeof(NavAgentComponent));
            em.SetComponentData(hero, LocalTransform.Identity);
            em.SetComponentData(hero, new HeroStatsComponent { baseSpeed = 4f, sprintMultiplier = 2f });
            em.SetComponentData(hero, new HeroLifeComponent { isAlive = true });
            em.SetComponentData(hero, new HeroAIDecision
            {
                action = AIActionType.MoveTo,
                targetPosition = new float3(100, 0, 100),
                shouldSprint = true
            });
            em.AddComponentObject(hero, agent);

            Assert.DoesNotThrow(() => world.GetOrCreateSystemManaged<HeroAIExecutionSystem>().Update());

            var navigation = em.GetComponentData<NavAgentComponent>(hero);
            var intent = em.GetComponentData<HeroMoveIntent>(hero);
            Assert.That(navigation.commandFailed, Is.True);
            Assert.That(agent.isStopped, Is.True);
            Assert.That(math.lengthsq(intent.Direction), Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(visual);
            navData.Remove();
            Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void TeamCollisionMatrixLetsLocalHeroBlockOnlyEnemyUnits()
    {
        int unitsA = LayerMask.NameToLayer("Units_A");
        int heroesA = LayerMask.NameToLayer("Heroes_A");
        int unitsB = LayerMask.NameToLayer("Units_B");
        int heroesB = LayerMask.NameToLayer("Heroes_B");
        Assert.That(unitsA, Is.GreaterThanOrEqualTo(0));
        Assert.That(heroesA, Is.GreaterThanOrEqualTo(0));
        Assert.That(unitsB, Is.GreaterThanOrEqualTo(0));
        Assert.That(heroesB, Is.GreaterThanOrEqualTo(0));
        Assert.That(Physics.GetIgnoreLayerCollision(heroesA, unitsA), Is.True,
            "El héroe local no debe bloquear físicamente a sus propias unidades.");
        Assert.That(Physics.GetIgnoreLayerCollision(heroesA, unitsB), Is.False,
            "El CharacterController local debe colisionar con unidades enemigas.");
        Assert.That(Physics.GetIgnoreLayerCollision(heroesB, unitsB), Is.True);
        Assert.That(Physics.GetIgnoreLayerCollision(heroesB, unitsA), Is.False);
    }

    [Test]
    public void OffMeshFormationSlotFallsBackToReachableCurrentPosition()
    {
        using var world = new World("Off-mesh formation fallback");
        var em = world.EntityManager;
        em.CreateEntity(typeof(MatchStateComponent));
        em.AddComponentData(em.CreateEntity(), new SquadSpawnConfigComponent
        {
            slotArrivalThreshold = 0.2f,
            navMeshDestinationSampleRadius = 0.5f,
            navMeshFailureRetryDistance = 1f
        });
        var sources = new System.Collections.Generic.List<NavMeshBuildSource>
        {
            new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                size = new Vector3(20, 0.2f, 20), transform = Matrix4x4.Translate(new Vector3(0, -0.1f, 0)), area = 0 }
        };
        var settings = NavMesh.GetSettingsByIndex(0);
        var data = NavMeshBuilder.BuildNavMeshData(settings, sources,
            new Bounds(Vector3.zero, new Vector3(25, 5, 25)), Vector3.zero, Quaternion.identity);
        var navData = NavMesh.AddNavMeshData(data);
        var visual = new GameObject("Off-mesh nav agent");
        try
        {
            var agent = visual.AddComponent<NavMeshAgent>();
            agent.agentTypeID = settings.agentTypeID;
            Assert.That(agent.Warp(Vector3.zero), Is.True);
            var unit = em.CreateEntity(typeof(NavAgentComponent), typeof(LocalTransform),
                typeof(UnitTargetPositionComponent), typeof(UnitFormationStateComponent));
            em.SetComponentData(unit, LocalTransform.Identity);
            var unreachable = new float3(100, 0, 100);
            em.SetComponentData(unit, new UnitTargetPositionComponent { position = unreachable });
            em.SetComponentData(unit, new UnitFormationStateComponent { State = UnitFormationState.Moving });
            em.AddComponentObject(unit, agent);

            world.GetOrCreateSystemManaged<UnitNavMeshSystem>().Update();

            var navigation = em.GetComponentData<NavAgentComponent>(unit);
            Assert.That(navigation.formationDestinationFailed, Is.True);
            Assert.That(math.distance(navigation.effectiveFormationDestination, float3.zero), Is.LessThan(0.01f));
            Assert.That(math.distance(SquadNavigationSystem.GetEffectiveFormationDestination(
                em, unit, unreachable), float3.zero), Is.LessThan(0.01f));
        }
        finally
        {
            Object.DestroyImmediate(visual);
            navData.Remove();
            Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void ExactEnemyOverlapIsSeparatedBeforePositionSync()
    {
        using var world = new World("Bodyblock integration");
        var em = world.EntityManager;
        em.CreateEntity(typeof(MatchStateComponent));
        em.AddComponentData(em.CreateEntity(), new SquadSpawnConfigComponent
        {
            bodyblockRadius = 0.8f, bodyblockRepulsionStrength = 8f,
            bodyblockWallStrength = 60f, bodyblockMaxPushSpeed = 18f,
            bodyblockEngagingRadius = 0.35f, bodyblockEngagingStrength = 3f
        });
        var sources = new System.Collections.Generic.List<NavMeshBuildSource>
        {
            new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                size = new Vector3(20, 0.2f, 20), transform = Matrix4x4.Translate(new Vector3(0, -0.1f, 0)), area = 0 }
        };
        var settings = NavMesh.GetSettingsByIndex(0);
        var data = NavMeshBuilder.BuildNavMeshData(settings, sources,
            new Bounds(Vector3.zero, new Vector3(25, 5, 25)), Vector3.zero, Quaternion.identity);
        var navData = NavMesh.AddNavMeshData(data);
        var firstGo = new GameObject("Bodyblock A");
        var secondGo = new GameObject("Bodyblock B");
        try
        {
            Entity first = CreateBodyblockUnit(em, firstGo, Team.TeamA, settings.agentTypeID);
            Entity second = CreateBodyblockUnit(em, secondGo, Team.TeamB, settings.agentTypeID);
            world.SetTime(new Unity.Core.TimeData(0, 1f / 60f));
            world.GetOrCreateSystemManaged<UnitBodyblockSystem>().Update();
            Assert.That(Vector3.Distance(firstGo.transform.position, secondGo.transform.position), Is.GreaterThan(0.001f));
            em.DestroyEntity(first);
            em.DestroyEntity(second);
        }
        finally
        {
            Object.DestroyImmediate(firstGo);
            Object.DestroyImmediate(secondGo);
            navData.Remove();
            Object.DestroyImmediate(data);
        }
    }

    static Entity CreateBodyblockUnit(EntityManager em, GameObject visual, Team team, int agentType)
    {
        var agent = visual.AddComponent<NavMeshAgent>();
        agent.agentTypeID = agentType;
        Assert.That(agent.Warp(Vector3.zero), Is.True);
        var entity = em.CreateEntity(typeof(NavAgentComponent), typeof(TeamComponent),
            typeof(UnitFormationStateComponent));
        em.SetComponentData(entity, new TeamComponent { value = team });
        em.SetComponentData(entity, new UnitFormationStateComponent { State = UnitFormationState.Moving });
        em.AddComponentObject(entity, agent);
        return entity;
    }

    [TestCase(SquadOrderType.HoldPosition, true)]
    [TestCase(SquadOrderType.FollowHero, true)]
    [TestCase(SquadOrderType.Attack, false)]
    public void NavMeshReceivesSlotOrPursuitDestinationDuringCombat(SquadOrderType order, bool expectSlot)
    {
        using var world = new World("Squad navigation regression");
        var em = world.EntityManager;
        em.CreateEntity(typeof(MatchStateComponent));
        var sources = new System.Collections.Generic.List<NavMeshBuildSource>
        {
            new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                size = new Vector3(100, 0.2f, 100),
                transform = Matrix4x4.Translate(new Vector3(0, -0.1f, 0)),
                area = 0
            }
        };
        var settings = NavMesh.GetSettingsByIndex(0);
        var data = NavMeshBuilder.BuildNavMeshData(settings, sources,
            new Bounds(Vector3.zero, new Vector3(110, 10, 110)), Vector3.zero, Quaternion.identity);
        Assert.That(data, Is.Not.Null);
        var navData = NavMesh.AddNavMeshData(data);
        var visual = new GameObject("Regression nav agent");
        try
        {
            var agent = visual.AddComponent<NavMeshAgent>();
            agent.agentTypeID = settings.agentTypeID;
            Assert.That(agent.Warp(Vector3.zero), Is.True);
            var target = em.CreateEntity(typeof(LocalTransform));
            em.SetComponentData(target, LocalTransform.FromPosition(new float3(20, 0, 0)));
            var unit = em.CreateEntity(typeof(NavAgentComponent), typeof(LocalTransform),
                typeof(UnitTargetPositionComponent), typeof(UnitFormationStateComponent),
                typeof(UnitCombatComponent), typeof(UnitWeaponComponent));
            em.SetComponentData(unit, LocalTransform.Identity);
            var slot = new float3(-4, 0, 0);
            em.SetComponentData(unit, new UnitTargetPositionComponent { position = slot });
            em.SetComponentData(unit, new UnitFormationStateComponent { State = UnitFormationState.Moving });
            em.SetComponentData(unit, new UnitCombatComponent { target = target });
            em.SetComponentData(unit, new UnitWeaponComponent { attackRange = 1.5f });
            em.AddComponentObject(unit, agent);
            var squad = em.CreateEntity(typeof(SquadStateComponent));
            em.AddBuffer<SquadUnitElement>(squad).Add(new SquadUnitElement { Value = unit });
            em.SetComponentData(squad, new SquadStateComponent { currentOrder = order, currentState = SquadFSMState.InCombat });
            world.GetOrCreateSystemManaged<UnitNavMeshSystem>().Update();
            if (expectSlot)
            {
                // The baked surface may be vertically offset by voxelization.
                // Compare to its projection, not to the source box's nominal height.
                Assert.That(NavMesh.SamplePosition((Vector3)slot, out var hit, 1f,
                    new NavMeshQueryFilter { agentTypeID = settings.agentTypeID, areaMask = NavMesh.AllAreas }), Is.True);
                Assert.That(Vector3.Distance(agent.destination, hit.position), Is.LessThan(0.01f));
            }
            else
                Assert.That(agent.destination.x, Is.GreaterThan(18f));
            Assert.That(em.GetComponentData<UnitCombatComponent>(unit).target, Is.EqualTo(target));
            em.DestroyEntity(unit);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(visual);
            navData.Remove();
            UnityEngine.Object.DestroyImmediate(data);
        }
    }
}

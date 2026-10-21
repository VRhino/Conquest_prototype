using System.Collections;
using NUnit.Framework;
using ConquestTactics.Animation;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

public class SquadNavigationRegressionTests
{
    [Test]
    public void UnitAnimationFallsBackToPoseDeltaWhenNavAgentComponentHasNoManagedAgent()
    {
        using var world = new World("Unit animation missing NavMeshAgent fallback");
        var em = world.EntityManager;
        em.SetComponentData(em.CreateEntity(typeof(MatchStateComponent)),
            new MatchStateComponent { currentState = MatchState.InBattle });
        em.AddComponentData(em.CreateEntity(), new SquadSpawnConfigComponent
        {
            animationMovingSpeedThreshold = 0.05f,
            animationRunningSpeedRatio = 0.6f
        });

        var entity = em.CreateEntity(typeof(NavAgentComponent), typeof(LocalTransform),
            typeof(UnitAnimationMovementComponent), typeof(UnitStatsComponent));
        em.SetComponentData(entity, LocalTransform.FromPosition(new float3(3f, 0f, 0f)));
        em.SetComponentData(entity, new UnitAnimationMovementComponent
        {
            PreviousPosition = float3.zero
        });
        em.SetComponentData(entity, new UnitStatsComponent { speed = 3f });

        world.SetTime(new Unity.Core.TimeData(0, 1f));
        world.GetOrCreateSystemManaged<NavMeshPositionSyncSystem>().Update();
        world.GetOrCreateSystemManaged<UnitAnimationSystem>().Update();

        var animation = em.GetComponentData<UnitAnimationMovementComponent>(entity);
        Assert.That(animation.HasNavMeshAgentSnapshot, Is.False);
        Assert.That(animation.CurrentSpeed, Is.EqualTo(3f).Within(0.001f));
        Assert.That(animation.IsMoving, Is.True);
        Assert.That(animation.MovementDirection.x, Is.EqualTo(1f).Within(0.001f));
    }

    [UnityTest]
    public IEnumerator VisualSyncKeepsAnimatedGameplayHitboxesUpdatingWithoutGlobalAlwaysAnimate()
    {
        var hitboxVisual = new GameObject("Hitbox animation culling regression");
        var shieldVisual = new GameObject("Shield animation culling regression");
        var visualOnly = new GameObject("Visual-only culling regression");
        try
        {
            var hitboxAnimator = hitboxVisual.AddComponent<Animator>();
            hitboxAnimator.cullingMode = AnimatorCullingMode.CullCompletely;
            var hitbox = new GameObject("WeaponHitbox");
            hitbox.transform.SetParent(hitboxVisual.transform);
            hitbox.AddComponent<BoxCollider>();
            hitbox.AddComponent<WeaponHitboxBehaviour>();
            var hitboxSync = hitboxVisual.AddComponent<ConquestTactics.Visual.EntityVisualSync>();

            var shieldAnimator = shieldVisual.AddComponent<Animator>();
            shieldAnimator.cullingMode = AnimatorCullingMode.CullCompletely;
            var shieldHitbox = new GameObject("ShieldHitbox");
            shieldHitbox.transform.SetParent(shieldVisual.transform);
            shieldHitbox.AddComponent<BoxCollider>();
            shieldHitbox.AddComponent<ShieldHitboxBehaviour>();
            var shieldSync = shieldVisual.AddComponent<ConquestTactics.Visual.EntityVisualSync>();

            var visualOnlyAnimator = visualOnly.AddComponent<Animator>();
            visualOnlyAnimator.cullingMode = AnimatorCullingMode.CullCompletely;
            var visualOnlySync = visualOnly.AddComponent<ConquestTactics.Visual.EntityVisualSync>();
            var autoFindField = typeof(ConquestTactics.Visual.EntityVisualSync).GetField(
                "_autoFindHeroEntity", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(autoFindField, Is.Not.Null);
            autoFindField.SetValue(hitboxSync, false);
            autoFindField.SetValue(shieldSync, false);
            autoFindField.SetValue(visualOnlySync, false);

            yield return null;

            Assert.That(hitboxAnimator.cullingMode, Is.EqualTo(AnimatorCullingMode.AlwaysAnimate),
                "An Animator that drives a weapon hitbox must keep updating outside the camera.");
            Assert.That(shieldAnimator.cullingMode, Is.EqualTo(AnimatorCullingMode.AlwaysAnimate),
                "An Animator that drives a shield hitbox must keep updating outside the camera.");
            Assert.That(visualOnlyAnimator.cullingMode, Is.EqualTo(AnimatorCullingMode.CullCompletely),
                "Visuals without gameplay hitboxes should retain Animator culling.");
        }
        finally
        {
            Object.DestroyImmediate(hitboxVisual);
            Object.DestroyImmediate(shieldVisual);
            Object.DestroyImmediate(visualOnly);
        }
    }

    [Test]
    public void PooledProjectileRestoresPreviousShooterCollisionsOnReuseAndDisable()
    {
        var shooterA = new GameObject("Projectile shooter A");
        var shooterB = new GameObject("Projectile shooter B");
        var projectile = new GameObject("Projectile collision regression");
        var colliderA = shooterA.AddComponent<BoxCollider>();
        var colliderB = shooterB.AddComponent<BoxCollider>();
        var projectileCollider = projectile.AddComponent<BoxCollider>();
        var controller = projectile.AddComponent<ProjectileController>();
        try
        {
            controller.Initialize(Entity.Null, Entity.Null, float3.zero, math.forward(), Entity.Null,
                default, Team.None, 1, "test", ProjectileTrajectory.Straight, new[] { colliderA });
            Assert.That(Physics.GetIgnoreCollision(projectileCollider, colliderA), Is.True);
            controller.Initialize(Entity.Null, Entity.Null, float3.zero, math.forward(), Entity.Null,
                default, Team.None, 1, "test", ProjectileTrajectory.Straight, new[] { colliderB });
            Assert.That(Physics.GetIgnoreCollision(projectileCollider, colliderA), Is.False);
            Assert.That(Physics.GetIgnoreCollision(projectileCollider, colliderB), Is.True);
            projectile.SetActive(false);
            Assert.That(Physics.GetIgnoreCollision(projectileCollider, colliderB), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(projectile);
            Object.DestroyImmediate(shooterA);
            Object.DestroyImmediate(shooterB);
        }
    }

    [Test]
    public void RemoteHeroRetainsValidPathUntilDestinationChangesOrPathFails()
    {
        var navigation = new NavAgentComponent
        {
            hasIssuedCommand = true,
            lastCommandDestination = new float3(4f, 0f, 5f)
        };
        var nearDestination = new float3(4.1f, 0f, 5f);
        const float threshold = 0.25f;

        Assert.That(HeroAIExecutionSystem.IsCurrentPathValid(navigation, nearDestination,
            threshold, true, false, NavMeshPathStatus.PathInvalid), Is.True);
        Assert.That(HeroAIExecutionSystem.IsCurrentPathValid(navigation, nearDestination,
            threshold, false, true, NavMeshPathStatus.PathComplete), Is.True);
        Assert.That(HeroAIExecutionSystem.IsCurrentPathValid(navigation, new float3(5f, 0f, 5f),
            threshold, false, true, NavMeshPathStatus.PathComplete), Is.False);
        Assert.That(HeroAIExecutionSystem.IsCurrentPathValid(navigation, nearDestination,
            threshold, false, true, NavMeshPathStatus.PathPartial), Is.False);
        Assert.That(HeroAIExecutionSystem.IsCurrentPathValid(navigation, nearDestination,
            threshold, false, false, NavMeshPathStatus.PathInvalid), Is.False);
    }

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
            Assert.That(navigation.nextCommandRetryTime, Is.GreaterThan(0));
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
        em.SetComponentData(em.CreateEntity(typeof(MatchStateComponent)),
            new MatchStateComponent { currentState = MatchState.InBattle });
        em.AddComponentData(em.CreateEntity(), new SquadSpawnConfigComponent
        {
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

            // A previously failed but now reachable slot must be sampled again
            // after the retry interval, even when the requested slot is unchanged.
            var reachable = new float3(1, 0, 1);
            navigation.lastFormationRequest = reachable;
            navigation.nextFormationRetryTime = 2;
            em.SetComponentData(unit, navigation);
            em.SetComponentData(unit, new UnitTargetPositionComponent { position = reachable });
            world.SetTime(new Unity.Core.TimeData(1, 0.1f));
            world.GetOrCreateSystemManaged<UnitNavMeshSystem>().Update();
            Assert.That(em.GetComponentData<NavAgentComponent>(unit).formationDestinationFailed, Is.True);
            world.SetTime(new Unity.Core.TimeData(3, 0.1f));
            world.GetOrCreateSystemManaged<UnitNavMeshSystem>().Update();
            Assert.That(em.GetComponentData<NavAgentComponent>(unit).formationDestinationFailed, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(visual);
            navData.Remove();
            Object.DestroyImmediate(data);
        }
    }

    [UnityTest, Explicit("Diagnostic-only comparison of NavMesh projection APIs; not a production navigation path.")]
    public IEnumerator LowLevelProjectionComparisonReportsDivergenceWithoutChangingNavigation()
    {
        var sources = new System.Collections.Generic.List<NavMeshBuildSource>
        {
            new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                size = new Vector3(8f, 0.2f, 20f),
                transform = Matrix4x4.Translate(new Vector3(-6f, -0.1f, 0f)),
                area = 0
            },
            new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                size = new Vector3(8f, 0.2f, 20f),
                transform = Matrix4x4.Translate(new Vector3(6f, -0.1f, 0f)),
                area = 2
            }
        };
        var settings = NavMesh.GetSettingsByIndex(0);
        var data = NavMeshBuilder.BuildNavMeshData(settings, sources,
            new Bounds(Vector3.zero, new Vector3(30f, 8f, 24f)), Vector3.zero, Quaternion.identity);
        var navData = NavMesh.AddNavMeshData(data);
        var agentObject = new GameObject("Low-level projection navigation sentinel");
        agentObject.transform.position = new Vector3(-8f, 0f, -8f);
        try
        {
            var agent = agentObject.AddComponent<NavMeshAgent>();
            agent.agentTypeID = settings.agentTypeID;
            agent.areaMask = NavMesh.AllAreas;
            Assert.That(agent.Warp(new Vector3(-8f, 0f, -8f)), Is.True,
                "The synthetic plane must accept the sentinel agent before comparison.");
            agent.isStopped = true;
            Vector3 requestedDestination = new(-5f, 0f, -5f);
            Assert.That(agent.SetDestination(requestedDestination), Is.True);
            yield return null;
            // NavMesh projects the request onto its baked surface (including height).
            // Compare the accepted physical state, not the unprojected request.
            Vector3 destinationBeforeComparison = agent.destination;
            Vector3 positionBeforeComparison = agent.nextPosition;

            Vector3[] positions = new Vector3[64];
            positions[0] = new Vector3(-6f, 0f, 0f);       // Interior of area 0.
            positions[1] = new Vector3(-2.05f, 0f, 0f);    // Edge of area 0.
            positions[2] = Vector3.zero;                   // Gap between the two surfaces.
            positions[3] = new Vector3(6f, 0f, 0f);        // Interior of area 2.
            positions[4] = new Vector3(15f, 0f, 0f);       // Outside both surfaces.
            positions[5] = new Vector3(-6f, 2f, 0f);       // Height offset from the plane.
            for (int i = 6; i < positions.Length; i++)
            {
                int column = (i - 6) % 10;
                int row = (i - 6) / 10;
                positions[i] = new Vector3(-12f + column * (24f / 9f), (row % 2) * 1.5f,
                    -9f + row * 3f);
            }

            var tools = System.Type.GetType("Conquest.Editor.Optimization.RenderOptimizationExperiments, Game.Editor");
            var compare = tools?.GetMethod("CompareLowLevelProjection",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.That(compare, Is.Not.Null, "The installed Editor diagnostic helper must be available.");
            string allAreasJson = (string)compare.Invoke(null, new object[]
            {
                positions, settings.agentTypeID, NavMesh.AllAreas, 2.5f,
                new Vector3(2.5f, 2.5f, 2.5f), 0.25f, 64
            });
            string areaZeroJson = (string)compare.Invoke(null, new object[]
            {
                positions, settings.agentTypeID, 1 << 0, 2.5f,
                new Vector3(2.5f, 2.5f, 2.5f), 0.25f, 64
            });
            string areaTwoJson = (string)compare.Invoke(null, new object[]
            {
                positions, settings.agentTypeID, 1 << 2, 2.5f,
                new Vector3(2.5f, 2.5f, 2.5f), 0.25f, 64
            });

            Assert.That(allAreasJson, Does.Contain("\"samples\": 64"));
            Assert.That(allAreasJson, Does.Contain("\"validityMismatches\""));
            Assert.That(areaZeroJson, Does.Contain("\"samples\": 64"));
            Assert.That(areaTwoJson, Does.Contain("\"samples\": 64"));
            Assert.That(Vector3.Distance(agent.destination, destinationBeforeComparison), Is.LessThan(0.001f),
                "The diagnostic comparison must not change the active NavMeshAgent destination.");
            Assert.That(Vector3.Distance(agent.nextPosition, positionBeforeComparison), Is.LessThan(0.001f),
                "The stopped sentinel agent must remain at its original location.");

            string report = "{\n  \"allAreas\": " + allAreasJson + ",\n  \"areaZeroOnly\": " + areaZeroJson +
                            ",\n  \"areaTwoOnly\": " + areaTwoJson + "\n}";
            const string outputPath = "Logs/unity6-optimization-round2/lowlevel-projection.json";
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outputPath));
            System.IO.File.WriteAllText(outputPath, report);
            TestContext.WriteLine(report);
        }
        finally
        {
            Object.DestroyImmediate(agentObject);
            navData.Remove();
            Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void ExactEnemyOverlapIsSeparatedBeforePositionSync()
    {
        using var world = new World("Bodyblock integration");
        var em = world.EntityManager;
        em.SetComponentData(em.CreateEntity(typeof(MatchStateComponent)),
            new MatchStateComponent { currentState = MatchState.InBattle });
        em.AddComponentData(em.CreateEntity(), new SquadSpawnConfigComponent
        {
            bodyblockJobBatchSize = 32, bodyblockParallelThreshold = 128,
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

    [Test, Timeout(60000)]
    public void NineHundredBodyblockAgentsReuseNativeStorageAfterWarmup()
    {
        using var world = new World("Bodyblock 900-agent stress");
        var em = world.EntityManager;
        em.SetComponentData(em.CreateEntity(typeof(MatchStateComponent)),
            new MatchStateComponent { currentState = MatchState.InBattle });
        em.AddComponentData(em.CreateEntity(), new SquadSpawnConfigComponent
        {
            bodyblockJobBatchSize = 32, bodyblockParallelThreshold = 128,
            bodyblockRadius = 0.8f, bodyblockRepulsionStrength = 8f,
            bodyblockWallStrength = 60f, bodyblockMaxPushSpeed = 18f,
            bodyblockEngagingRadius = 0.35f, bodyblockEngagingStrength = 3f
        });
        var sources = new System.Collections.Generic.List<NavMeshBuildSource>
        {
            new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                size = new Vector3(60, 0.2f, 60), transform = Matrix4x4.Translate(new Vector3(0, -0.1f, 0)), area = 0 }
        };
        var settings = NavMesh.GetSettingsByIndex(0);
        var data = NavMeshBuilder.BuildNavMeshData(settings, sources,
            new Bounds(Vector3.zero, new Vector3(65, 5, 65)), Vector3.zero, Quaternion.identity);
        var navData = NavMesh.AddNavMeshData(data);
        var visuals = new System.Collections.Generic.List<GameObject>(900);
        try
        {
            for (int z = 0; z < 30; z++)
            for (int x = 0; x < 30; x++)
            {
                var visual = new GameObject($"Bodyblock stress {x}-{z}");
                visuals.Add(visual);
                var position = new Vector3((x - 14.5f) * 1.2f, 0f, (z - 14.5f) * 1.2f);
                CreateBodyblockUnit(em, visual, (x + z) % 2 == 0 ? Team.TeamA : Team.TeamB,
                    settings.agentTypeID, position);
            }

            world.SetTime(new Unity.Core.TimeData(0, 1f / 60f));
            var system = world.GetOrCreateSystemManaged<UnitBodyblockSystem>();
            system.Update();
            int warmedNativeCapacity = system.DebugNativeAgentCapacity;
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            long allocatedBefore = System.GC.GetAllocatedBytesForCurrentThread();
            watch.Restart();
            system.Update();
            watch.Stop();
            long allocatedBytes = System.GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            int sparseCandidatePairs = system.DebugCandidatePairs;
            int sparseContactPairs = system.DebugContactPairs;
            uint sparsePositionFingerprint = GetPositionFingerprint(visuals);

            Assert.That(warmedNativeCapacity, Is.GreaterThanOrEqualTo(900));
            Assert.That(system.DebugNativeAgentCapacity, Is.EqualTo(warmedNativeCapacity),
                "La capacidad nativa debe reutilizarse después del calentamiento.");
            Assert.That(allocatedBytes, Is.Zero,
                "El frame sparse no debe crear asignaciones administradas en el hilo principal.");
            TestContext.WriteLine($"Bodyblock 900 agents warm-frame: {watch.Elapsed.TotalMilliseconds:F2} ms, " +
                $"managed allocation: {allocatedBytes} bytes, native capacity: {warmedNativeCapacity}, " +
                $"candidate pairs: {sparseCandidatePairs}, contact pairs: {sparseContactPairs}, " +
                $"position fingerprint: {sparsePositionFingerprint:X8}");

            // Pack all agents into a 1.45 x 1.45 m area to exercise the contact-heavy case.
            for (int i = 0; i < visuals.Count; i++)
            {
                int x = i % 30;
                int z = i / 30;
                Assert.That(visuals[i].GetComponent<NavMeshAgent>().Warp(
                    new Vector3((x - 14.5f) * 0.05f, 0f, (z - 14.5f) * 0.05f)), Is.True);
            }
            system.Update();
            allocatedBefore = System.GC.GetAllocatedBytesForCurrentThread();
            watch.Restart();
            system.Update();
            watch.Stop();
            allocatedBytes = System.GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            int denseCandidatePairs = system.DebugCandidatePairs;
            int denseContactPairs = system.DebugContactPairs;
            uint densePositionFingerprint = GetPositionFingerprint(visuals);
            Assert.That(system.DebugNativeAgentCapacity, Is.EqualTo(warmedNativeCapacity),
                "La capacidad nativa debe mantenerse estable en el caso denso.");
            Assert.That(allocatedBytes, Is.Zero,
                "El frame denso no debe crear asignaciones administradas en el hilo principal.");
            Assert.That(denseContactPairs, Is.GreaterThan(0),
                "El caso denso debe ejercitar contactos de bodyblock.");
            Assert.That(denseCandidatePairs, Is.GreaterThanOrEqualTo(denseContactPairs));
            TestContext.WriteLine($"Bodyblock 900 agents dense-frame: {watch.Elapsed.TotalMilliseconds:F2} ms, " +
                $"managed allocation: {allocatedBytes} bytes, native capacity: {system.DebugNativeAgentCapacity}, " +
                $"candidate pairs: {denseCandidatePairs}, contact pairs: {denseContactPairs}, " +
                $"position fingerprint: {densePositionFingerprint:X8}");
        }
        finally
        {
            foreach (var visual in visuals) Object.DestroyImmediate(visual);
            navData.Remove();
            Object.DestroyImmediate(data);
        }
    }

    [UnityTest, Timeout(60000)]
    public IEnumerator CrossingAlliesDoNotOverlapWithAvoidance()
    {
        float withoutAvoidance = 0f, withAvoidance = 0f;
        yield return CrossAllies(false, d => withoutAvoidance = d);
        yield return CrossAllies(true, d => withAvoidance = d);
        Assert.That(withoutAvoidance, Is.LessThan(0.5f), "control: without avoidance the allies walk through each other");
        Assert.That(withAvoidance, Is.GreaterThan(0.85f));
    }

    /// <summary>Two allies swap places head-on through UnitAvoidanceSystem; reports their closest distance.</summary>
    static IEnumerator CrossAllies(bool avoidance, System.Action<float> report)
    {
        var world = new World("Ally avoidance crossing");
        var em = world.EntityManager;
        em.SetComponentData(em.CreateEntity(typeof(MatchStateComponent)),
            new MatchStateComponent { currentState = MatchState.InBattle });
        var tuning = MovementTuningComponent.Default;
        tuning.avoidanceEnabled = avoidance;
        em.AddComponentData(em.CreateEntity(), tuning);
        var sources = new System.Collections.Generic.List<NavMeshBuildSource>
        {
            new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                size = new Vector3(20, 0.2f, 20), transform = Matrix4x4.Translate(new Vector3(0, -0.1f, 0)), area = 0 }
        };
        var settings = NavMesh.GetSettingsByIndex(0);
        var data = NavMeshBuilder.BuildNavMeshData(settings, sources,
            new Bounds(Vector3.zero, new Vector3(25, 5, 25)), Vector3.zero, Quaternion.identity);
        var navData = NavMesh.AddNavMeshData(data);
        var firstGo = new GameObject("Crossing A");
        var secondGo = new GameObject("Crossing B");
        try
        {
            var units = new[]
            {
                CreateCrossingUnit(em, firstGo, settings.agentTypeID, new Vector3(-5f, 0f, 0.05f), new Vector3(5f, 0f, 0.05f)),
                CreateCrossingUnit(em, secondGo, settings.agentTypeID, new Vector3(5f, 0f, 0f), new Vector3(-5f, 0f, 0f)),
            };
            var system = world.GetOrCreateSystemManaged<UnitAvoidanceSystem>();
            float closest = float.MaxValue;
            double time = 0;
            for (int frame = 0; frame < 360; frame++)
            {
                foreach (var (entity, visual) in new[] { (units[0], firstGo), (units[1], secondGo) })
                    em.SetComponentData(entity, LocalTransform.FromPosition(visual.transform.position));
                time += Time.deltaTime;
                world.SetTime(new Unity.Core.TimeData(time, Mathf.Max(Time.deltaTime, 1e-3f)));
                system.Update();
                yield return null;
                closest = Mathf.Min(closest, Vector3.Distance(firstGo.transform.position, secondGo.transform.position));
            }
            report(closest);
        }
        finally
        {
            Object.DestroyImmediate(firstGo);
            Object.DestroyImmediate(secondGo);
            navData.Remove();
            Object.DestroyImmediate(data);
            world.Dispose();
        }
    }

    static Entity CreateCrossingUnit(EntityManager em, GameObject visual, int agentType, Vector3 start, Vector3 destination)
    {
        var agent = visual.AddComponent<NavMeshAgent>();
        agent.agentTypeID = agentType;
        agent.speed = 3f;
        agent.acceleration = 20f;
        agent.radius = 0.5f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        Assert.That(agent.Warp(start), Is.True);
        agent.SetDestination(destination);
        var entity = em.CreateEntity(typeof(NavAgentComponent), typeof(TeamComponent),
            typeof(UnitFormationStateComponent), typeof(UnitTargetPositionComponent), typeof(LocalTransform));
        em.SetComponentData(entity, new TeamComponent { value = Team.TeamA });
        em.SetComponentData(entity, new UnitFormationStateComponent { State = UnitFormationState.Moving });
        em.SetComponentData(entity, new UnitTargetPositionComponent { position = destination });
        em.AddComponentObject(entity, agent);
        return entity;
    }

    static Entity CreateBodyblockUnit(EntityManager em, GameObject visual, Team team, int agentType,
        Vector3? startPosition = null)
    {
        var agent = visual.AddComponent<NavMeshAgent>();
        agent.agentTypeID = agentType;
        Assert.That(agent.Warp(startPosition ?? Vector3.zero), Is.True);
        var entity = em.CreateEntity(typeof(NavAgentComponent), typeof(TeamComponent),
            typeof(UnitFormationStateComponent));
        em.SetComponentData(entity, new TeamComponent { value = team });
        em.SetComponentData(entity, new UnitFormationStateComponent { State = UnitFormationState.Moving });
        em.AddComponentObject(entity, agent);
        return entity;
    }

    static uint GetPositionFingerprint(System.Collections.Generic.List<GameObject> visuals)
    {
        unchecked
        {
            uint fingerprint = 2166136261;
            foreach (var visual in visuals)
            {
                var position = visual.transform.position;
                fingerprint = (fingerprint ^ (uint)Mathf.RoundToInt(position.x * 10000f)) * 16777619;
                fingerprint = (fingerprint ^ (uint)Mathf.RoundToInt(position.y * 10000f)) * 16777619;
                fingerprint = (fingerprint ^ (uint)Mathf.RoundToInt(position.z * 10000f)) * 16777619;
            }
            return fingerprint;
        }
    }

    [TestCase(SquadOrderType.HoldPosition, true)]
    [TestCase(SquadOrderType.FollowHero, true)]
    [TestCase(SquadOrderType.Attack, false)]
    public void NavMeshReceivesSlotOrPursuitDestinationDuringCombat(SquadOrderType order, bool expectSlot)
    {
        using var world = new World("Squad navigation regression");
        var em = world.EntityManager;
        em.SetComponentData(em.CreateEntity(typeof(MatchStateComponent)),
            new MatchStateComponent { currentState = MatchState.InBattle });
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
            em.AddComponentData(unit, new UnitOwnerComponent { squad = squad });
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

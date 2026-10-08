#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using System.Linq;
using Core.Persistence;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;
using ConquestTactics.Animation;
using ConquestTactics.Visual;

public class BattleSceneIntegrationTests
{
    const string ScenePath = "Assets/Scenes/BattleTestScene.unity";
    const float ReadyTimeout = 60f;
    sealed class MemorySave : ISaveProvider
    {
        public PlayerData Player;
        public void Save(PlayerData data) => Player = data;
        public PlayerData Load() => Player;
    }

    Keyboard keyboard;
    Mouse mouse;
    ISaveProvider previousSave, previousLoad;
    PlayerData previousPlayer;
    HeroData previousHero;
    bool previousBackground;
    UnityEngine.Random.State previousRandom;
    Entity hero, squad;
    EntityManager em;
    World previousWorld, testWorld;
    bool worldSwapped;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        worldSwapped = false;
        testWorld = null;
        keyboard = null;
        mouse = null;
        previousSave = GetProvider(typeof(SaveSystem));
        previousLoad = GetProvider(typeof(LoadSystem));
        previousPlayer = PlayerSessionService.CurrentPlayer;
        previousHero = PlayerSessionService.SelectedHero;
        previousBackground = Application.runInBackground;
        previousRandom = UnityEngine.Random.state;
        Application.runInBackground = true;
        UnityEngine.Random.InitState(7319);
        var memory = new MemorySave();
        SaveSystem.SetProvider(memory);
        LoadSystem.SetProvider(memory);
        PlayerSessionService.Clear();

        var pool = TestHeroPool.Load();
        Assert.That(pool.Length, Is.GreaterThan(0), "Repository hero fixture is missing.");
        var local = pool[0];
        local.heroName = "IntegrationLocal";
        var reserve = SquadDataService.CreateSquadInstance("sqd01");
        Assert.That(reserve, Is.Not.Null, "Repository squad sqd01 is missing.");
        local.squadProgress.Clear();
        local.squadProgress.Add(reserve);
        local.loadouts.Clear();
        var loadout = new LoadoutSaveData { name = "Integration", isActive = true };
        loadout.squadInstanceIDs.Add(reserve.id);
        local.loadouts.Add(loadout);
        memory.Player = new PlayerData { playerName = "IntegrationFixture" };
        memory.Player.heroes.Add(local);
        PlayerSessionService.SetPlayer(memory.Player);
        PlayerSessionService.SetSelectedHero(local);

        var battle = BattleDebugCreator.CreateBattleWithLocalHero(local, Team.TeamA, 0, 0,
            EnemySquadMode.Fixed, SquadDataService.GetSquadById("sqd01"));
        var remote = new BattleHeroData
        {
            heroName = "IntegrationRemote", classID = local.classId,
            gender = local.gender, avatar = local.avatar, equipment = local.equipment,
            spawnPointId = "4"
        };
        remote.squadInstances.Add(SquadDataService.CreateSquadInstance("sqd01"));
        battle.defenders.Add(remote);
        BattleTransitionData.Instance.SetBattleData(battle);
        SceneTransitionService.UpdateGamePhase(GamePhase.Combate);
        keyboard = InputSystem.AddDevice<Keyboard>("IntegrationKeyboard");
        mouse = InputSystem.AddDevice<Mouse>("IntegrationMouse");
        keyboard.MakeCurrent();
        mouse.MakeCurrent();
        previousWorld = World.DefaultGameObjectInjectionWorld;
        if (previousWorld != null && previousWorld.IsCreated)
            ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(previousWorld);
        World.DefaultGameObjectInjectionWorld = null;
        worldSwapped = true;
        testWorld = DefaultWorldInitialization.Initialize("Battle integration world");
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath,
            new LoadSceneParameters(LoadSceneMode.Single));
        yield return Until(() =>
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return false;
            em = world.EntityManager;
            using var query = em.CreateEntityQuery(typeof(IsLocalPlayer),
                typeof(HeroVisualInstance), typeof(HeroSquadReference), typeof(LocalTransform));
            if (query.CalculateEntityCount() != 1) return false;
            hero = query.GetSingletonEntity();
            squad = em.GetComponentData<HeroSquadReference>(hero).squad;
            return em.Exists(squad) && em.HasBuffer<SquadUnitElement>(squad)
                && em.GetBuffer<SquadUnitElement>(squad).Length > 0;
        }, ReadyTimeout, "Local hero visual and squad did not become ready.");
    }

    [UnityTest]
    public IEnumerator BattleSceneSpawnsAndAcceptsHeroMovementAndSquadOrders()
    {
        using (var squads = em.CreateEntityQuery(typeof(SquadInstanceComponent)))
            yield return Until(() => squads.CalculateEntityCount() >= 2, ReadyTimeout,
                "Remote squad did not spawn.");

        yield return new WaitForSeconds(4f); // Allow loading overlay and grounding to finish.
        yield return Until(() =>
        {
            var members = em.GetBuffer<SquadUnitElement>(squad);
            for (int i = 0; i < members.Length; i++)
            {
                var unit = members[i].Value;
                if (!em.HasComponent<NavMeshAgent>(unit)) return false;
                var agent = em.GetComponentObject<NavMeshAgent>(unit);
                if (agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
            }
            return true;
        }, 10f, "One or more local units have no active agent on the scene NavMesh.");
        var leader = em.GetBuffer<SquadUnitElement>(squad)[0].Value;
        var leaderStart = em.GetComponentData<LocalTransform>(leader).Position;
        var start = em.GetComponentData<LocalTransform>(hero).Position;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
        yield return Until(() => math.distance(start.xz,
            em.GetComponentData<LocalTransform>(hero).Position.xz) > 3f, 5f,
            "W input did not move the local hero by 3m.");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
        Assert.That(em.HasComponent<HeroMotorStateComponent>(hero), Is.True,
            "CharacterController did not publish confirmed motor state.");

        yield return Press(Key.V);
        yield return Until(() => em.GetComponentData<SquadPlayerOrderIntentComponent>(squad)
            .orderType == SquadOrderType.Attack, 3f, "V did not reach the squad intent.");
        yield return Press(Key.C);
        yield return Until(() => em.GetComponentData<SquadPlayerOrderIntentComponent>(squad)
            .orderType == SquadOrderType.FollowHero, 3f, "C did not restore FollowHero intent.");
        yield return Until(() => em.GetComponentData<SquadResolvedOrderComponent>(squad)
            .order == SquadOrderType.FollowHero, 3f, "FollowHero did not reach order resolution.");
        yield return Until(() => math.distance(leaderStart.xz,
            em.GetComponentData<LocalTransform>(leader).Position.xz) > 0.5f, 10f,
            "Squad leader did not physically move after the hero moved.");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
        yield return Until(() => em.GetComponentData<UnitAnimationMovementComponent>(leader).CurrentSpeed > 0.5f,
            8f, "Physical unit movement did not reach animation ECS data.");
        var leaderAgent = em.GetComponentObject<NavMeshAgent>(leader);
        var leaderAnimator = leaderAgent.GetComponent<Animator>();
        var leaderAdapter = leaderAgent.GetComponent<UnitAnimationAdapter>();
        leaderAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        yield return new WaitForSeconds(0.3f);
        var movement = em.GetComponentData<UnitAnimationMovementComponent>(leader);
        Assert.That(movement.CurrentSpeed, Is.EqualTo(new Vector2(leaderAgent.velocity.x,
            leaderAgent.velocity.z).magnitude).Within(0.5f));
        Assert.That(leaderAnimator.GetInteger(AnimationHashes.CurrentGait), Is.EqualTo((int)leaderAdapter.Gait));
        Assert.That(leaderAnimator.GetBool(AnimationHashes.IsStopped), Is.False);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        var definition = em.GetComponentData<SquadDefinitionComponent>(squad);
        Assert.That(SquadPlayerCommandResolver.TryResolveFormation(definition, default, 1,
            false, out var secondFormation), Is.True, "Fixture needs a second formation.");
        yield return Press(Key.F2);
        yield return Until(() => em.GetComponentData<FormationComponent>(squad).currentFormation
            == secondFormation, 10f, "F2 did not apply the second formation.");
        var units = em.GetBuffer<SquadUnitElement>(squad);
        Assert.That(units.Length, Is.GreaterThan(0));
        for (int i = 0; i < units.Length; i++)
        {
            Assert.That(em.Exists(units[i].Value), Is.True, "Squad contains a missing unit.");
            Assert.That(math.all(math.isfinite(em.GetComponentData<LocalTransform>(units[i].Value).Position)),
                Is.True, "Unit position is non-finite.");
        }
    }

    [UnityTest]
    public IEnumerator BattleSceneAppliesDamageRemovesDeadUnitAndRespawnsHero()
    {
        // Inject at the damage-event boundary; this does not simulate a weapon collision.
        var units = em.GetBuffer<SquadUnitElement>(squad);
        int count = units.Length;
        var victim = units[count - 1].Value;
        QueueLethalDamage(victim);
        yield return Until(() => !em.Exists(victim), 5f, "Lethal damage did not remove the unit.");
        Assert.That(em.GetBuffer<SquadUnitElement>(squad).Length, Is.EqualTo(count - 1),
            "Unit death did not compact squad membership.");

        var life = em.GetComponentData<HeroLifeComponent>(hero);
        life.respawnCooldown = 1f;
        em.SetComponentData(hero, life);
        uint revision = em.GetComponentData<HeroSpawnComponent>(hero).positionRevision;
        QueueLethalDamage(hero);
        yield return Until(() => !em.GetComponentData<HeroLifeComponent>(hero).isAlive,
            5f, "Lethal damage did not mark the hero dead.");
        yield return Until(() => em.GetComponentData<HeroLifeComponent>(hero).isAlive
            && em.GetComponentData<HeroSpawnComponent>(hero).positionRevision > revision,
            10f, "Local hero did not publish a respawn pose.");
        yield return null;
        yield return new WaitForFixedUpdate();
        Assert.That(em.HasComponent<IsDeadComponent>(hero), Is.False);
        var health = em.GetComponentData<HeroHealthComponent>(hero);
        Assert.That(health.currentHealth, Is.EqualTo(health.maxHealth));
        var spawn = em.GetComponentData<HeroSpawnComponent>(hero);
        Assert.That(math.distance(em.GetComponentData<LocalTransform>(hero).Position.xz,
            spawn.spawnPosition.xz), Is.LessThan(0.5f), "Motor did not consume the respawn pose.");
    }

    void QueueLethalDamage(Entity target)
    {
        var damage = em.CreateEntity();
        em.AddComponentData(damage, new PendingDamageEvent
        {
            destroyEntityAfterProcessing = true, target = target,
            hasInlineDamageProfile = true,
            inlineDamageProfile = new DamageProfileComponent { bluntDamage = 100000f },
            sourceTeam = Team.TeamB, multiplier = 1f, hitType = HitType.Body
        });
    }

    [UnityTest]
    public IEnumerator BattleSceneHeroAnimationUsesPhysicalSpeedAndStopsAfterSprintExhaustion()
    {
        var sync = UnityEngine.Object.FindObjectsByType<EntityVisualSync>(FindObjectsSortMode.None)
            .Single(v => v.GetHeroEntity() == hero);
        var animator = sync.GetComponentInChildren<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var adapter = sync.GetComponentInChildren<EcsAnimationInputAdapter>();
        Assert.That(animator.applyRootMotion, Is.False);
        var settings = new LocomotionAnimationSettings();
        var stats = em.GetComponentData<HeroStatsComponent>(hero);

        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
        yield return Until(() => adapter.HorizontalSpeed > stats.baseSpeed * 0.9f, 8f,
            "Hero failed to reach normal physical speed.");
        yield return new WaitForSeconds(0.5f);
        Assert.That(animator.GetInteger(AnimationHashes.CurrentGait), Is.EqualTo((int)LocomotionGait.Run));
        Assert.That(animator.GetFloat(AnimationHashes.MoveSpeed),
            Is.EqualTo(settings.runBlendSpeed).Within(0.2f));
        Assert.That(animator.GetCurrentAnimatorClipInfo(0).Any(c => c.weight > 0.1f
            && c.clip.name.Contains("Run")), Is.True, "Normal movement must actually play a run clip.");

        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift));
        yield return Until(() => animator.GetInteger(AnimationHashes.CurrentGait)
            == (int)LocomotionGait.Sprint, 5f, "Accepted sprint did not reach the Animator.");
        yield return Until(() => animator.GetFloat(AnimationHashes.MoveSpeed)
            >= settings.sprintBlendSpeed - 0.2f, 5f, "Sprint blend does not match physical sprint.");
        var stamina = em.GetComponentData<StaminaComponent>(hero);
        stamina.currentStamina = 0f;
        stamina.isExhausted = true;
        em.SetComponentData(hero, stamina);
        yield return Until(() => !adapter.IsSprinting && animator.GetInteger(AnimationHashes.CurrentGait)
            == (int)LocomotionGait.Run, 3f, "Holding Shift while exhausted retained visual sprint.");

        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return Until(() => animator.GetBool(AnimationHashes.IsStopped), 3f, "Hero did not stop.");
        Assert.That(animator.GetFloat(AnimationHashes.MoveSpeed), Is.Zero);
        Assert.That(animator.GetBool(AnimationHashes.MovementInputPressed), Is.False);
        Assert.That(animator.GetBool(AnimationHashes.MovementInputHeld), Is.False);
        yield return Until(() => animator.GetCurrentAnimatorStateInfo(0).shortNameHash
            == Animator.StringToHash("Idle_Standing"), 5f, "Hero did not return to the actual idle state.");
    }

    [UnityTest]
    public IEnumerator BattleSceneBlockedHeroStopsAnimatingWhileMovementKeyRemainsHeld()
    {
        var sync = UnityEngine.Object.FindObjectsByType<EntityVisualSync>(FindObjectsSortMode.None)
            .Single(v => v.GetHeroEntity() == hero);
        var adapter = sync.GetComponentInChildren<EcsAnimationInputAdapter>();
        var animator = sync.GetComponentInChildren<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        yield return Until(() => em.GetComponentData<HeroMotorStateComponent>(hero).isGrounded,
            8f, "Wall fixture must start grounded.");
        yield return new WaitForSeconds(0.5f);
        var cameraController = UnityEngine.Object.FindAnyObjectByType<HeroCameraController>();
        bool cameraWasEnabled = cameraController != null && cameraController.enabled;
        if (cameraController != null) cameraController.enabled = false;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
        yield return Until(() => adapter.HorizontalSpeed > 1f, 8f, "Hero did not start moving.");
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            var intent = em.GetComponentData<HeroMoveIntent>(hero);
            Vector3 direction = new Vector3(intent.Direction.x, 0f, intent.Direction.z).normalized;
            wall.name = "LocomotionRegressionWall";
            wall.transform.SetPositionAndRotation(sync.transform.position + direction * 1.5f
                + Vector3.up * 2f, Quaternion.LookRotation(direction));
            wall.transform.localScale = new Vector3(10f, 8f, 0.5f);
            Physics.SyncTransforms();
            yield return Until(() => em.GetComponentData<HeroMotorStateComponent>(hero).hitSides
                && !adapter._movementInputDetected, 5f,
                "CharacterController did not collide with the regression wall.");
            yield return new WaitForSeconds(0.5f);
            var motor = em.GetComponentData<HeroMotorStateComponent>(hero);
            Assert.That(adapter._movementInputDetected, Is.False, "Blocked motion velocity="
                + motor.velocity + " speed=" + adapter.HorizontalSpeed + " sides=" + motor.hitSides);
            Assert.That(em.GetComponentData<HeroInputComponent>(hero).MoveInput.y, Is.EqualTo(1f));
            Assert.That(animator.GetFloat(AnimationHashes.MoveSpeed), Is.Zero);
            Assert.That(animator.GetBool(AnimationHashes.IsStopped), Is.True);
            Assert.That(animator.GetInteger(AnimationHashes.CurrentGait), Is.EqualTo((int)LocomotionGait.Idle));
        }
        finally
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            if (cameraController != null) cameraController.enabled = cameraWasEnabled;
            UnityEngine.Object.Destroy(wall);
        }
    }

    [UnityTest]
    public IEnumerator BattleSceneRemoteHeroAnimationFollowsAcceptedAgentSpeedAndStops()
    {
        Entity remote;
        using (var query = em.CreateEntityQuery(typeof(HeroAITag), typeof(HeroVisualInstance), typeof(NavMeshAgent)))
        {
            yield return Until(() => query.CalculateEntityCount() == 1, 10f, "Remote hero agent missing.");
            remote = query.GetSingletonEntity();
        }
        var agent = em.GetComponentObject<NavMeshAgent>(remote);
        var animator = agent.GetComponentInChildren<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        Assert.That(agent.GetComponent<RemoteHeroAnimationDriver>().IsBound, Is.True);
        var localInput = agent.GetComponentInChildren<EcsAnimationInputAdapter>(true);
        Assert.That(localInput == null || !localInput.enabled, Is.True);
        Assert.That(animator.applyRootMotion, Is.False);
        // Retain the scene's navigation + animation pipeline, but give this test ownership of its command.
        em.RemoveComponent<HeroAITag>(remote);
        var stats = em.GetComponentData<HeroStatsComponent>(remote);
        NavMeshHit hit = default;
        Vector3 remoteStart = agent.transform.position;
        bool found = false;
        foreach (var direction in new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left })
        {
            if (!NavMesh.SamplePosition(agent.transform.position + direction * 20f, out hit, 3f, agent.areaMask)) continue;
            var path = new NavMeshPath();
            if (agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
            { found = true; break; }
        }
        Assert.That(found, Is.True, "Remote fixture needs a navigable test corridor.");
        Assert.That(agent.Warp(hit.position), Is.True, "Remote fixture could not enter its clear corridor.");
        agent.speed = stats.baseSpeed;
        agent.isStopped = false;
        Assert.That(agent.SetDestination(remoteStart), Is.True);
        float runDeadline = Time.time + 8f;
        while (animator.GetInteger(AnimationHashes.CurrentGait) != (int)LocomotionGait.Run
            && Time.time < runDeadline) yield return null;
        Assert.That(animator.GetInteger(AnimationHashes.CurrentGait), Is.EqualTo((int)LocomotionGait.Run),
            "Remote speed=" + agent.velocity + " base=" + stats.baseSpeed + " agentSpeed=" + agent.speed
            + " stopped=" + agent.isStopped + " path=" + agent.pathStatus + " remain=" + agent.remainingDistance
            + " bound=" + agent.GetComponent<RemoteHeroAnimationDriver>().IsBound
            + " driverEnabled=" + agent.GetComponent<RemoteHeroAnimationDriver>().enabled);
        yield return new WaitForSeconds(0.4f);
        Assert.That(animator.GetCurrentAnimatorClipInfo(0).Any(c => c.weight > 0.1f
            && c.clip.name.Contains("Run")), Is.True, "Remote normal speed must play a run clip.");
        var decision = em.GetComponentData<HeroAIDecision>(remote);
        decision.shouldSprint = true;
        em.SetComponentData(remote, decision);
        yield return null;
        yield return null;
        Assert.That(animator.GetInteger(AnimationHashes.CurrentGait), Is.EqualTo((int)LocomotionGait.Run),
            "Unaccepted AI sprint request changed the gait.");
        agent.speed = stats.baseSpeed * stats.sprintMultiplier;
        yield return Until(() => animator.GetInteger(AnimationHashes.CurrentGait)
            == (int)LocomotionGait.Sprint, 5f, "Accepted remote sprint did not change the gait.");
        agent.isStopped = true;
        agent.ResetPath();
        yield return Until(() => animator.GetBool(AnimationHashes.IsStopped), 5f, "Remote did not stop.");
        Assert.That(animator.GetFloat(AnimationHashes.MoveSpeed), Is.Zero);
        Assert.That(animator.GetBool(AnimationHashes.MovementInputPressed), Is.False);
        Assert.That(animator.GetBool(AnimationHashes.MovementInputHeld), Is.False);
    }

    [UnityTest]
    public IEnumerator BattleSceneAllTroopControllersUseLocalDirectionAndReturnToIdle()
    {
        var prefabs = new[] {
            "Assets/Resources/Squads/Squires/Squires_GO_Squeleton.prefab",
            "Assets/Resources/Squads/Spearmen/Spearmen_GO_Squeleton.prefab",
            "Assets/Resources/Squads/Levy_Archers/Levy_Archer_GO_Squeleton.prefab"
        };
        foreach (var path in prefabs)
        {
            // Use the scene's real rig/controller with isolated presentation data. No
            // LocalTransform means navigation/animation ECS systems cannot move this fixture.
            var entity = em.CreateEntity(typeof(UnitAnimationMovementComponent), typeof(UnitCombatComponent));
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var visual = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var agent = visual.GetComponent<NavMeshAgent>();
                if (agent != null) agent.enabled = false;
                visual.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                var fixtureSync = visual.GetComponent<EntityVisualSync>();
                fixtureSync.SetHeroEntity(entity);
                fixtureSync.enabled = false;
                var animator = visual.GetComponent<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var adapter = visual.GetComponent<UnitAnimationAdapter>();
                adapter.SetUnitEntity(entity);
                em.SetComponentData(entity, new UnitAnimationMovementComponent {
                    CurrentSpeed = 3.5f, MaxSpeed = 3.5f, RequestedSpeed = 3.5f,
                    MovementDirection = new float3(1f, 0f, 0f), IsMoving = true
                });
                yield return new WaitForSeconds(0.8f);
                Assert.That(animator.applyRootMotion, Is.False, path);
                Assert.That(adapter.IsSprinting, Is.False, "Base speed was misclassified as sprint: " + path);
                Assert.That(animator.GetInteger(AnimationHashes.CurrentGait), Is.EqualTo((int)LocomotionGait.Run), path);
                Assert.That(animator.GetFloat(AnimationHashes.MoveSpeed), Is.EqualTo(5f).Within(0.1f), path);
                Assert.That(animator.GetFloat(AnimationHashes.StrafeDirectionX), Is.EqualTo(0f).Within(0.01f), path);
                Assert.That(animator.GetFloat(AnimationHashes.StrafeDirectionZ), Is.EqualTo(1f).Within(0.01f), path);
                Assert.That(animator.GetCurrentAnimatorClipInfo(0).Any(c => c.weight > 0.1f
                    && c.clip.name.Contains("Run")), Is.True, "Run clip not visible: " + path);
                var controller = (UnityEditor.Animations.AnimatorController)animator.runtimeAnimatorController;
                foreach (var layer in controller.layers.Where(l => l.name == "Combat"))
                {
                    Assert.That(layer.avatarMask, Is.Not.Null, path);
                    Assert.That(layer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Root), Is.False, path);
                    Assert.That(layer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg), Is.False, path);
                    Assert.That(layer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg), Is.False, path);
                    if (layer.stateMachine.states.Any(state => state.state.name == "Attack" && state.state.motion != null))
                    {
                        em.SetComponentData(entity, new UnitCombatComponent { isAttacking = true });
                        yield return new WaitForSeconds(0.3f);
                        int combatLayer = animator.GetLayerIndex("Combat");
                        Assert.That(animator.GetCurrentAnimatorStateInfo(combatLayer).shortNameHash,
                            Is.EqualTo(Animator.StringToHash("Attack")), path);
                        animator.SetLayerWeight(combatLayer, 0f);
                        animator.Update(0f);
                        var leftLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                        var rightLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                        Quaternion leftPose = leftLeg.localRotation;
                        Quaternion rightPose = rightLeg.localRotation;
                        animator.SetLayerWeight(combatLayer, 1f);
                        animator.Update(0f);
                        Assert.That(Quaternion.Angle(leftPose, leftLeg.localRotation), Is.LessThan(0.1f),
                            "Attack overrode the moving left leg: " + path);
                        Assert.That(Quaternion.Angle(rightPose, rightLeg.localRotation), Is.LessThan(0.1f),
                            "Attack overrode the moving right leg: " + path);
                        em.SetComponentData(entity, default(UnitCombatComponent));
                    }
                }
                em.SetComponentData(entity, new UnitAnimationMovementComponent { MaxSpeed = 3.5f });
                yield return new WaitForSeconds(1f);
                Assert.That(animator.GetBool(AnimationHashes.MovementInputPressed), Is.False, path);
                Assert.That(animator.GetBool(AnimationHashes.MovementInputHeld), Is.False, path);
                Assert.That(animator.GetBool(AnimationHashes.MovementInputTapped), Is.False, path);
                Assert.That(animator.GetFloat(AnimationHashes.MoveSpeed), Is.Zero, path);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                    Is.EqualTo(Animator.StringToHash("Idle_Standing")), path);
            }
            finally
            {
                UnityEngine.Object.Destroy(visual);
                if (em.Exists(entity)) em.DestroyEntity(entity);
            }
            yield return null;
        }
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
        // Stop ECS before unloading its blobs, but keep EntityManager alive until
        // scene behaviours have disposed their cached queries in OnDestroy.
        if (testWorld != null && testWorld.IsCreated)
            ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(testWorld);
        // Keep the memory provider installed until scene destruction has completed.
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (scene.IsValid() && scene.isLoaded)
        {
            SceneManager.CreateScene("IntegrationCleanup");
            yield return SceneManager.UnloadSceneAsync(scene);
        }
        if (testWorld != null && testWorld.IsCreated) testWorld.Dispose();
        BattleTransitionData.Instance.ClearData();
        PlayerSessionService.Clear();
        if (previousPlayer != null) PlayerSessionService.SetPlayer(previousPlayer);
        if (previousHero != null) PlayerSessionService.SetSelectedHero(previousHero);
        SaveSystem.SetProvider(previousSave);
        LoadSystem.SetProvider(previousLoad);
        Application.runInBackground = previousBackground;
        UnityEngine.Random.state = previousRandom;
        if (worldSwapped)
        {
            World.DefaultGameObjectInjectionWorld = previousWorld;
            if (previousWorld != null && previousWorld.IsCreated)
                ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(previousWorld);
        }
    }

    static ISaveProvider GetProvider(Type type) => (ISaveProvider)type
        .GetField("_provider", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

    IEnumerator Press(Key key)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
    }

    static IEnumerator Until(Func<bool> ready, float seconds, string failure)
    {
        float deadline = Time.realtimeSinceStartup + seconds;
        while (!ready())
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), failure);
            yield return null;
        }
    }
}
#endif

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
    const string PrepPath = "Assets/Scenes/GameScenes/BattlePrepScene.unity";
    const string FullBattlePath = "Assets/Scenes/GameScenes/BattleScene.unity";
    const int FullBattleHeroesPerSide = 10;
    bool fullBattleProfile;
    string loadedScenePath;
    int expectedFullBattleUnits;
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
    ProgressFileStore previousProgressStore;
    string progressDirectory;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        fullBattleProfile = TestContext.CurrentContext.Test.Name == nameof(FullBattlePreparationRenderedPerformanceProfile);
        loadedScenePath = fullBattleProfile ? FullBattlePath : ScenePath;
        worldSwapped = false;
        testWorld = null;
        keyboard = null;
        mouse = null;
        previousSave = GetProvider(typeof(SaveSystem));
        previousLoad = GetProvider(typeof(LoadSystem));
        previousProgressStore = LocalSaveSystem.Store;
        progressDirectory = System.IO.Path.Combine(Application.temporaryCachePath,
            "battle-integration-" + Guid.NewGuid().ToString("N"));
        LocalSaveSystem.Store = new ProgressFileStore(System.IO.Path.Combine(progressDirectory, "progress.json"));
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
        if (fullBattleProfile)
        {
            var definition = SquadDataService.GetSquadById(reserve.baseSquadID);
            reserve.unitsInSquad = definition.unitCount;
            battle = BattleDebugCreator.CreateBattleWithLocalHero(local, Team.TeamA,
                FullBattleHeroesPerSide - 1, FullBattleHeroesPerSide, EnemySquadMode.Fixed, definition);
            battle.PreparationTimer = 600;
            int participantIndex = 0;
            foreach (var participant in battle.attackers.Concat(battle.defenders))
            {
                if (participant.heroId == local.heroId) continue;
                participant.heroId = Guid.NewGuid().ToString();
                participant.heroName = "FullBattleRemote" + participantIndex++;
                var instance = SquadDataService.CreateSquadInstance(definition.id);
                instance.unitsInSquad = definition.unitCount;
                participant.squadInstances.Clear();
                participant.squadInstances.Add(instance);
            }
            expectedFullBattleUnits = FullBattleHeroesPerSide * 2 * definition.unitCount;
        }
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
        if (fullBattleProfile)
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(PrepPath,
                new LoadSceneParameters(LoadSceneMode.Single));
            SceneTransitionService.UpdateGamePhase(GamePhase.BattlePreparation);
            yield return null; // Footer.Start binds the selected loadout and button.
            var footer = UnityEngine.Object.FindAnyObjectByType<FooterController>();
            Assert.That(footer, Is.Not.Null);
            Assert.That(footer.GetSelectedSquadInstanceIDs(), Does.Contain(reserve.id));
            var startButton = (UnityEngine.UI.Button)typeof(FooterController)
                .GetField("startBattleButton", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(footer);
            Assert.That(startButton, Is.Not.Null);
            startButton.onClick.Invoke(); // Exercise the production preparation transition.
            yield return Until(() => SceneManager.GetActiveScene().path == FullBattlePath,
                ReadyTimeout, "Preparation did not load the production battle scene.");
        }
        else
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
        yield return Until(() => animator.GetCurrentAnimatorClipInfo(0).Any(c => c.weight > 0.1f
            && c.clip.name.Contains("Run")), 3f, "Normal movement must actually play a run clip.");
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

    // Equipment changes must refresh only the linked local visual, including empty slots.
    [UnityTest]
    public IEnumerator BattleSceneEquipmentRefreshesTheLinkedVisualAfterACompletedChange()
    {
        var sync = UnityEngine.Object.FindObjectsByType<EntityVisualSync>(FindObjectsSortMode.None)
            .Single(s => s.IsLocalHero);
        Assert.That(sync.GetComponent<HeroVisualEquipmentInitializer>(), Is.Not.Null);
        var database = Resources.Load<Data.Avatar.AvatarPartDatabase>("Data/Avatar/AvatarPartDatabase");
        var gender = PlayerSessionService.SelectedHero.gender == "Male" ? Gender.Male : Gender.Female;
        var definition = ItemService.GetItemsByType(ItemType.Armor).First(d =>
            d.itemCategory == ItemCategory.Torso && d.IsEquipment && !string.IsNullOrEmpty(d.visualPartId)
            && Data.Avatar.AvatarVisualUtils.FindAvatarPartById(database, d.visualPartId) != null);
        var item = ItemInstanceService.CreateItem(definition.id);
        Assert.That(InventoryManager.AddExistingItem(item), Is.True);
        Assert.That(InventoryManager.EquipItem(item), Is.True);
        var part = Data.Avatar.AvatarVisualUtils.FindAvatarPartById(database, definition.visualPartId);
        int verified = 0;
        foreach (var attachment in part.attachments)
        {
            string target = gender == Gender.Male ? attachment.boneTargetMale : attachment.boneTargetFemale;
            string prefab = gender == Gender.Male ? attachment.prefabPathMale : attachment.prefabPathFemale;
            var bone = sync.transform.Find(target) ?? Data.Avatar.AvatarVisualUtils.FindDeepChild(sync.transform, target);
            if (bone == null || string.IsNullOrEmpty(prefab)) continue;
            foreach (Transform child in bone)
                if (child.name == prefab) { Assert.That(child.gameObject.activeSelf, Is.True); verified++; }
        }
        Assert.That(verified, Is.GreaterThan(0), "No equipped visual attachment was checked.");
        Assert.That(InventoryManager.UnequipItem(ItemType.Armor, ItemCategory.Torso), Is.True);
        Assert.That(InventoryManager.GetCurrentHero().equipment.torso, Is.Null);
        Assert.That(sync.GetComponent<HeroVisualEquipmentInitializer>().isActiveAndEnabled, Is.True);
        yield return null;
    }

    [UnityTest, Explicit("Production preparation-to-battle flow and rendered N vs N Editor profile.")]
    public IEnumerator FullBattlePreparationRenderedPerformanceProfile()
    {
        int expectedHeroes = FullBattleHeroesPerSide * 2;
        using var heroes = em.CreateEntityQuery(typeof(HeroSpawnComponent), typeof(HeroVisualInstance));
        using var squads = em.CreateEntityQuery(typeof(SquadInstanceComponent), typeof(SquadUnitElement));
        using var units = em.CreateEntityQuery(typeof(UnitFormationStateComponent), typeof(NavAgentComponent));
        yield return Until(() => heroes.CalculateEntityCount() == expectedHeroes
            && squads.CalculateEntityCount() == expectedHeroes
            && units.CalculateEntityCount() == expectedFullBattleUnits, ReadyTimeout,
            "The production flow did not deploy the complete hero/squad/unit roster.");
        using (var entities = heroes.ToEntityArray(Unity.Collections.Allocator.Temp))
        {
            int attackers = 0, defenders = 0;
            foreach (var entity in entities)
            {
                var team = em.GetComponentData<TeamComponent>(entity).value;
                if (team == Team.TeamA) attackers++;
                if (team == Team.TeamB) defenders++;
                var ownedSquad = em.GetComponentData<HeroSquadReference>(entity).squad;
                Assert.That(em.Exists(ownedSquad) && em.HasBuffer<SquadUnitElement>(ownedSquad), Is.True);
                Assert.That(em.GetBuffer<SquadUnitElement>(ownedSquad).Length, Is.GreaterThan(0));
            }
            Assert.That(attackers, Is.EqualTo(FullBattleHeroesPerSide));
            Assert.That(defenders, Is.EqualTo(FullBattleHeroesPerSide));
        }
        InputSystem.QueueStateEvent(mouse, new MouseState
        {
            position = new Vector2(Screen.width / 2f, Screen.height / 2f)
        });
        yield return Press(Key.X);
        Assert.That(em.GetComponentData<SquadPlayerOrderIntentComponent>(squad).orderType,
            Is.EqualTo(SquadOrderType.HoldPosition), "HoldPosition failed in the production scene.");
        var bodyblock = testWorld.GetExistingSystemManaged<UnitBodyblockSystem>();
        yield return Until(() => bodyblock.DebugContactPairs > 0, 120f,
            "The autonomous battle did not produce opposing-team bodyblock contact.");
        yield return RenderedPerformanceProfile(FullBattlePath,
            "Logs/system-optimization-full-battle-profile.json", 600, expectedHeroes, expectedFullBattleUnits);
    }

    [UnityTest, Explicit("Rendered Editor profile; run separately from functional regressions.")]
    public IEnumerator BattleSceneRenderedPerformanceProfile()
    {
        yield return RenderedPerformanceProfile(ScenePath,
            "Logs/system-optimization-rendered-profile.json", 300, 2, 24);
    }

    IEnumerator RenderedPerformanceProfile(string scenePath, string outputPath, int sampleCount,
        int initialHeroes, int initialUnits)
    {
        UnityEditor.EditorApplication.ExecuteMenuItem("Window/General/Game");
        if (!fullBattleProfile) yield return new WaitForSeconds(4f);
        using (var squads = em.CreateEntityQuery(typeof(SquadInstanceComponent)))
            yield return Until(() => squads.CalculateEntityCount() >= 2, ReadyTimeout,
                "Both squads must be present for the profile.");
        int renderedFrames = 0;
        Action<UnityEngine.Rendering.ScriptableRenderContext, Camera> pipelineRendered = (_, camera) =>
        { if (camera.cameraType == CameraType.Game) renderedFrames++; };
        Camera.CameraCallback builtinRendered = camera =>
        { if (camera.cameraType == CameraType.Game) renderedFrames++; };
        UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += pipelineRendered;
        Camera.onPostRender += builtinRendered;
        try
        {
            if (!fullBattleProfile) for (int i = 0; i < 120; i++) yield return null;
            var elapsed = new double[sampleCount];
            var cpu = new double[sampleCount];
            var bodyblock = new double[sampleCount];
            var detection = new double[sampleCount];
            var allocations = new double[sampleCount];
            var candidatePairs = new double[sampleCount];
            var contactPairs = new double[sampleCount];
            var bodyblockSystem = testWorld.GetExistingSystemManaged<UnitBodyblockSystem>();
            using var mainRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Internal, "Main Thread", 1);
            using var bodyRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Scripts, "Conquest.Bodyblock", 1);
            using var detectionRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Scripts, "Conquest.EnemyDetection", 1);
            using var allocationRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            Assert.That(mainRecorder.Valid, Is.True, "Main Thread recorder unavailable.");
            renderedFrames = 0;
            double previous = Time.realtimeSinceStartupAsDouble;
            for (int i = 0; i < sampleCount; i++)
            {
                // Capture contacts before yielding so the first detected collision is included.
                candidatePairs[i] = bodyblockSystem.DebugCandidatePairs;
                contactPairs[i] = bodyblockSystem.DebugContactPairs;
                yield return null;
                double now = Time.realtimeSinceStartupAsDouble;
                elapsed[i] = (now - previous) * 1000d;
                previous = now;
                cpu[i] = mainRecorder.LastValue / 1000000d;
                bodyblock[i] = bodyRecorder.Valid ? bodyRecorder.LastValue / 1000000d : -1d;
                detection[i] = detectionRecorder.Valid ? detectionRecorder.LastValue / 1000000d : -1d;
                allocations[i] = allocationRecorder.Valid ? allocationRecorder.LastValue : -1d;
            }
            Assert.That(renderedFrames, Is.GreaterThan(0), "No game camera rendered during the sample.");
            if (fullBattleProfile)
                Assert.That(contactPairs.Max(), Is.GreaterThan(0), "The profile missed all bodyblock contacts.");
            using var units = em.CreateEntityQuery(typeof(UnitFormationStateComponent));
            using var heroes = em.CreateEntityQuery(typeof(HeroSpawnComponent));
            string json = FormattableString.Invariant($@"{{
  ""scene"": ""{scenePath}"", ""editor"": ""{Application.unityVersion}"",
  ""width"": {Screen.width}, ""height"": {Screen.height}, ""sampleCount"": {sampleCount},
  ""renderedCameraFrames"": {renderedFrames}, ""units"": {units.CalculateEntityCount()}, ""heroes"": {heroes.CalculateEntityCount()},
  ""initialHeroes"": {initialHeroes}, ""initialUnits"": {initialUnits},
  ""framesWithContact"": {contactPairs.Count(value => value > 0)},
  ""candidatePairs"": {ProfilePercentiles(candidatePairs)}, ""contactPairs"": {ProfilePercentiles(contactPairs)},
  ""vSyncCount"": {QualitySettings.vSyncCount}, ""targetFrameRate"": {Application.targetFrameRate},
  ""wallFrameMs"": {ProfilePercentiles(elapsed)}, ""mainThreadMs"": {ProfilePercentiles(cpu)},
  ""bodyblockMs"": {ProfilePercentiles(bodyblock)}, ""detectionMs"": {ProfilePercentiles(detection)},
  ""gcBytesPerFrame"": {ProfilePercentiles(allocations)},
  ""limitations"": ""Editor + test runner; no before/after baseline, no Player/GPU claim. -1 means unavailable counter.""
}}");
            System.IO.Directory.CreateDirectory("Logs");
            System.IO.File.WriteAllText(outputPath, json);
            TestContext.WriteLine(json);
        }
        finally
        {
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= pipelineRendered;
            Camera.onPostRender -= builtinRendered;
        }
    }

    static string ProfilePercentiles(double[] values)
    {
        Array.Sort(values);
        return FormattableString.Invariant($"{{\"p50\":{values[(int)Math.Ceiling(values.Length * 0.50) - 1]},\"p95\":{values[(int)Math.Ceiling(values.Length * 0.95) - 1]},\"p99\":{values[(int)Math.Ceiling(values.Length * 0.99) - 1]},\"max\":{values[values.Length - 1]}}}");
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
        var scene = SceneManager.GetSceneByPath(loadedScenePath);
        if (!scene.IsValid() || !scene.isLoaded) scene = SceneManager.GetSceneByPath(PrepPath);
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
        LocalSaveSystem.Store = previousProgressStore;
        if (progressDirectory != null)
        {
            string path = System.IO.Path.Combine(progressDirectory, "progress.json");
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
                if (System.IO.File.Exists(path + suffix)) System.IO.File.Delete(path + suffix);
            if (System.IO.Directory.Exists(progressDirectory)) System.IO.Directory.Delete(progressDirectory);
        }
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

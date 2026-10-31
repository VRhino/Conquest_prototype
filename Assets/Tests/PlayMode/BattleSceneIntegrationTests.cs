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

public partial class BattleSceneIntegrationTests
{
    const string ScenePath = "Assets/Scenes/BattleTestScene.unity";
    const float ReadyTimeout = 60f;
    const string PrepPath = "Assets/Scenes/GameScenes/BattlePrepScene.unity";
    const string FullBattlePath = "Assets/Scenes/GameScenes/BattleScene.unity";
    const int FullBattleHeroesPerSide = 10;
    bool fullBattleProfile;
    bool pikemenFlow;
    string renderVariant;
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
        fullBattleProfile = TestContext.CurrentContext.Test.Name.StartsWith(
            nameof(FullBattlePreparationRenderedPerformanceProfile), StringComparison.Ordinal);
        pikemenFlow = TestContext.CurrentContext.Test.Name == nameof(PikemenRecruitmentPreparationAndProductionBattle);
        renderVariant = null;
        ConfigureMovementBaseline();
        loadedScenePath = fullBattleProfile || pikemenFlow || TestContext.CurrentContext.Test.Name.StartsWith("ProductionBattleScene", StringComparison.Ordinal) ? FullBattlePath : ScenePath;
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
        UnityEngine.Random.InitState(BaselineRandomSeed);
        var memory = new MemorySave();
        SaveSystem.SetProvider(memory);
        LoadSystem.SetProvider(memory);
        PlayerSessionService.Clear();

        var pool = TestHeroPool.Load();
        Assert.That(pool.Length, Is.GreaterThan(0), "Repository hero fixture is missing.");
        var local = pool[0];
        local.heroName = "IntegrationLocal";
        string squadId = TestContext.CurrentContext.Test.Name.Contains("Spearmen") ? "spm01" : "sqd01";
        var reserve = SquadDataService.CreateSquadInstance(squadId);
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

        if (pikemenFlow)
        {
            local.squadProgress.Clear();
            loadout.squadInstanceIDs.Clear();
            var data = SquadDataService.GetSquadById("pkm01");
            Assert.That(SquadDataService.GetSquadsForHero(local.availableSquads, UnitType.Infantry), Does.Contain(data));
            var barracksObject = new GameObject("RecruitmentFixture");
            var barracks = barracksObject.AddComponent<BarracksMenuUIController>();
            barracks.Open(local);
            typeof(BarracksMenuUIController).GetMethod("OnSquadSelectedFromPanel", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(barracks, new object[] { data });
            reserve = local.squadProgress.Single();
            Assert.That(reserve.baseSquadID, Is.EqualTo("pkm01"));
            Assert.That(memory.Player.heroes.Single().squadProgress.Single().id, Is.EqualTo(reserve.id));
            UnityEngine.Object.Destroy(barracksObject);
        }

        var battle = BattleDebugCreator.CreateBattleWithLocalHero(local, Team.TeamA, 0, 0,
            EnemySquadMode.Fixed, SquadDataService.GetSquadById(squadId));
        var remote = new BattleHeroData
        {
            heroName = "IntegrationRemote", classID = local.classId,
            gender = local.gender, avatar = local.avatar, equipment = local.equipment,
            spawnPointId = "4"
        };
        remote.squadInstances.Add(SquadDataService.CreateSquadInstance(squadId));
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
        if (fullBattleProfile || pikemenFlow)
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(PrepPath,
                new LoadSceneParameters(LoadSceneMode.Single));
            SceneTransitionService.UpdateGamePhase(GamePhase.BattlePreparation);
            yield return null; // Footer.Start binds the selected loadout and button.
            var footer = UnityEngine.Object.FindAnyObjectByType<FooterController>();
            Assert.That(footer, Is.Not.Null);
            if (pikemenFlow)
            {
                Assert.That(footer.GetSelectedSquadInstanceIDs(), Is.Empty);
                typeof(FooterController).GetMethod("OnPlaceholderClicked", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(footer, null);
                var selector = UnityEngine.Object.FindAnyObjectByType<TroopsSelectorController>();
                Assert.That(selector, Is.Not.Null);
                typeof(TroopsSelectorController).GetMethod("OnSquadInstanceClicked", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(selector, new object[] { reserve.id });
            }
            Assert.That(footer.GetSelectedSquadInstanceIDs(), Does.Contain(reserve.id));
            var startButton = (UnityEngine.UI.Button)typeof(FooterController)
                .GetField("startBattleButton", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(footer);
            Assert.That(startButton, Is.Not.Null);
            startButton.onClick.Invoke(); // Exercise the production preparation transition.
            yield return Until(() => SceneManager.GetActiveScene().path == FullBattlePath,
                ReadyTimeout, "Preparation did not load the production battle scene.");
        }
        else
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(loadedScenePath,
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
    public IEnumerator PikemenRecruitmentPreparationAndProductionBattle()
    {
        Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(FullBattlePath));
        var data = SquadDataService.GetSquadById("pkm01");
        Assert.That(em.GetComponentData<SquadDefinitionComponent>(squad).squadType, Is.EqualTo(SquadType.Pikemen));
        yield return Until(() =>
        {
            var members = em.GetBuffer<SquadUnitElement>(squad);
            for (int i = 0; i < members.Length; i++)
                if (!em.HasComponent<NavMeshAgent>(members[i].Value)) return false;
            return true;
        }, 15f, "Pikemen visuals did not bind.");
        var units = em.GetBuffer<SquadUnitElement>(squad);
        Assert.That(units.Length, Is.EqualTo(data.unitCount));
        for (int i = 0; i < units.Length; i++)
        {
            var unit = units[i].Value;
            Assert.That(em.HasComponent<UnitRangedStatsComponent>(unit), Is.False);
            var weapon = em.GetComponentData<UnitWeaponComponent>(unit);
            Assert.That(weapon.attackRange, Is.EqualTo(data.meleeData.attackRange));
            Assert.That(em.GetComponentData<DamageProfileComponent>(weapon.damageProfile).piercingDamage, Is.GreaterThan(0));
            var visual = em.GetComponentObject<NavMeshAgent>(unit).gameObject;
            Assert.That(visual.GetComponentInChildren<ShieldHitboxBehaviour>(true), Is.Null);
            Assert.That(visual.GetComponentInChildren<WeaponHitboxBehaviour>(true).ownerUnit, Is.EqualTo(unit));
            Assert.That(visual.GetComponent<Animator>().runtimeAnimatorController.name, Is.EqualTo("AC_Pikemen"));
        }
        yield return Press(Key.F2);
        yield return Until(() => em.GetComponentData<FormationComponent>(squad).currentFormation == FormationType.Dispersed,
            10f, "Pikemen F2 formation did not apply.");
        yield return Press(Key.F1);
        yield return Until(() => em.GetComponentData<FormationComponent>(squad).currentFormation == FormationType.Line,
            10f, "Pikemen F1 line did not apply.");
        var pikeLibrary = em.GetComponentData<SquadDefinitionComponent>(squad).formationLibrary;
        Assert.That(pikeLibrary.Value.formations[0].useNormalPostureInFreeCombat, Is.True,
            "Production subscene must rebake the free-combat posture setting.");
        yield return Press(Key.V);
        yield return Until(() => em.GetComponentData<SquadStateComponent>(squad).currentOrder == SquadOrderType.Attack,
            5f, "V did not request free attack.");
        yield return Until(() =>
        {
            var members = em.GetBuffer<SquadUnitElement>(squad);
            for (int i = 0; i < members.Length; i++)
                if (em.GetComponentData<UnitAnimationMovementComponent>(members[i].Value).CurrentStance != UnitStance.Normal)
                    return false;
            return true;
        }, 5f, "V retained a rank-specific posture on a Pikemen unit.");
        var leader = em.GetBuffer<SquadUnitElement>(squad)[0].Value;
        float3 start = em.GetComponentData<LocalTransform>(leader).Position;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
        yield return Press(Key.C);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
        yield return Until(() => math.distance(start.xz, em.GetComponentData<LocalTransform>(leader).Position.xz) > 0.5f,
            12f, "Pikemen did not follow the hero.");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
    }

    [UnityTest]
    public IEnumerator ProductionBattleSceneSpearmenDamageAndBraceRelease()
    {
        yield return VerifyProductionMeleeContacts(false);
        var intent = em.GetComponentData<SquadPlayerOrderIntentComponent>(squad);
        intent.orderType = SquadOrderType.HoldPosition;
        intent.desiredFormation = FormationType.Line;
        intent.holdPosition = em.GetComponentData<LocalTransform>(hero).Position;
        em.SetComponentData(squad, intent);
        yield return Until(() => em.HasComponent<SquadCombatModeComponent>(squad)
            && em.GetComponentData<SquadCombatModeComponent>(squad).mode == SquadCombatMode.Brace,
            10f, "Spearmen line did not activate brace.");
        yield return new WaitForSeconds(1f);
        var brace = em.GetComponentData<SquadCombatModeComponent>(squad);
        foreach (var member in em.GetBuffer<SquadUnitElement>(squad))
        {
            if (!em.Exists(member.Value)) continue;
            var agent = em.GetComponentObject<NavMeshAgent>(member.Value);
            Assert.That(SquadCombatModeComponent.InsideArc(brace.forward, agent.transform.forward,
                brace.halfAngleRadians + 0.001f), Is.True, "Braced unit turned outside the front arc.");
        }
        yield return Press(Key.C);
        yield return Until(() => em.GetComponentData<SquadCombatModeComponent>(squad).mode == SquadCombatMode.Normal
            && em.GetComponentData<FormationComponent>(squad).currentFormation == FormationType.Dispersed,
            5f, "Follow did not release brace and line formation.");
    }

    [UnityTest]
    public IEnumerator ProductionBattleSceneAttackAtEnemySpawnCausesDamage()
        => VerifyProductionMeleeContacts(true);

    [UnityTest]
    public IEnumerator ProductionBattleSceneMeleeContactsCauseDamage()
        => VerifyProductionMeleeContacts(false);

    IEnumerator VerifyProductionMeleeContacts(bool atEnemySpawn)
    {
        yield return new WaitForSeconds(4f);
        using var enemies = em.CreateEntityQuery(typeof(HeroAITag), typeof(NavMeshAgent), typeof(HeroSquadReference));
        yield return Until(() => enemies.CalculateEntityCount() > 0, 10f, "Remote hero did not spawn.");
        Entity enemy = Entity.Null;
        using (var candidates = enemies.ToEntityArray(Unity.Collections.Allocator.Temp))
            foreach (var candidate in candidates)
                if (em.GetComponentData<TeamComponent>(candidate).value == Team.TeamB)
                { enemy = candidate; break; }
        Assert.That(enemy, Is.Not.EqualTo(Entity.Null));
        testWorld.GetExistingSystemManaged<HeroSkillRequestSystem>().Enabled = false;
        // This fixture tests troop combat. Keep hero deaths from replacing the observed squads.
        foreach (var participant in new[] { hero, enemy })
        {
            StatStack.SetBase(em, participant, StatId.MaxHealth, 100000f);
            StatStack.MarkDirty(em, participant);
            var health = em.GetComponentData<HeroHealthComponent>(participant);
            health.currentHealth = health.maxHealth = 100000f;
            em.SetComponentData(participant, health);
        }
        if (atEnemySpawn)
        {
            var destination = em.GetComponentData<LocalTransform>(enemy).Position;
            var spawn = em.GetComponentData<HeroSpawnComponent>(hero);
            spawn.spawnPosition = destination + new float3(0, 0, 4);
            spawn.positionRevision++;
            em.SetComponentData(hero, spawn);
            var localMembers = em.GetBuffer<SquadUnitElement>(squad).ToNativeArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < localMembers.Length; i++)
            {
                var unit = localMembers[i].Value;
                var agent = em.GetComponentObject<NavMeshAgent>(unit);
                Assert.That(agent.Warp(destination + new float3(i % 4, 0, 5 + i / 4)), Is.True);
                var unitPose = em.GetComponentData<LocalTransform>(unit);
                unitPose.Position = agent.transform.position;
                em.SetComponentData(unit, unitPose);
            }
            localMembers.Dispose();
            yield return null;
        }
        var enemySquad = em.GetComponentData<HeroSquadReference>(enemy).squad;
        var origin = em.GetComponentData<LocalTransform>(hero).Position;
        var enemyAgent = em.GetComponentObject<NavMeshAgent>(enemy);
        if (!atEnemySpawn) Assert.That(enemyAgent.Warp(origin + new float3(0, 0, 4)), Is.True);
        var pose = em.GetComponentData<LocalTransform>(enemy);
        pose.Position = enemyAgent.transform.position;
        em.SetComponentData(enemy, pose);
        var members = em.GetBuffer<SquadUnitElement>(enemySquad).ToNativeArray(Unity.Collections.Allocator.Temp);
        foreach (var member in members)
        {
            var agent = em.GetComponentObject<NavMeshAgent>(member.Value);
            if (!atEnemySpawn) Assert.That(agent.Warp(origin + new float3(0, 0, 5)), Is.True);
            var unitPose = em.GetComponentData<LocalTransform>(member.Value);
            unitPose.Position = agent.transform.position;
            em.SetComponentData(member.Value, unitPose);
        }
        members.Dispose();
        yield return Press(Key.V);
        using var healthQuery = em.CreateEntityQuery(typeof(UnitCombatComponent), typeof(HealthComponent));
        bool teamAHurt = false, teamBHurt = false;
        yield return Until(() =>
        {
            using var entities = healthQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            foreach (var unit in entities)
            {
                var value = em.GetComponentData<HealthComponent>(unit);
                if (value.currentHealth >= value.maxHealth) continue;
                var owner = em.GetComponentData<UnitOwnerComponent>(unit).squad;
                if (owner == squad) teamAHurt = true;
                if (owner == enemySquad) teamBHurt = true;
            }
            return teamAHurt && teamBHurt;
        }, 25f, "Both teams must receive real weapon damage after the attack order.", () =>
        {
            var report = new System.Text.StringBuilder();
            foreach (var group in new[] { squad, enemySquad })
            {
                if (!em.Exists(group)) { report.AppendLine($"squad={group} no longer exists"); continue; }
                report.AppendLine($"squad={group} state={em.GetComponentData<SquadStateComponent>(group).currentState} order={em.GetComponentData<SquadStateComponent>(group).currentOrder}");
                foreach (var member in em.GetBuffer<SquadUnitElement>(group))
                {
                    var unit = member.Value;
                    if (!em.Exists(unit)) continue;
                    var combat = em.GetComponentData<UnitCombatComponent>(unit);
                    var hp = em.GetComponentData<HealthComponent>(unit);
                    var agent = em.GetComponentObject<NavMeshAgent>(unit);
                    report.AppendLine($"unit={unit} hp={hp.currentHealth}/{hp.maxHealth} pos={agent.transform.position} target={combat.target} attacking={combat.isAttacking} hit={combat.hitboxFired} nav={agent.isOnNavMesh} dest={agent.destination}");
                }
            }
            System.IO.Directory.CreateDirectory("Logs");
            System.IO.File.WriteAllText("Logs/battle-spawn-combat-diagnostic.txt", report.ToString());
        });
    }

    [UnityTest]
    public IEnumerator ProductionBattleSceneSpawnsAndAcceptsHeroMovementAndSquadOrders()
        => BattleSceneSpawnsAndAcceptsHeroMovementAndSquadOrders();

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
    public IEnumerator BattleSceneHeroAttackBridgesUseMatchingAnimatorParametersAndConsumeTrigger()
    {
        Entity remote;
        using (var query = em.CreateEntityQuery(typeof(HeroAITag), typeof(HeroVisualInstance), typeof(NavMeshAgent)))
        {
            yield return Until(() => query.CalculateEntityCount() == 1, 10f,
                "Remote hero visual must be ready for the animation bridge check.");
            remote = query.GetSingletonEntity();
        }

        var localSync = UnityEngine.Object.FindObjectsByType<EntityVisualSync>(FindObjectsSortMode.None)
            .Single(sync => sync.GetHeroEntity() == hero);
        var remoteAgent = em.GetComponentObject<NavMeshAgent>(remote);
        var remoteSync = remoteAgent.GetComponent<EntityVisualSync>();
        var remoteDriver = remoteAgent.GetComponent<RemoteHeroAnimationDriver>();
        Assert.That(localSync.IsLocalHero, Is.True, "Local visual must use the EntityVisualSync attack bridge.");
        Assert.That(remoteSync, Is.Not.Null);
        Assert.That(remoteSync.IsLocalHero, Is.False, "Remote visual must not use the local attack bridge.");
        Assert.That(remoteDriver, Is.Not.Null);
        Assert.That(remoteDriver.IsBound, Is.True);

        var localAnimator = localSync.GetComponentInChildren<Animator>(true);
        var remoteAnimator = remoteAgent.GetComponentInChildren<Animator>(true);
        AssertCombatParameterTypes(localAnimator, "local hero");
        AssertCombatParameterTypes(remoteAnimator, "remote hero");

        var localAttack = em.GetComponentData<HeroCombatComponent>(hero);
        localAttack.isAttacking = true;
        em.SetComponentData(hero, localAttack);
        var localAnimation = em.GetComponentData<HeroAnimationComponent>(hero);
        localAnimation.triggerAttack = true;
        em.SetComponentData(hero, localAnimation);

        var localUpdate = typeof(EntityVisualSync).GetMethod("Update",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(localUpdate, Is.Not.Null, "EntityVisualSync.Update reflection target is missing.");
        localUpdate.Invoke(localSync, null);
        Assert.That(localAnimator.GetBool(AnimationHashes.IsAttacking), Is.True,
            "Local EntityVisualSync did not publish isAttacking to the Animator.");
        Assert.That(em.GetComponentData<HeroAnimationComponent>(hero).triggerAttack, Is.False,
            "Local EntityVisualSync did not consume the one-shot attack trigger.");

        localAttack.isAttacking = false;
        em.SetComponentData(hero, localAttack);
        localUpdate.Invoke(localSync, null);
        Assert.That(localAnimator.GetBool(AnimationHashes.IsAttacking), Is.False,
            "Local EntityVisualSync did not publish the released attack state.");

        var remoteAttack = em.GetComponentData<HeroCombatComponent>(remote);
        remoteAttack.isAttacking = true;
        em.SetComponentData(remote, remoteAttack);
        var remoteAnimation = em.GetComponentData<HeroAnimationComponent>(remote);
        remoteAnimation.triggerAttack = true;
        em.SetComponentData(remote, remoteAnimation);

        var driveCombat = typeof(RemoteHeroAnimationDriver).GetMethod("DriveCombatAnimation",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(driveCombat, Is.Not.Null, "Remote combat bridge reflection target is missing.");
        driveCombat.Invoke(remoteDriver, null);
        Assert.That(remoteAnimator.GetBool(AnimationHashes.IsAttacking), Is.True,
            "RemoteHeroAnimationDriver did not publish isAttacking to the Animator.");
        Assert.That(em.GetComponentData<HeroAnimationComponent>(remote).triggerAttack, Is.False,
            "RemoteHeroAnimationDriver did not consume the one-shot attack trigger.");

        remoteAttack.isAttacking = false;
        em.SetComponentData(remote, remoteAttack);
        driveCombat.Invoke(remoteDriver, null);
        Assert.That(remoteAnimator.GetBool(AnimationHashes.IsAttacking), Is.False,
            "RemoteHeroAnimationDriver did not publish the released attack state.");
    }

    static void AssertCombatParameterTypes(Animator animator, string visualName)
    {
        Assert.That(animator, Is.Not.Null, visualName + " Animator is missing.");
        var parameters = animator.parameters;
        var attacking = parameters.SingleOrDefault(parameter => parameter.name == "IsAttacking");
        Assert.That(attacking, Is.Not.Null,
            visualName + " Animator must expose the canonical IsAttacking parameter name.");
        Assert.That(attacking.nameHash, Is.EqualTo(AnimationHashes.IsAttacking),
            visualName + " IsAttacking name hash must match AnimationHashes.");
        Assert.That(attacking.type, Is.EqualTo(AnimatorControllerParameterType.Bool),
            visualName + " IsAttacking parameter must be Bool.");
        var trigger = parameters.SingleOrDefault(parameter => parameter.name == "TriggerAttack");
        Assert.That(trigger, Is.Not.Null,
            visualName + " Animator must expose the canonical TriggerAttack parameter name.");
        Assert.That(trigger.nameHash, Is.EqualTo(AnimationHashes.TriggerAttack),
            visualName + " TriggerAttack name hash must match AnimationHashes.");
        Assert.That(trigger.type, Is.EqualTo(AnimatorControllerParameterType.Trigger),
            visualName + " TriggerAttack parameter must be Trigger.");
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

    [UnityTest]
    public IEnumerator BattleSceneMeleeHitboxRigsContinueAnimatingOutsideAllCameraFrusta()
    {
        foreach (var path in new[] {
            "Assets/Resources/Squads/Squires/Squires_GO_Squeleton.prefab",
            "Assets/Resources/Squads/Spearmen/Spearmen_GO_Squeleton.prefab" })
        {
            var entity = em.CreateEntity(typeof(LocalTransform), typeof(UnitAnimationMovementComponent),
                typeof(UnitCombatComponent));
            var position = new Vector3(10000f, 0f, 10000f);
            em.SetComponentData(entity, LocalTransform.FromPosition(position));
            var visual = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path),
                position, Quaternion.identity);
            try
            {
                var agent = visual.GetComponent<NavMeshAgent>();
                if (agent != null) agent.enabled = false;
                var sync = VisualSyncUtility.SetupVisualSync(visual);
                sync.SetHeroEntity(entity);
                visual.GetComponent<UnitAnimationAdapter>().SetUnitEntity(entity);
                yield return null; // Run the production Start/culling policy.
                sync.enabled = false;
                var animator = visual.GetComponentInChildren<Animator>();
                var hitbox = visual.GetComponentInChildren<WeaponHitboxBehaviour>(true);
                Assert.That(hitbox, Is.Not.Null, path);
                var weaponCollider = hitbox.GetComponent<BoxCollider>();
                Assert.That(weaponCollider, Is.Not.Null, path);
                Assert.That(weaponCollider.isTrigger, Is.True, path);
                var weaponBody = hitbox.GetComponent<Rigidbody>();
                Assert.That(weaponBody, Is.Not.Null, "Animated weapon triggers need a physics body: " + path);
                Assert.That(weaponBody.isKinematic && !weaponBody.useGravity, Is.True, path);
                Assert.That(animator.cullingMode, Is.EqualTo(AnimatorCullingMode.AlwaysAnimate), path);
                em.SetComponentData(entity, new UnitCombatComponent { isAttacking = true });
                int combatLayer = animator.GetLayerIndex("Combat");
                if (combatLayer >= 0)
                {
                    yield return Until(() => animator.GetCurrentAnimatorStateInfo(combatLayer).shortNameHash
                        == Animator.StringToHash("Attack"), 3f, "Offscreen attack clip did not start: " + path);
                }
                else
                {
                    // Spearmen currently uses the base locomotion controller. Exercise
                    // its authored Run clip rather than inventing a Combat layer for it.
                    em.RemoveComponent<LocalTransform>(entity);
                    em.SetComponentData(entity, new UnitAnimationMovementComponent {
                        CurrentSpeed = 3.5f, MaxSpeed = 3.5f, RequestedSpeed = 3.5f,
                        MovementDirection = new float3(0f, 0f, 1f), IsMoving = true
                    });
                    yield return Until(() => animator.GetCurrentAnimatorClipInfo(0).Any(clip =>
                        clip.weight > 0.1f && clip.clip.name.Contains("Run")), 3f,
                        "Offscreen locomotion clip did not start: " + path);
                    TestContext.WriteLine("No Combat layer configured; checked the authored locomotion rig: " + path);
                }
                Vector3 previousPosition = hitbox.transform.position;
                Quaternion previousRotation = hitbox.transform.rotation;
                float maxDistance = 0f, maxAngle = 0f;
                for (int i = 0; i < 30; i++)
                {
                    yield return null;
                    maxDistance = Mathf.Max(maxDistance, Vector3.Distance(previousPosition, hitbox.transform.position));
                    maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(previousRotation, hitbox.transform.rotation));
                }
                Assert.That(visual.GetComponentsInChildren<Renderer>().All(renderer => !renderer.isVisible),
                    Is.True, "The offscreen fixture was visible to a camera: " + path);
                Assert.That(maxDistance > 0.01f || maxAngle > 0.5f, Is.True,
                    "The actual weapon collider's rig froze outside the frustum: " + path);
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
        using (var match = em.CreateEntityQuery(typeof(MatchStateComponent)))
        {
            Assert.That(match.CalculateEntityCount(), Is.EqualTo(1), "Expected a single battle state.");
            Assert.That(match.GetSingleton<MatchStateComponent>().currentState, Is.EqualTo(MatchState.InBattle));
        }
        yield return Until(() => bodyblock.DebugContactPairs > 0, 120f,
            "The autonomous battle did not produce opposing-team bodyblock contact.",
            () => WriteBattleContactDiagnostics(bodyblock));
        yield return RenderedPerformanceProfile(FullBattlePath,
            renderVariant == null ? "Logs/system-optimization-full-battle-profile.json"
                : $"Logs/system-optimization-full-battle-{renderVariant}.json",
            600, expectedHeroes, expectedFullBattleUnits);
    }

    [UnityTest, Explicit("Isolated Forward baseline; restores the active render profile.")]
    public IEnumerator FullBattlePreparationRenderedPerformanceProfileAForward() => RunRenderExperiment("AForward");

    [UnityTest, Explicit("Forward+ without Resident Drawer; restores the active render profile.")]
    public IEnumerator FullBattlePreparationRenderedPerformanceProfileBForwardPlus() => RunRenderExperiment("BForwardPlus");

    [UnityTest, Explicit("Forward+ with Resident Drawer; restores the active render profile.")]
    public IEnumerator FullBattlePreparationRenderedPerformanceProfileCForwardPlusGRD() => RunRenderExperiment("CForwardPlusGRD");

    [UnityTest, Explicit("Resident Drawer with GPU occlusion; restores the active render profile.")]
    public IEnumerator FullBattlePreparationRenderedPerformanceProfileDForwardPlusGRDOcclusion() => RunRenderExperiment("DForwardPlusGRDOcclusion");

    [UnityTest, Explicit("SSAO disabled in a clone of the active profile.")]
    public IEnumerator FullBattlePreparationRenderedPerformanceProfileSSAOOff() => RunRenderExperiment("SSAOOff");

    [UnityTest, Explicit("SSAO enabled in a clone of the active profile.")]
    public IEnumerator FullBattlePreparationRenderedPerformanceProfileSSAOOn() => RunRenderExperiment("SSAOOn");

    IEnumerator RunRenderExperiment(string variant)
    {
        // Editor tooling stays out of the runtime/test assembly dependency graph.
        var tools = Type.GetType("Conquest.Editor.Optimization.RenderOptimizationExperiments, Game.Editor");
        Assert.That(tools, Is.Not.Null, "Render experiment Editor tools are unavailable.");
        var begin = tools.GetMethod("Begin", BindingFlags.Public | BindingFlags.Static);
        var restore = tools.GetMethod("Restore", BindingFlags.Public | BindingFlags.Static);
        Assert.That(begin, Is.Not.Null);
        Assert.That(restore, Is.Not.Null);
        renderVariant = variant;
        try
        {
            begin.Invoke(null, new object[] { variant });
            yield return FullBattlePreparationRenderedPerformanceProfile();
        }
        finally
        {
            restore.Invoke(null, null);
            renderVariant = null;
        }
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
        var renderTools = Type.GetType("Conquest.Editor.Optimization.RenderOptimizationExperiments, Game.Editor");
        var census = renderTools?.GetMethod("Census", BindingFlags.Public | BindingFlags.Static);
        if (census != null)
        {
            System.IO.Directory.CreateDirectory("Logs");
            System.IO.File.WriteAllText(outputPath + ".census.json", (string)census.Invoke(null, null));
        }
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
            var gpu = new double[sampleCount];
            var frameTimings = new FrameTiming[1];
            bool frameTimingEnabled = FrameTimingManager.IsFeatureEnabled();
            var bodyblock = new double[sampleCount];
            var detection = new double[sampleCount];
            var targeting = new double[sampleCount];
            var detectionSnapshot = new double[sampleCount];
            var detectionSearch = new double[sampleCount];
            var detectionPublish = new double[sampleCount];
            var detectionChecks = new double[sampleCount];
            var publishedTargets = new double[sampleCount];
            var targetingChecks = new double[sampleCount];
            var retargets = new double[sampleCount];
            var projection = new double[sampleCount];
            var projectionRequests = new double[sampleCount];
            var projectionFailures = new double[sampleCount];
            var allocations = new double[sampleCount];
            var candidatePairs = new double[sampleCount];
            var contactPairs = new double[sampleCount];
            var bodyblockSystem = testWorld.GetExistingSystemManaged<UnitBodyblockSystem>();
            var detectionSystem = testWorld.GetExistingSystemManaged<EnemyDetectionSystem>();
            var targetingSystem = testWorld.GetExistingSystemManaged<UnitTargetingSystem>();
            using var mainRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Internal, "Main Thread", 1);
            using var bodyRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Scripts, "Conquest.Bodyblock", 1);
            using var detectionRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Scripts, "Conquest.EnemyDetection", 1);
            using var snapshotRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Scripts, "Conquest.EnemyDetection.Snapshot", 1);
            using var searchRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Scripts, "Conquest.EnemyDetection.SearchAndWait", 1);
            using var publishRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Scripts, "Conquest.EnemyDetection.Publish", 1);
            using var targetingRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Scripts, "Conquest.UnitTargeting", 1);
            using var projectionRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Scripts, "Conquest.NavMeshProjection", 1);
            using var allocationRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            Assert.That(mainRecorder.Valid, Is.True, "Main Thread recorder unavailable.");
            Assert.That(targetingRecorder.Valid, Is.True, "Unit targeting recorder unavailable.");
            renderedFrames = 0;
            double previous = Time.realtimeSinceStartupAsDouble;
            for (int i = 0; i < sampleCount; i++)
            {
                // Capture contacts before yielding so the first detected collision is included.
                candidatePairs[i] = bodyblockSystem.DebugCandidatePairs;
                contactPairs[i] = bodyblockSystem.DebugContactPairs;
                detectionChecks[i] = detectionSystem.DebugExactDistanceChecks;
                publishedTargets[i] = detectionSystem.DebugPublishedTargets;
                targetingChecks[i] = targetingSystem.DebugCandidateChecks;
                retargets[i] = targetingSystem.DebugRetargetCount;
                long projectionRequestsBefore = UnitNavMeshSystem.DebugTotalProjectionRequests;
                long projectionFailuresBefore = UnitNavMeshSystem.DebugTotalProjectionFailures;
                if (frameTimingEnabled) FrameTimingManager.CaptureFrameTimings();
                yield return null;
                gpu[i] = frameTimingEnabled && FrameTimingManager.GetLatestTimings(1, frameTimings) > 0
                    && frameTimings[0].gpuFrameTime > 0d ? frameTimings[0].gpuFrameTime : -1d;
                projectionRequests[i] = UnitNavMeshSystem.DebugTotalProjectionRequests - projectionRequestsBefore;
                projectionFailures[i] = UnitNavMeshSystem.DebugTotalProjectionFailures - projectionFailuresBefore;
                double now = Time.realtimeSinceStartupAsDouble;
                elapsed[i] = (now - previous) * 1000d;
                previous = now;
                cpu[i] = mainRecorder.LastValue / 1000000d;
                bodyblock[i] = bodyRecorder.Valid ? bodyRecorder.LastValue / 1000000d : -1d;
                detection[i] = detectionRecorder.Valid ? detectionRecorder.LastValue / 1000000d : -1d;
                detectionSnapshot[i] = snapshotRecorder.Valid ? snapshotRecorder.LastValue / 1000000d : -1d;
                detectionSearch[i] = searchRecorder.Valid ? searchRecorder.LastValue / 1000000d : -1d;
                detectionPublish[i] = publishRecorder.Valid ? publishRecorder.LastValue / 1000000d : -1d;
                targeting[i] = targetingRecorder.LastValue / 1000000d;
                projection[i] = projectionRecorder.Valid ? projectionRecorder.LastValue / 1000000d : -1d;
                allocations[i] = allocationRecorder.Valid ? allocationRecorder.LastValue : -1d;
            }
            Assert.That(renderedFrames, Is.GreaterThan(0), "No game camera rendered during the sample.");
            if (fullBattleProfile)
                Assert.That(contactPairs.Max(), Is.GreaterThan(0), "The profile missed all bodyblock contacts.");
            using var units = em.CreateEntityQuery(typeof(UnitFormationStateComponent));
            using var heroes = em.CreateEntityQuery(typeof(HeroSpawnComponent));
            var gpuSamples = gpu.Where(value => value >= 0d).ToArray();
            string gpuPercentiles = ProfilePercentiles(gpuSamples.Length > 0 ? gpuSamples : new[] { -1d });
            string json = FormattableString.Invariant($@"{{
  ""scene"": ""{scenePath}"", ""editor"": ""{Application.unityVersion}"",
  ""renderVariant"": ""{renderVariant ?? "Production"}"",
  ""burstEnabled"": {Unity.Burst.BurstCompiler.Options.EnableBurstCompilation.ToString().ToLowerInvariant()},
  ""graphicsApi"": ""{SystemInfo.graphicsDeviceType}"", ""gpu"": ""{SystemInfo.graphicsDeviceName}"",
  ""quality"": ""{QualitySettings.names[QualitySettings.GetQualityLevel()]}"",
  ""frameTimingEnabled"": {frameTimingEnabled.ToString().ToLowerInvariant()}, ""gpuAvailableSamples"": {gpuSamples.Length}, ""gpuFrameMs"": {gpuPercentiles},
  ""width"": {Screen.width}, ""height"": {Screen.height}, ""sampleCount"": {sampleCount},
  ""renderedCameraFrames"": {renderedFrames}, ""units"": {units.CalculateEntityCount()}, ""heroes"": {heroes.CalculateEntityCount()},
  ""initialHeroes"": {initialHeroes}, ""initialUnits"": {initialUnits},
  ""framesWithContact"": {contactPairs.Count(value => value > 0)},
  ""candidatePairs"": {ProfilePercentiles(candidatePairs)}, ""contactPairs"": {ProfilePercentiles(contactPairs)},
  ""vSyncCount"": {QualitySettings.vSyncCount}, ""targetFrameRate"": {Application.targetFrameRate},
  ""wallFrameMs"": {ProfilePercentiles(elapsed)}, ""mainThreadMs"": {ProfilePercentiles(cpu)},
  ""bodyblockMs"": {ProfilePercentiles(bodyblock)}, ""detectionMs"": {ProfilePercentiles(detection)},
  ""targetingMs"": {ProfilePercentiles(targeting)},
  ""detectionSnapshotMs"": {ProfilePercentiles(detectionSnapshot)}, ""detectionSearchAndWaitMs"": {ProfilePercentiles(detectionSearch)}, ""detectionPublishMs"": {ProfilePercentiles(detectionPublish)},
  ""detectionExactChecks"": {ProfilePercentiles(detectionChecks)}, ""publishedTargets"": {ProfilePercentiles(publishedTargets)},
  ""targetingCandidateChecks"": {ProfilePercentiles(targetingChecks)}, ""retargets"": {ProfilePercentiles(retargets)},
  ""navMeshProjectionMs"": {ProfilePercentiles(projection)},
  ""projectionRequestsPerFrame"": {ProfilePercentiles(projectionRequests)}, ""projectionFailuresPerFrame"": {ProfilePercentiles(projectionFailures)},
  ""gcBytesPerFrame"": {ProfilePercentiles(allocations)},
  ""limitations"": ""Editor + test runner; compare population and Burst settings before attributing changes. No Player claim. GPU timings may lag CPU by a few frames; -1 means unavailable counter.""
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
        // The test runner can abort a nested IEnumerator without disposing its parent.
        // Restore here as well as in the wrapper's finally, including assertion failures.
        if (renderVariant != null)
        {
            Type.GetType("Conquest.Editor.Optimization.RenderOptimizationExperiments, Game.Editor")
                ?.GetMethod("Restore", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
            renderVariant = null;
        }
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
        Time.captureFramerate = previousCaptureFramerate;
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

    void WriteBattleContactDiagnostics(UnitBodyblockSystem bodyblock)
    {
        var text = new System.Text.StringBuilder();
        text.AppendLine($"time={Time.time} candidates={bodyblock.DebugCandidatePairs} contacts={bodyblock.DebugContactPairs}");
        using (var query = em.CreateEntityQuery(typeof(MatchStateComponent)))
        using (var states = query.ToComponentDataArray<MatchStateComponent>(Unity.Collections.Allocator.Temp))
            for (int i = 0; i < states.Length; i++) text.AppendLine($"match[{i}]={states[i].currentState}");
        using (var query = em.CreateEntityQuery(typeof(HeroAITag), typeof(HeroAIDecision), typeof(TeamComponent)))
        using (var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp))
            foreach (var entity in entities)
            {
                var decision = em.GetComponentData<HeroAIDecision>(entity);
                text.Append($"hero={entity} team={em.GetComponentData<TeamComponent>(entity).value} action={decision.action} target={decision.targetPosition}");
                if (em.HasComponent<NavMeshAgent>(entity))
                {
                    var agent = em.GetComponentObject<NavMeshAgent>(entity);
                    if (agent != null && agent.enabled && agent.isOnNavMesh)
                        text.Append($" pos={agent.transform.position} path={agent.pathStatus} remaining={agent.remainingDistance} velocity={agent.velocity}");
                    else text.Append(" agent inactive/off mesh");
                }
                text.AppendLine();
            }
        TestContext.WriteLine(text.ToString());
    }

    static IEnumerator Until(Func<bool> ready, float seconds, string failure, Action onTimeout = null)
    {
        float deadline = Time.realtimeSinceStartup + seconds;
        while (!ready())
        {
            if (Time.realtimeSinceStartup >= deadline) onTimeout?.Invoke();
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), failure);
            yield return null;
        }
    }
}
#endif

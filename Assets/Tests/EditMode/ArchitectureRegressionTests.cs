using System;
using System.IO;
using NUnit.Framework;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class ArchitectureRegressionTests
{
    World world;
    EntityManager em;

    [SetUp]
    public void SetUp()
    {
        world = new World("Architecture regression");
        em = world.EntityManager;
    }

    [TearDown]
    public void TearDown() => world.Dispose();

    [Test]
    public void MissingCurvesAreNeutralAndOutOfRangeLevelsAreClamped()
    {
        var blob = SquadProgressionCurves.Create(null);
        try
        {
            Assert.That(blob.Value.health.Length, Is.EqualTo(30));
            Assert.That(SquadProgressionCurves.Read(ref blob.Value.health, -1), Is.EqualTo(1f));
            Assert.That(SquadProgressionCurves.Read(ref blob.Value.speed, 100), Is.EqualTo(1f));
        }
        finally { blob.Dispose(); }
    }

    [Test]
    public void ConfiguredCurvesUseLevelAsX()
    {
        var data = ScriptableObject.CreateInstance<SquadProgressionData>();
        var blob = SquadProgressionCurves.Create(data);
        try
        {
            Assert.That(blob.Value.health[0], Is.EqualTo(1f).Within(0.0001f));
            Assert.That(blob.Value.health[29], Is.EqualTo(2f).Within(0.0001f));
            Assert.That(blob.Value.speed[29], Is.EqualTo(1.5f).Within(0.0001f));
        }
        finally { blob.Dispose(); UnityEngine.Object.DestroyImmediate(data); }
    }

    [Test]
    public void ScalingUpdatesCombatStatsWithoutHealingOrRefillingAmmo()
    {
        var unit = em.CreateEntity(typeof(HealthComponent), typeof(DefenseComponent),
            typeof(UnitWeaponComponent), typeof(RangedAttackStateComponent));
        var profile = em.CreateEntity(typeof(DamageProfileComponent));
        em.SetComponentData(unit, new HealthComponent { maxHealth = 100, currentHealth = 25 });
        em.SetComponentData(unit, new UnitWeaponComponent { damageProfile = profile });
        em.SetComponentData(unit, new RangedAttackStateComponent
        { currentAmmo = 3, isReloading = true, reloadTimer = 2, shotTimer = 1 });
        UnitStatsUtility.ApplyStatsToUnit(unit, new SquadDataComponent
        { baseHealth = 100, baseSpeed = 5, bluntDamage = 10, bluntDefense = 20, isRangedUnit = true, ammoCapacity = 50 },
            1, 2, 3, 4, 1, em);
        Assert.That(em.GetComponentData<HealthComponent>(unit).maxHealth, Is.EqualTo(200));
        Assert.That(em.GetComponentData<HealthComponent>(unit).currentHealth, Is.EqualTo(50));
        Assert.That(em.GetComponentData<DefenseComponent>(unit).bluntDefense, Is.EqualTo(80));
        Assert.That(em.GetComponentData<DamageProfileComponent>(profile).bluntDamage, Is.EqualTo(30));
        var ammo = em.GetComponentData<RangedAttackStateComponent>(unit);
        Assert.That(ammo.currentAmmo, Is.EqualTo(3));
        Assert.That(ammo.isReloading, Is.True);
        Assert.That(ammo.reloadTimer, Is.EqualTo(2));
        Assert.That(ammo.shotTimer, Is.EqualTo(1));
    }

    [Test]
    public void XPRequiresAnEventAndConsumesItOnce()
    {
        var squad = em.CreateEntity(typeof(SquadProgressComponent));
        em.SetComponentData(squad, new SquadProgressComponent { level = 1, xpToNextLevel = 100 });
        var phase = em.CreateEntity(typeof(GameStateComponent));
        em.SetComponentData(phase, new GameStateComponent { currentPhase = GamePhase.PostPartida });
        var system = world.GetOrCreateSystemManaged<SquadProgressionSystem>();
        for (int i = 0; i < 120; i++) system.Update();
        Assert.That(em.GetComponentData<SquadProgressComponent>(squad).currentXP, Is.Zero);
        var evt = em.CreateEntity(typeof(SquadXPEvent));
        em.SetComponentData(evt, new SquadXPEvent { squad = squad, amount = 150 });
        system.Update();
        Assert.That(em.Exists(evt), Is.False);
        var result = em.GetComponentData<SquadProgressComponent>(squad);
        Assert.That(result.level, Is.EqualTo(2));
        Assert.That(result.currentXP, Is.EqualTo(50));
        system.Update();
        Assert.That(em.GetComponentData<SquadProgressComponent>(squad).currentXP, Is.EqualTo(50));
    }

    [Test]
    public void ALevelEventDoesNotRescaleAnotherSquad()
    {
        var squadA = CreateSquad();
        var squadB = CreateSquad();
        var system = world.GetOrCreateSystemManaged<UnitStatScalingSystem>();
        system.Update();
        var unitB = em.GetBuffer<SquadUnitElement>(squadB)[0].Value;
        var sentinel = em.GetComponentData<UnitStatsComponent>(unitB);
        sentinel.health = 777;
        em.SetComponentData(unitB, sentinel);
        var evt = em.CreateEntity(typeof(SquadLevelUpEvent));
        em.SetComponentData(evt, new SquadLevelUpEvent { squad = squadA });
        system.Update();
        Assert.That(em.GetComponentData<UnitStatsComponent>(unitB).health, Is.EqualTo(777));
    }

    Entity CreateSquad()
    {
        var unit = em.CreateEntity();
        var squad = em.CreateEntity(typeof(SquadProgressComponent), typeof(SquadDataComponent), typeof(SquadDataReference));
        em.SetComponentData(squad, new SquadProgressComponent { level = 1, xpToNextLevel = 100 });
        em.SetComponentData(squad, new SquadDataReference { dataEntity = squad });
        em.SetComponentData(squad, new SquadDataComponent { baseHealth = 100, baseSpeed = 5 });
        em.AddBuffer<SquadUnitElement>(squad).Add(new SquadUnitElement { Value = unit });
        return squad;
    }

    [TestCase(true)]
    [TestCase(false)]
    public void DamageEventsOnlyDestroyOwnedEntities(bool ownsEntity)
    {
        var target = em.CreateEntity(typeof(HealthComponent));
        em.SetComponentData(target, new HealthComponent { maxHealth = 100, currentHealth = 100 });
        var profile = em.CreateEntity(typeof(DamageProfileComponent));
        em.SetComponentData(profile, new DamageProfileComponent { bluntDamage = 10 });
        var evt = em.CreateEntity(typeof(PendingDamageEvent));
        em.SetComponentData(evt, new PendingDamageEvent
        { target = target, damageProfile = profile, multiplier = 1, destroyEntityAfterProcessing = ownsEntity });
        world.GetOrCreateSystemManaged<DamageCalculationSystem>().Update();
        Assert.That(em.GetComponentData<HealthComponent>(target).currentHealth, Is.EqualTo(90));
        Assert.That(em.Exists(evt), Is.EqualTo(!ownsEntity));
        if (!ownsEntity) Assert.That(em.HasComponent<PendingDamageEvent>(evt), Is.False);
    }

    [Test]
    public void InvalidDamageProfileAlsoDisposesOwnedEvent()
    {
        var evt = em.CreateEntity(typeof(PendingDamageEvent));
        em.SetComponentData(evt, new PendingDamageEvent { destroyEntityAfterProcessing = true });
        world.GetOrCreateSystemManaged<DamageCalculationSystem>().Update();
        Assert.That(em.Exists(evt), Is.False);
    }

    [Test]
    public void PersistentSquadLookupDoesNotReuseBattleLocalId()
    {
        var data = new LocalSaveSystem.PlayerProgressData();
        data.squads.Add(new LocalSaveSystem.SquadInstanceData { id = 0, persistentId = "first" });
        Assert.That(LocalSaveSystem.FindSquad(data, 0, "second"), Is.Null);
        Assert.That(LocalSaveSystem.FindSquad(data, 5, "first"), Is.SameAs(data.squads[0]));
    }

    [Test]
    public void StorageUpdatesPreserveOtherFieldsAndCreateBackup()
    {
        string directory = Path.Combine(Path.GetTempPath(), "conquest-regression-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "progress.json");
        try
        {
            var heroStore = new ProgressFileStore(path);
            var squadStore = new ProgressFileStore(path);
            heroStore.Update(data => data.currentXP = 45);
            squadStore.Update(data => data.squads.Add(new LocalSaveSystem.SquadInstanceData { id = 8, armorPercent = 70 }));
            heroStore.Update(data => data.level = 2);
            var result = squadStore.Load();
            Assert.That(result.currentXP, Is.EqualTo(45));
            Assert.That(result.squads[0].armorPercent, Is.EqualTo(70));
            Assert.That(File.Exists(path + ".bak"), Is.True);
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }
        finally
        {
            // Only exact files created by this test, never the player's save.
            foreach (var file in new[] { path, path + ".bak", path + ".tmp" })
                if (File.Exists(file)) File.Delete(file);
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }

    [Test]
    public void FormationCompatibilityMethodsHaveTheSameOutput()
    {
        var formation = ScriptableObject.CreateInstance<GridFormationScriptableObject>();
        try
        {
            Assert.That(formation.GetWorldOffsets(), Is.Empty);
            formation.gridPositions = new[] { new Vector2Int(2, 3), new Vector2Int(4, 5) };
            CollectionAssert.AreEqual(formation.GetWorldOffsets(), formation.GetAbsoluteWorldOffsets());
            CollectionAssert.AreEqual(formation.GetWorldOffsets(), formation.GetCenteredWorldOffsets());
        }
        finally { UnityEngine.Object.DestroyImmediate(formation); }
    }

    [Test]
    public void SpawnUsesSnapshotIdentityLevelAndSurvivors()
    {
        using var builder = new BlobBuilder(Allocator.Temp);
        ref var root = ref builder.ConstructRoot<FormationLibraryBlob>();
        var forms = builder.Allocate(ref root.formations, 1);
        forms[0].formationType = FormationType.Line;
        var positions = builder.Allocate(ref forms[0].gridPositions, 4);
        for (int i = 0; i < 4; i++) positions[i] = new int2(i, 0);
        var blob = builder.CreateBlobAssetReference<FormationLibraryBlob>(Allocator.Persistent);
        try
        {
            em.CreateEntity(typeof(SquadSpawnConfigComponent));
            em.CreateEntity(typeof(MatchStateComponent));
            var definition = em.CreateEntity(typeof(SquadDataComponent), typeof(SquadDefinitionComponent));
            em.SetComponentData(definition, new SquadDataComponent { baseHealth = 100, baseSpeed = 5, bluntPenetration = 3 });
            em.SetComponentData(definition, new SquadDefinitionComponent { formationLibrary = blob, unitCount = 4 });
            var hero = em.CreateEntity(typeof(HeroSpawnComponent), typeof(HeroSquadSelectionComponent),
                typeof(LocalTransform), typeof(TeamComponent));
            em.SetComponentData(hero, new HeroSpawnComponent { hasSpawned = true });
            em.SetComponentData(hero, LocalTransform.Identity);
            em.SetComponentData(hero, new HeroSquadSelectionComponent { squadDataEntity = definition, instanceId = 0 });
            em.SetComponentData(hero, new TeamComponent { value = Team.TeamA });
            em.AddBuffer<SquadIdMapElement>(hero).Add(new SquadIdMapElement
            {
                squadId = 0, persistentId = new FixedString64Bytes("persistent-squad"), hasSnapshot = true,
                level = 7, currentXP = 42, totalUnits = 4, aliveUnits = 2
            });
            world.GetOrCreateSystemManaged<SquadSpawningSystem>().Update();
            var squad = em.GetComponentData<HeroSquadReference>(hero).squad;
            Assert.That(em.GetComponentData<SquadProgressComponent>(squad).level, Is.EqualTo(7));
            Assert.That(em.GetComponentData<SquadProgressComponent>(squad).currentXP, Is.EqualTo(42));
            Assert.That(em.GetComponentData<SquadInstanceComponent>(squad).persistentId.ToString(), Is.EqualTo("persistent-squad"));
            var units = em.GetBuffer<SquadUnitElement>(squad);
            Assert.That(units.Length, Is.EqualTo(2));
            var unit = units[0].Value;
            var profileEntity = em.GetComponentData<UnitWeaponComponent>(unit).damageProfile;
            var profile = em.GetComponentData<DamageProfileComponent>(profileEntity);
            var bonus = em.GetComponentData<PenetrationComponent>(unit);
            Assert.That(profile.bluntPenetration + bonus.bluntPenetration, Is.EqualTo(3));
        }
        finally { blob.Dispose(); }
    }

    [Test]
    public void RemoteSnapshotIsAppliedByECS()
    {
        var hero = em.CreateEntity();
        BattleBootstrapRequests.SubmitRemote(world, hero, new System.Collections.Generic.List<SquadInstanceData>
        {
            new SquadInstanceData { id = "remote", baseSquadID = "type", level = 9, experience = 12,
                unitsInSquad = 20, unitsKilled = 3, unitsInjured = 2 }
        });
        world.GetOrCreateSystemManaged<BattleBootstrapSystem>().Update();
        var map = em.GetBuffer<SquadIdMapElement>(hero);
        Assert.That(map.Length, Is.EqualTo(1));
        Assert.That(map[0].persistentId.ToString(), Is.EqualTo("remote"));
        Assert.That(map[0].level, Is.EqualTo(9));
        Assert.That(map[0].aliveUnits, Is.EqualTo(15));
    }

    [Test]
    public void InitialScalingRestoresAbilityUnlocksForSavedLevel()
    {
        var squad = CreateSquad();
        var ability = em.CreateEntity();
        em.SetComponentData(squad, new SquadProgressComponent { level = 10 });
        em.AddBuffer<AbilityByLevelElement>(squad).Add(new AbilityByLevelElement { Value = ability });
        world.GetOrCreateSystemManaged<UnitStatScalingSystem>().Update();
        Assert.That(em.GetBuffer<UnlockedAbilityElement>(squad)[0].Value, Is.EqualTo(ability));
    }

    [Test]
    public void EnemyDetectionKeepsOneCandidateBufferPerSquadAtNineHundredUnits()
    {
        Entity CreateDetectionSquad(Team team, float xOffset)
        {
            var squad = em.CreateEntity(typeof(SquadDefinitionComponent), typeof(SquadDataComponent),
                typeof(TeamComponent), typeof(SquadAIComponent), typeof(SquadStateComponent));
            em.SetComponentData(squad, new SquadDefinitionComponent { detectionRange = 1000f });
            em.SetComponentData(squad, new TeamComponent { value = team });
            em.SetComponentData(squad, new SquadStateComponent { currentState = SquadFSMState.InCombat });
            em.AddBuffer<SquadUnitElement>(squad);
            em.AddBuffer<DetectedEnemy>(squad);
            em.AddBuffer<SquadTargetEntity>(squad);
            for (int i = 0; i < 450; i++)
            {
                var unit = em.CreateEntity(typeof(LocalTransform), typeof(UnitCombatComponent));
                em.SetComponentData(unit, LocalTransform.FromPosition(new float3(
                    xOffset + (i % 30), 0f, i / 30)));
                em.GetBuffer<SquadUnitElement>(squad).Add(new SquadUnitElement { Value = unit });
            }
            return squad;
        }

        var squadA = CreateDetectionSquad(Team.TeamA, 0f);
        var squadB = CreateDetectionSquad(Team.TeamB, 40f);
        var system = world.GetOrCreateSystemManaged<EnemyDetectionSystem>();
        system.Update();

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        system.Update();
        watch.Stop();
        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.That(em.GetBuffer<SquadTargetEntity>(squadA).Length, Is.EqualTo(450));
        Assert.That(em.GetBuffer<SquadTargetEntity>(squadB).Length, Is.EqualTo(450));
        Assert.That(em.GetBuffer<DetectedEnemy>(squadA).Length, Is.EqualTo(1));
        Assert.That(em.GetBuffer<DetectedEnemy>(squadB).Length, Is.EqualTo(1));
        TestContext.WriteLine($"Enemy detection 900 units warm-frame: {watch.Elapsed.TotalMilliseconds:F2} ms, " +
            $"managed allocation: {allocatedBytes} bytes, candidate entries: 900");

        var targeting = world.GetOrCreateSystemManaged<UnitTargetingSystem>();
        targeting.Update();
        allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        watch.Restart();
        targeting.Update();
        watch.Stop();
        allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        int assignedTargets = 0;
        foreach (var unit in em.GetBuffer<SquadUnitElement>(squadA))
            if (em.GetComponentData<UnitCombatComponent>(unit.Value).target != Entity.Null)
                assignedTargets++;
        foreach (var unit in em.GetBuffer<SquadUnitElement>(squadB))
            if (em.GetComponentData<UnitCombatComponent>(unit.Value).target != Entity.Null)
                assignedTargets++;
        Assert.That(assignedTargets, Is.EqualTo(900));
        TestContext.WriteLine($"Unit targeting 900 units warm-frame: {watch.Elapsed.TotalMilliseconds:F2} ms, " +
            $"managed allocation: {allocatedBytes} bytes");
    }

    [Test]
    public void UnitTargetingConsumesSharedSquadCandidates()
    {
        var nearEnemy = em.CreateEntity(typeof(LocalTransform));
        var farEnemy = em.CreateEntity(typeof(LocalTransform));
        em.SetComponentData(nearEnemy, LocalTransform.FromPosition(new float3(2, 0, 0)));
        em.SetComponentData(farEnemy, LocalTransform.FromPosition(new float3(10, 0, 0)));
        var unit = em.CreateEntity(typeof(LocalTransform), typeof(UnitCombatComponent));
        em.SetComponentData(unit, LocalTransform.Identity);
        var squad = em.CreateEntity(typeof(SquadAIComponent), typeof(SquadStateComponent));
        em.SetComponentData(squad, new SquadStateComponent { currentState = SquadFSMState.InCombat });
        em.AddBuffer<SquadUnitElement>(squad).Add(new SquadUnitElement { Value = unit });
        var candidates = em.AddBuffer<SquadTargetEntity>(squad);
        candidates.Add(new SquadTargetEntity { Value = farEnemy });
        candidates.Add(new SquadTargetEntity { Value = nearEnemy });

        world.GetOrCreateSystemManaged<UnitTargetingSystem>().Update();

        Assert.That(em.GetComponentData<UnitCombatComponent>(unit).target, Is.EqualTo(nearEnemy));
    }

    [Test]
    public void UnitTargetingClearsStaleEngagementOutsideCombat()
    {
        var staleTarget = em.CreateEntity(typeof(LocalTransform));
        var unit = em.CreateEntity(typeof(UnitCombatComponent), typeof(IsEngagingTag));
        em.SetComponentData(unit, new UnitCombatComponent { target = staleTarget });
        em.SetComponentEnabled<IsEngagingTag>(unit, true);

        var squad = em.CreateEntity(typeof(SquadAIComponent), typeof(SquadStateComponent));
        em.SetComponentData(squad, new SquadStateComponent { currentState = SquadFSMState.Idle });
        em.AddBuffer<SquadUnitElement>(squad).Add(new SquadUnitElement { Value = unit });
        em.AddBuffer<SquadTargetEntity>(squad).Add(new SquadTargetEntity { Value = staleTarget });

        world.GetOrCreateSystemManaged<UnitTargetingSystem>().Update();

        Assert.That(em.GetComponentData<UnitCombatComponent>(unit).target, Is.EqualTo(Entity.Null));
        Assert.That(em.IsComponentEnabled<IsEngagingTag>(unit), Is.False);
    }
}

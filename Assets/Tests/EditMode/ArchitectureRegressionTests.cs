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
    public void ProjectileDamageSnapshotSurvivesSquadProfileRetirement()
    {
        var target = em.CreateEntity(typeof(HealthComponent));
        em.SetComponentData(target, new HealthComponent { maxHealth = 100, currentHealth = 100 });
        var retiredProfile = em.CreateEntity(typeof(DamageProfileComponent));
        var snapshot = new DamageProfileComponent { bluntDamage = 10 };
        em.DestroyEntity(retiredProfile);
        var evt = em.CreateEntity(typeof(PendingDamageEvent));
        em.SetComponentData(evt, new PendingDamageEvent
        {
            target = target,
            damageProfile = retiredProfile,
            hasInlineDamageProfile = true,
            inlineDamageProfile = snapshot,
            multiplier = 1,
            destroyEntityAfterProcessing = true
        });

        world.GetOrCreateSystemManaged<DamageCalculationSystem>().Update();

        Assert.That(em.GetComponentData<HealthComponent>(target).currentHealth, Is.EqualTo(90));
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
    public void UnifiedSaveImportsLegacyWithoutAssigningAnOwnerOrReimporting()
    {
        WithSaveFiles((profilePath, progressPath) =>
        {
            var legacy = new ProgressFileStore(progressPath);
            legacy.Update(p => { p.level = 7; p.currentXP = 45; p.perkPoints = 3;
                p.loadouts.Add(new LocalSaveSystem.LoadoutData { name = "Legacy", squadIDs = new() { 8 } });
                p.squads.Add(new LocalSaveSystem.SquadInstanceData { id = 8, persistentId = "legacy-squad", armorPercent = 70 }); });
            var hero = new HeroData { heroName = "Modern", level = 2 };
            var original = new PlayerData { playerName = "Player", gold = 21, heroes = new() { hero } };
            File.WriteAllText(profilePath, JsonUtility.ToJson(original));
            string originalProfile = File.ReadAllText(profilePath);
            string originalProgress = File.ReadAllText(progressPath);
            var provider = new Core.Persistence.LocalSaveProvider(profilePath, progressPath);
            var imported = provider.Load();
            Assert.That(File.ReadAllText(profilePath), Is.EqualTo(originalProfile), "Load must be read-only.");
            Assert.That(imported.battleProgress.level, Is.EqualTo(7));
            Assert.That(imported.heroes[0].level, Is.EqualTo(2), "Legacy owner is unknown.");
            Assert.That(imported.heroes[0].heroId, Is.EqualTo(hero.heroId));
            provider.Save(imported);
            legacy.Update(p => p.level = 99);
            var result = provider.Load();
            Assert.That(result.saveVersion, Is.EqualTo(Core.Persistence.LocalSaveProvider.CurrentVersion));
            Assert.That(result.battleProgress.level, Is.EqualTo(7));
            Assert.That(result.battleProgress.currentXP, Is.EqualTo(45));
            Assert.That(result.battleProgress.perkPoints, Is.EqualTo(3));
            Assert.That(result.battleProgress.loadouts[0].squadIDs[0], Is.EqualTo(8));
            Assert.That(result.battleProgress.squads[0].persistentId, Is.EqualTo("legacy-squad"));
            Assert.That(result.battleProgress.squads[0].armorPercent, Is.EqualTo(70));
            Assert.That(File.ReadAllText(progressPath + ".bak"), Is.EqualTo(originalProgress));
            Assert.That(File.Exists(profilePath + ".bak"), Is.True);
        });
    }

    [Test]
    public void UnifiedProgressAndStaleInventoryWritesPreserveEachOthersFields()
    {
        WithSaveFiles((profilePath, progressPath) =>
        {
            var provider = new Core.Persistence.LocalSaveProvider(profilePath, progressPath);
            provider.Save(new PlayerData { playerName = "Player", gold = 21 });
            var inventorySnapshot = provider.Load();
            var progress = new ProgressFileStore(progressPath, profilePath);
            progress.Update(p => p.currentXP = 45);
            inventorySnapshot.gold = 75;
            provider.Save(inventorySnapshot);
            progress.Update(p => p.squads.Add(new LocalSaveSystem.SquadInstanceData { persistentId = "squad", armorPercent = 60 }));
            var final = provider.Load();
            Assert.That(final.gold, Is.EqualTo(75));
            Assert.That(final.battleProgress.currentXP, Is.EqualTo(45));
            Assert.That(final.battleProgress.squads[0].armorPercent, Is.EqualTo(60));
            Assert.That(File.Exists(progressPath), Is.False, "After a profile exists there is one active writer.");
        });
    }

    [Test]
    public void ProgressBeforeProfileCreationRemainsAvailableForFirstSave()
    {
        WithSaveFiles((profilePath, progressPath) =>
        {
            var progress = new ProgressFileStore(progressPath, profilePath);
            progress.Update(p => p.currentXP = 56);
            var provider = new Core.Persistence.LocalSaveProvider(profilePath, progressPath);
            Assert.That(provider.Load(), Is.Null, "Progress must not fabricate an empty player profile.");
            string original = File.ReadAllText(progressPath);
            provider.Save(new PlayerData { playerName = "New" });
            progress.Update(p => p.currentXP = 67);
            Assert.That(provider.Load().battleProgress.currentXP, Is.EqualTo(67));
            Assert.That(File.ReadAllText(progressPath), Is.EqualTo(original));
        });
    }

    [Test]
    public void FutureSaveVersionIsRejectedEvenWhenAnOlderBackupExists()
    {
        WithSaveFiles((profilePath, progressPath) =>
        {
            var provider = new Core.Persistence.LocalSaveProvider(profilePath, progressPath);
            provider.Save(new PlayerData { playerName = "Old" });
            provider.Save(new PlayerData { playerName = "New" });
            string future = "{\"saveVersion\":99,\"unknownField\":\"preserve\"}";
            File.WriteAllText(profilePath, future);
            Assert.Throws<NotSupportedException>(() => provider.Load());
            Assert.Throws<NotSupportedException>(() => provider.Save(new PlayerData()));
            Assert.Throws<NotSupportedException>(() => new ProgressFileStore(progressPath, profilePath).Update(p => p.level++));
            Assert.That(File.ReadAllText(profilePath), Is.EqualTo(future));
        });
    }

    [Test]
    public void LegacyProgressBackupIsImportedWhenItsPrimaryFileIsMissing()
    {
        WithSaveFiles((profilePath, progressPath) =>
        {
            var legacy = new ProgressFileStore(progressPath);
            legacy.Update(p => p.currentXP = 56);
            legacy.Update(p => p.currentXP = 67);
            File.Delete(progressPath);
            var provider = new Core.Persistence.LocalSaveProvider(profilePath, progressPath);
            provider.Save(new PlayerData());
            Assert.That(provider.Load().battleProgress.currentXP, Is.EqualTo(56));
            Assert.That(File.Exists(progressPath + ".bak"), Is.True);
        });
    }

    [Test]
    public void CorruptProfileRecoversBackupAndCorruptLegacyDoesNotBecomeEmptyProgress()
    {
        WithSaveFiles((profilePath, progressPath) =>
        {
            var provider = new Core.Persistence.LocalSaveProvider(profilePath, progressPath);
            provider.Save(new PlayerData { gold = 21 });
            provider.Save(new PlayerData { gold = 75 });
            File.WriteAllText(profilePath, "{broken");
            Assert.That(provider.Load().gold, Is.EqualTo(21));
            File.Delete(profilePath + ".bak");
            Assert.Throws<InvalidDataException>(() => provider.Save(new PlayerData()));
            Assert.That(File.ReadAllText(profilePath), Is.EqualTo("{broken"));
            File.Delete(profilePath);
            File.WriteAllText(progressPath, "{broken");
            Assert.Throws<InvalidDataException>(() => provider.Save(new PlayerData()));
            Assert.That(File.Exists(profilePath), Is.False);
            Assert.That(File.ReadAllText(progressPath), Is.EqualTo("{broken"));
        });
    }

    static void WithSaveFiles(Action<string, string> action)
    {
        string directory = Path.Combine(Path.GetTempPath(), "conquest-unified-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string profile = Path.Combine(directory, "player_save.json");
        string progress = Path.Combine(directory, "player_progress.json");
        try { action(profile, progress); }
        finally
        {
            foreach (string path in new[] { profile, progress })
                foreach (string suffix in new[] { "", ".bak", ".tmp" })
                    if (File.Exists(path + suffix)) File.Delete(path + suffix);
            Directory.Delete(directory);
        }
    }

    [Test]
    public void StaleInventorySaveCannotRevertCommittedHeroXpButKeepsInventoryChanges()
    {
        var previousProvider = (Core.Persistence.ISaveProvider)typeof(SaveSystem)
            .GetField("_provider", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
        try
        {
            WithSaveFiles((profilePath, progressPath) =>
            {
                var provider = new Core.Persistence.LocalSaveProvider(profilePath, progressPath);
                var first = new HeroData { heroName = "Same name" };
                var second = new HeroData { heroName = "Same name" };
                provider.Save(new PlayerData { gold = 20, heroes = new() { first, second } });
                var stale = provider.Load();
                SaveSystem.SetProvider(provider);
                Assert.That(SaveSystem.TryUpdateHeroProgress(first.heroId, 2, 50, 1), Is.True);
                stale.gold = 75;
                stale.heroes[0].bronze = 300;
                provider.Save(stale);
                var result = provider.Load();
                var updated = HeroDataService.FindById(result, first.heroId);
                Assert.That(result.gold, Is.EqualTo(75));
                Assert.That(updated.bronze, Is.EqualTo(300));
                Assert.That(updated.level, Is.EqualTo(2));
                Assert.That(updated.currentXP, Is.EqualTo(50));
                Assert.That(updated.perkPoints, Is.EqualTo(1));
                Assert.That(updated.progressRevision, Is.EqualTo(1));
                Assert.That(HeroDataService.FindById(result, second.heroId).level, Is.EqualTo(1));
                // A current snapshot can intentionally spend its perk points.
                updated.perkPoints = 0;
                provider.Save(result);
                Assert.That(HeroDataService.FindById(provider.Load(), first.heroId).perkPoints, Is.Zero);
            });
        }
        finally { SaveSystem.SetProvider(previousProvider); }
    }

    [Test]
    public void SquadProgressCommitPreservesRosterFieldsAndSurvivesAStaleInventorySave()
    {
        var previousProvider = (Core.Persistence.ISaveProvider)typeof(SaveSystem)
            .GetField("_provider", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
        try
        {
            WithSaveFiles((profilePath, progressPath) =>
            {
                var provider = new Core.Persistence.LocalSaveProvider(profilePath, progressPath);
                var owner = new HeroData();
                owner.squadProgress.Add(new SquadInstanceData { id = "owned", baseSquadID = "base",
                    unitsInSquad = 12, unitsKilled = 2, unitsInjured = 1, selectedFormationIndex = 3 });
                provider.Save(new PlayerData { heroes = new() { owner } });
                var stale = provider.Load();
                SaveSystem.SetProvider(provider);
                Assert.That(SaveSystem.TryUpdateSquadProgress(owner.heroId, "owned", 2, 50), Is.True);
                var afterXp = SquadDataService.FindInstanceById(HeroDataService.FindById(provider.Load(), owner.heroId), "owned");
                Assert.That(afterXp.unitsKilled, Is.EqualTo(2));
                Assert.That(afterXp.unitsInjured, Is.EqualTo(1));
                Assert.That(afterXp.unitsInSquad, Is.EqualTo(12));
                Assert.That(afterXp.selectedFormationIndex, Is.EqualTo(3));
                stale.gold = 75;
                stale.heroes[0].squadProgress[0].selectedFormationIndex = 4;
                provider.Save(stale);
                var result = provider.Load();
                var final = SquadDataService.FindInstanceById(HeroDataService.FindById(result, owner.heroId), "owned");
                Assert.That(result.gold, Is.EqualTo(75));
                Assert.That(final.level, Is.EqualTo(2));
                Assert.That(final.experience, Is.EqualTo(50));
                Assert.That(final.progressRevision, Is.EqualTo(1));
                Assert.That(final.selectedFormationIndex, Is.EqualTo(4));
                Assert.That(final.unitsAlive, Is.EqualTo(9));
                Assert.That(result.battleProgress.squads, Is.Empty);
            });
        }
        finally { SaveSystem.SetProvider(previousProvider); }
    }

    [Test]
    public void SpawnUsesSnapshotIdentityLevelAndSurvivors()
    {
        var blob = TestFormationLibrary.FromCells(new int2(0, 0), new int2(1, 0), new int2(2, 0), new int2(3, 0));
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
            var unitOwner = em.GetComponentData<UnitOwnerComponent>(unit);
            Assert.That(unitOwner.squad, Is.EqualTo(squad));
            Assert.That(unitOwner.hero, Is.EqualTo(hero));
            var profileEntity = em.GetComponentData<UnitWeaponComponent>(unit).damageProfile;
            var profile = em.GetComponentData<DamageProfileComponent>(profileEntity);
            var bonus = em.GetComponentData<PenetrationComponent>(unit);
            Assert.That(profile.bluntPenetration + bonus.bluntPenetration, Is.EqualTo(3));
        }
        finally { blob.Dispose(); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void UnitSpawnBuilderPreservesMeleeAndOptionalRangedComponents(bool ranged)
    {
        var squad = em.CreateEntity();
        var hero = em.CreateEntity();
        var profile = em.CreateEntity();
        var unit = em.CreateEntity();
        var position = new float3(2f, 0f, 3f);
        var context = new SquadUnitSpawnContext
        {
            squad = squad,
            hero = hero,
            damageProfile = profile,
            data = new SquadDataComponent
            {
                baseHealth = 80f,
                attackRange = 2f,
                isRangedUnit = ranged,
                ammoCapacity = 12
            },
            config = new SquadSpawnConfigComponent(),
            squadType = SquadType.Archers,
            speed = 4.5f,
            rotation = quaternion.identity
        };
        using var ecb = new EntityCommandBuffer(Allocator.Temp);
        SquadUnitSpawnBuilder.Configure(ecb, unit, context,
            new UnitGridSlotComponent { slotIndex = 1, localOffset = new float3(0.5f, 0f, 0f) }, position);
        ecb.Playback(em);

        Assert.That(em.GetComponentData<UnitOwnerComponent>(unit).squad, Is.EqualTo(squad));
        Assert.That(em.GetComponentData<UnitOwnerComponent>(unit).hero, Is.EqualTo(hero));
        Assert.That(em.GetComponentData<LocalTransform>(unit).Position, Is.EqualTo(position));
        Assert.That(em.GetComponentData<UnitStatsComponent>(unit).speed, Is.EqualTo(4.5f));
        Assert.That(em.GetComponentData<UnitWeaponComponent>(unit).damageProfile, Is.EqualTo(profile));
        Assert.That(em.IsComponentEnabled<WeaponHitboxActiveTag>(unit), Is.False);
        Assert.That(em.HasComponent<UnitRangedStatsComponent>(unit), Is.EqualTo(ranged));
        Assert.That(em.HasComponent<RangedAttackStateComponent>(unit), Is.EqualTo(ranged));
        if (ranged)
            Assert.That(em.GetComponentData<RangedAttackStateComponent>(unit).currentAmmo, Is.EqualTo(12));
    }

    [Test]
    public void SpawnBuildsReserveCountsFromTheirOwnDefinitions()
    {
        var blob = TestFormationLibrary.FromCells(int2.zero, new int2(1, 0));
        try
        {
            em.CreateEntity(typeof(SquadSpawnConfigComponent));
            em.CreateEntity(typeof(MatchStateComponent));
            var active = em.CreateEntity(typeof(SquadDataComponent),
                typeof(SquadDefinitionComponent), typeof(SquadDataIDComponent));
            em.SetComponentData(active, new SquadDataComponent { baseHealth = 100, baseSpeed = 5 });
            em.SetComponentData(active, new SquadDefinitionComponent { formationLibrary = blob, unitCount = 2 });
            em.SetComponentData(active, new SquadDataIDComponent { id = "active" });
            var reserve = em.CreateEntity(typeof(SquadDataIDComponent), typeof(SquadDefinitionComponent));
            em.SetComponentData(reserve, new SquadDataIDComponent { id = "reserve" });
            em.SetComponentData(reserve, new SquadDefinitionComponent { unitCount = 5 });
            var hero = em.CreateEntity(typeof(HeroSpawnComponent), typeof(HeroSquadSelectionComponent),
                typeof(LocalTransform), typeof(TeamComponent));
            em.SetComponentData(hero, new HeroSpawnComponent { hasSpawned = true });
            em.SetComponentData(hero, LocalTransform.Identity);
            em.SetComponentData(hero, new HeroSquadSelectionComponent { squadDataEntity = active, instanceId = 0 });
            em.SetComponentData(hero, new TeamComponent { value = Team.TeamA });
            var map = em.AddBuffer<SquadIdMapElement>(hero);
            map.Add(new SquadIdMapElement { squadId = 0, baseSquadID = "active" });
            map.Add(new SquadIdMapElement { squadId = 1, baseSquadID = "reserve",
                hasSnapshot = true, totalUnits = 4, aliveUnits = 3 });

            world.GetOrCreateSystemManaged<SquadSpawningSystem>().Update();

            var reserves = em.GetBuffer<InactiveSquadElement>(hero);
            Assert.That(reserves.Length, Is.EqualTo(1));
            Assert.That(reserves[0].squadId, Is.EqualTo(1));
            Assert.That(reserves[0].totalUnits, Is.EqualTo(4));
            Assert.That(reserves[0].aliveUnits, Is.EqualTo(3));
            Assert.That(reserves[0].isEliminated, Is.False);
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

        var watch = System.Diagnostics.Stopwatch.StartNew();
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        watch.Restart();
        system.Update();
        watch.Stop();
        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.That(em.GetBuffer<SquadTargetEntity>(squadA).Length, Is.EqualTo(450));
        Assert.That(em.GetBuffer<SquadTargetEntity>(squadB).Length, Is.EqualTo(450));
        Assert.That(em.GetBuffer<DetectedEnemy>(squadA).Length, Is.EqualTo(1));
        Assert.That(em.GetBuffer<DetectedEnemy>(squadB).Length, Is.EqualTo(1));
        Assert.That(system.DebugObserverCount, Is.EqualTo(2));
        Assert.That(system.DebugCandidateOccurrences, Is.EqualTo(900));
        Assert.That(system.DebugExactDistanceChecks, Is.EqualTo(900));
        Assert.That(system.DebugPublishedTargets, Is.EqualTo(900));
        Assert.That(allocatedBytes, Is.Zero, "Warmed detection must reuse storage without main-thread managed allocation.");
        TestContext.WriteLine($"Enemy detection 900 units warm-frame: {watch.Elapsed.TotalMilliseconds:F2} ms, " +
            $"managed allocation: {allocatedBytes} bytes, candidate entries: {system.DebugCandidateOccurrences}, " +
            $"grid cells: {system.DebugGridCellVisits}, exact checks: {system.DebugExactDistanceChecks}");

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
            $"managed allocation: {allocatedBytes} bytes, candidate checks: {targeting.DebugCandidateChecks}, " +
            $"retargets: {targeting.DebugRetargetCount}");
    }

    [Test]
    public void EnemyDetectionPreservesNegativeRadiusBoundaryAndDuplicateOrder()
    {
        Entity CreateUnit(float x, float y = 0f, bool dead = false)
        {
            var unit = dead
                ? em.CreateEntity(typeof(LocalTransform), typeof(IsDeadComponent))
                : em.CreateEntity(typeof(LocalTransform));
            em.SetComponentData(unit, LocalTransform.FromPosition(new float3(x, y, 0f)));
            return unit;
        }

        Entity CreateSquad(Team team, float range, params Entity[] members)
        {
            var squad = em.CreateEntity(typeof(SquadDefinitionComponent), typeof(SquadDataComponent),
                typeof(TeamComponent));
            em.SetComponentData(squad, new SquadDefinitionComponent { detectionRange = range });
            em.SetComponentData(squad, new TeamComponent { value = team });
            em.AddBuffer<DetectedEnemy>(squad);
            em.AddBuffer<SquadTargetEntity>(squad);
            var units = em.AddBuffer<SquadUnitElement>(squad);
            foreach (var member in members)
                units.Add(new SquadUnitElement { Value = member });
            return squad;
        }

        var observerUnit = CreateUnit(0f);
        var observer = CreateSquad(Team.TeamA, -2f, observerUnit);
        var firstEnemy = CreateUnit(1f);
        var boundaryEnemy = CreateUnit(0f, 2f);
        var deadEnemy = CreateUnit(0.5f, dead: true);
        var enemySquad = CreateSquad(Team.TeamB, -1f,
            firstEnemy, firstEnemy, boundaryEnemy, deadEnemy);
        var hero = em.CreateEntity(typeof(TeamComponent), typeof(LocalTransform), typeof(DetectableEntityTag));
        em.SetComponentData(hero, new TeamComponent { value = Team.TeamB });
        em.SetComponentData(hero, LocalTransform.FromPosition(new float3(2f, 0f, 0f)));

        world.GetOrCreateSystemManaged<EnemyDetectionSystem>().Update();

        var detected = em.GetBuffer<DetectedEnemy>(observer);
        Assert.That(detected.Length, Is.EqualTo(2));
        Assert.That(detected[0].Value, Is.EqualTo(enemySquad));
        Assert.That(detected[1].Value, Is.EqualTo(hero));
        var targets = em.GetBuffer<SquadTargetEntity>(observer);
        Assert.That(targets.Length, Is.EqualTo(4));
        Assert.That(targets[0].Value, Is.EqualTo(firstEnemy));
        Assert.That(targets[1].Value, Is.EqualTo(firstEnemy));
        Assert.That(targets[2].Value, Is.EqualTo(boundaryEnemy));
        Assert.That(targets[3].Value, Is.EqualTo(hero));
    }

    [Test]
    public void EnemyDetectionSpatialIndexPrunesDistantOccurrences()
    {
        Entity CreateDetectionSquad(Team team, float xOffset)
        {
            var squad = em.CreateEntity(typeof(SquadDefinitionComponent), typeof(SquadDataComponent),
                typeof(TeamComponent));
            em.SetComponentData(squad, new SquadDefinitionComponent { detectionRange = 2f });
            em.SetComponentData(squad, new TeamComponent { value = team });
            em.AddBuffer<SquadUnitElement>(squad);
            em.AddBuffer<DetectedEnemy>(squad);
            em.AddBuffer<SquadTargetEntity>(squad);
            for (int i = 0; i < 100; i++)
            {
                var unit = em.CreateEntity(typeof(LocalTransform));
                em.SetComponentData(unit, LocalTransform.FromPosition(new float3(xOffset + i * 10f, 0f, 0f)));
                em.GetBuffer<SquadUnitElement>(squad).Add(new SquadUnitElement { Value = unit });
            }
            return squad;
        }

        var squadA = CreateDetectionSquad(Team.TeamA, 0f);
        var squadB = CreateDetectionSquad(Team.TeamB, 5000f);
        var system = world.GetOrCreateSystemManaged<EnemyDetectionSystem>();

        system.Update();

        Assert.That(em.GetBuffer<SquadTargetEntity>(squadA).Length, Is.Zero);
        Assert.That(em.GetBuffer<SquadTargetEntity>(squadB).Length, Is.Zero);
        Assert.That(system.DebugCandidateOccurrences, Is.EqualTo(200));
        Assert.That(system.DebugExactDistanceChecks, Is.LessThan(200));
    }

    [Test]
    public void EnemyDetectionMatchesOriginalBruteForceScanAcrossEdgeCasesAndRespawn()
    {
        Entity CreateUnit(float3 position, bool hasTransform = true, bool dead = false)
        {
            var entity = em.CreateEntity();
            if (hasTransform)
                em.AddComponentData(entity, LocalTransform.FromPosition(position));
            if (dead)
                em.AddComponent<IsDeadComponent>(entity);
            return entity;
        }

        Entity CreateSquad(Team team, float range, params Entity[] members)
        {
            var squad = em.CreateEntity(typeof(SquadDefinitionComponent), typeof(SquadDataComponent),
                typeof(TeamComponent));
            em.SetComponentData(squad, new SquadDefinitionComponent { detectionRange = range });
            em.SetComponentData(squad, new TeamComponent { value = team });
            em.AddBuffer<DetectedEnemy>(squad);
            em.AddBuffer<SquadTargetEntity>(squad);
            var units = em.AddBuffer<SquadUnitElement>(squad);
            foreach (var member in members)
                units.Add(new SquadUnitElement { Value = member });
            return squad;
        }

        Entity CreateHero(Team team, float3 position, bool alive, bool deadTag = false)
        {
            var hero = em.CreateEntity(typeof(TeamComponent), typeof(LocalTransform), typeof(DetectableEntityTag),
                typeof(HeroLifeComponent));
            em.SetComponentData(hero, new TeamComponent { value = team });
            em.SetComponentData(hero, LocalTransform.FromPosition(position));
            em.SetComponentData(hero, new HeroLifeComponent { isAlive = alive });
            if (deadTag)
                em.AddComponent<IsDeadComponent>(hero);
            return hero;
        }

        var origin = CreateUnit(float3.zero);
        CreateSquad(Team.TeamA, 0f, origin);
        var negativeRangeObserverUnit = CreateUnit(new float3(0f, 0f, 0f));
        CreateSquad(Team.TeamB, -2f, negativeRangeObserverUnit);
        var nanCentroidUnit = CreateUnit(new float3(float.NaN, 0f, 0f));
        CreateSquad(Team.TeamA, float.NaN, nanCentroidUnit);
        var infinityRangeObserverUnit = CreateUnit(new float3(1e30f, 0f, 0f));
        CreateSquad(Team.None, float.PositiveInfinity, infinityRangeObserverUnit);
        var deadObserverUnit = CreateUnit(float3.zero, dead: true);
        var noTransformObserverUnit = CreateUnit(float3.zero, hasTransform: false);
        CreateSquad(Team.TeamB, 3f, deadObserverUnit, noTransformObserverUnit);

        var duplicate = CreateUnit(new float3(1f, 0f, 0f));
        var boundary = CreateUnit(new float3(0f, 2f, 0f));
        var nanCandidate = CreateUnit(new float3(float.NaN, 0f, 0f));
        var infiniteCandidate = CreateUnit(new float3(float.PositiveInfinity, 0f, 0f));
        var hugeCandidate = CreateUnit(new float3(2e30f, 0f, 0f));
        var deadCandidate = CreateUnit(float3.zero, dead: true);
        var missingTransformCandidate = CreateUnit(float3.zero, hasTransform: false);
        var destroyedCandidate = CreateUnit(float3.zero);
        em.DestroyEntity(destroyedCandidate);
        var overlapHero = CreateHero(Team.TeamB, new float3(0f, 0f, 0f), alive: true);
        CreateSquad(Team.TeamB, 0f, duplicate, duplicate, boundary, nanCandidate,
            infiniteCandidate, hugeCandidate, deadCandidate, missingTransformCandidate, destroyedCandidate,
            overlapHero);
        var candidateSquadBUnit = CreateUnit(new float3(0f, 0f, 0f));
        CreateSquad(Team.TeamA, 0f, candidateSquadBUnit);
        CreateHero(Team.TeamB, float3.zero, alive: true);
        CreateHero(Team.TeamB, float3.zero, alive: false);
        CreateHero(Team.TeamB, float3.zero, alive: true, deadTag: true);
        var respawningHero = CreateHero(Team.TeamB, float3.zero, alive: false, deadTag: true);
        CreateHero(Team.TeamA, new float3(0f, float.NegativeInfinity, 0f), alive: true);

        var system = world.GetOrCreateSystemManaged<EnemyDetectionSystem>();

        System.Collections.Generic.Dictionary<Entity, (System.Collections.Generic.List<Entity> Detected,
            System.Collections.Generic.List<Entity> Targets)> ScanOriginal()
        {
            var expected = new System.Collections.Generic.Dictionary<Entity,
                (System.Collections.Generic.List<Entity>, System.Collections.Generic.List<Entity>)>();
            var observerQuery = em.CreateEntityQuery(typeof(SquadDefinitionComponent), typeof(TeamComponent),
                typeof(SquadUnitElement), typeof(DetectedEnemy), typeof(SquadTargetEntity));
            var squadQuery = em.CreateEntityQuery(typeof(TeamComponent), typeof(SquadUnitElement),
                typeof(SquadDataComponent));
            var heroQuery = em.CreateEntityQuery(typeof(TeamComponent), typeof(LocalTransform),
                typeof(DetectableEntityTag));
            try
            {
                using var observers = observerQuery.ToEntityArray(Allocator.Temp);
                using var squads = squadQuery.ToEntityArray(Allocator.Temp);
                using var heroes = heroQuery.ToEntityArray(Allocator.Temp);
                foreach (var observer in observers)
                {
                    var detected = new System.Collections.Generic.List<Entity>();
                    var targets = new System.Collections.Generic.List<Entity>();
                    var unitsA = em.GetBuffer<SquadUnitElement>(observer);
                    float3 centroid = float3.zero;
                    int aliveCount = 0;
                    for (int i = 0; i < unitsA.Length; i++)
                    {
                        Entity unit = unitsA[i].Value;
                        if (!em.Exists(unit) || !em.HasComponent<LocalTransform>(unit)
                            || em.HasComponent<IsDeadComponent>(unit))
                            continue;
                        centroid += em.GetComponentData<LocalTransform>(unit).Position;
                        aliveCount++;
                    }
                    if (aliveCount > 0)
                    {
                        centroid /= aliveCount;
                        Team teamA = em.GetComponentData<TeamComponent>(observer).value;
                        float range = em.GetComponentData<SquadDefinitionComponent>(observer).detectionRange;
                        float rangeSquared = range * range;
                        foreach (var enemySquad in squads)
                        {
                            if (enemySquad == observer)
                                continue;
                            Team teamB = em.GetComponentData<TeamComponent>(enemySquad).value;
                            if (teamB == teamA)
                                continue;

                            bool squadRegistered = false;
                            var unitsB = em.GetBuffer<SquadUnitElement>(enemySquad);
                            for (int j = 0; j < unitsB.Length; j++)
                            {
                                Entity unit = unitsB[j].Value;
                                if (!em.Exists(unit) || !em.HasComponent<LocalTransform>(unit)
                                    || em.HasComponent<IsDeadComponent>(unit))
                                    continue;
                                float3 position = em.GetComponentData<LocalTransform>(unit).Position;
                                if (math.distancesq(centroid, position) > rangeSquared)
                                    continue;
                                if (!squadRegistered)
                                {
                                    detected.Add(enemySquad);
                                    squadRegistered = true;
                                }
                                targets.Add(unit);
                            }
                        }

                        foreach (var hero in heroes)
                        {
                            if (em.GetComponentData<TeamComponent>(hero).value == teamA)
                                continue;
                            if (em.HasComponent<IsDeadComponent>(hero))
                                continue;
                            if (em.HasComponent<HeroLifeComponent>(hero)
                                && !em.GetComponentData<HeroLifeComponent>(hero).isAlive)
                                continue;
                            float3 position = em.GetComponentData<LocalTransform>(hero).Position;
                            if (math.distancesq(centroid, position) > rangeSquared)
                                continue;
                            detected.Add(hero);
                            targets.Add(hero);
                        }
                    }
                    expected.Add(observer, (detected, targets));
                }
            }
            finally
            {
                observerQuery.Dispose();
                squadQuery.Dispose();
                heroQuery.Dispose();
            }
            return expected;
        }

        void AssertMatchesOriginalScan()
        {
            var expected = ScanOriginal();
            system.Update();
            foreach (var pair in expected)
            {
                var actualDetected = em.GetBuffer<DetectedEnemy>(pair.Key);
                var actualTargets = em.GetBuffer<SquadTargetEntity>(pair.Key);
                Assert.That(actualDetected.Length, Is.EqualTo(pair.Value.Detected.Count), $"Detected length for {pair.Key}");
                Assert.That(actualTargets.Length, Is.EqualTo(pair.Value.Targets.Count), $"Targets length for {pair.Key}");
                for (int i = 0; i < actualDetected.Length; i++)
                    Assert.That(actualDetected[i].Value, Is.EqualTo(pair.Value.Detected[i]), $"Detected[{i}] for {pair.Key}");
                for (int i = 0; i < actualTargets.Length; i++)
                    Assert.That(actualTargets[i].Value, Is.EqualTo(pair.Value.Targets[i]), $"Targets[{i}] for {pair.Key}");
            }
        }

        AssertMatchesOriginalScan();

        em.RemoveComponent<IsDeadComponent>(respawningHero);
        em.SetComponentData(respawningHero, new HeroLifeComponent { isAlive = true });
        AssertMatchesOriginalScan();
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
    [Test]
    public void EndMatchPublishesInactiveSnapshotAndStopsStaleAIDecision()
    {
        var match = em.CreateEntity(typeof(MatchStateComponent));
        em.SetComponentData(match, new MatchStateComponent { currentState = MatchState.InBattle });
        var snapshot = world.GetOrCreateSystemManaged<BattleWorldStateSystem>();
        snapshot.Update();
        var ws = em.GetComponentObject<TeamWorldState>(em.CreateEntityQuery(typeof(TeamWorldState)).GetSingletonEntity());
        Assert.That(ws.match.isActive, Is.True);
        em.SetComponentData(match, new MatchStateComponent { currentState = MatchState.EndMatch });
        snapshot.Update();
        Assert.That(ws.match.isActive, Is.False);
        Assert.That(ws.match.state, Is.EqualTo(MatchState.EndMatch));

        var hero = em.CreateEntity(typeof(HeroAITag), typeof(RusherBehaviorActive), typeof(LocalTransform),
            typeof(HeroLifeComponent), typeof(HeroAIDecision));
        em.SetComponentData(hero, new HeroLifeComponent { isAlive = true });
        em.AddComponentObject(hero, new HeroAIBlackboard
        { selfIsAlive = true, matchIsActive = true, bestObjectiveZone = match, bestObjectivePosition = new float3(10,0,0) });
        em.SetComponentData(hero, new HeroAIDecision { action = AIActionType.AttackTarget, shouldAttack = true });
        world.GetOrCreateSystemManaged<HeroAIRusherSystem>().Update();
        var decision = em.GetComponentData<HeroAIDecision>(hero);
        Assert.That(decision.action, Is.EqualTo(AIActionType.Idle));
        Assert.That(decision.shouldAttack, Is.False);
        Assert.That(decision.hasNewSquadOrder, Is.False);
    }

    [Test]
    public void DeadAttackerCannotBeginMeleeSwingOrEmitProjectile()
    {
        var target = em.CreateEntity(typeof(LocalTransform), typeof(HealthComponent));
        em.SetComponentData(target, LocalTransform.Identity);
        em.SetComponentData(target, new HealthComponent { currentHealth = 100, maxHealth = 100 });
        var profile = em.CreateEntity(typeof(DamageProfileComponent));
        var melee = em.CreateEntity(typeof(LocalTransform), typeof(UnitCombatComponent), typeof(UnitWeaponComponent), typeof(IsDeadComponent));
        em.SetComponentData(melee, LocalTransform.Identity);
        em.SetComponentData(melee, new UnitCombatComponent { target = target });
        em.SetComponentData(melee, new UnitWeaponComponent { attackRange = 5, damageProfile = profile });
        var ranged = em.CreateEntity(typeof(LocalTransform), typeof(UnitCombatComponent), typeof(UnitWeaponComponent),
            typeof(UnitRangedStatsComponent), typeof(RangedAttackStateComponent), typeof(IsDeadComponent));
        em.SetComponentData(ranged, LocalTransform.Identity);
        em.SetComponentData(ranged, new UnitCombatComponent { target = target });
        em.SetComponentData(ranged, new UnitWeaponComponent { damageProfile = profile });
        em.SetComponentData(ranged, new UnitRangedStatsComponent { range = 10 });
        em.SetComponentData(ranged, new RangedAttackStateComponent { currentAmmo = 3 });
        world.GetOrCreateSystemManaged<UnitAttackSystem>().Update();
        world.GetOrCreateSystemManaged<RangedAttackSystem>().Update();
        world.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().Update();
        Assert.That(em.GetComponentData<UnitCombatComponent>(melee).isAttacking, Is.False);
        Assert.That(em.GetComponentData<RangedAttackStateComponent>(ranged).currentAmmo, Is.EqualTo(3));
        Assert.That(em.CreateEntityQuery(typeof(ProjectileSpawnRequest)).CalculateEntityCount(), Is.Zero);
    }

    [Test]
    public void EndMatchClearsActiveMeleeHitbox()
    {
        var match = em.CreateEntity(typeof(MatchStateComponent));
        em.SetComponentData(match, new MatchStateComponent { currentState = MatchState.EndMatch });
        var unit = em.CreateEntity(typeof(LocalTransform), typeof(UnitWeaponComponent),
            typeof(UnitCombatComponent), typeof(WeaponHitboxActiveTag));
        em.SetComponentData(unit, new UnitCombatComponent { isAttacking = true, attackAnimationTimer = 1 });
        em.SetComponentEnabled<WeaponHitboxActiveTag>(unit, true);
        world.GetOrCreateSystemManaged<UnitAttackSystem>().Update();
        Assert.That(em.GetComponentData<UnitCombatComponent>(unit).isAttacking, Is.False);
        Assert.That(em.IsComponentEnabled<WeaponHitboxActiveTag>(unit), Is.False);
    }

    [Test]
    public void TargetingIgnoresDeadCandidateEvenWhenItsTransformStillExists()
    {
        var dead = em.CreateEntity(typeof(LocalTransform), typeof(IsDeadComponent));
        var alive = em.CreateEntity(typeof(LocalTransform));
        em.SetComponentData(dead, LocalTransform.Identity);
        em.SetComponentData(alive, LocalTransform.FromPosition(new float3(10,0,0)));
        var unit = em.CreateEntity(typeof(LocalTransform), typeof(UnitCombatComponent));
        em.SetComponentData(unit, LocalTransform.Identity);
        em.SetComponentData(unit, new UnitCombatComponent { target = dead });
        var squad = em.CreateEntity(typeof(SquadAIComponent), typeof(SquadStateComponent));
        em.SetComponentData(squad, new SquadStateComponent { currentState = SquadFSMState.InCombat });
        em.AddBuffer<SquadUnitElement>(squad).Add(new SquadUnitElement { Value = unit });
        var candidates = em.AddBuffer<SquadTargetEntity>(squad);
        candidates.Add(new SquadTargetEntity { Value = dead });
        candidates.Add(new SquadTargetEntity { Value = alive });
        world.GetOrCreateSystemManaged<UnitTargetingSystem>().Update();
        Assert.That(em.GetComponentData<UnitCombatComponent>(unit).target, Is.EqualTo(alive));
    }

    [Test]
    public void VisualLookupSeparatesWorldsAndDoesNotUnregisterReplacement()
    {
        using var otherWorld = new World("Other visual lookup world");
        var entity = em.CreateEntity();
        var host = new GameObject("Visual lookup regression");
        var replacementHost = new GameObject("Replacement visual lookup regression");
        host.SetActive(false);
        replacementHost.SetActive(false);
        var visual = host.AddComponent<ConquestTactics.Visual.EntityVisualSync>();
        var replacement = replacementHost.AddComponent<ConquestTactics.Visual.EntityVisualSync>();
        try
        {
            VisualSyncUtility.Register(world, entity, visual);
            Assert.That(VisualSyncUtility.TryGetVisual(world, entity, out var found), Is.True);
            Assert.That(found, Is.SameAs(visual));
            Assert.That(VisualSyncUtility.TryGetVisual(otherWorld, entity, out _), Is.False);
            VisualSyncUtility.Register(world, entity, replacement);
            VisualSyncUtility.Unregister(world, entity, visual);
            Assert.That(VisualSyncUtility.TryGetVisual(world, entity, out found), Is.True);
            Assert.That(found, Is.SameAs(replacement));
        }
        finally
        {
            VisualSyncUtility.Unregister(world, entity, replacement);
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(replacementHost);
        }
    }

    [Test]
    public void CampaignRouteSearchesRemainIndependentWhenInterleavedAndCancelled()
    {
        var navigation = ScriptableObject.CreateInstance<Conquest.WorldMap.WorldMapLocalNavigation>();
        navigation.columns = 5;
        navigation.rows = 5;
        navigation.cellSize = 1;
        navigation.blocked = new byte[25];
        navigation.heights = new float[25];
        navigation.blocked[12] = 1;
        Vector3[] first = null, second = null;
        var a = navigation.Search(navigation.Center(0), navigation.Center(24), (route, _) => first = route, 1);
        var b = navigation.Search(navigation.Center(4), navigation.Center(20), (route, _) => second = route, 1);
        try
        {
            Assert.That(a.MoveNext(), Is.True);
            Assert.That(b.MoveNext(), Is.True);
            (a as IDisposable)?.Dispose();
            while (b.MoveNext()) { }
            Assert.That(first, Is.Null);
            Assert.That(second, Is.Not.Null);
            Assert.That(second[0], Is.EqualTo(navigation.Center(4)));
            Assert.That(second[second.Length - 1], Is.EqualTo(navigation.Center(20)));
            for (int i = 1; i < second.Length; i++)
                Assert.That(navigation.SegmentClear(second[i - 1], second[i]), Is.True);
        }
        finally
        {
            (a as IDisposable)?.Dispose();
            (b as IDisposable)?.Dispose();
            UnityEngine.Object.DestroyImmediate(navigation);
        }
    }

    [Test]
    public void CampaignDesiredSectorSnapshotsAreOrderedAndIndependent()
    {
        var manifest = ScriptableObject.CreateInstance<Conquest.WorldMap.WorldMapCampaignManifest>();
        manifest.sectorSize = 10;
        manifest.worldSize = new Vector2(30, 10);
        manifest.sectors = new[]
        {
            new Conquest.WorldMap.CampaignSectorAddress { column = 0, row = 0, origin = Vector3.zero },
            new Conquest.WorldMap.CampaignSectorAddress { column = 1, row = 0, origin = new Vector3(10,0,0) },
            new Conquest.WorldMap.CampaignSectorAddress { column = 2, row = 0, origin = new Vector3(20,0,0) }
        };
        var host = new GameObject("Sector planning regression");
        host.SetActive(false);
        var streamer = host.AddComponent<Conquest.WorldMap.WorldMapSectorStreamer>();
        streamer.manifest = manifest;
        streamer.preloadRadius = 1;
        try
        {
            var first = streamer.DesiredIndices(new Vector3(5,0,5));
            Assert.That(first, Is.EqualTo(new[] { 0, 1 }));
            var second = streamer.DesiredIndices(new Vector3(25,0,5));
            Assert.That(second, Is.EqualTo(new[] { 2, 1 }));
            Assert.That(first, Is.EqualTo(new[] { 0, 1 }));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(manifest);
        }
    }

    [Test]
    public void BattleLookupUsesPersistentIdentityAndRejectsAmbiguousLegacyNames()
    {
        var selected = new HeroData { heroName = "Shared name" };
        var battle = new BattleData();
        var impostor = new BattleHeroData { heroId = Guid.NewGuid().ToString("N"), heroName = selected.heroName };
        var actual = new BattleHeroData { heroId = selected.heroId, heroName = selected.heroName };
        battle.attackers.Add(impostor);
        battle.defenders.Add(actual);
        Assert.That(battle.FindHero(selected), Is.SameAs(actual));
        Assert.That(battle.GetSide(actual), Is.EqualTo(Side.Defenders));
        actual.heroId = string.Empty;
        impostor.heroId = string.Empty;
        Assert.That(battle.FindHero(selected), Is.Null);
        battle.attackers.Clear();
        Assert.That(battle.FindHero(selected), Is.SameAs(actual));
        actual.heroId = Guid.NewGuid().ToString("N");
        Assert.That(battle.FindHero(selected), Is.Null);
    }

    [Test]
    public void NewBattleBootstrapRearmsEndedMatchInTheSameWorld()
    {
        var previousWorld = World.DefaultGameObjectInjectionWorld;
        World.DefaultGameObjectInjectionWorld = world;
        try
        {
            var match = em.CreateEntity(typeof(MatchStateComponent));
            em.SetComponentData(match, new MatchStateComponent
            { currentState = MatchState.EndMatch, winnerTeam = 2, victoryConditionMet = true });
            var controller = world.GetOrCreateSystemManaged<MatchControllerSystem>();
            controller.Update();
            Assert.That(controller.Enabled, Is.False);
            em.CreateEntity(typeof(DataContainerComponent));
            var participant = new BattleHeroData { heroId = "persistent-local", heroName = "Local" };
            participant.squadInstances.Add(new SquadInstanceData
            { id = "squad-instance", baseSquadID = "test-squad", unitsInSquad = 1 });
            var battle = new BattleData();
            battle.attackers.Add(participant);
            BattleBootstrapRequests.SubmitLocal(battle, participant.heroName, participant.heroId);
            world.GetOrCreateSystemManaged<BattleBootstrapSystem>().Update();
            var restarted = em.GetComponentData<MatchStateComponent>(match);
            Assert.That(restarted.currentState, Is.EqualTo(MatchState.InBattle));
            Assert.That(restarted.victoryConditionMet, Is.False);
            Assert.That(restarted.winnerTeam, Is.Zero);
            Assert.That(controller.Enabled, Is.True);
            controller.Update();
            Assert.That(em.GetComponentData<MatchStateComponent>(match).currentState, Is.EqualTo(MatchState.InBattle));
        }
        finally { World.DefaultGameObjectInjectionWorld = previousWorld; }
    }

}

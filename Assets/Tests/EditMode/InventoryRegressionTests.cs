using System;
using System.Collections.Generic;
using System.Reflection;
using Core.Persistence;
using Data.Items;
using NUnit.Framework;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;

public class InventoryRegressionTests
{
    HeroData hero, previousInventoryHero, previousHero;
    PlayerData previousPlayer;
    ISaveProvider previousProvider;
    ItemService items;
    object previousCache;
    bool previousCatalogInitialized;
    GameObject itemHost, cacheHost;
    int previousLimit, changes, saves;
    string fixtureClassId;
    Action previousSaveAction;
    readonly List<ScriptableObject> definitions = new();
    readonly Dictionary<string, ItemDataSO> catalog = new();

    [SetUp]
    public void SetUp()
    {
        previousInventoryHero = InventoryManager.GetCurrentHero();
        previousPlayer = PlayerSessionService.CurrentPlayer;
        previousHero = PlayerSessionService.SelectedHero;
        previousProvider = (ISaveProvider)Field(typeof(SaveSystem), "_provider").GetValue(null);
        previousSaveAction = (Action)Field(typeof(InventoryEventService), "_saveAction").GetValue(null);
        previousLimit = InventoryManager.InventoryLimit;
        SaveSystem.SetProvider(new MemorySaveProvider());
        items = (ItemService)Field(typeof(ItemService), "_instance").GetValue(null);
        if (items == null)
        {
            itemHost = new GameObject("Inventory test catalog");
            items = itemHost.AddComponent<ItemService>();
            Field(typeof(ItemService), "_instance").SetValue(null, items);
        }
        if ((DataCacheService)Field(typeof(DataCacheService), "_instance").GetValue(null) == null)
        {
            cacheHost = new GameObject("Inventory test attributes");
            Field(typeof(DataCacheService), "_instance").SetValue(null, cacheHost.AddComponent<DataCacheService>());
        }
        DataCacheService.CleanupEventListeners();
        previousCache = Field(typeof(ItemService), "_itemCache").GetValue(items);
        previousCatalogInitialized = (bool)Field(typeof(ItemService), "_isInitialized").GetValue(items);
        Field(typeof(ItemService), "_itemCache").SetValue(items, catalog);
        Field(typeof(ItemService), "_isInitialized").SetValue(items, true);
        Define("bow", ItemType.Weapon, ItemCategory.Bow, ArmorType.None);
        Define("helmet", ItemType.Armor, ItemCategory.Helmet, ArmorType.Heavy);
        Define("torso", ItemType.Armor, ItemCategory.Torso, ArmorType.Heavy);
        Define("stack", ItemType.Consumable, ItemCategory.None, ArmorType.None);
        fixtureClassId = Guid.NewGuid().ToString("N");
        var classDefinition = ScriptableObject.CreateInstance<HeroClassDefinition>();
        definitions.Add(classDefinition);
        ((Dictionary<string, HeroClassDefinition>)Field(typeof(HeroClassManager), "_classCache").GetValue(null))[fixtureClassId] = classDefinition;
        hero = new HeroData { heroName = "Inventory regression", bronze = 1000, classId = fixtureClassId };
        Bind(hero);
        InventoryManager.OnInventoryChanged += CountChange;
    }

    [TearDown]
    public void TearDown()
    {
        InventoryManager.OnInventoryChanged -= CountChange;
        PlayerSessionService.Clear();
        if (previousPlayer != null) PlayerSessionService.SetPlayer(previousPlayer);
        if (previousHero != null) PlayerSessionService.SetSelectedHero(previousHero);
        if (previousInventoryHero != null) InventoryManager.Initialize(previousInventoryHero);
        else InventoryManager.Reset();
        Field(typeof(InventoryEventService), "_saveAction").SetValue(null, previousSaveAction);
        Field(typeof(ItemService), "_itemCache").SetValue(items, previousCache);
        Field(typeof(ItemService), "_isInitialized").SetValue(items, previousCatalogInitialized);
        SaveSystem.SetProvider(previousProvider);
        InventoryManager.InventoryLimit = previousLimit;
        ((Dictionary<string, HeroClassDefinition>)Field(typeof(HeroClassManager), "_classCache").GetValue(null)).Remove(fixtureClassId);
        foreach (var definition in definitions) UnityEngine.Object.DestroyImmediate(definition);
        definitions.Clear();
        catalog.Clear();
        changes = saves = 0;
        if (itemHost != null) UnityEngine.Object.DestroyImmediate(itemHost);
        if (cacheHost != null) UnityEngine.Object.DestroyImmediate(cacheHost);
        itemHost = cacheHost = null;
    }

    [Test]
    public void SplittingCreatesASeparateStackAndPreservesTotal()
    {
        var stack = Add("stack", 10);
        ResetCounts();
        Assert.That(InventoryManager.SplitStack(stack, 4), Is.True);
        Assert.That(hero.inventory.Count, Is.EqualTo(2));
        Assert.That(stack.quantity, Is.EqualTo(6));
        Assert.That(hero.inventory[1].quantity, Is.EqualTo(4));
        Assert.That(hero.inventory[1].slotIndex, Is.Not.EqualTo(stack.slotIndex));
        AssertSingleChange();
    }

    [Test]
    public void MovingAndRemovingQuantityPublishAndSave()
    {
        var stack = Add("stack", 10);
        ResetCounts();
        Assert.That(InventoryManager.MoveItemToSlot(stack, 3), Is.True);
        AssertSingleChange();
        ResetCounts();
        Assert.That(InventoryManager.RemoveItem("stack", 2), Is.True);
        Assert.That(stack.quantity, Is.EqualTo(8));
        AssertSingleChange();
    }

    [Test]
    public void IncompatibleEquipmentCannotLeaveAPartialChange()
    {
        InventoryManager.InventoryLimit = 2;
        var bow = Add("bow");
        Add("stack", 1);
        var helmet = Make("helmet");
        var torso = Make("torso");
        hero.equipment.helmet = helmet;
        hero.equipment.torso = torso;
        ResetCounts();
        Assert.That(InventoryManager.EquipItem(bow), Is.False);
        Assert.That(hero.inventory, Does.Contain(bow));
        Assert.That(hero.equipment.weapon, Is.Null);
        Assert.That(hero.equipment.helmet, Is.SameAs(helmet));
        Assert.That(hero.equipment.torso, Is.SameAs(torso));
        Assert.That(changes, Is.Zero);
        Assert.That(saves, Is.Zero);
    }

    [Test]
    public void SessionSelectionRebindsEveryInventoryOperation()
    {
        var player = new PlayerData();
        var other = new HeroData { heroName = "Other inventory" };
        PlayerSessionService.SetPlayer(player);
        PlayerSessionService.SetSelectedHero(other);
        Assert.That(InventoryManager.CreateAndAddItem("stack", 2), Is.True);
        Assert.That(other.inventory.Count, Is.EqualTo(1));
        Assert.That(hero.inventory, Is.Empty);
        Assert.That(InventoryManager.GetCurrentHero(), Is.SameAs(other));
        PlayerSessionService.Clear();
        Assert.That(InventoryManager.IsInitialized, Is.False);
    }

    [Test]
    public void SellingAWholeStackPaysForEveryUnitAndSavesOnce()
    {
        var stack = Add("stack", 5);
        stack.price = 80;
        StoreTransactionService.Initialize(hero);
        int before = hero.bronze;
        ResetCounts();
        var result = StoreTransactionService.SellItem(stack);
        Assert.That(result.Success, Is.True);
        Assert.That(hero.bronze - before, Is.EqualTo(200));
        Assert.That(hero.inventory, Is.Empty);
        AssertSingleChange();
    }

    [Test]
    public void BuyingStackablesWithFullInventoryReusesTheExistingStack()
    {
        InventoryManager.InventoryLimit = 1;
        var stack = Add("stack", 5);
        StoreTransactionService.Initialize(hero);
        ResetCounts();
        Assert.That(StoreTransactionService.PurchaseItem("stack", 2).Success, Is.True);
        Assert.That(stack.quantity, Is.EqualTo(7));
        AssertSingleChange();
    }

    [Test]
    public void SameStackCannotBeCombinedWithItself()
    {
        var stack = Add("stack", 5);
        ResetCounts();
        Assert.That(InventoryManager.CombineStacks(stack, stack), Is.False);
        Assert.That(stack.quantity, Is.EqualTo(5));
        Assert.That(hero.inventory, Does.Contain(stack));
        Assert.That(changes, Is.Zero);
    }

    [Test]
    public void EquippingUsesTheSlotFreedByTheIncomingItem()
    {
        InventoryManager.InventoryLimit = 1;
        var bow = Add("bow");
        var helmet = Make("helmet");
        hero.equipment.helmet = helmet;
        ResetCounts();
        Assert.That(InventoryManager.EquipItem(bow), Is.True);
        Assert.That(hero.equipment.weapon, Is.SameAs(bow));
        Assert.That(hero.equipment.helmet, Is.Null);
        Assert.That(hero.inventory, Is.EquivalentTo(new[] { helmet }));
        Assert.That(helmet.slotIndex, Is.Zero);
        AssertSingleChange();
    }

    [Test]
    public void BattleEquipmentLockRejectsEveryOperationWithoutSavingAndReleasesOutsideBattle()
    {
        var previousWorld = World.DefaultGameObjectInjectionWorld;
        using var world = new World("Battle equipment policy");
        World.DefaultGameObjectInjectionWorld = world;
        try
        {
            var phase = world.EntityManager.CreateEntity(typeof(GameStateComponent));
            world.EntityManager.SetComponentData(phase, new GameStateComponent { currentPhase = GamePhase.Combate });
            var bow = Add("bow");
            var helmet = Make("helmet");
            hero.equipment.helmet = helmet;
            ResetCounts();
            Assert.That(InventoryManager.RequestEquipItem(bow), Is.False);
            Assert.That(EquipmentManagerService.EquipItem(bow), Is.False);
            Assert.That(EquipmentManagerService.UnequipItem(ItemType.Armor, ItemCategory.Helmet), Is.False);
            Assert.That(EquipmentManagerService.SwapEquipment(ItemType.Armor, ItemCategory.Helmet, ItemType.Armor, ItemCategory.Torso), Is.False);
            Assert.That(hero.equipment.helmet, Is.SameAs(helmet));
            Assert.That(hero.inventory, Does.Contain(bow));
            Assert.That(changes, Is.Zero);
            Assert.That(saves, Is.Zero);
            world.EntityManager.SetComponentData(phase, new GameStateComponent { currentPhase = GamePhase.Feudo });
            Assert.That(EquipmentManagerService.EquipItem(bow), Is.True);
        }
        finally { World.DefaultGameObjectInjectionWorld = previousWorld; }
    }

    [Test]
    public void BattleAppearanceSnapshotSurvivesChangesToPersistentAvatarAndEquipment()
    {
        hero.avatar.headId = "initial-head";
        hero.equipment.helmet = Make("helmet");
        var snapshot = HeroAppearanceComponent.Capture(hero.avatar, hero.equipment, "Male");
        hero.avatar.headId = "changed-head";
        hero.equipment.helmet.itemId = "torso";
        hero.equipment.helmet = null;
        Assert.That(snapshot.avatar.headId, Is.EqualTo("initial-head"));
        Assert.That(snapshot.equipment.helmet.itemId, Is.EqualTo("helmet"));
    }

    [Test]
    public void FailedTransactionRestoresCurrencyItemsStatsAndEvents()
    {
        var item = Add("helmet");
        item.SetStat("defense", 12);
        int before = hero.bronze;
        ResetCounts();
        Assert.That(InventoryEventService.RunTransaction(() =>
        {
            CurrencyService.DebitBronze(100);
            InventoryManager.EquipItem(item);
            item.SetStat("defense", 99);
            return false;
        }), Is.False);
        Assert.That(hero.bronze, Is.EqualTo(before));
        Assert.That(hero.inventory, Does.Contain(item));
        Assert.That(hero.equipment.helmet, Is.Null);
        Assert.That(item.GetStat("defense"), Is.EqualTo(12));
        Assert.That(changes, Is.Zero);
        Assert.That(saves, Is.Zero);
    }

    [Test]
    public void PurchaseKeepsTheDisplayedEquipmentInstance()
    {
        var product = Make("helmet");
        product.price = 137;
        product.SetStat("defense", 19);
        int before = hero.bronze;
        ResetCounts();
        Assert.That(StoreTransactionService.PurchaseItem(product).Success, Is.True);
        Assert.That(hero.inventory[0], Is.SameAs(product));
        Assert.That(hero.inventory[0].GetStat("defense"), Is.EqualTo(19));
        Assert.That(hero.bronze, Is.EqualTo(before - 137));
        AssertSingleChange();
    }

    [Test]
    public void SellingPartOfAStackOnlyTouchesTheChosenStack()
    {
        var first = Add("stack", 10);
        var second = Make("stack", 6);
        second.price = 80;
        Assert.That(InventoryManager.AddExistingItemAtSlot(second, 2), Is.True);
        ResetCounts();
        Assert.That(StoreTransactionService.SellItem(second, 2).Success, Is.True);
        Assert.That(first.quantity, Is.EqualTo(10));
        Assert.That(second.quantity, Is.EqualTo(4));
        AssertSingleChange();
    }

    [Test]
    public void ConfirmationDoesNotMutateUntilAcceptedWithoutACallback()
    {
        var bow = Add("bow");
        hero.equipment.helmet = Make("helmet");
        UnityEngine.Events.UnityAction accept = null;
        Action<InventoryItem, string, UnityEngine.Events.UnityAction, UnityEngine.Events.UnityAction> listener =
            (item, message, yes, no) => accept = yes;
        InventoryEventService.OnEquipmentConfirmationRequired += listener;
        try
        {
            ResetCounts();
            Assert.That(InventoryManager.RequestEquipItem(bow), Is.True);
            Assert.That(hero.equipment.weapon, Is.Null);
            Assert.That(changes, Is.Zero);
            Assert.That(accept, Is.Not.Null);
            accept();
            Assert.That(hero.equipment.weapon, Is.SameAs(bow));
            AssertSingleChange();
        }
        finally { InventoryEventService.OnEquipmentConfirmationRequired -= listener; }
    }

    [Test]
    public void StatUpdatesRoundTripThroughTheExistingSaveFormat()
    {
        var item = Make("helmet");
        item.GeneratedStats = new Dictionary<string, float> { ["defense"] = 12 };
        item.SetStat("defense", 22);
        var restored = JsonUtility.FromJson<InventoryItem>(JsonUtility.ToJson(item));
        Assert.That(restored.GetStat("defense"), Is.EqualTo(22));
        Assert.That(restored.instanceId, Is.EqualTo(item.instanceId));
    }

    [Test]
    public void InitializationRepairsDuplicateAndInvalidStackSlots()
    {
        var first = Make("stack", 4);
        var second = Make("stack", 3);
        var third = Make("helmet");
        first.slotIndex = second.slotIndex = 0;
        third.slotIndex = -1;
        hero.inventory.AddRange(new[] { first, second, third });
        Bind(hero);
        Assert.That(new HashSet<int> { first.slotIndex, second.slotIndex, third.slotIndex }.Count, Is.EqualTo(3));
        Assert.That(third.slotIndex, Is.GreaterThanOrEqualTo(0));
        Assert.That(first.quantity + second.quantity, Is.EqualTo(7));
    }

    [Test]
    public void LastConsumedUnitFreesItsSlotForEquipmentReward()
    {
        var grant = ScriptableObject.CreateInstance<EquipmentGrantEffect>();
        definitions.Add(grant);
        Field(typeof(EquipmentGrantEffect), "targetItemId").SetValue(grant, "helmet");
        Field(typeof(ItemDataSO), "_effects").SetValue(catalog["stack"], new ItemEffect[] { grant });
        InventoryManager.InventoryLimit = 1;
        var box = Add("stack");
        ResetCounts();
        Assert.That(InventoryManager.UseConsumable(box), Is.True);
        Assert.That(hero.inventory.Count, Is.EqualTo(1));
        Assert.That(hero.inventory[0].itemId, Is.EqualTo("helmet"));
        AssertSingleChange();
    }

    [Test]
    public void NonConsumingItemsKeepTheirQuantityAfterUse()
    {
        var currency = ScriptableObject.CreateInstance<CurrencyEffect>();
        definitions.Add(currency);
        Field(typeof(ItemDataSO), "_effects").SetValue(catalog["stack"], new ItemEffect[] { currency });
        Field(typeof(ItemDataSO), "_consumeOnUse").SetValue(catalog["stack"], false);
        var item = Add("stack", 3);
        ResetCounts();
        Assert.That(InventoryManager.UseConsumable(item), Is.True);
        Assert.That(item.quantity, Is.EqualTo(3));
        Assert.That(hero.inventory, Does.Contain(item));
        AssertSingleChange();
    }

    [Test]
    public void RewardFailureRestoresTheConsumedBoxAndEarlierEffects()
    {
        var grant = ScriptableObject.CreateInstance<EquipmentGrantEffect>();
        definitions.Add(grant);
        Field(typeof(EquipmentGrantEffect), "targetItemId").SetValue(grant, "helmet");
        Field(typeof(ItemDataSO), "_effects").SetValue(catalog["stack"], new ItemEffect[] { grant, grant });
        InventoryManager.InventoryLimit = 1;
        var box = Add("stack");
        ResetCounts();
        UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "[EquipmentGrantEffect] Cannot execute EquipmentGrantEffect - validation failed");
        UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "[ItemEffectSystem] Failed to execute effect: ");
        Assert.That(InventoryManager.UseConsumable(box), Is.False);
        Assert.That(hero.inventory, Is.EquivalentTo(new[] { box }));
        Assert.That(box.quantity, Is.EqualTo(1));
        Assert.That(changes, Is.Zero);
        Assert.That(saves, Is.Zero);
    }

    void Bind(HeroData target)
    {
        InventoryManager.Initialize(target);
        InventoryEventService.Initialize(target, () => saves++);
    }

    [Test]
    public void DirectEquipmentGrantAlsoRevertsWhenTheWholeRewardDoesNotFit()
    {
        var grant = ScriptableObject.CreateInstance<EquipmentGrantEffect>();
        definitions.Add(grant);
        Field(typeof(EquipmentGrantEffect), "targetItemId").SetValue(grant, "helmet");
        InventoryManager.InventoryLimit = 1;
        ResetCounts();
        Assert.That(grant.Execute(hero, 2), Is.False);
        Assert.That(hero.inventory, Is.Empty);
        Assert.That(changes, Is.Zero);
        Assert.That(saves, Is.Zero);
    }

    [Test]
    public void EquipmentNotificationIsCoalescedAndNotPublishedForRollbackOrBackpackChange()
    {
        int equipmentChanges = 0;
        Action listener = () => equipmentChanges++;
        InventoryEventService.OnEquipmentChanged += listener;
        try
        {
            var item = Add("bow");
            Assert.That(equipmentChanges, Is.Zero);
            Assert.That(InventoryEventService.RunTransaction(() =>
            {
                InventoryEventService.TriggerItemEquipped(item);
                InventoryEventService.TriggerItemUnequipped(item);
                return true;
            }), Is.True);
            Assert.That(equipmentChanges, Is.EqualTo(1));
            Assert.That(InventoryEventService.RunTransaction(() =>
            {
                InventoryEventService.TriggerItemEquipped(item);
                return false;
            }), Is.False);
            Assert.That(equipmentChanges, Is.EqualTo(1));
        }
        finally { InventoryEventService.OnEquipmentChanged -= listener; }
    }

    [Test]
    public void SameNameHeroesKeepDistinctCachesAndIdentitySurvivesRenameAndJsonRoundTrip()
    {
        var first = new HeroData { heroName = "Same name", strength = 3, classId = fixtureClassId };
        var second = new HeroData { heroName = "Same name", strength = 9, classId = fixtureClassId };
        string firstId = HeroAttributeValidator.GetHeroId(first);
        string secondId = HeroAttributeValidator.GetHeroId(second);
        Assert.That(firstId, Is.Not.EqualTo(secondId));
        DataCacheService.CacheAttributes(first);
        DataCacheService.CacheAttributes(second);
        Assert.That(DataCacheService.GetHeroCalculatedAttributes(firstId).finalStrength,
            Is.EqualTo(DataCacheService.CalculateAttributes(first).finalStrength));
        Assert.That(DataCacheService.GetHeroCalculatedAttributes(secondId).finalStrength,
            Is.EqualTo(DataCacheService.CalculateAttributes(second).finalStrength));
        first.heroName = "Renamed";
        Assert.That(HeroAttributeValidator.GetHeroId(first), Is.EqualTo(firstId));
        var loaded = JsonUtility.FromJson<HeroData>(JsonUtility.ToJson(first));
        Assert.That(HeroAttributeValidator.GetHeroId(loaded), Is.EqualTo(firstId));
        var legacy = JsonUtility.FromJson<HeroData>("{\"heroName\":\"Legacy\"}");
        Assert.That(HeroAttributeValidator.GetHeroId(legacy), Is.Not.Empty);
    }

    [Test]
    public void SavingPlayerDoesNotEvictAttributeCache()
    {
        var id = HeroAttributeValidator.GetHeroId(hero);
        DataCacheService.CacheAttributes(hero);
        var before = DataCacheService.GetHeroCalculatedAttributes(id);
        SaveSystem.SavePlayer(new PlayerData { heroes = new List<HeroData> { hero } });
        Assert.That(DataCacheService.GetHeroCalculatedAttributes(id).maxHealth, Is.EqualTo(before.maxHealth));
    }

    [Test]
    public void EquipmentCommitUpdatesAttributesBeforeGeneralInventoryNotification()
    {
        PlayerSessionService.SetPlayer(new PlayerData { heroes = new List<HeroData> { hero } });
        PlayerSessionService.SetSelectedHero(hero);
        var bow = Add("bow");
        bow.GeneratedStats = new Dictionary<string, float> { ["Strength"] = 7 };
        var id = HeroAttributeValidator.GetHeroId(hero);
        float before = DataCacheService.GetHeroCalculatedAttributes(id).finalStrength;
        float observed = before;
        Action observer = () => observed = DataCacheService.GetHeroCalculatedAttributes(id).finalStrength;
        InventoryManager.OnInventoryChanged += observer;
        try
        {
            Assert.That(InventoryManager.EquipItem(bow), Is.True);
            Assert.That(observed, Is.GreaterThan(before));
            Assert.That(observed, Is.EqualTo(DataCacheService.CalculateAttributes(hero).finalStrength));
        }
        finally { InventoryManager.OnInventoryChanged -= observer; }
    }
    [Test]
    public void TemporaryAttributesUseGuidAndDoNotBorrowTheSelectedNamesake()
    {
        var second = new HeroData { heroName = hero.heroName, classId = fixtureClassId, strength = 9 };
        var player = new PlayerData { heroes = new() { hero, second } };
        PlayerSessionService.SetPlayer(player);
        PlayerSessionService.SetSelectedHero(hero);
        string id = hero.heroId;
        float original = DataCacheService.GetBaseWithEquipment(hero, "strength");
        try
        {
            HeroTempAttributeService.ApplyTempChange(id, "strength", original + 5);
            Assert.That(HeroTempAttributeService.GetFinalAttributeValue(id, "strength"), Is.EqualTo(original + 5));
            Assert.That(HeroTempAttributeService.GetAttributesWithTempChanges(id).maxHealth, Is.GreaterThan(0));
            Assert.That(HeroTempAttributeService.CalculateUsedTempPoints(id, hero), Is.EqualTo(5));
            Assert.That(HeroTempAttributeService.CalculateUsedTempPoints(id, second), Is.Zero);
            hero.heroName = "Renamed";
            Assert.That(HeroTempAttributeService.GetFinalAttributeValue(id, "strength"), Is.EqualTo(original + 5));
            PlayerSessionService.SetSelectedHero(second);
            Assert.That(HeroTempAttributeService.GetFinalAttributeValue(id, "strength"), Is.Zero);
            Assert.That(HeroTempAttributeService.GetTempChangesAsEquipmentBonuses(id).strengthBonus, Is.Zero);
        }
        finally { HeroTempAttributeService.ClearTempChanges(id); }
    }

    [Test]
    public void HeroExperienceViewUsesSavedLevelAndRemainingXpWithTheBattleThreshold()
    {
        hero.level = 2;
        hero.currentXP = 50;
        var info = HeroExperienceCalculator.GetExperienceInfo(hero);
        Assert.That(info.currentLevel, Is.EqualTo(2));
        Assert.That(info.xpInCurrentLevel, Is.EqualTo(50));
        Assert.That(info.xpNeededForNextLevel, Is.EqualTo(120));
        Assert.That(info.progressToNextLevel, Is.EqualTo(50f / 120f).Within(0.0001f));
        hero.level = 7;
        Assert.That(HeroExperienceCalculator.GetExperienceInfo(hero).xpNeededForNextLevel,
            Is.EqualTo(HeroExperienceCalculator.GetBattleXPForNextLevel(7)));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void SquadXpCommitsToCapturedHeroAndAppearsInTheNextBootstrapSnapshot(bool active)
    {
        var owned = new SquadInstanceData { id = "owned-guid", baseSquadID = "base", unitsInSquad = 12 };
        hero.squadProgress.Add(owned);
        var second = new HeroData { heroName = hero.heroName, classId = fixtureClassId };
        var other = new SquadInstanceData { id = "other-guid", baseSquadID = "base" };
        second.squadProgress.Add(other);
        var player = new PlayerData { heroes = new() { hero, second },
            battleProgress = new LocalSaveSystem.PlayerProgressData { level = 8 } };
        var provider = new MemorySaveProvider { Player = player };
        SaveSystem.SetProvider(provider);
        PlayerSessionService.SetPlayer(player);
        PlayerSessionService.SetSelectedHero(second);
        using var xpWorld = new World("Owned squad XP");
        var manager = xpWorld.EntityManager;
        Entity squad = CreateAddressedSquad(manager, hero, owned);
        // Exercise the actual local-hero -> DataContainer bridge too.
        var localHero = manager.CreateEntity(typeof(IsLocalPlayer));
        manager.SetComponentData(squad, new SquadOwnerComponent { hero = localHero });
        if (!active) manager.RemoveComponent<IsLocalSquadActive>(squad);
        var reward = manager.CreateEntity(typeof(SquadXPEvent));
        manager.SetComponentData(reward, new SquadXPEvent { squad = squad, amount = 150 });
        var system = xpWorld.GetOrCreateSystemManaged<SquadProgressionSystem>();
        system.Update();
        Assert.That(manager.Exists(reward), Is.False);
        Assert.That(owned.level, Is.EqualTo(2));
        Assert.That(owned.experience, Is.EqualTo(50));
        Assert.That(owned.progressRevision, Is.EqualTo(1));
        Assert.That(other.level, Is.EqualTo(1));
        Assert.That(provider.Player.battleProgress.squads, Is.Empty);
        var persisted = HeroDataService.FindById(provider.Player, hero.heroId);
        var participant = new BattleHeroData { heroId = persisted.heroId, heroName = persisted.heroName,
            squadInstances = persisted.squadProgress };
        var battle = new BattleData();
        battle.attackers.Add(participant);
        BattleBootstrapRequests.SubmitLocal(battle, participant.heroName, participant.heroId);
        xpWorld.GetOrCreateSystemManaged<BattleBootstrapSystem>().Update();
        using var containers = manager.CreateEntityQuery(typeof(DataContainerComponent));
        var next = manager.GetBuffer<SquadIdMapElement>(containers.GetSingletonEntity());
        Assert.That(next[0].persistentId.ToString(), Is.EqualTo(owned.id));
        Assert.That(next[0].level, Is.EqualTo(2));
        Assert.That(next[0].currentXP, Is.EqualTo(50));
        system.Update();
        Assert.That(provider.SaveCount, Is.EqualTo(1));
    }

    [Test]
    public void ASecondFailedSquadRewardCannotReplayTheFirstCommittedReward()
    {
        var owned = new SquadInstanceData { id = "owned-guid", baseSquadID = "base" };
        hero.squadProgress.Add(owned);
        var player = new PlayerData { heroes = new() { hero } };
        var provider = new MemorySaveProvider { Player = player, FailOnSaveAttempt = 2 };
        SaveSystem.SetProvider(provider);
        PlayerSessionService.SetPlayer(player);
        using var xpWorld = new World("Squad reward partial batch retry");
        var manager = xpWorld.EntityManager;
        Entity squad = CreateAddressedSquad(manager, hero, owned);
        for (int i = 0; i < 2; i++)
            manager.SetComponentData(manager.CreateEntity(typeof(SquadXPEvent)), new SquadXPEvent { squad = squad, amount = 75 });
        var system = xpWorld.GetOrCreateSystemManaged<SquadProgressionSystem>();
        Assert.Throws<System.IO.IOException>(() => system.Update());
        using var pending = manager.CreateEntityQuery(typeof(SquadXPEvent));
        Assert.That(pending.CalculateEntityCount(), Is.EqualTo(1));
        Assert.That(manager.GetComponentData<SquadProgressComponent>(squad).currentXP, Is.EqualTo(75));
        Assert.That(owned.experience, Is.EqualTo(75));
        provider.FailOnSaveAttempt = 0;
        system.Update();
        Assert.That(pending.CalculateEntityCount(), Is.Zero);
        Assert.That(owned.level, Is.EqualTo(2));
        Assert.That(owned.experience, Is.EqualTo(50));
        Assert.That(owned.progressRevision, Is.EqualTo(2));
        Assert.That(provider.SaveCount, Is.EqualTo(2));
        using var levels = manager.CreateEntityQuery(typeof(SquadLevelUpEvent));
        Assert.That(levels.CalculateEntityCount(), Is.EqualTo(1));
    }

    [Test]
    public void RecycledSquadLocalIdCannotAwardXpToAnotherPersistentInstance()
    {
        var owned = new SquadInstanceData { id = "current-guid", baseSquadID = "base" };
        hero.squadProgress.Add(owned);
        var provider = new MemorySaveProvider { Player = new PlayerData { heroes = new() { hero } } };
        SaveSystem.SetProvider(provider);
        using var xpWorld = new World("Recycled squad ID");
        var manager = xpWorld.EntityManager;
        var squad = CreateAddressedSquad(manager, hero, owned);
        manager.SetComponentData(squad, new SquadInstanceComponent { id = 0, persistentId = new FixedString64Bytes("old-guid") });
        var reward = manager.CreateEntity(typeof(SquadXPEvent));
        manager.SetComponentData(reward, new SquadXPEvent { squad = squad, amount = 150 });
        xpWorld.GetOrCreateSystemManaged<SquadProgressionSystem>().Update();
        Assert.That(manager.Exists(reward), Is.False);
        Assert.That(manager.GetComponentData<SquadProgressComponent>(squad).currentXP, Is.Zero);
        Assert.That(owned.experience, Is.Zero);
        Assert.That(provider.SaveCount, Is.Zero);
    }

    [Test]
    public void SquadXpRejectsUnknownAndAmbiguousOwnershipWithoutWriting()
    {
        var duplicate = new SquadInstanceData { id = "duplicate-guid" };
        hero.squadProgress.Add(duplicate);
        hero.squadProgress.Add(new SquadInstanceData { id = duplicate.id });
        var provider = new MemorySaveProvider { Player = new PlayerData { heroes = new() { hero } } };
        SaveSystem.SetProvider(provider);
        Assert.That(SaveSystem.TryUpdateSquadProgress(hero.heroId, "unknown", 2, 50), Is.False);
        Assert.That(SaveSystem.TryUpdateSquadProgress("another-hero", duplicate.id, 2, 50), Is.False);
        Assert.That(SaveSystem.TryUpdateSquadProgress(hero.heroId, duplicate.id, 2, 50), Is.False);
        Assert.That(provider.SaveCount, Is.Zero);
    }

    static Entity CreateAddressedSquad(EntityManager manager, HeroData owner, SquadInstanceData data)
    {
        var container = manager.CreateEntity(typeof(DataContainerComponent), typeof(HeroProgressComponent));
        manager.SetComponentData(container, new HeroProgressComponent { heroId = new FixedString64Bytes(owner.heroId), level = owner.level });
        var map = manager.AddBuffer<SquadIdMapElement>(container);
        map.Add(new SquadIdMapElement { squadId = 0, persistentId = new FixedString64Bytes(data.id),
            level = data.level, currentXP = data.experience });
        var squad = manager.CreateEntity(typeof(SquadOwnerComponent), typeof(SquadInstanceComponent),
            typeof(SquadProgressComponent), typeof(IsLocalSquadActive));
        manager.SetComponentData(squad, new SquadOwnerComponent { hero = container });
        manager.SetComponentData(squad, new SquadInstanceComponent { id = 0, persistentId = new FixedString64Bytes(data.id) });
        manager.SetComponentData(squad, new SquadProgressComponent { level = data.level, currentXP = data.experience,
            xpToNextLevel = SquadProgressionCurves.DefaultXPToNext(data.level) });
        return squad;
    }

    [Test]
    public void HeroXpUsesCapturedRecipientAfterSelectionChangesAndConsumesOnce()
    {
        var second = new HeroData { heroName = hero.heroName, classId = fixtureClassId };
        var player = new PlayerData { heroes = new() { hero, second },
            battleProgress = new LocalSaveSystem.PlayerProgressData { level = 8, currentXP = 90 } };
        var provider = new MemorySaveProvider { Player = player };
        SaveSystem.SetProvider(provider);
        PlayerSessionService.SetPlayer(player);
        PlayerSessionService.SetSelectedHero(second);
        using var xpWorld = new World("Addressed hero XP");
        var manager = xpWorld.EntityManager;
        manager.CreateEntity(typeof(MatchStateComponent));
        var owner = manager.CreateEntity(typeof(HeroProgressComponent));
        manager.SetComponentData(owner, new HeroProgressComponent
        { heroId = new FixedString64Bytes(hero.heroId), level = 1, xpToNextLevel = 100 });
        var reward = manager.CreateEntity(typeof(XPEventComponent));
        manager.SetComponentData(reward, new XPEventComponent { heroId = new FixedString64Bytes(hero.heroId), amount = 150 });
        var unrelated = manager.CreateEntity(typeof(XPEventComponent));
        manager.SetComponentData(unrelated, new XPEventComponent { heroId = new FixedString64Bytes(second.heroId), amount = 999 });
        var unaddressed = manager.CreateEntity(typeof(XPEventComponent));
        manager.SetComponentData(unaddressed, new XPEventComponent { amount = 999 });
        var system = xpWorld.GetOrCreateSystemManaged<HeroLevelSystem>();
        system.Update();
        Assert.That(manager.Exists(reward), Is.False);
        Assert.That(manager.Exists(unrelated), Is.False);
        Assert.That(manager.Exists(unaddressed), Is.False);
        Assert.That(hero.level, Is.EqualTo(2));
        Assert.That(hero.currentXP, Is.EqualTo(50));
        Assert.That(hero.perkPoints, Is.EqualTo(1));
        Assert.That(hero.progressRevision, Is.EqualTo(1));
        Assert.That(second.level, Is.EqualTo(1));
        Assert.That(second.currentXP, Is.Zero);
        Assert.That(PlayerSessionService.SelectedHero, Is.SameAs(second));
        Assert.That(provider.Player.battleProgress.level, Is.EqualTo(8));
        Assert.That(provider.Player.battleProgress.currentXP, Is.EqualTo(90));
        system.Update();
        Assert.That(provider.SaveCount, Is.EqualTo(1));
        Assert.That(manager.GetComponentData<HeroProgressComponent>(owner).currentXP, Is.EqualTo(50));
    }

    [Test]
    public void FailedHeroXpCommitKeepsEventAndProgressForOneSuccessfulRetry()
    {
        var player = new PlayerData { heroes = new() { hero } };
        var provider = new MemorySaveProvider { Player = player, FailSave = true };
        SaveSystem.SetProvider(provider);
        using var xpWorld = new World("Failed hero XP commit");
        var manager = xpWorld.EntityManager;
        manager.CreateEntity(typeof(MatchStateComponent));
        var owner = manager.CreateEntity(typeof(HeroProgressComponent));
        manager.SetComponentData(owner, new HeroProgressComponent
        { heroId = new FixedString64Bytes(hero.heroId), level = 1, xpToNextLevel = 100 });
        var reward = manager.CreateEntity(typeof(XPEventComponent));
        manager.SetComponentData(reward, new XPEventComponent { heroId = new FixedString64Bytes(hero.heroId), amount = 150 });
        var system = xpWorld.GetOrCreateSystemManaged<HeroLevelSystem>();
        Assert.Throws<System.IO.IOException>(() => system.Update());
        Assert.That(manager.Exists(reward), Is.True);
        Assert.That(manager.GetComponentData<HeroProgressComponent>(owner).level, Is.EqualTo(1));
        Assert.That(hero.level, Is.EqualTo(1));
        provider.FailSave = false;
        system.Update();
        Assert.That(manager.Exists(reward), Is.False);
        Assert.That(HeroDataService.FindById(provider.Player, hero.heroId).currentXP, Is.EqualTo(50));
        Assert.That(provider.SaveCount, Is.EqualTo(1));
    }

    [Test]
    public void HeroXpRejectsUnknownAndDuplicateIdsWithoutWriting()
    {
        var duplicate = new HeroData { heroId = hero.heroId, heroName = "Another" };
        var provider = new MemorySaveProvider { Player = new PlayerData { heroes = new() { hero, duplicate } } };
        SaveSystem.SetProvider(provider);
        Assert.That(SaveSystem.TryUpdateHeroProgress("unknown", 2, 50, 1), Is.False);
        Assert.That(SaveSystem.TryUpdateHeroProgress(hero.heroId, 2, 50, 1), Is.False);
        Assert.That(SaveSystem.TryUpdateHeroProgress(hero.heroId, 0, -1, -1), Is.False);
        Assert.That(provider.SaveCount, Is.Zero);
        Assert.That(hero.level, Is.EqualTo(1));
    }

    [Test]
    public void SecondBattleBootstrapRebindsProgressAndRejectsOldRecipients()
    {
        var second = new HeroData { heroName = hero.heroName, classId = fixtureClassId, currentXP = 16 };
        hero.currentXP = 65;
        var player = new PlayerData { heroes = new() { hero, second } };
        var provider = new MemorySaveProvider { Player = player };
        SaveSystem.SetProvider(provider);
        PlayerSessionService.SetPlayer(player);
        PlayerSessionService.SetSelectedHero(second);
        using var xpWorld = new World("Second battle XP binding");
        var manager = xpWorld.EntityManager;
        manager.CreateEntity(typeof(MatchStateComponent));
        var container = manager.CreateEntity(typeof(DataContainerComponent), typeof(HeroProgressComponent));
        var bootstrap = xpWorld.GetOrCreateSystemManaged<BattleBootstrapSystem>();
        foreach (var current in new[] { hero, second })
        {
            var participant = new BattleHeroData { heroId = current == hero ? string.Empty : current.heroId, heroName = current.heroName };
            participant.squadInstances.Add(new SquadInstanceData { id = "instance", baseSquadID = "squad" });
            var battle = new BattleData();
            battle.attackers.Add(participant);
            BattleBootstrapRequests.SubmitLocal(battle, participant.heroName, current.heroId);
            bootstrap.Update();
            var bound = manager.GetComponentData<HeroProgressComponent>(container);
            Assert.That(bound.heroId.ToString(), Is.EqualTo(current.heroId));
            Assert.That(bound.currentXP, Is.EqualTo(current.currentXP));
        }
        var staleReward = manager.CreateEntity(typeof(XPEventComponent));
        manager.SetComponentData(staleReward, new XPEventComponent { heroId = new FixedString64Bytes(hero.heroId), amount = 99 });
        xpWorld.GetOrCreateSystemManaged<HeroLevelSystem>().Update();
        Assert.That(manager.Exists(staleReward), Is.False);
        Assert.That(second.currentXP, Is.EqualTo(16));
        Assert.That(provider.SaveCount, Is.Zero);
    }

    [Test]
    public void LegacyIdentityIsCommittedByBootstrapBeforeTheFirstAddressedReward()
    {
        string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "conquest-identity-" + Guid.NewGuid().ToString("N"));
        string path = System.IO.Path.Combine(directory, "player_save.json");
        System.IO.Directory.CreateDirectory(directory);
        try
        {
            string json = "{\"heroes\":[{\"heroName\":\"Legacy\",\"classId\":\"" + fixtureClassId + "\",\"level\":1}]}";
            System.IO.File.WriteAllText(path, json);
            var provider = new LocalSaveProvider(path);
            SaveSystem.SetProvider(provider);
            var loaded = provider.Load();
            Assert.That(loaded.requiresIdentitySave, Is.True);
            Assert.That(System.IO.File.ReadAllText(path), Is.EqualTo(json), "Loading cannot write identity.");
            var legacyHero = loaded.heroes[0];
            string capturedId = HeroAttributeValidator.GetHeroId(legacyHero);
            PlayerSessionService.SetPlayer(loaded);
            PlayerSessionService.SetSelectedHero(legacyHero);
            using var xpWorld = new World("Legacy identity commit");
            var manager = xpWorld.EntityManager;
            manager.CreateEntity(typeof(MatchStateComponent));
            manager.CreateEntity(typeof(DataContainerComponent));
            var battle = new BattleData();
            var participant = new BattleHeroData { heroName = legacyHero.heroName };
            participant.squadInstances.Add(new SquadInstanceData { id = "instance", baseSquadID = "squad" });
            battle.attackers.Add(participant);
            BattleBootstrapRequests.SubmitLocal(battle, participant.heroName, capturedId);
            xpWorld.GetOrCreateSystemManaged<BattleBootstrapSystem>().Update();
            Assert.That(loaded.requiresIdentitySave, Is.False);
            Assert.That(provider.Load().requiresIdentitySave, Is.False);
            Assert.That(HeroDataService.FindById(provider.Load(), capturedId), Is.Not.Null);
            Assert.That(System.IO.File.ReadAllText(path + ".bak"), Is.EqualTo(json));
            var reward = manager.CreateEntity(typeof(XPEventComponent));
            manager.SetComponentData(reward, new XPEventComponent { heroId = new FixedString64Bytes(capturedId), amount = 150 });
            xpWorld.GetOrCreateSystemManaged<HeroLevelSystem>().Update();
            Assert.That(manager.Exists(reward), Is.False);
            var final = HeroDataService.FindById(provider.Load(), capturedId);
            Assert.That(final.level, Is.EqualTo(2));
            Assert.That(final.currentXP, Is.EqualTo(50));
        }
        finally
        {
            foreach (var suffix in new[] { "", ".bak", ".tmp" })
                if (System.IO.File.Exists(path + suffix)) System.IO.File.Delete(path + suffix);
            System.IO.Directory.Delete(directory);
        }
    }

    void CountChange() => changes++;
    void ResetCounts() { changes = 0; saves = 0; }
    void AssertSingleChange()
    {
        Assert.That(changes, Is.EqualTo(1));
        Assert.That(saves, Is.EqualTo(1));
    }
    InventoryItem Add(string id, int quantity = 1)
    {
        var item = Make(id, quantity);
        Assert.That(InventoryManager.AddExistingItem(item), Is.True);
        return item;
    }
    InventoryItem Make(string id, int quantity = 1)
    {
        return new InventoryItem { itemId = id, itemType = catalog[id].itemType,
            quantity = quantity, instanceId = id == "stack" ? null : Guid.NewGuid().ToString() };
    }
    void Define(string id, ItemType type, ItemCategory category, ArmorType armor)
    {
        var definition = ScriptableObject.CreateInstance<ItemDataSO>();
        definitions.Add(definition);
        Field(typeof(ItemDataSO), "_id").SetValue(definition, id);
        Field(typeof(ItemDataSO), "_name").SetValue(definition, id);
        Field(typeof(ItemDataSO), "_itemType").SetValue(definition, type);
        Field(typeof(ItemDataSO), "_itemCategory").SetValue(definition, category);
        Field(typeof(ItemDataSO), "_armorType").SetValue(definition, armor);
        Field(typeof(ItemDataSO), "_stackable").SetValue(definition, id == "stack");
        if (id != "stack")
        {
            var generator = ScriptableObject.CreateInstance<BasicWeaponStatGenerator>();
            definitions.Add(generator);
            Field(typeof(ItemDataSO), "_statGenerator").SetValue(definition, generator);
        }
        catalog.Add(id, definition);
    }
    static FieldInfo Field(Type type, string name) => type.GetField(name,
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);

    sealed class MemorySaveProvider : ISaveProvider
    {
        public PlayerData Player;
        public int SaveCount;
        public bool FailSave;
        public int SaveAttempts, FailOnSaveAttempt;
        public void Save(PlayerData data)
        {
            SaveAttempts++;
            if (FailSave || SaveAttempts == FailOnSaveAttempt) throw new System.IO.IOException("Fixture save failure");
            Player = data;
            SaveCount++;
        }
        public PlayerData Load() => Player;
    }
}

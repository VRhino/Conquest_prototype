#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Persistence;
using Data.Items;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEditor.SceneManagement;

public class InventoryUiIntegrationTests
{
    const string FeudoPath = "Assets/Scenes/GameScenes/FeudoScene.unity";
    const string PrepPath = "Assets/Scenes/GameScenes/BattlePrepScene.unity";
    HeroData hero, previousHero;
    PlayerData previousPlayer;
    ISaveProvider previousSave, previousLoad;
    World previousWorld, testWorld;
    readonly List<ScriptableObject> runtimeData = new();
    string loadedPath;
    MemorySave memory;
    CursorLockMode previousCursor;
    bool previousCursorVisible;

    sealed class MemorySave : ISaveProvider
    {
        public PlayerData Player;
        public int Saves;
        public void Save(PlayerData data) { Player = data; Saves++; }
        public PlayerData Load() => Player;
    }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        previousCursor = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        previousSave = Provider(typeof(SaveSystem));
        previousLoad = Provider(typeof(LoadSystem));
        previousPlayer = PlayerSessionService.CurrentPlayer;
        previousHero = PlayerSessionService.SelectedHero;
        memory = new MemorySave();
        SaveSystem.SetProvider(memory);
        LoadSystem.SetProvider(memory);
        hero = TestHeroPool.Load()[0];
        hero.inventory.Clear();
        hero.equipment = new Equipment();
        hero.bronze = 100000;
        var reserve = SquadDataService.CreateSquadInstance("sqd01");
        hero.squadProgress.Clear();
        hero.squadProgress.Add(reserve);
        hero.loadouts.Clear();
        var loadout = new LoadoutSaveData { name = "Inventory UI", isActive = true };
        loadout.squadInstanceIDs.Add(reserve.id);
        hero.loadouts.Add(loadout);
        memory.Player = new PlayerData { playerName = "Inventory UI fixture" };
        memory.Player.heroes.Add(hero);
        PlayerSessionService.SetPlayer(memory.Player);
        PlayerSessionService.SetSelectedHero(hero);
        previousWorld = World.DefaultGameObjectInjectionWorld;
        if (previousWorld != null && previousWorld.IsCreated)
            ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(previousWorld);
        testWorld = DefaultWorldInitialization.Initialize("Inventory UI scene regression");
        // These tests exercise scene UI; battle spawning is covered by BattleSceneIntegrationTests.
        testWorld.GetExistingSystemManaged<HeroSpawnSystem>().Enabled = false;
        yield return null;
    }

    [UnityTest]
    public IEnumerator FeudoInventoryStoreSelectorAndDelayedTooltipUseCurrentItems()
    {
        yield return Load(FeudoPath);
        var panel = Find<InventoryPanelController>();
        var definition = ItemService.GetItemsByType(ItemType.Weapon).First(d => d.IsEquipment);
        var item = ItemInstanceService.CreateItem(definition.id);
        item.GeneratedStats = new Dictionary<string, float> { ["damage"] = 17 };
        Assert.That(InventoryManager.AddExistingItem(item), Is.True);
        panel.OpenPanel(hero);
        Assert.That(panel.CellControllers.Count, Is.EqualTo(InventoryManager.InventoryLimit));
        Assert.That(InventoryManager.MoveItemToSlot(item, 2), Is.True);
        var cell = panel.CellControllers[2];
        Assert.That(cell.CurrentItem, Is.SameAs(item));
        Assert.That(panel.CellControllers[0].CurrentItem, Is.Null);
        var interaction = cell.GetComponent<InventoryItemCellInteraction>();
        var pointer = new PointerEventData(EventSystem.current) { position = new Vector2(600, 300) };
        var manager = panel.tooltipManager;
        var primary = (InventoryTooltipController)Field(typeof(TooltipManager), "primaryTooltipController").GetValue(manager);
        float delay = (float)Field(typeof(TooltipManager), "hoverDelay").GetValue(manager);
        interaction.OnPointerEnter(pointer);
        Assert.That(primary.IsShowing, Is.False, "Hover delay was skipped.");
        interaction.OnPointerExit(pointer);
        yield return new WaitForSeconds(delay + 0.1f);
        Assert.That(primary.IsShowing, Is.False, "An abandoned hover appeared later.");
        interaction.OnPointerEnter(pointer);
        yield return new WaitForSeconds(delay + 0.2f);
        Assert.That(primary.IsShowing, Is.True);
        Assert.That(primary.LifecycleManager.CurrentItem, Is.SameAs(item));
        var entries = primary.statsContainer.GetComponentsInChildren<TMPro.TMP_Text>(true);
        var entryIds = entries.Select(t => t.GetEntityId()).ToArray();
        item.SetStat("damage", 23);
        manager.ForceValidateTooltip();
        yield return null;
        yield return null;
        var refreshed = primary.statsContainer.GetComponentsInChildren<TMPro.TMP_Text>(true);
        Assert.That(refreshed.Select(t => t.GetEntityId()), Is.EqualTo(entryIds), "Stat rows were recreated.");
        Assert.That(refreshed.Any(t => t.text.Contains("23")), Is.True, "Reused hidden rows kept their old text.");
        cell.SetItem(null, null);
        Assert.That(primary.IsShowing, Is.False);
        Assert.That(cell.CurrentItemData, Is.Null);
        panel.OpenPanel(hero);

        var selector = Find<ItemSelectorController>();
        selector.Initialize();
        selector.Show();
        selector.SetItems(new List<InventoryItem> { item });
        int clicks = 0;
        selector.SetEvents((i,d) => clicks++);
        selector.SetEvents((i,d) => clicks++);
        var selectedCell = selector.GetComponentsInChildren<ItemCellController>(true).First(c => c.CurrentItem != null);
        selectedCell.GetComponent<ItemCellInteraction>().OnPointerClick(pointer);
        Assert.That(clicks, Is.EqualTo(1));
        selector.SetItems(new List<InventoryItem>());
        selectedCell.GetComponent<ItemCellInteraction>().OnPointerClick(pointer);
        Assert.That(clicks, Is.EqualTo(1), "Empty cell retained its old item/callback.");
        selector.Hide();

        var store = Find<UIStoreController>();
        var stackDefinition = ItemService.GetConsumableItems().First(d => d.stackable && !d.RequiresInstances);
        store.Initialize(new List<string> { definition.id, stackDefinition.id });
        store.OpenPanel();
        store.OpenPanel();
        var offering = store.GetComponentsInChildren<StoreItemController>(true).First(c=>c.ProtoProduct.id==definition.id);
        var product = offering.GetProductInstance();
        var purchaseButton = (Button)Field(typeof(StoreItemController), "buyButton").GetValue(offering);
        Assert.That(product, Is.Not.Null);
        purchaseButton.onClick.Invoke();
        Assert.That(hero.inventory, Does.Contain(product), "Displayed equipment was replaced by a reroll.");
        int beforeSaves = memory.Saves;
        var sellCell = panel.CellControllers[product.slotIndex];
        Assert.That(sellCell.Context, Is.EqualTo(ItemInteractionContext.StoreInventory));
        pointer.button = PointerEventData.InputButton.Right;
        sellCell.GetComponent<InventoryItemCellInteraction>().OnPointerClick(pointer);
        Assert.That(hero.inventory.Contains(product), Is.False);
        Assert.That(memory.Saves - beforeSaves, Is.EqualTo(1), "Repeated opening duplicated the sale callback.");
        var stack = ItemInstanceService.CreateItem(stackDefinition.id, 3);
        Assert.That(InventoryManager.AddExistingItem(stack), Is.True);
        var stackOffering = store.GetComponentsInChildren<StoreItemController>(true).First(c=>c.ProtoProduct.id==stackDefinition.id);
        var stackButton = (Button)Field(typeof(StoreItemController), "buyButton").GetValue(stackOffering);
        beforeSaves = memory.Saves;
        stackButton.onClick.Invoke();
        stackButton.onClick.Invoke();
        Assert.That(stack.quantity, Is.EqualTo(4), "Sold offering accepted a second click before its GameObject was destroyed.");
        Assert.That(memory.Saves - beforeSaves, Is.EqualTo(1));
        store.ClosePanel();
        Assert.That(sellCell.Context, Is.EqualTo(ItemInteractionContext.Inventory));
    }

    [UnityTest]
    public IEnumerator FeudoWeaponChoiceCancelsRetriesAndConsumesOnlyAfterDelivery()
    {
        yield return Load(FeudoPath);
        var dialog = Find<RewardDialogUIController>();
        var weapons = ItemService.GetItemsByType(ItemType.Weapon).Where(d => d.IsEquipment).Take(2).ToArray();
        Assert.That(weapons.Length, Is.EqualTo(2));
        var template = ScriptableObject.CreateInstance<SimpleRewardDialogDataSO>();
        runtimeData.Add(template);
        template.rewardItemIds = new List<string> { "original template" };
        var effect = ScriptableObject.CreateInstance<ChooseWeaponRewardEffect>();
        runtimeData.Add(effect);
        Field(typeof(ChooseWeaponRewardEffect), "rewardDialogData").SetValue(effect, template);
        Field(typeof(ChooseWeaponRewardEffect), "weaponItemIds").SetValue(effect, weapons.Select(w => w.id).ToList());
        var definition = UnityEngine.Object.Instantiate(ItemService.GetConsumableItems().First());
        runtimeData.Add(definition);
        Field(typeof(ItemDataSO), "_effects").SetValue(definition, new ItemEffect[] { effect });
        var catalog = (Dictionary<string, ItemDataSO>)Field(typeof(ItemService), "_itemCache").GetValue(ItemService.Instance);
        var original = catalog[definition.id];
        catalog[definition.id] = definition;
        int previousLimit = InventoryManager.InventoryLimit;
        try
        {
            InventoryManager.InventoryLimit = 1;
            var box = ItemInstanceService.CreateItem(definition.id);
            Assert.That(InventoryManager.AddExistingItem(box), Is.True);
            Assert.That(InventoryManager.UseConsumable(box), Is.True);
            Assert.That(InventoryManager.UseConsumable(box), Is.False, "A pending box reopened a second dialog.");
            Assert.That(box.quantity, Is.EqualTo(1));
            dialog.Close();
            Assert.That(hero.inventory, Does.Contain(box));
            Assert.That(InventoryManager.UseConsumable(box), Is.True);
            yield return null;
            var rewardCell = dialog.rewardItemsContainer.GetComponentsInChildren<BaseItemCellController>().First();
            rewardCell.GetComponent<Button>().onClick.Invoke();
            int saves = memory.Saves;
            dialog.acceptButton.onClick.Invoke();
            dialog.acceptButton.onClick.Invoke();
            Assert.That(dialog.IsOpen, Is.False);
            Assert.That(hero.inventory.Count, Is.EqualTo(1));
            Assert.That(hero.inventory[0].itemId, Is.EqualTo(weapons[0].id));
            Assert.That(memory.Saves - saves, Is.EqualTo(1));
            Assert.That(template.rewardItemIds, Is.EquivalentTo(new[] { "original template" }));
        }
        finally { catalog[definition.id] = original; InventoryManager.InventoryLimit = previousLimit; }
    }

    [UnityTest]
    public IEnumerator BattlePreparationReleasesGameplayCursorAndAcceptsMouseEquipmentSelection()
    {
        yield return Load(FeudoPath);
        var definition = ItemService.GetItemsByType(ItemType.Weapon).First(d => d.IsEquipment);
        var item = ItemInstanceService.CreateItem(definition.id);
        Assert.That(InventoryManager.AddExistingItem(item), Is.True);
        var battle = BattleDebugCreator.CreateBattleWithLocalHero(hero, Team.TeamA, 0, 0,
            EnemySquadMode.Fixed, SquadDataService.GetSquadById("sqd01"));
        battle.PreparationTimer = 600;
        BattleTransitionData.Instance.SetBattleData(battle);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        yield return Load(PrepPath);
        Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
        Assert.That(Cursor.visible, Is.True, "Preparation inherited the hidden gameplay cursor.");
        var controller = Find<BattlePreparationController>();
        var panel = (HeroEquipmentPanel)Field(typeof(BattlePreparationController), "_equipmentPanel").GetValue(controller);
        var selector = (ItemSelectorController)Field(typeof(BattlePreparationController), "_itemPopUpSelector").GetValue(controller);
        selector.Hide();
        var slot = panel.GetSlot(ItemType.Weapon, ItemCategory.None).GetComponent<RectTransform>();
        var canvas = slot.GetComponentInParent<Canvas>();
        var screen = RectTransformUtility.WorldToScreenPoint(
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            slot.TransformPoint(slot.rect.center));
        var pointer = InputSystem.AddDevice<Mouse>("PreparationRegressionMouse");
        try
        {
            pointer.MakeCurrent();
            InputSystem.QueueStateEvent(pointer, new MouseState { position = screen });
            yield return null;
            InputSystem.QueueStateEvent(pointer, new MouseState { position = screen }.WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(pointer, new MouseState { position = screen });
            yield return null;
            yield return null;
            Assert.That(selector.IsEnabled, Is.True, "A mouse click did not open the equipment selector.");
            selector.Hide();
            // Closing a nested UI must not put an interface scene back into gameplay mode.
            if (FullscreenPanelManager.Instance != null)
                FullscreenPanelManager.Instance.SetUIInteractionState(false);
            yield return null;
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            Assert.That(Cursor.visible, Is.True);
        }
        finally { InputSystem.RemoveDevice(pointer); }
    }

    [UnityTest]
    public IEnumerator PostBattleExitReturnsToQueueOriginAndAllowsAnotherQueue()
    {
        yield return Load(FeudoPath);
        var service = MatchmakingService.Instance;
        Assert.That(service, Is.Not.Null);
        service.StartMatchmaking(hero);
        Assert.That(service.IsInQueue, Is.True);
        service.CancelMatchmaking(); // Preserve origin, but isolate the simulated backend timer.
        Field(typeof(MatchmakingService), "_currentState").SetValue(service,
            MatchmakingService.MatchmakingState.Assigned);
        loadedPath = "Assets/Scenes/GameScenes/PostBattleScene.unity";
        SceneTransitionService.LoadScene(SceneTransitionService.SceneNames.PostBattle);
        yield return null;
        yield return null;
        Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
        Assert.That(Cursor.visible, Is.True);
        var ui = Find<MatchmakingUI>();
        var exit = (Button)Field(typeof(MatchmakingUI), "matchmakingButton").GetValue(ui);
        Assert.That(exit, Is.Not.Null);
        Assert.That(exit.GetComponentInChildren<TMPro.TMP_Text>().text, Is.EqualTo("Salir"));
        loadedPath = FeudoPath;
        exit.onClick.Invoke();
        yield return null;
        yield return null;
        Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(FeudoPath));
        Assert.That(service.CurrentState, Is.EqualTo(MatchmakingService.MatchmakingState.Idle));
        Assert.That(PlayerSessionService.SelectedHero, Is.SameAs(hero));
        service.StartMatchmaking(hero);
        Assert.That(service.IsInQueue, Is.True, "Completing a battle left matchmaking stuck in Assigned.");
        service.CancelMatchmaking();
    }

    [UnityTest]
    public IEnumerator BattlePreparationEquipmentUsesTheSharedConfirmationDialog()
    {
        var battle = BattleDebugCreator.CreateBattleWithLocalHero(hero, Team.TeamA, 0, 0,
            EnemySquadMode.Fixed, SquadDataService.GetSquadById("sqd01"));
        battle.PreparationTimer = 600;
        BattleTransitionData.Instance.SetBattleData(battle);
        yield return Load(PrepPath);
        var controller = Find<BattlePreparationController>();
        var dialog = Find<RewardDialogUIController>();
        var definition = ItemService.GetItemsByType(ItemType.Weapon).First(d => d.IsEquipment);
        var confirmed = UnityEngine.Object.Instantiate(definition);
        runtimeData.Add(confirmed);
        Field(typeof(ItemDataSO), "_requiresConfirmation").SetValue(confirmed, true);
        var catalog = (Dictionary<string, ItemDataSO>)Field(typeof(ItemService), "_itemCache").GetValue(ItemService.Instance);
        catalog[definition.id] = confirmed;
        try
        {
            var item = ItemInstanceService.CreateItem(definition.id);
            Assert.That(InventoryManager.AddExistingItem(item), Is.True);
            var click = typeof(BattlePreparationController).GetMethod("OnItemClicked", BindingFlags.Instance | BindingFlags.NonPublic);
            click.Invoke(controller, new object[] { item, confirmed });
            Assert.That(dialog.IsOpen, Is.True);
            Assert.That(hero.equipment.weapon, Is.Null);
            dialog.Close();
            Assert.That(hero.inventory, Does.Contain(item));
            click.Invoke(controller, new object[] { item, confirmed });
            dialog.acceptButton.onClick.Invoke();
            Assert.That(hero.equipment.weapon, Is.SameAs(item));
            var panel = (HeroEquipmentPanel)Field(typeof(BattlePreparationController), "_equipmentPanel").GetValue(controller);
            Assert.That(panel.GetSlot(ItemType.Weapon, ItemCategory.None).CurrentItem, Is.SameAs(item));
        }
        finally { catalog[definition.id] = definition; }
    }

    [UnityTest]
    public IEnumerator OpenInventoryRebindsWhenTheSessionHeroChangesWithoutSaving()
    {
        yield return Load(FeudoPath);
        var panel = Find<InventoryPanelController>();
        var definition = ItemService.GetItemsByType(ItemType.Weapon).First(d => d.IsEquipment);
        var original = ItemInstanceService.CreateItem(definition.id);
        InventoryManager.AddExistingItem(original);
        panel.OpenPanel(hero);
        var other = TestHeroPool.Load()[0];
        other.heroName = "Other inventory UI hero";
        other.inventory.Clear();
        var replacement = ItemInstanceService.CreateItem(definition.id);
        replacement.slotIndex = 3;
        other.inventory.Add(replacement);
        memory.Player.heroes.Add(other);
        int saves = memory.Saves;
        PlayerSessionService.SetSelectedHero(other);
        Assert.That(panel.CellControllers[original.slotIndex].CurrentItem, Is.Null);
        Assert.That(panel.CellControllers[3].CurrentItem, Is.SameAs(replacement));
        Assert.That(memory.Saves, Is.EqualTo(saves), "Selecting a hero saved an inventory mutation.");
        PlayerSessionService.Clear();
        Assert.That(panel.IsPanelOpen, Is.False);
        Assert.That(InventoryManager.IsInitialized, Is.False);
    }

    IEnumerator Load(string path)
    {
        loadedPath = path;
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
        yield return null;
        yield return null;
    }
    T Find<T>() where T : Component => SceneManager.GetSceneByPath(loadedPath).GetRootGameObjects()
        .SelectMany(g => g.GetComponentsInChildren<T>(true)).First();

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (testWorld != null && testWorld.IsCreated) ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(testWorld);
        var scene = SceneManager.GetSceneByPath(loadedPath ?? "");
        if (scene.IsValid() && scene.isLoaded)
        {
            SceneManager.CreateScene("Inventory UI cleanup");
            yield return SceneManager.UnloadSceneAsync(scene);
        }
        if (testWorld != null && testWorld.IsCreated) testWorld.Dispose();
        World.DefaultGameObjectInjectionWorld = previousWorld;
        if (previousWorld != null && previousWorld.IsCreated) ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(previousWorld);
        BattleTransitionData.Instance.ClearData();
        foreach (var data in runtimeData) if(data != null) UnityEngine.Object.Destroy(data);
        runtimeData.Clear();
        PlayerSessionService.Clear();
        if (previousPlayer != null) PlayerSessionService.SetPlayer(previousPlayer);
        if (previousHero != null) PlayerSessionService.SetSelectedHero(previousHero);
        SaveSystem.SetProvider(previousSave);
        LoadSystem.SetProvider(previousLoad);
        Cursor.lockState = previousCursor;
        Cursor.visible = previousCursorVisible;
    }
    static ISaveProvider Provider(Type type) => (ISaveProvider)Field(type, "_provider").GetValue(null);
    static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
}
#endif

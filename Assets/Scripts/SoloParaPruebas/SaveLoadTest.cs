using UnityEngine;

public class SaveLoadTest : MonoBehaviour
{
    public bool useLocalProvider = true;

    private void Awake()
    {
        if (useLocalProvider)
        {
            SaveSystem.SetProvider(new Core.Persistence.LocalSaveProvider());
            LoadSystem.SetProvider(new Core.Persistence.LocalSaveProvider());
        }
    }

    [ContextMenu("Save Simple Test")]
    public void SaveSimpleTest()
    {
        if (useLocalProvider)
        {
            SaveSystem.SetProvider(new Core.Persistence.LocalSaveProvider());
        }
        PlayerData data = new PlayerData();
        data.playerName = "TestPlayer";
        data.accountLevel = 10;
        data.accountXP = 2500;
        data.gold = 500;
        SaveSystem.SavePlayer(data);
        Debug.Log("Simple player data saved.");
    }

    [ContextMenu("Save Complex Test")]
    public void SaveComplexTest()
    {
        if (useLocalProvider)
        {
            SaveSystem.SetProvider(new Core.Persistence.LocalSaveProvider());
        }
        PlayerData data = new PlayerData();
        data.playerName = "ComplexPlayer";
        data.accountLevel = 99;
        data.accountXP = 99999;
        data.gold = 12345;

        // HeroData
        HeroData hero = new HeroData();
        hero.classId = "Warrior";
        hero.heroName = "HeroTest";
        hero.level = 50;
        hero.currentXP = 50000;
        hero.attributePoints = 10;
        hero.perkPoints = 5;
        hero.bronze = 777;
        hero.fuerza = 20;
        hero.destreza = 15;
        hero.armadura = 30;
        hero.vitalidad = 25;
        hero.unlockedPerks.Add(1);
        hero.unlockedPerks.Add(2);
        hero.ownedSquads.Add(101);
        hero.ownedSquads.Add(102);

        // LoadoutSaveData
        LoadoutSaveData loadout = new LoadoutSaveData();
        loadout.name = "MainLoadout";
        loadout.squadIDs.Add(101);
        loadout.squadIDs.Add(102);
        loadout.perkIDs.Add(1);
        loadout.perkIDs.Add(2);
        loadout.totalLeadership = 50;
        hero.loadouts.Add(loadout);

        // SquadInstanceData
        SquadInstanceData squad = new SquadInstanceData();
        squad.id = "SQ001";
        squad.baseSquadID = "BaseSquadA";
        squad.level = 5;
        squad.experience = 1500;
        squad.unlockedAbilities.Add("SpecialAttack");
        squad.unlockedFormationsIndices.Add(0);
        squad.unlockedFormationsIndices.Add(2);
        squad.selectedFormationIndex = 2;
        squad.customName = "AlphaSquad";
        hero.squadProgress.Add(squad);

        // InventoryItem
        InventoryItem item = new InventoryItem();
        item.itemId = "LegendarySword";
        item.itemType = ItemType.None;
        item.quantity = 1;
        hero.inventory.Add(item);

        // Equipment
        hero.equipment.weaponId = "LegendarySword";
        hero.equipment.helmetId = "HeroicHelmet";
        hero.equipment.torsoId = "HeavyArmor";
        hero.equipment.glovesId = "QuickGloves";
        hero.equipment.pantsId = "ResistantPants";

        // AvatarParts
        hero.avatar.headId = "Head01";
        hero.avatar.hairId = "LongHair";
        hero.avatar.beardId = "ShortBeard";
        VisualAttachment attachment = new VisualAttachment();
        attachment.attachmentId = "DarkGlasses";
        attachment.socket = "Head";
        hero.avatar.attachments.Add(attachment);

        data.heroes.Add(hero);

        SaveSystem.SavePlayer(data);
        Debug.Log("Complex player data saved.");
    }

    [ContextMenu("Load Test")]
    public void LoadTest()
    {
        if (useLocalProvider)
        {
            LoadSystem.SetProvider(new Core.Persistence.LocalSaveProvider());
        }
        PlayerData loaded = LoadSystem.LoadPlayer();
        if (loaded != null)
        {
            Debug.Log($"Loaded: {loaded.playerName}, Level: {loaded.accountLevel}, Gold: {loaded.gold}, Heroes: {loaded.heroes.Count}");
            if (loaded.heroes.Count > 0)
            {
                var hero = loaded.heroes[0];
                Debug.Log($"Hero: {hero.heroName}, Class: {hero.classId}, Level: {hero.level}, XP: {hero.currentXP}");
                Debug.Log($"Unlocked perks: {string.Join(",", hero.unlockedPerks)}");
                Debug.Log($"Owned squads: {string.Join(",", hero.ownedSquads)}");
                Debug.Log($"Loadouts: {hero.loadouts.Count}");
                Debug.Log($"SquadProgress: {hero.squadProgress.Count}");
                Debug.Log($"Inventory: {hero.inventory.Count}");
                Debug.Log($"Equipment: {hero.equipment.weaponId}, {hero.equipment.helmetId}, {hero.equipment.torsoId}, {hero.equipment.glovesId}, {hero.equipment.pantsId}");
                Debug.Log($"Avatar: {hero.avatar.headId}, {hero.avatar.hairId}, {hero.avatar.beardId}, Attachments: {hero.avatar.attachments.Count}");
            }
        }
        else
        {
            Debug.Log("No save file found.");
        }
    }
}
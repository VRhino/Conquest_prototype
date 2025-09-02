using System;
using System.Collections.Generic;

/// <summary>
/// Persistent data for a single hero commander created by the player.
/// Combines progression values with references to static definitions.
/// </summary>
[Serializable]
public class HeroData
{
    // Static references

    /// <summary>Identifier of the hero class ScriptableObject.</summary>
    public string classId = string.Empty;

    /// <summary>Name chosen by the player for this hero.</summary>
    public string heroName = string.Empty;

    public string gender = string.Empty;

    // Progression

    /// <summary>Current hero level.</summary>
    public int level = 1;

    /// <summary>Experience points accumulated towards the next level.</summary>
    public int currentXP = 0;

    /// <summary>Unspent attribute points.</summary>
    public int attributePoints = 0;

    /// <summary>Unspent perk points.</summary>
    public int perkPoints = 0;

    // Currency

    /// <summary>Bronze currency earned by this hero.</summary>
    public int bronze = 0;

    /// <summary>Silver currency earned by this hero.</summary>
    public int silver = 0;

    /// <summary>Gold currency earned by this hero.</summary>
    public int gold = 0;

    // Attributes

    /// <summary>Base strength value.</summary>
    public int strength = 0;
    /// <summary>Base dexterity value.</summary>
    public int dexterity = 0;
    /// <summary>Base armor value.</summary>
    public int armor = 0;
    /// <summary>Base vitality value.</summary>
    public int vitality = 0;

    // Unlocks and selections

    /// <summary>Perk identifiers unlocked by this hero.</summary>
    public List<int> unlockedPerks = new();

    /// <summary>Identifiers of the Type of squads available by the hero to recruit.</summary>
    public List<string> availableSquads = new();

    /// <summary>Loadout configurations available to this hero.</summary>
    public List<LoadoutSaveData> loadouts = new();

    /// <summary>Progress for each squad instance.</summary>
    public List<SquadInstanceData> squadProgress = new();

    // Inventory and equipment

    /// <summary>All items owned by this hero.</summary>
    public List<InventoryItem> inventory = new();

    // Current visual and equipment state

    /// <summary>IDs of equipped functional items.</summary>
    public Equipment equipment = new();

    /// <summary>Visual customization of the avatar.</summary>
    public AvatarParts avatar = new();

    public List<string> GetEquipment()
    {
        return new List<string>
        {
            equipment.weapon?.itemId ?? string.Empty,
            equipment.helmet?.itemId ?? string.Empty,
            equipment.torso?.itemId ?? string.Empty,
            equipment.gloves?.itemId ?? string.Empty,
            equipment.pants?.itemId ?? string.Empty,
            equipment.boots?.itemId ?? string.Empty
        };
    }
}
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

    // Progression

    /// <summary>Current hero level.</summary>
    public int level = 1;

    /// <summary>Experience points accumulated towards the next level.</summary>
    public int currentXP = 0;

    /// <summary>Unspent attribute points.</summary>
    public int attributePoints = 0;

    /// <summary>Unspent perk points.</summary>
    public int perkPoints = 0;

     /// <summary>Bronze currency earned by this hero.</summary>
    public int bronze = 0;

    // Attributes

    /// <summary>Base strength value.</summary>
    public int fuerza = 0;
    /// <summary>Base dexterity value.</summary>
    public int destreza = 0;
    /// <summary>Base armor value.</summary>
    public int armadura = 0;
    /// <summary>Base vitality value.</summary>
    public int vitalidad = 0;

    // Unlocks and selections

    /// <summary>Perk identifiers unlocked by this hero.</summary>
    public List<int> unlockedPerks = new();

    /// <summary>Identifiers of squads owned by the hero.</summary>
    public List<int> ownedSquads = new();

    /// <summary>Loadout configurations available to this hero.</summary>
    public List<LoadoutSaveData> loadouts = new();

    /// <summary>Progress for each squad instance.</summary>
    public List<SquadInstanceData> squadProgress = new();

    // Inventory and equipment

    /// <summary>All items owned by this hero.</summary>
    public List<Item> inventory = new();

    // Current visual and equipment state

    /// <summary>IDs of equipped functional items.</summary>
    public Equipment equipment = new();

    /// <summary>Visual customization of the avatar.</summary>
    public AvatarParts avatar = new();
}

/// <summary>
/// Current functional equipment worn by the hero.
/// Each field stores the ID of an item owned in the inventory.
/// </summary>
[Serializable]
public class Equipment
{
    public string weaponId = string.Empty;
    public string helmetId = string.Empty;
    public string torsoId = string.Empty;
    public string glovesId = string.Empty;
    public string pantsId = string.Empty;
}

/// <summary>
/// Visual customization references for the hero's modular avatar.
/// </summary>
[Serializable]
public class AvatarParts
{
    /// <summary>Identifier of the head mesh or prefab.</summary>
    public string headId = string.Empty;
    /// <summary>Identifier for the hair style.</summary>
    public string hairId = string.Empty;
    /// <summary>Identifier for facial hair or beard.</summary>
    public string beardId = string.Empty;
    /// <summary>Optional extra attachments.</summary>
    public List<VisualAttachment> attachments = new();
}

/// <summary>
/// Attachment applied to the avatar for purely visual customization.
/// </summary>
[Serializable]
public class VisualAttachment
{
    /// <summary>Unique id of the attachment asset.</summary>
    public string attachmentId = string.Empty;
    /// <summary>Name of the mount point on the rig.</summary>
    public string socket = string.Empty;
}

/// <summary>
/// Serializable representation of an item stored in a hero's inventory.
/// Uses an identifier to resolve the actual ScriptableObject definition.
/// </summary>
[Serializable]
public class Item
{
    /// <summary>Identifier of the item definition.</summary>
    public string itemId = string.Empty;

    /// <summary>Type of item for fast filtering.</summary>
    public ItemType itemType = ItemType.None;

    /// <summary>Amount of this item owned. Non-stackable items should use 1.</summary>
    public int quantity = 1;
}
using System;
using System.Collections.Generic;

/// <summary>
/// Root persistent profile for the local player.
/// Contains dynamic values such as owned heroes and account progression.
/// ScriptableObject references are represented by string identifiers.
/// </summary>
[Serializable]
public class PlayerData
{
    /// <summary>Display name chosen by the player.</summary>
    public string playerName = string.Empty;

    /// <summary>Overall account level accumulated across heroes.</summary>
    public int accountLevel = 1;

    /// <summary>Total account experience used to reach the next level.</summary>
    public int accountXP = 0;

    /// <summary>Current amount of in-game currency.</summary>
    public int gold = 0;

    /// <summary>All heroes created by the player.</summary>
    public List<HeroData> heroes = new();
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

/// <summary>
/// Item categories used by the game for inventory management.
/// </summary>
public enum ItemType
{
    None,
    Weapon,
    Helmet,
    Torso,
    Gloves,
    Pants,
    Consumable,
    Visual
}

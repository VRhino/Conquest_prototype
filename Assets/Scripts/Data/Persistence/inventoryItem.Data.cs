using System;

/// <summary>
/// Serializable representation of an item stored in a hero's inventory.
/// Uses an identifier to resolve the actual ScriptableObject definition.
/// </summary>
[Serializable]
public class InventoryItem
{
    /// <summary>Identifier of the item definition.</summary>
    public string itemId = string.Empty;

    /// <summary>Type of item for fast filtering.</summary>
    public ItemType itemType = ItemType.None;

    /// <summary>Amount of this item owned. Non-stackable items should use 1.</summary>
    public int quantity = 1;
}
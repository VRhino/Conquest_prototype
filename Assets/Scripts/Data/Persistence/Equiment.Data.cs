using System;

/// <summary>
/// Current functional equipment worn by the hero.
/// Each field stores a complete InventoryItem instance to preserve stats and unique data.
/// </summary>
[Serializable]
public class Equipment
{
    public InventoryItem weapon = null;
    public InventoryItem helmet = null;
    public InventoryItem torso = null;
    public InventoryItem gloves = null;
    public InventoryItem pants = null;
    public InventoryItem boots = null;
}
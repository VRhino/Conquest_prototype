using System.Collections.Generic;

/// <summary>
/// Read-only facet: static identity fields that never change after hero creation.
/// </summary>
public interface IHeroIdentity
{
    string      ClassId  { get; }
    string      HeroName { get; }
    string      Gender   { get; }
    AvatarParts Avatar   { get; }
}

/// <summary>
/// Read-only facet: progression and attribute values.
/// </summary>
public interface IHeroProgression
{
    int       Level           { get; }
    int       CurrentXP       { get; }
    int       AttributePoints { get; }
    int       PerkPoints      { get; }
    int       Strength        { get; }
    int       Dexterity       { get; }
    int       Armor           { get; }
    int       Vitality        { get; }
    List<int> UnlockedPerks   { get; }
}

/// <summary>
/// Read-only facet: currency balances.
/// </summary>
public interface IHeroEconomy
{
    int Bronze { get; }
    int Silver { get; }
    int Gold   { get; }
}

/// <summary>
/// Read-only facet: squad unlocks, loadouts and instance progress.
/// </summary>
public interface IHeroSquads
{
    List<string>            AvailableSquads { get; }
    List<LoadoutSaveData>   Loadouts        { get; }
    List<SquadInstanceData> SquadProgress   { get; }
}

/// <summary>
/// Read-only facet: inventory items and equipped items.
/// </summary>
public interface IHeroInventory
{
    List<InventoryItem> Inventory { get; }
    Equipment           Equipment { get; }
}

// ── Write interfaces (Phase 3) ──────────────────────────────────────────────

/// <summary>
/// Write facet for progression values that services are allowed to mutate.
/// </summary>
public interface IHeroProgressionMutator : IHeroProgression
{
    new int CurrentXP       { get; set; }
    new int AttributePoints { get; set; }
    new int PerkPoints      { get; set; }
    new int Strength        { get; set; }
    new int Dexterity       { get; set; }
    new int Armor           { get; set; }
    new int Vitality        { get; set; }
}

/// <summary>
/// Write facet for currency balances.
/// </summary>
public interface IHeroEconomyMutator : IHeroEconomy
{
    new int Bronze { get; set; }
    new int Silver { get; set; }
    new int Gold   { get; set; }
}

/// <summary>
/// Write facet for equipped items. The inventory list is mutable by reference — no setter needed.
/// </summary>
public interface IHeroInventoryMutator : IHeroInventory
{
    new Equipment Equipment { get; set; }
}

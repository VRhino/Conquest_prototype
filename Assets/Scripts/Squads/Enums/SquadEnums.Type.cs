using Unity.Entities;

/// <summary>
/// Enumerates the possible orders that can be issued to a squad.
/// </summary>
public enum SquadOrderType
{
    FollowHero,
    HoldPosition,
    Attack
}

/// <summary>
/// Supported squad formations.
/// </summary>
public enum FormationType
{
    Line,
    Dispersed,
    Testudo,
    Wedge,
    Column,
    Square,
    ShieldWall,
    /// <summary>
    /// «En reposo»: how the troop stands while it follows its hero. Every troop has one (<see cref="SquadData.restFormation"/>),
    /// always available, never in the F1-F4 list nor in the X cycle.
    /// </summary>
    Rest,
}

/// <summary>
/// Classification of squad unit types available to the player.
/// </summary>
public enum SquadType
{
    Squires,
    Archers,
    Pikemen,
    Spearmen,
    /// <summary>Cavalry (M17): soldier and horse are one body.</summary>
    LanceCavalry,
    SwordCavalry,
    HorseArchers
}

/// <summary>
/// Tactical behavior profile used by AI systems.
/// </summary>
public enum BehaviorProfile
{
    Defensive,
    Harassing,
    AntiCharge,
    Versatile
}

public enum UnitType
{
    Infantry,
    Cavalry,
    Distance
}

/// <summary>
/// Enum que define los diferentes niveles de rareza para unidades/escuadrones.
/// </summary>
public enum SquadRarity
{
    peasant_tier,
    levy_tier,
    conscript_tier,
    trained_tier,
    seasoned_tier,
    veteran_tier,
    hardened_tier,
    elite_tier,
    master_tier,
    legendary_tier
}

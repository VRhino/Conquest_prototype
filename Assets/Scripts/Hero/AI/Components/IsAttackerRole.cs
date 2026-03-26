using Unity.Entities;

/// <summary>
/// Tag added to AI heroes that play the attacker role in this match.
/// Added at spawn time based on match configuration (side of the map chosen in BattlePrepScene).
/// Heroes without this tag are treated as defenders by behavior systems.
/// </summary>
public struct IsAttackerRole : IComponentData { }

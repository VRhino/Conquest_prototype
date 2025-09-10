using System;
using System.Collections.Generic;

/// <summary>
/// Dynamic persistent data for a squad owned by the player.
/// References the static <see cref="SquadData"/> definition through an identifier.
/// Used by save/load systems and converted to entities during world bootstrap.
/// </summary>
[Serializable]
public class SquadInstanceData
{
    // Identification

    /// <summary>Unique identifier of this squad instance.</summary>
    public string id = string.Empty;

    /// <summary>Identifier of the base <c>SquadData</c> ScriptableObject.</summary>
    public string baseSquadID = string.Empty;

    // Progression

    /// <summary>Current squad level.</summary>
    public int level = 1;

    /// <summary>Total experience accumulated by this squad.</summary>
    public int experience = 0;

    // Unlocks and selections

    /// <summary>Ability identifiers unlocked for this squad.</summary>
    public List<string> unlockedAbilities = new();

    /// <summary>Indices of unlocked formations inside the base data array.</summary>
    public List<int> permittedFormationIndexes = new();

    /// <summary>Index of the currently selected formation.</summary>
    public int selectedFormationIndex = 0;

    /// <summary>Number of units injured in this squad.</summary>
    public int unitsInjured = 0;

    /// <summary>Number of units killed in this squad.</summary>
    public int unitsKilled = 0;

    /// <summary>Number of pieces of equipment lost by this squad.</summary>
    public int equipmentLost = 0;
    /// <summary>Number of units currently in this squad.</summary>
    public int unitsInSquad = 0;

    public int unitsAlive => unitsInSquad - unitsInjured - unitsKilled;
}

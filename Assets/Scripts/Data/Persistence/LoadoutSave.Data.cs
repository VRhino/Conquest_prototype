using System;
using System.Collections.Generic;

/// <summary>
/// Preset combination of squads and perks chosen by the player.
/// </summary>
[Serializable]
public class LoadoutSaveData
{
    /// <summary>Name assigned to this loadout.</summary>
    public string name = string.Empty;

    /// <summary>Identifiers of squads included in the loadout.</summary>
    public List<string> squadInstanceIDs = new();

    /// <summary>Perk identifiers selected for the loadout.</summary>
    public List<string> perkIDs = new();

    /// <summary>Total leadership required by this setup.</summary>
    public int totalLeadership = 0;

    /// <summary>Indicates if this loadout is currently active.</summary>
    public bool isActive = false;
}

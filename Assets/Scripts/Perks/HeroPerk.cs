using UnityEngine;

/// <summary>
/// Data describing a gameplay perk available to the player.
/// </summary>
[CreateAssetMenu(fileName = "HeroPerk", menuName = "Hero/Perk Data")]
public class HeroPerk : ScriptableObject
{
    /// <summary>Unique identifier for the perk.</summary>
    public int perkID;

    /// <summary>Display name of the perk.</summary>
    public string perkName;

    /// <summary>Icon used in UI lists.</summary>
    public Sprite icon;

    /// <summary>Description of the perk's effect.</summary>
    public string description;

    /// <summary>True if the perk is passive. False indicates an active perk.</summary>
    public bool isPassive;

    /// <summary>Numeric value for the perk effect interpreted by systems.</summary>
    public float effectValue;

    /// <summary>Cooldown in seconds for active perks.</summary>
    public float cooldown;

    /// <summary>Duration in seconds the perk remains active.</summary>
    public float duration;

    /// <summary>Tags used for filtering or synergy.</summary>
    public PerkTag[] tags;

    /// <summary>Level required to unlock the perk.</summary>
    public int unlockLevel;

    /// <summary>Whether the perk is available from the start.</summary>
    public bool isUnlockedByDefault;
}

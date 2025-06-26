using UnityEngine;

/// <summary>
/// Basic description of a squad ability unlockable through progression.
/// </summary>
[CreateAssetMenu(menuName = "Squads/Ability Data")]
public class AbilityData : ScriptableObject
{
    /// <summary>Name displayed for the ability.</summary>
    public string abilityName;
    /// <summary>Icon used in UI.</summary>
    public Sprite icon;
    /// <summary>Text description of the effect.</summary>
    public string description;
    /// <summary>Cooldown in seconds before reuse.</summary>
    public float cooldown;
}

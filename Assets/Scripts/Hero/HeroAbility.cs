using UnityEngine;

/// <summary>
/// Data defining an active hero ability used during combat.
/// Loaded by <see cref="HeroClassDefinition"/> and referenced by the HUD.
/// </summary>
[CreateAssetMenu(menuName = "Hero/Ability Data")]
public class HeroAbility : ScriptableObject
{
    /// <summary>Name shown to players.</summary>
    public string abilityName;

    /// <summary>Icon representing the ability in UI.</summary>
    public Sprite icon;

    /// <summary>Tooltip or descriptive text.</summary>
    public string description;

    /// <summary>Cooldown time in seconds before the ability can be reused.</summary>
    public float cooldown;

    /// <summary>Stamina required to activate the ability.</summary>
    public float staminaCost;

    /// <summary>Button slot this ability belongs to (Q, E, R or Ultimate).</summary>
    public HeroAbilityCategory category;

    /// <summary>Optional animation tag triggered when used.</summary>
    public string animationTag;

    /// <summary>Optional radius for area based effects.</summary>
    public float areaRadius;

    /// <summary>Optional multiplier applied to base damage.</summary>
    public float damageMultiplier;
}

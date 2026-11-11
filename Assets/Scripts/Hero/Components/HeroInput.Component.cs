using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Stores input values for the hero. Other systems can read these values
/// to drive movement, abilities and combat.
/// </summary>
public struct HeroInputComponent : IComponentData
{
    /// <summary>Direction on the XZ plane from WASD keys.</summary>
    public float2 MoveInput;

    /// <summary>True while left shift is held.</summary>
    public bool IsSprintPressed;

    /// <summary>True while left mouse button is held.</summary>
    public bool IsAttackPressed;

    /// <summary>True while Q key is held.</summary>
    public bool UseSkill1;

    /// <summary>True while E key is held.</summary>
    public bool UseSkill2;

    /// <summary>True while R key is held.</summary>
    public bool UseSkill3;
    /// <summary>True while F key is held (the ultimate, as in the GDD).</summary>
    public bool UseUltimate;
    /// <summary>True while 1 / 2 are held: the two active perks of the loadout (L7-4).</summary>
    public bool UsePerk1;
    public bool UsePerk2;
    /// <summary>True while 3 / 4 / 5 are held: the abilities of the active squad (L2-9, GDD 12.3; 1 and 2 are the perks).</summary>
    public bool UseSquadAbility1;
    public bool UseSquadAbility2;
    public bool UseSquadAbility3;

    /// <summary>True on the frame G is pressed: call the horse and get on, or get off (M19).</summary>
    public bool MountPressed;

    /// <summary>True when walk toggle key is pressed (for animation system).</summary>
    public bool IsWalkTogglePressed;
}

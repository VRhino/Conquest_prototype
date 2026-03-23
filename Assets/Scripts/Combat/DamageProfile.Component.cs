using Unity.Entities;

/// <summary>
/// Component holding runtime data for a weapon damage profile.
/// Each field represents one damage/penetration type. Values of 0 are ignored during damage
/// calculation, allowing units to deal mixed or single-type damage purely from SquadData config.
/// Typically baked from a <see cref="DamageProfile"/> ScriptableObject or created by SquadSpawningSystem.
/// </summary>
public struct DamageProfileComponent : IComponentData
{
    public float bluntDamage;
    public float slashingDamage;
    public float piercingDamage;

    public float bluntPenetration;
    public float slashingPenetration;
    public float piercingPenetration;
}

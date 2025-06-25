using Unity.Entities;

/// <summary>
/// Penetration values that reduce enemy defenses when dealing damage.
/// Useful for entities with multiple damage sources or bonus effects.
/// </summary>
public struct PenetrationComponent : IComponentData
{
    /// <summary>Penetration applied against blunt defense.</summary>
    public float bluntPenetration;

    /// <summary>Penetration applied against slashing defense.</summary>
    public float slashPenetration;

    /// <summary>Penetration applied against piercing defense.</summary>
    public float piercePenetration;
}

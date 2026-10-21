using Unity.Entities;

/// <summary>
/// Contact-ring reservation of a melee attacker (C11). Written only by <see cref="MeleeContactRingSystem"/>;
/// <see cref="UnitNavMeshSystem"/> reads it to choose the approach point.
/// </summary>
public struct MeleeContactSlotComponent : IComponentData
{
    /// <summary>Target the reservation belongs to; Entity.Null when the unit has no melee target.</summary>
    public Entity target;
    /// <summary>Reserved slot on the target's ring, or -1 while waiting for one (ring full).</summary>
    public int slot;
    /// <summary>Slots on that ring when it was reserved.</summary>
    public int slotCount;
    /// <summary>Ring radius (the smallest stop distance among the target's attackers), m.</summary>
    public float radius;
}

using Unity.Entities;

/// <summary>
/// Temporary pool of damage absorption, separate from health and from the physical shield
/// (<see cref="UnitShieldComponent"/>) (Aprendizajes_Referencia.md L1-06). <see cref="DamageCalculationSystem"/> takes
/// each hit from it first and only the excess reaches health. No source adds it yet; abilities will.
/// </summary>
public struct AbsorbShieldComponent : IComponentData
{
    /// <summary>Damage it can still absorb.</summary>
    public float value;

    /// <summary>Simulation time it ends at; 0 = until depleted.</summary>
    public double expiresAt;
}

using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Presentation event of a resolved hit (L1-03): where to show which floating text. Created by
/// <see cref="DamageCalculationSystem"/>, which no longer calls UI; consumed by
/// <see cref="DamageFeedbackPresentationSystem"/>.
/// </summary>
public struct DamageFeedbackEvent : IComponentData
{
    /// <summary>World position of the target when it was hit.</summary>
    public float3 position;
    public DamageCategory category;
    public float amount;
}

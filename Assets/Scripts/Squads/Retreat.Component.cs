using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Data required to handle a squad retreat behaviour.
/// </summary>
public struct RetreatComponent : IComponentData
{
    /// <summary>Destination where the squad should retreat.</summary>
    public float3 retreatTarget;

    /// <summary>Time elapsed since the retreat started.</summary>
    public float retreatTimer;

    /// <summary>Time after which the squad is removed.</summary>
    public float retreatDuration;
}

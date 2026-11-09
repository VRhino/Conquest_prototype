using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Enabled while a squad in Testudo advances (the squires' Advance ability): the formation walks straight ahead, slowly,
/// every soldier keeping its shield posture and its slot heading. It belongs to the Hold Position order it started from;
/// how far it went is <see cref="SquadHoldPositionComponent.advanceOffset"/>. Single writer: <see cref="SquadAdvanceSystem"/>.
/// </summary>
public struct SquadAdvanceComponent : IComponentData, IEnableableComponent
{
    /// <summary>Centre of the Hold Position order the advance started from: another order changes it and stops it.</summary>
    public float3 holdCenter;
    /// <summary>Horizontal unit vector the formation walks along (its facing when it started).</summary>
    public float3 direction;
    /// <summary>Metres per second.</summary>
    public float speed;
}

/// <summary>
/// Turns the advance on or off: enabled by <see cref="SkillEffectSystem"/> when the Advance ability fires, consumed by
/// <see cref="SquadAdvanceSystem"/>.
/// </summary>
public struct SquadAdvanceToggleRequest : IComponentData, IEnableableComponent { }

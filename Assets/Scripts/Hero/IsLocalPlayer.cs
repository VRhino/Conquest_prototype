using Unity.Entities;

/// <summary>
/// Tag component used to identify the local player's entity.
/// Systems that should operate only on the local player can
/// query for this component.
/// </summary>
public struct IsLocalPlayer : IComponentData
{
}

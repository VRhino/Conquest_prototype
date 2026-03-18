using Unity.Entities;

/// <summary>
/// Tag component used to identify the local player's currently active squad.
/// This avoids confusion with the IsLocalPlayer tag used for the hero.
/// </summary>
public struct IsLocalSquadActive : IComponentData
{
}

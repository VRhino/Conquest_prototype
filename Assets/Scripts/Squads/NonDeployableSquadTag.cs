using Unity.Entities;

/// <summary>
/// Tag added to squads that cannot be deployed due to zero equipment.
/// Checked by <see cref="LoadoutSystem"/>.
/// </summary>
public struct NonDeployableSquadTag : IComponentData
{
}

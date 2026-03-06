using Unity.Entities;

/// <summary>
/// Tag component added to every supply point ECS entity.
/// Use this to filter supply points in the Entities hierarchy or in system queries.
/// </summary>
public struct SupplyPointTag : IComponentData { }

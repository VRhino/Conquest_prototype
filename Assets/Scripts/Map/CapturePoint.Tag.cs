using Unity.Entities;

/// <summary>
/// Tag component added to every capture point ECS entity.
/// Use this to filter capture points in the Entities hierarchy or in system queries.
/// </summary>
public struct CapturePointTag : IComponentData { }

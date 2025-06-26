using Unity.Entities;

/// <summary>
/// Singleton component used to reference all zone entities in the scene.
/// A dynamic buffer of <see cref="ZoneReferenceBuffer"/> must be attached to
/// the same entity.
/// </summary>
public struct ZoneManagerSingleton : IComponentData
{
}

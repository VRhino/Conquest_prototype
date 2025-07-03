using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Utilidad centralizada para obtener posiciones de héroes de escuadrones.
/// Evita duplicación de código en múltiples sistemas.
/// </summary>
public static class HeroPositionUtility
{
    /// <summary>
    /// Obtiene la posición del héroe de un escuadrón de forma segura.
    /// </summary>
    /// <param name="squadEntity">Entidad del escuadrón</param>
    /// <param name="ownerLookup">Lookup de SquadOwnerComponent</param>
    /// <param name="transformLookup">Lookup de LocalTransform</param>
    /// <param name="heroPosition">Posición del héroe (out)</param>
    /// <returns>True si se obtuvo la posición exitosamente</returns>
    public static bool TryGetHeroPosition(
        Entity squadEntity,
        ComponentLookup<SquadOwnerComponent> ownerLookup,
        ComponentLookup<LocalTransform> transformLookup,
        out float3 heroPosition)
    {
        heroPosition = float3.zero;
        
        if (!ownerLookup.TryGetComponent(squadEntity, out var squadOwner))
            return false;
            
        if (!transformLookup.TryGetComponent(squadOwner.hero, out var heroTransform))
            return false;
            
        heroPosition = heroTransform.Position;
        return true;
    }
    
    /// <summary>
    /// Obtiene la entidad del héroe de un escuadrón.
    /// </summary>
    /// <param name="squadEntity">Entidad del escuadrón</param>
    /// <param name="ownerLookup">Lookup de SquadOwnerComponent</param>
    /// <param name="heroEntity">Entidad del héroe (out)</param>
    /// <returns>True si se obtuvo la entidad exitosamente</returns>
    public static bool TryGetHeroEntity(
        Entity squadEntity,
        ComponentLookup<SquadOwnerComponent> ownerLookup,
        out Entity heroEntity)
    {
        heroEntity = Entity.Null;
        
        if (!ownerLookup.TryGetComponent(squadEntity, out var squadOwner))
            return false;
            
        heroEntity = squadOwner.hero;
        return true;
    }
}

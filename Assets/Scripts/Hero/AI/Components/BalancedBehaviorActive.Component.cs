using Unity.Entities;

/// <summary>
/// Enableable tag — presente en todos los héroes AI, activo solo cuando este héroe
/// usa el behavior <see cref="HeroAIBalancedSystem"/>.
/// Activar/desactivar con <c>SetComponentEnabled</c> para cambiar behavior en runtime.
/// </summary>
public struct BalancedBehaviorActive : IComponentData, IEnableableComponent { }

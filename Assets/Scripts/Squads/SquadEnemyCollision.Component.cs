using Unity.Entities;

/// <summary>
/// Per-squad switch of the collision with enemy bodies (F9/C16, decision D2; P2 of the melee notes). The reference's rule:
/// a pair of enemies collides only if both have it on. A squad without the component counts as on, so it is added only
/// to turn the collision off (a "break through" ability, a squad that walks through the enemy line). Read by
/// <see cref="EnemyContact"/>, <see cref="UnitBodyblockSystem"/> and <see cref="UnitHitKnockbackSystem"/> through
/// <see cref="EnemyCollision.PassesEnemies"/>.
/// </summary>
public struct SquadEnemyCollisionComponent : IComponentData
{
    public bool enabled;
}

public static class EnemyCollision
{
    /// <summary>
    /// True when the unit walks through enemy bodies: its squad has the collision off, or a hit is pushing it with
    /// <see cref="UnitHitKnockbackComponent.passesEnemies"/> (heavy reactions). Heroes and units without squad always collide.
    /// </summary>
    public static bool PassesEnemies(EntityManager entityManager, Entity unit)
    {
        if (entityManager.HasComponent<UnitHitKnockbackComponent>(unit))
        {
            var knockback = entityManager.GetComponentData<UnitHitKnockbackComponent>(unit);
            if (knockback.active && knockback.passesEnemies) return true;
        }
        if (!entityManager.HasComponent<UnitOwnerComponent>(unit)) return false;
        Entity squad = entityManager.GetComponentData<UnitOwnerComponent>(unit).squad;
        return squad != Entity.Null && entityManager.HasComponent<SquadEnemyCollisionComponent>(squad)
            && !entityManager.GetComponentData<SquadEnemyCollisionComponent>(squad).enabled;
    }
}

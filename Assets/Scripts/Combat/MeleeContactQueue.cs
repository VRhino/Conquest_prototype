using System.Collections.Concurrent;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>Physical contacts awaiting validation by the ECS combat pipeline.</summary>
public struct MeleeContact
{
    public World world;
    public Entity attacker;
    public Entity target;
    public HitType hitType;
    public float3 attackerPosition;
    public float3 contactPoint;
}

public static class MeleeContactQueue
{
    public static readonly ConcurrentQueue<MeleeContact> Pending = new ConcurrentQueue<MeleeContact>();

    /// <summary>
    /// Only queue contacts observed during an active, unused strike window.
    /// The resolution system validates again because ECS state can change before it runs.
    /// </summary>
    public static bool TryEnqueue(MeleeContact contact)
    {
        if (contact.world == null || !contact.world.IsCreated
            || contact.attacker == Entity.Null || contact.target == Entity.Null)
            return false;

        var em = contact.world.EntityManager;
        if (!em.Exists(contact.attacker) || !em.Exists(contact.target)
            || contact.attacker == contact.target
            || em.HasComponent<IsDeadComponent>(contact.attacker)
            || (em.HasComponent<HeroLifeComponent>(contact.attacker)
                && !em.GetComponentData<HeroLifeComponent>(contact.attacker).isAlive)
            || !em.HasComponent<WeaponHitboxActiveTag>(contact.attacker)
            || !em.IsComponentEnabled<WeaponHitboxActiveTag>(contact.attacker))
            return false;

        if (em.HasComponent<HeroCombatComponent>(contact.attacker))
        {
            if (em.GetComponentData<HeroCombatComponent>(contact.attacker).hitboxFired)
                return false;
        }
        else if (em.HasComponent<UnitCombatComponent>(contact.attacker))
        {
            if (em.GetComponentData<UnitCombatComponent>(contact.attacker).hitboxFired)
                return false;
        }
        else
            return false;

        Pending.Enqueue(contact);
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        while (Pending.TryDequeue(out _)) { }
    }
}

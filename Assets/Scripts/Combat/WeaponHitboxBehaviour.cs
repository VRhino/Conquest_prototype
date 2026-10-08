using ConquestTactics.Visual;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Reports weapon trigger contacts to ECS. Attack validity, damage and effects
/// are resolved by MeleeContactResolutionSystem.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class WeaponHitboxBehaviour : MonoBehaviour
{
    /// <summary>Set at visual creation time by the unit or hero visual system.</summary>
    [HideInInspector] public Entity ownerUnit;

    private World _world;

    private void Awake()
    {
        _world = World.DefaultGameObjectInjectionWorld;
    }

    private void OnTriggerStay(Collider other)
    {
        if (_world == null || !_world.IsCreated || ownerUnit == Entity.Null) return;

        var sync = other.GetComponentInParent<EntityVisualSync>();
        if (sync == null) return;

        Entity target = sync.GetHeroEntity();
        if (target == Entity.Null) return;

        MeleeContactQueue.TryEnqueue(new MeleeContact
        {
            world = _world,
            attacker = ownerUnit,
            target = target,
            hitType = other.GetComponent<ShieldHitboxBehaviour>() != null
                ? HitType.Shield : HitType.Body,
            attackerPosition = transform.position,
            contactPoint = other.ClosestPoint(transform.position)
        });
    }
}

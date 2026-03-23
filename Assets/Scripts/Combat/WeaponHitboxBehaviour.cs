using ConquestTactics.Visual;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Place on the "WeaponHitbox" child GameObject of a unit visual prefab.
/// Position and scale the BoxCollider in the Editor under the weapon bone.
/// UnitAttackSystem enables/disables WeaponHitboxActiveTag on the owner unit to
/// gate detection to the strike window only.
/// The enemy's CharacterController acts as the hurtbox — no extra collider needed.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class WeaponHitboxBehaviour : MonoBehaviour
{
    /// <summary>Set at unit visual creation time by SquadVisualManagementSystem.</summary>
    [HideInInspector] public Entity ownerUnit;

    private World _world;

    private void Awake()
    {
        _world = World.DefaultGameObjectInjectionWorld;
    }

    private void OnTriggerStay(Collider other)
    {
        if (_world == null || !_world.IsCreated || ownerUnit == Entity.Null) return;

        var em = _world.EntityManager;



        // Gate: only active during strike window (WeaponHitboxActiveTag enabled by UnitAttackSystem)
        if (!em.IsComponentEnabled<WeaponHitboxActiveTag>(ownerUnit))
        {

            return;
        }

        // One hit per swing
        var combat = em.GetComponentData<UnitCombatComponent>(ownerUnit);
        if (combat.hitboxFired)
        {

            return;
        }

        // Resolve target entity from the collider's visual GO
        var sync = other.GetComponentInParent<EntityVisualSync>();
        if (sync == null)
        {
            Debug.LogWarning($"[BattleTestDebug] EntityVisualSync NOT FOUND in parent of {other.name}");
            return;
        }
        Entity target = sync.GetHeroEntity();
        if (target == Entity.Null || !em.Exists(target))
        {
            Debug.LogWarning($"[BattleTestDebug] Target entity NULL from sync on {other.name}");
            return;
        }

        // Friendly fire check
        bool hasTeam = em.HasComponent<TeamComponent>(ownerUnit);
        if (hasTeam && em.HasComponent<TeamComponent>(target))
        {
            if (em.GetComponentData<TeamComponent>(ownerUnit).value ==
                em.GetComponentData<TeamComponent>(target).value)
            {

                return;
            }
        }

        // Dead check
        if (em.HasComponent<IsDeadComponent>(target)) return;

        // Build PendingDamageEvent
        var weapon = em.GetComponentData<UnitWeaponComponent>(ownerUnit);
        bool crit = Random.value <= weapon.criticalChance;
        float attackerSpeed = em.HasComponent<ConquestTactics.Animation.UnitAnimationMovementComponent>(ownerUnit)
            ? em.GetComponentData<ConquestTactics.Animation.UnitAnimationMovementComponent>(ownerUnit).CurrentSpeed
            : 0f;

        em.AddComponentData(ownerUnit, new PendingDamageEvent
        {
            target           = target,
            damageSource     = ownerUnit,
            damageProfile    = weapon.damageProfile,
            sourceTeam       = hasTeam ? em.GetComponentData<TeamComponent>(ownerUnit).value : Team.None,
            category         = crit ? DamageCategory.Critical : DamageCategory.Normal,
            multiplier       = crit ? weapon.criticalMultiplier : 1f,
            attackDirection  = transform.forward,
            attackerSpeed    = attackerSpeed,
            attackerPosition = transform.position
        });

        // Spawn hit impact VFX at contact point
        Vector3 contactPoint = other.ClosestPoint(transform.position);
        HitImpactEffectManager.Instance?.Spawn(contactPoint);

        combat.hitboxFired = true;
        em.SetComponentData(ownerUnit, combat);
    }
}

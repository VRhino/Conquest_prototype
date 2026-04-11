using Unity.Entities;
using UnityEngine;

/// <summary>
/// Place on the "ShieldHitbox" child GameObject of a unit visual prefab.
/// Position and scale the BoxCollider in the Editor to cover the shield area.
///
/// This behaviour's role is discovery: SquadVisualManagementSystem detects it
/// post-instantiation via GetComponentInChildren and adds UnitShieldComponent
/// to the ECS entity. Only units/heroes whose prefab has this component will
/// gain shield blocking capability.
///
/// The actual block logic lives in DamageCalculationSystem (dot-product check
/// against UnitShieldComponent.orientation). BlockRegenSystem handles regen.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class ShieldHitboxBehaviour : MonoBehaviour
{
    /// <summary>Set at unit visual creation time by SquadVisualManagementSystem.</summary>
    [HideInInspector] public Entity ownerUnit;

    [Header("Shield Configuration")]
    [Tooltip("Shield orientation relative to unit forward")]
    public ShieldOrientation orientation = ShieldOrientation.Forward;

    [Tooltip("Override maxBlock. If 0, uses SquadData.block value")]
    public float maxBlockOverride;

    [Tooltip("Override regenRate. If 0, uses SquadData.blockRegenRate value")]
    public float regenRateOverride;
}

using Unity.Entities;
using UnityEngine;

/// <summary>
/// Authoring MonoBehaviour for HeroGameplayConfigComponent.
/// Add to a scene GameObject (e.g. "HeroGameplayConfig") so systems can read tuning values
/// without magic numbers in code.
/// </summary>
public class HeroGameplayConfigAuthoring : MonoBehaviour
{
    [Header("Combat")]
    public float attackStaminaCost        = 15f;
    public float attackCooldown           = 0.7f;
    public float criticalDamageMultiplier = 1.5f;

    [Header("Skills")]
    [Tooltip("Golpes de la cadena del ataque básico (combo).")]
    public int basicComboLength = 3;
    [Tooltip("Segundos tras acabar un golpe en los que el siguiente de la cadena aún se acepta.")]
    public float comboChainWindow = 0.4f;
    [Tooltip("Segundos que se recuerda una pulsación rechazada porque la fase actual no se puede interrumpir.")]
    public float skillInputBuffer = 0.2f;

    [Header("Stamina")]
    public float sprintStaminaCostPerSecond   = 10f;
    public float skill1StaminaCost            = 20f;
    public float skill2StaminaCost            = 25f;
    public float ultimateStaminaCost          = 40f;

    [Tooltip("Fraction of maxStamina required to clear exhaustion (0.2 = 20%)")]
    public float exhaustionRecoveryThreshold  = 0.2f;

    class Baker : Baker<HeroGameplayConfigAuthoring>
    {
        public override void Bake(HeroGameplayConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new HeroGameplayConfigComponent
            {
                attackStaminaCost           = authoring.attackStaminaCost,
                attackCooldown              = authoring.attackCooldown,
                criticalDamageMultiplier    = authoring.criticalDamageMultiplier,
                basicComboLength            = authoring.basicComboLength,
                comboChainWindow            = authoring.comboChainWindow,
                skillInputBuffer            = authoring.skillInputBuffer,
                sprintStaminaCostPerSecond  = authoring.sprintStaminaCostPerSecond,
                skill1StaminaCost           = authoring.skill1StaminaCost,
                skill2StaminaCost           = authoring.skill2StaminaCost,
                ultimateStaminaCost         = authoring.ultimateStaminaCost,
                exhaustionRecoveryThreshold = authoring.exhaustionRecoveryThreshold,
            });
        }
    }
}

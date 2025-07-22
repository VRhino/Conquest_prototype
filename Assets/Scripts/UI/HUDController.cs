using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Handles real time updates for the in-game HUD during combat.
/// It reads ECS component data from the local player and
/// reflects the values on the Unity UI.
/// </summary>
public class HUDController : MonoBehaviour
{
    [System.Serializable]
    public class AbilitySlot
    {
        public Image icon;
        public Image cooldownFill;
        [Tooltip("Entity that holds a CooldownComponent")] public Entity abilityEntity = Entity.Null;

        /// <summary>Updates the cooldown display.</summary>
        public void UpdateSlot(EntityManager em)
        {
            if (abilityEntity == Entity.Null || cooldownFill == null)
                return;

            if (em.HasComponent<CooldownComponent>(abilityEntity))
            {
                var cd = em.GetComponentData<CooldownComponent>(abilityEntity);
                if (cd.cooldownDuration > 0f)
                    cooldownFill.fillAmount = cd.currentCooldown / cd.cooldownDuration;
                else
                    cooldownFill.fillAmount = 0f;

                // Flash or hide fill when ready
                cooldownFill.enabled = !cd.isReady;
            }
        }
    }

    [Header("Section Toggles")]
    [SerializeField] GameObject _heroSection;
    [SerializeField] GameObject _abilitiesSection;
    [SerializeField] GameObject _squadSection;

    [Header("Hero Elements")]
    [SerializeField] Image _healthFill;
    [SerializeField] Image _staminaFill;
    [SerializeField] TMP_Text _healthText;
    [SerializeField] TMP_Text _staminaText;

    [Header("Ability Slots")]
    [SerializeField] AbilitySlot[] _abilitySlots = new AbilitySlot[4];

    [Header("Squad Elements")]
    [SerializeField] TMP_Text _unitCountText;
    [SerializeField] Image _formationIcon;

    void Update()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;

        UpdateHeroSection(em);
        UpdateSquadSection(em);
    }

    void UpdateHeroSection(EntityManager em)
    {
        if (_heroSection != null && !_heroSection.activeSelf)
            return;
    
        var query = em.CreateEntityQuery(
            ComponentType.ReadOnly<HeroHealthComponent>(),
            ComponentType.ReadOnly<StaminaComponent>(),
            ComponentType.ReadOnly<IsLocalPlayer>());
        if (query.IsEmptyIgnoreFilter)
            return;
        Entity hero = query.GetSingletonEntity();
        var health = em.GetComponentData<HeroHealthComponent>(hero);
        var stamina = em.GetComponentData<StaminaComponent>(hero);
       
        if (_healthFill != null)
            _healthFill.fillAmount = health.maxHealth > 0f ? health.currentHealth / health.maxHealth : 0f;
        if (_staminaFill != null)
            _staminaFill.fillAmount = stamina.maxStamina > 0f ? stamina.currentStamina / stamina.maxStamina : 0f;
        if (_healthText != null)
            _healthText.text = $"{Mathf.CeilToInt(health.currentHealth)} / {Mathf.CeilToInt(health.maxHealth)}";
        if (_staminaText != null)
            _staminaText.text = $"{Mathf.CeilToInt(stamina.currentStamina)} / {Mathf.CeilToInt(stamina.maxStamina)}";

        foreach (var slot in _abilitySlots)
            slot?.UpdateSlot(em);
    }

    void UpdateSquadSection(EntityManager em)
    {
        if (_squadSection != null && !_squadSection.activeSelf)
            return;

        var query = em.CreateEntityQuery(
            ComponentType.ReadOnly<SquadStatusComponent>(),
            ComponentType.ReadOnly<IsLocalPlayer>());

        if (query.IsEmptyIgnoreFilter)
            return;

        Entity squad = query.GetSingletonEntity();
        var status = em.GetComponentData<SquadStatusComponent>(squad);

        if (_unitCountText != null)
            _unitCountText.text = $"{status.aliveUnits}/{status.totalUnits}";

        if (_formationIcon != null)
        {
            // Placeholder: reduce fill while formation is on cooldown
            if (status.formationCooldown > 0f)
                _formationIcon.fillAmount = 1f - status.formationCooldown;
            else
                _formationIcon.fillAmount = 1f;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ConquestTactics.Visual;
using TMPro;

/// <summary>
/// Panel de detalle para mostrar toda la información de un escuadrón (SquadInstanceData + SquadData).
/// </summary>
public class SquadDetailPanel : MonoBehaviour
{
    [Header("Panel principal")]
    public GameObject mainPanel;

    [Header("Título")]
    public Image squadIcon;
    public Image squadIconBackground;
    public TMP_Text squadNameText;
    public TMP_Text squadTypeText;
    public Transform starsContainer;
    public GameObject starsPrefab;

    [Header("Progreso")]
    public TMP_Text currentLevelText;
    public TMP_Text maxLevelText;
    public Image experienceFill;
    public TMP_Text experiencePointsText;

    [Header("Status panel")]
    public TMP_Text leadershipText;
    public TMP_Text unitCountText;
    public TMP_Text equipmentText;
    public TMP_Text injuredText;

    [Header("Stats panel")]
    public TMP_Text healthText;
    public TMP_Text statsUnitCountText;
    public TMP_Text speedText;
    public TMP_Text statsLeadershipText;
    public TMP_Text rangeText;
    public TMP_Text ammoText;
    public TMP_Text piercingPenText;
    public TMP_Text slashingPenText;
    public TMP_Text bluntPenText;
    public TMP_Text piercingDmgText;
    public TMP_Text slashingDmgText;
    public TMP_Text bluntDmgText;
    public TMP_Text piercingDefText;
    public TMP_Text slashingDefText;
    public TMP_Text bluntDefText;
    public TMP_Text blockText;
    public TMP_Text blockRegenText;

    [Header("Formaciones")]
    public Transform formationsContainer;
    public GameObject formationItemPrefab;

    [Header("Habilidades")]
    public Transform abilitiesContainer;
    public GameObject abilityItemPrefab;


    [Header("Preview Visual")]
    public Transform modelContainer;
    private GameObject _currentVisualInstance;
    [Header("Botones")]
    public Button closeButton;

    public void Show(SquadInstanceData instance, SquadData data)
    {
        if (mainPanel != null)
            mainPanel.SetActive(true);

        // Limpiar visual anterior
        if (modelContainer != null && _currentVisualInstance != null)
        {
            Destroy(_currentVisualInstance);
            _currentVisualInstance = null;
        }
        
        instantiate3DModel(data);
        fillTitle(data);
        fillProgress(instance, data);
        fillStatusPanel(instance, data);
        fillStatsPanel(data);
        fillFormations(data);
        FillAbilities(data);

        // Botones
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }
    }

    public void Hide()
    {
        if (mainPanel != null)
            mainPanel.SetActive(false);

        // Eliminar visual instanciado
        if (_currentVisualInstance != null)
        {
            Destroy(_currentVisualInstance);
            _currentVisualInstance = null;
        }
    }

    // Ejemplo de cálculo de exp para siguiente nivel (ajusta según tu sistema real)
    private int GetExpForNextLevel(int level, SquadData data)
    {
        // Placeholder: 100 * nivel
        return 1000;
    }
    private void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null) return;
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            if (child != null)
                SetLayerRecursively(child.gameObject, layer);
        }
    }
    public static readonly Dictionary<SquadRarity, (Color color, float stars)> RarityInfo = new()
    {
        { SquadRarity.peasant_tier,   (new Color(0.5f, 0.5f, 0.5f), 0.5f) }, // gray
        { SquadRarity.levy_tier,      (new Color(0.5f, 0.5f, 0.5f), 1f) },
        { SquadRarity.conscript_tier, (new Color(0.5f, 0.5f, 0.5f), 1.5f) },
        { SquadRarity.trained_tier,   (Color.green, 2f) },
        { SquadRarity.seasoned_tier,  (Color.green, 2.5f) },
        { SquadRarity.veteran_tier,   (new Color(0.3f, 0.5f, 1f), 3f) }, // blue
        { SquadRarity.hardened_tier,  (new Color(0.3f, 0.5f, 1f), 3.5f) },
        { SquadRarity.elite_tier,     (new Color(0.6f, 0.2f, 0.7f), 4f) }, // purple
        { SquadRarity.master_tier,    (new Color(0.6f, 0.2f, 0.7f), 4.5f) },
        { SquadRarity.legendary_tier, (new Color(1f, 0.85f, 0.2f), 5f) } // golden
    };

    public static (Color color, float stars) GetRarityInfo(SquadRarity rarity)
    {
        if (RarityInfo.TryGetValue(rarity, out var info))
            return info;
        return (Color.white, 0f);
    }

    private void fillTitle(SquadData data)
    {
        if (squadIcon != null) squadIcon.sprite = data.icon;
        if (squadIconBackground != null) squadIconBackground.sprite = data.background;
        if (squadNameText != null) squadNameText.text = data.squadName;
        if (squadTypeText != null) squadTypeText.text = data.unitType.ToString();
        buildRarityUI(data.rarity);
    }

    private void buildRarityUI(SquadRarity rarity)
    {
        if (starsContainer == null || starsPrefab == null)
        {
            Debug.LogWarning("[SquadDetailPanel] 2 starsContainer o starsPrefab no asignados");
            return;
        }
        // Limpiar estrellas previas
        foreach (Transform child in starsContainer)
        {
            Destroy(child.gameObject);
        }
        // Obtener información de rareza
        if (RarityInfo.TryGetValue(rarity, out var info))
        {
            // Crear estrellas según la información de rareza, incluyendo medias estrellas
            int fullStars = Mathf.FloorToInt(info.stars);
            float partialStar = info.stars - fullStars;
            
            // Crear estrellas completas
            for (int i = 0; i < fullStars; i++)
            {
                var starGO = Instantiate(starsPrefab, starsContainer);
                var starImage = starGO.GetComponent<Image>();
                if (starImage != null)
                {
                    starImage.fillAmount = 1f; // Estrella completa
                }
            }
            
            // Crear media estrella si es necesario
            if (partialStar > 0f)
            {
                var starGO = Instantiate(starsPrefab, starsContainer);
                var starImage = starGO.GetComponent<Image>();
                if (starImage != null)
                {
                    starImage.fillAmount = partialStar; // Estrella parcial
                }
            }
        }
        else
        {
            Debug.LogWarning($"[SquadDetailPanel] No se encontró información de rareza para: {rarity}");
        }
    }

    private void fillProgress(SquadInstanceData instance, SquadData data)
    {
        if (currentLevelText != null) currentLevelText.text = instance.level.ToString();
        if (maxLevelText != null) maxLevelText.text = "10"; // Placeholder
        int expForNext = GetExpForNextLevel(instance.level, data);
        if (experienceFill != null)
            experienceFill.fillAmount = expForNext > 0 ? (float)instance.experience / expForNext : 0f;
        if (experiencePointsText != null)
            experiencePointsText.text = $"{instance.experience}/{expForNext}";

    }

    private void fillStatusPanel(SquadInstanceData instance, SquadData data)
    {
        if (leadershipText != null) leadershipText.text = data.leadershipCost.ToString();
        if (unitCountText != null) unitCountText.text = $"{instance.unitsInSquad - (instance.unitsKilled + instance.unitsInjured)}/{instance.unitsInSquad}";
        if (equipmentText != null) equipmentText.text = $"{instance.unitsInSquad - instance.equipmentLost}/{instance.unitsInSquad}";
        if (injuredText != null) injuredText.text = instance.unitsInjured.ToString();

    }

    private void fillStatsPanel(SquadData data)
    {
        if (healthText != null) healthText.text = data.baseHealth.ToString();
        if (statsUnitCountText != null) statsUnitCountText.text = data.unitCount.ToString();
        if (speedText != null) speedText.text = data.baseSpeed.ToString("F1");
        if (statsLeadershipText != null) statsLeadershipText.text = data.leadershipCost.ToString();
        if (rangeText != null) rangeText.text = data.range.ToString() == "0" ? "-" : data.range.ToString();
        if (ammoText != null) ammoText.text = data.ammo.ToString() == "0" ? "-" : data.ammo.ToString();
        if (piercingPenText != null) piercingPenText.text = data.piercingPenetration.ToString();
        if (slashingPenText != null) slashingPenText.text = data.slashingPenetration.ToString();
        if (bluntPenText != null) bluntPenText.text = data.bluntPenetration.ToString();
        if (piercingDmgText != null) piercingDmgText.text = data.piercingDamage.ToString();
        if (slashingDmgText != null) slashingDmgText.text = data.slashingDamage.ToString();
        if (bluntDmgText != null) bluntDmgText.text = data.bluntDamage.ToString();
        if (piercingDefText != null) piercingDefText.text = data.piercingDefense.ToString();
        if (slashingDefText != null) slashingDefText.text = data.slashingDefense.ToString();
        if (bluntDefText != null) bluntDefText.text = data.bluntDefense.ToString();
        if (blockText != null) blockText.text = data.block.ToString() == "0" ? "-" : data.block.ToString();
        if (blockRegenText != null) blockRegenText.text = data.blockRegenRate.ToString() == "0" ? "-" : data.blockRegenRate.ToString();
    }

    private void fillFormations(SquadData data)
    {

        if (formationsContainer != null && formationItemPrefab != null)
        {
            foreach (Transform c in formationsContainer) Destroy(c.gameObject);
            foreach (var formation in data.gridFormations)
            {
                var go = Instantiate(formationItemPrefab, formationsContainer);
                var formationOptionUI = go.GetComponentInChildren<FormationOptionUI>();
                if (formationOptionUI != null) formationOptionUI.SetFormation(formation);
            }
        }
    }

    private void FillAbilities(SquadData data)
    {
        if (abilitiesContainer != null && abilityItemPrefab != null)
        {
            foreach (Transform c in abilitiesContainer) Destroy(c.gameObject);
            foreach (var ability in data.abilitiesByLevel)
            {
                var go = Instantiate(abilityItemPrefab, abilitiesContainer);
                var txt = go.GetComponentInChildren<TMP_Text>();
                if (txt != null) txt.text = ability != null ? ability.name : "-";
            }
        }
    }
    
    private void instantiate3DModel(SquadData data)
    {
        if (modelContainer != null && data != null && data.prefab != null)
        {
            var registry = VisualPrefabRegistry.Instance;
            if (registry != null)
            {
                var visualPrefab = registry.GetPrefab(data.visualPrefabName);
                if (visualPrefab != null)
                {
                    // Deshabilitar EntityVisualSync en el prefab antes de instanciar
                    var visualSync = visualPrefab.GetComponent<EntityVisualSync>();
                    if (visualSync != null)
                        visualSync.enabled = false;

                    // Cambiar layer recursivamente a squad_preview
                    int previewLayer = LayerMask.NameToLayer("squad_preview");
                    if (previewLayer >= 0)
                        SetLayerRecursively(visualPrefab, previewLayer);

                    _currentVisualInstance = Instantiate(visualPrefab, modelContainer);
                    _currentVisualInstance.transform.localPosition = Vector3.zero;
                    _currentVisualInstance.transform.localRotation = Quaternion.identity;
                    _currentVisualInstance.transform.localScale = Vector3.one;
                    // Apagar CharacterController si existe
                    var cc = _currentVisualInstance.GetComponent<CharacterController>();
                    if (cc != null)
                        cc.enabled = false;
                }
                else
                {
                    Debug.LogWarning($"[SquadDetailPanel] No se encontró visualPrefab '{data.visualPrefabName}' en VisualPrefabRegistry");
                }
            }
            else
            {
                Debug.LogWarning("[SquadDetailPanel] VisualPrefabRegistry.Instance es null");
            }
        }
    }
}

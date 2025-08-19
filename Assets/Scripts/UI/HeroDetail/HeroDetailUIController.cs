using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Data.Items;

/// <summary>
/// Controlador principal para el panel de detalles del héroe.
/// Maneja la apertura/cierre del panel y coordina todos los sub-componentes.
/// Sigue el patrón singleton similar a InventoryPanelController.
/// </summary>
public class HeroDetailUIController : MonoBehaviour
{
    #region Singleton Pattern
    
    public static HeroDetailUIController Instance { get; private set; }

    #endregion

    #region UI References - Basado en Hero_detail_prefab_structure.md
    
    [Header("Main Panel")]
    public GameObject mainPanel;
    
    [Header("3D Preview")]
    public RawImage previewImage;
    
    [Header("Left Panel - Hero Status")]
    public TMP_Text heroNameText;
    public TMP_Text levelText;
    public TMP_Text expText;
    public Image expBarForeground;
    public TMP_Text actualLevelText;
    public TMP_Text nextLevelText;
    
    [Header("Left Panel - Basic Stats")]
    public GameObject basicStatsPanel;
    
    // Leadership
    public TMP_Text leadershipValueText;
    public Button leadershipMoreButton;
    public Button leadershipMinusButton;
    
    // Strength (Fuerza)
    public TMP_Text strengthValueText;
    public Button strengthMoreButton;
    public Button strengthMinusButton;
    
    // Agility (Destreza)
    public TMP_Text agilityValueText;
    public Button agilityMoreButton;
    public Button agilityMinusButton;
    
    // Armor
    public TMP_Text armorValueText;
    public Button armorMoreButton;
    public Button armorMinusButton;
    
    // Toughness (Vitalidad)
    public TMP_Text toughnessValueText;
    public Button toughnessMoreButton;
    public Button toughnessMinusButton;
    
    [Header("Left Panel - Buttons")]
    public Button moreDetailsButton;
    public Button resetButton;
    
    [Header("Right Panel - Equipment")]
    
    // Equipment Slots
    public GameObject helmetSlot;
    public GameObject torsoSlot;
    public GameObject glovesSlot;
    public GameObject pantsSlot;
    public GameObject bootsSlot;
    public GameObject weaponSlot;
    
    [Header("Right Panel - Repair")]
    public Button repairButton;
    public Button repairAllButton;
    
    [Header("Detailed Attributes Panel")]
    public GameObject attributesDetailPanel;
    public TMP_Text healthText;
    public TMP_Text staminaText;
    public TMP_Text piercingPenetrationText;
    public TMP_Text slashingPenetrationText;
    public TMP_Text bluntPenetrationText;
    public TMP_Text piercingDamageText;
    public TMP_Text slashingDamageText;
    public TMP_Text bluntDamageText;
    public TMP_Text piercingDefenseText;
    public TMP_Text slashingDefenseText;
    public TMP_Text bluntDefenseText;
    public TMP_Text blockText;
    public TMP_Text blockRegenText;

    #endregion

    #region Private Fields

    private HeroData _currentHeroData;
    private bool _isDetailedPanelVisible = false;
    private Dictionary<string, int> _tempAttributeChanges = new Dictionary<string, int>();
    
    // Referencias a sub-componentes (se crearán en las siguientes fases)
    // private HeroDetail3DPreview _preview3D;
    // private HeroDetailStatsPanel _statsPanel;
    // private HeroDetailAttributePanel _attributePanel;
    // private List<HeroDetailEquipmentSlot> _equipmentSlots;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("[HeroDetailUIController] Multiple instances detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        
        // Inicializar panel oculto
        if (mainPanel != null)
            mainPanel.SetActive(false);
    }

    void Start()
    {
        InitializeUI();
        SetupButtonListeners();
    }

    void OnDestroy()
    {
        // Limpiar singleton
        if (Instance == this)
        {
            Instance = null;
        }
        
        // Desuscribirse de eventos cuando se implemente
        // CleanupEventListeners();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Abre el panel de detalles del héroe.
    /// </summary>
    public void OpenPanel()
    {
        var heroData = PlayerSessionService.SelectedHero;
        if (heroData == null)
        {
            Debug.LogWarning("[HeroDetailUIController] No hay héroe seleccionado");
            return;
        }
        
        OpenPanel(heroData);
    }

    /// <summary>
    /// Abre el panel de detalles para un héroe específico.
    /// </summary>
    public void OpenPanel(HeroData heroData)
    {
        if (heroData == null)
        {
            Debug.LogError("[HeroDetailUIController] HeroData es null");
            return;
        }

        _currentHeroData = heroData;
        _tempAttributeChanges.Clear();
        
        if (mainPanel != null)
        {
            mainPanel.SetActive(true);
            PopulateUI();
        }
        
        Debug.Log($"[HeroDetailUIController] Panel abierto para héroe: {heroData.heroName}");
    }

    /// <summary>
    /// Cierra el panel de detalles del héroe.
    /// </summary>
    public void ClosePanel()
    {
        if (mainPanel != null)
        {
            mainPanel.SetActive(false);
        }
        
        // Limpiar datos temporales
        _currentHeroData = null;
        _tempAttributeChanges.Clear();
        _isDetailedPanelVisible = false;
        
        // Ocultar panel detallado si estaba visible
        if (attributesDetailPanel != null)
        {
            attributesDetailPanel.SetActive(false);
        }
        
        Debug.Log("[HeroDetailUIController] Panel cerrado");
    }

    /// <summary>
    /// Alterna la visibilidad del panel.
    /// </summary>
    public void TogglePanel()
    {
        if (mainPanel != null && mainPanel.activeSelf)
        {
            ClosePanel();
        }
        else
        {
            OpenPanel();
        }
    }

    /// <summary>
    /// Verifica si el panel está actualmente visible.
    /// </summary>
    public bool IsPanelOpen => mainPanel != null && mainPanel.activeSelf;

    #endregion

    #region UI Population

    private void PopulateUI()
    {
        if (_currentHeroData == null) return;
        
        PopulateHeroStatus();
        PopulateBasicStats();
        PopulateEquipmentSlots();
        UpdateDetailedAttributesPanel();
    }

    private void PopulateHeroStatus()
    {
        if (_currentHeroData == null) return;
        
        // Nombre del héroe
        if (heroNameText != null)
        {
            heroNameText.text = _currentHeroData.heroName;
        }
        
        // Nivel
        if (levelText != null)
        {
            levelText.text = _currentHeroData.level.ToString();
        }
        
        // Experiencia - Se implementará completamente en fase posterior
        if (expText != null)
        {
            expText.text = $"EXP: {_currentHeroData.currentXP}";
        }
        
        // TODO: Calcular experiencia para próximo nivel y actualizar barra
        // TODO: Actualizar sprite del nivel basado en rarity
    }

    private void PopulateBasicStats()
    {
        if (_currentHeroData == null) return;
        
        // Usar cache de atributos si está disponible
        string heroKey = string.IsNullOrEmpty(_currentHeroData.heroName) ? 
                        _currentHeroData.classId : _currentHeroData.heroName;
        
        var cachedAttributes = DataCacheService.GetCachedAttributes(heroKey);
        
        // Leadership - Se calculará basado en stats + equipo
        UpdateStatDisplay("leadership", 0, leadershipValueText, leadershipMoreButton, leadershipMinusButton); // TODO: Calcular leadership real
        
        // Strength (Fuerza)
        UpdateStatDisplay("fuerza", _currentHeroData.fuerza, strengthValueText, strengthMoreButton, strengthMinusButton);
        
        // Agility (Destreza) 
        UpdateStatDisplay("destreza", _currentHeroData.destreza, agilityValueText, agilityMoreButton, agilityMinusButton);
        
        // Armor
        UpdateStatDisplay("armadura", _currentHeroData.armadura, armorValueText, armorMoreButton, armorMinusButton);
        
        // Toughness (Vitalidad)
        UpdateStatDisplay("vitalidad", _currentHeroData.vitalidad, toughnessValueText, toughnessMoreButton, toughnessMinusButton);
    }

    private void UpdateStatDisplay(string statName, int baseValue, TMP_Text valueText, Button moreButton, Button minusButton)
    {
        if (valueText == null) return;
        
        // Obtener cambio temporal si existe
        _tempAttributeChanges.TryGetValue(statName, out int tempChange);
        int displayValue = baseValue + tempChange;
        
        // Actualizar texto con color
        if (tempChange != 0)
        {
            valueText.color = Color.green;
            valueText.text = $"{displayValue}";
        }
        else
        {
            valueText.color = Color.white;
            valueText.text = displayValue.ToString();
        }
        
        // Mostrar/ocultar botones basado en puntos disponibles
        bool hasAttributePoints = _currentHeroData.attributePoints > 0;
        bool canDecrease = tempChange > 0;
        
        if (moreButton != null)
        {
            moreButton.gameObject.SetActive(hasAttributePoints);
        }
        
        if (minusButton != null)
        {
            minusButton.gameObject.SetActive(canDecrease);
        }
    }

    private void PopulateEquipmentSlots()
    {
        // Se implementará en Fase 3 cuando se creen los componentes de slots
        // Por ahora, logging básico
        if (_currentHeroData?.equipment != null)
        {
            Debug.Log($"[HeroDetailUIController] Equipment loaded - Weapon: {_currentHeroData.equipment.weapon?.itemId ?? "None"}");
            Debug.Log($"[HeroDetailUIController] Equipment loaded - Helmet: {_currentHeroData.equipment.helmet?.itemId ?? "None"}");
            // ... otros slots
        }
    }

    private void UpdateDetailedAttributesPanel()
    {
        if (!_isDetailedPanelVisible || attributesDetailPanel == null) return;
        
        string heroKey = string.IsNullOrEmpty(_currentHeroData.heroName) ? 
                        _currentHeroData.classId : _currentHeroData.heroName;
        
        var cachedAttributes = DataCacheService.GetCachedAttributes(heroKey);
        if (cachedAttributes == null) return;
        
        // Actualizar todos los valores detallados
        if (healthText != null) healthText.text = $"Health: {cachedAttributes.maxHealth:F1}";
        if (staminaText != null) staminaText.text = $"Stamina: {cachedAttributes.stamina:F1}";
        
        if (piercingDamageText != null) piercingDamageText.text = $"Piercing Damage: {cachedAttributes.piercingDamage:F1}";
        if (slashingDamageText != null) slashingDamageText.text = $"Slashing Damage: {cachedAttributes.slashingDamage:F1}";
        if (bluntDamageText != null) bluntDamageText.text = $"Blunt Damage: {cachedAttributes.bluntDamage:F1}";
        
        if (piercingDefenseText != null) piercingDefenseText.text = $"Piercing Defense: {cachedAttributes.pierceDefense:F1}";
        if (slashingDefenseText != null) slashingDefenseText.text = $"Slashing Defense: {cachedAttributes.slashDefense:F1}";
        if (bluntDefenseText != null) bluntDefenseText.text = $"Blunt Defense: {cachedAttributes.bluntDefense:F1}";
        
        if (piercingPenetrationText != null) piercingPenetrationText.text = $"Piercing Penetration: {cachedAttributes.piercePenetration:F1}";
        if (slashingPenetrationText != null) slashingPenetrationText.text = $"Slashing Penetration: {cachedAttributes.slashPenetration:F1}";
        if (bluntPenetrationText != null) bluntPenetrationText.text = $"Blunt Penetration: {cachedAttributes.bluntPenetration:F1}";
        
        if (blockText != null) blockText.text = $"Block Power: {cachedAttributes.blockPower:F1}";
        // if (blockRegenText != null) blockRegenText.text = $"Block Regen: {0:F1}"; // TODO: Implementar en CalculatedAttributes
    }

    #endregion

    #region UI Event Handlers

    private void InitializeUI()
    {
        // Asegurar que el panel detallado esté oculto inicialmente
        if (attributesDetailPanel != null)
        {
            attributesDetailPanel.SetActive(false);
            _isDetailedPanelVisible = false;
        }
    }

    private void SetupButtonListeners()
    {
        // Botón More Details (toggle)
        if (moreDetailsButton != null)
        {
            moreDetailsButton.onClick.RemoveAllListeners();
            moreDetailsButton.onClick.AddListener(ToggleDetailedPanel);
        }
        
        // Botón Reset
        if (resetButton != null)
        {
            resetButton.onClick.RemoveAllListeners();
            resetButton.onClick.AddListener(ResetTempChanges);
        }
        
        // Botones de stats - se implementarán en fase posterior
        SetupStatButtons();
        
        // Botones de reparación - placeholder para fase posterior
        if (repairButton != null)
        {
            repairButton.onClick.RemoveAllListeners();
            repairButton.onClick.AddListener(() => Debug.Log("Repair individual - TODO"));
        }
        
        if (repairAllButton != null)
        {
            repairAllButton.onClick.RemoveAllListeners();
            repairAllButton.onClick.AddListener(() => Debug.Log("Repair all - TODO"));
        }
    }

    private void SetupStatButtons()
    {
        // Leadership buttons
        SetupStatButtonPair("leadership", leadershipMoreButton, leadershipMinusButton);
        
        // Strength buttons  
        SetupStatButtonPair("fuerza", strengthMoreButton, strengthMinusButton);
        
        // Agility buttons
        SetupStatButtonPair("destreza", agilityMoreButton, agilityMinusButton);
        
        // Armor buttons
        SetupStatButtonPair("armadura", armorMoreButton, armorMinusButton);
        
        // Toughness buttons
        SetupStatButtonPair("vitalidad", toughnessMoreButton, toughnessMinusButton);
    }

    private void SetupStatButtonPair(string statName, Button moreButton, Button minusButton)
    {
        if (moreButton != null)
        {
            moreButton.onClick.RemoveAllListeners();
            moreButton.onClick.AddListener(() => ModifyStat(statName, 1));
        }
        
        if (minusButton != null)
        {
            minusButton.onClick.RemoveAllListeners();
            minusButton.onClick.AddListener(() => ModifyStat(statName, -1));
        }
    }

    private void ToggleDetailedPanel()
    {
        if (attributesDetailPanel == null) return;
        
        _isDetailedPanelVisible = !_isDetailedPanelVisible;
        attributesDetailPanel.SetActive(_isDetailedPanelVisible);
        
        if (_isDetailedPanelVisible)
        {
            UpdateDetailedAttributesPanel();
        }
        
        Debug.Log($"[HeroDetailUIController] Panel detallado: {(_isDetailedPanelVisible ? "Mostrado" : "Oculto")}");
    }

    private void ModifyStat(string statName, int change)
    {
        if (_currentHeroData == null) return;
        
        // Verificar puntos disponibles para aumentar
        if (change > 0 && _currentHeroData.attributePoints <= 0)
        {
            Debug.Log("[HeroDetailUIController] No hay puntos de atributo disponibles");
            return;
        }
        
        // Verificar que no se puede reducir por debajo de 0
        _tempAttributeChanges.TryGetValue(statName, out int currentTempChange);
        if (change < 0 && currentTempChange <= 0)
        {
            Debug.Log($"[HeroDetailUIController] No se puede reducir {statName} más");
            return;
        }
        
        // Aplicar cambio temporal
        _tempAttributeChanges[statName] = currentTempChange + change;
        
        // Si vuelve a 0, remover del diccionario
        if (_tempAttributeChanges[statName] == 0)
        {
            _tempAttributeChanges.Remove(statName);
        }
        
        // Actualizar UI
        PopulateBasicStats();
        
        // Mostrar panel detallado automáticamente si se modifica un stat
        if (!_isDetailedPanelVisible)
        {
            ToggleDetailedPanel();
        }
        else
        {
            UpdateDetailedAttributesPanel();
        }
        
        Debug.Log($"[HeroDetailUIController] {statName} modificado en {change}. Cambio total: {_tempAttributeChanges.GetValueOrDefault(statName, 0)}");
    }

    private void ResetTempChanges()
    {
        _tempAttributeChanges.Clear();
        PopulateBasicStats();
        UpdateDetailedAttributesPanel();
        Debug.Log("[HeroDetailUIController] Cambios temporales reseteados");
    }

    #endregion
}
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
    
    // Available Points Display
    public GameObject availablePointsPanel;
    public TMP_Text availablePointsText;
    
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
    public Button saveButton;
    public Button cancelButton;
    
    [Header("Right Panel - Equipment")]
    
    // Equipment Panel Controller (Fase 3) - se inicializa dinámicamente 
    private MonoBehaviour equipmentPanel;
    
    // Equipment Slots (Individual GameObjects - para compatibilidad con prefab)
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
        
        // Inicializar el InventoryManager para que los equipment slots puedan funcionar
        // Sin esto, las operaciones como CanUnequipCurrentItem() fallan porque InventoryStorageService no está inicializado
        if (!InventoryManager.IsInitialized)
        {
            InventoryManager.Initialize(heroData);
        }
        
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
        ToggleUIInteraction();
    }

     private void ToggleUIInteraction()
    {
        if (DialogueUIState.IsDialogueOpen)
        {
            DialogueUIState.IsDialogueOpen = false;
            if (HeroCameraController.Instance != null)
                HeroCameraController.Instance.SetCameraFollowEnabled(true);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            DialogueUIState.IsDialogueOpen = true;
            if (HeroCameraController.Instance != null)
                HeroCameraController.Instance.SetCameraFollowEnabled(false);
            
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
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
        
        // Obtener información de experiencia usando el nuevo calculador
        var expInfo = HeroExperienceCalculator.GetExperienceInfo(_currentHeroData);
        
        // Nivel
        if (levelText != null) levelText.text = expInfo.currentLevel.ToString();
        
        // Experiencia
        if (expText != null) expText.text = expInfo.GetProgressText();

        // Barra de progreso de experiencia
        if (expBarForeground != null) expBarForeground.fillAmount = expInfo.progressToNextLevel;
        
        // Nivel actual y siguiente
        if (actualLevelText != null) actualLevelText.text = expInfo.currentLevel.ToString();
        
        if (nextLevelText != null)
        {
            if (expInfo.isMaxLevel)
                nextLevelText.text = "MAX";
            else
                nextLevelText.text = (expInfo.currentLevel + 1).ToString();
        }
    }

    private void PopulateBasicStats()
    {
        if (_currentHeroData == null) return;
        
        // Obtener ID del héroe para cache y servicios temporales
        string heroId = GetHeroId(_currentHeroData);
        
        // Actualizar puntos disponibles
        UpdateAvailablePointsDisplay();
        
        // Usar atributos con cambios temporales si están disponibles
        var attributes = HeroTempAttributeService.HasTempChanges(heroId) 
            ? HeroTempAttributeService.GetAttributesWithTempChanges(heroId)
            : DataCacheService.GetCachedAttributes(heroId);
        
        if (attributes == null)
        {
            Debug.LogWarning($"[HeroDetailUIController] No se pudieron obtener atributos para hero: {heroId}");
            return;
        }
        
        UpdateStatDisplay("leadership", attributes.leadership, leadershipValueText, leadershipMoreButton, leadershipMinusButton, false);
        UpdateStatDisplay("fuerza", attributes.strength, strengthValueText, strengthMoreButton, strengthMinusButton);
        UpdateStatDisplay("destreza", attributes.dexterity, agilityValueText, agilityMoreButton, agilityMinusButton);
        UpdateStatDisplay("armadura", attributes.armor, armorValueText, armorMoreButton, armorMinusButton);
        UpdateStatDisplay("vitalidad", attributes.vitality, toughnessValueText, toughnessMoreButton, toughnessMinusButton);
    }

    /// <summary>
    /// Actualiza el panel de puntos disponibles con la información actual.
    /// </summary>
    private void UpdateAvailablePointsDisplay()
    {
        if (_currentHeroData == null || availablePointsText == null) return;
        
        string heroId = GetHeroId(_currentHeroData);
        // Usar el mismo método que el validador para consistencia
        int availablePoints = HeroTempAttributeService.GetAvailablePoints(heroId, _currentHeroData);
        
        availablePointsText.text = $"{availablePoints}";
        
        availablePointsText.color = Color.white;
        
        // Mostrar/ocultar panel solo si hay puntos disponibles o cambios temporales
        bool showPanel = availablePoints > 0 || HeroTempAttributeService.HasTempChanges(heroId);
        if (availablePointsPanel != null) availablePointsPanel.SetActive(showPanel);
    }

    private void UpdateStatDisplay(string statName, float displayValue, TMP_Text valueText, Button moreButton, Button minusButton, bool canModify = true)
    {
        if (valueText == null) return;
        
        string heroId = GetHeroId(_currentHeroData);
        
        // Obtener valor base y temporal
        float baseValue = HeroAttributeValidator.GetCurrentAttributeValue(_currentHeroData, statName);
        float tempValue = HeroTempAttributeService.GetTempAttributeValue(heroId, statName, baseValue);
        
        bool hasChanges = Mathf.Abs(tempValue - baseValue) > 0.01f;
        
        // Actualizar texto con color
        if (hasChanges)
        {
            valueText.color = Color.green;
            valueText.text = $"{tempValue:F0}";
        }
        else
        {
            valueText.color = Color.white;
            valueText.text = $"{displayValue:F0}";
        }
        
        // Mostrar/ocultar botones basado en validaciones y disponibilidad
        bool canIncrease = canModify && HeroAttributeValidator.CanIncrementAttribute(_currentHeroData, statName);
        bool canDecrease = canModify && HeroAttributeValidator.CanDecrementAttribute(_currentHeroData, statName) && hasChanges;
        
        if (moreButton != null) moreButton.gameObject.SetActive(canIncrease);

        if (minusButton != null) minusButton.gameObject.SetActive(canDecrease);
    }

    private void PopulateEquipmentSlots()
    {
        if (equipmentPanel == null) InitializeEquipmentPanel();

        if (equipmentPanel != null)
        {
            // Usar reflexión para evitar errores de compilación temporales
            var populateMethod = equipmentPanel.GetType().GetMethod("PopulateFromSelectedHero");
            if (populateMethod != null)
            {
                populateMethod.Invoke(equipmentPanel, null);
                Debug.Log("[HeroDetailUIController] Equipment slots populated via HeroEquipmentPanel");
            }
            else
                Debug.LogWarning("[HeroDetailUIController] PopulateFromSelectedHero method not found on equipment panel");
        }
        else
            if (_currentHeroData?.equipment != null) Debug.Log($"[HeroDetailUIController] Equipment loaded - Helmet: {_currentHeroData.equipment.helmet?.itemId ?? "None"}");
    }

    private void UpdateDetailedAttributesPanel()
    {
        if (!_isDetailedPanelVisible || attributesDetailPanel == null) return;
        
        string heroId = GetHeroId(_currentHeroData);
        
        // Obtener valores base (sin cambios temporales)
        var baseAttributes = DataCacheService.GetCachedAttributes(heroId);
        
        // Obtener valores con cambios temporales si existen
        var currentAttributes = HeroTempAttributeService.HasTempChanges(heroId) 
            ? HeroTempAttributeService.GetAttributesWithTempChanges(heroId)
            : baseAttributes;
            
        if (baseAttributes == null || currentAttributes == null) return;
        
        // Actualizar valores usando el formato "base+cambio"
        FormatAttributeValueWithChange(baseAttributes.maxHealth, currentAttributes.maxHealth, healthText, 0);
        FormatAttributeValueWithChange(baseAttributes.stamina, currentAttributes.stamina, staminaText, 0);
        
        FormatAttributeValueWithChange(baseAttributes.piercingDamage, currentAttributes.piercingDamage, piercingDamageText, 1);
        FormatAttributeValueWithChange(baseAttributes.slashingDamage, currentAttributes.slashingDamage, slashingDamageText, 1);
        FormatAttributeValueWithChange(baseAttributes.bluntDamage, currentAttributes.bluntDamage, bluntDamageText, 1);
        
        FormatAttributeValueWithChange(baseAttributes.pierceDefense, currentAttributes.pierceDefense, piercingDefenseText, 1);
        FormatAttributeValueWithChange(baseAttributes.slashDefense, currentAttributes.slashDefense, slashingDefenseText, 1);
        FormatAttributeValueWithChange(baseAttributes.bluntDefense, currentAttributes.bluntDefense, bluntDefenseText, 1);
        
        FormatAttributeValueWithChange(baseAttributes.piercePenetration, currentAttributes.piercePenetration, piercingPenetrationText, 1);
        FormatAttributeValueWithChange(baseAttributes.slashPenetration, currentAttributes.slashPenetration, slashingPenetrationText, 1);
        FormatAttributeValueWithChange(baseAttributes.bluntPenetration, currentAttributes.bluntPenetration, bluntPenetrationText, 1);
        
        FormatAttributeValueWithChange(baseAttributes.blockPower, currentAttributes.blockPower, blockText, 1);
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
        
        // Ocultar botones Save/Cancel inicialmente
        if (saveButton != null) saveButton.gameObject.SetActive(false);
        if (cancelButton != null) cancelButton.gameObject.SetActive(false);
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
        
        // Botón Save
        if (saveButton != null)
        {
            saveButton.onClick.RemoveAllListeners();
            saveButton.onClick.AddListener(SaveChanges);
        }
        
        // Botón Cancel
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(CancelChanges);
        }
        
        // Botones de stats
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
        SetupStatButtonPair("leadership", leadershipMoreButton, leadershipMinusButton);
        SetupStatButtonPair("fuerza", strengthMoreButton, strengthMinusButton);
        SetupStatButtonPair("destreza", agilityMoreButton, agilityMinusButton);
        SetupStatButtonPair("armadura", armorMoreButton, armorMinusButton);
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
        
        if (_isDetailedPanelVisible) UpdateDetailedAttributesPanel();
        
        Debug.Log($"[HeroDetailUIController] Panel detallado: {(_isDetailedPanelVisible ? "Mostrado" : "Oculto")}");
    }

    private void ModifyStat(string statName, int change)
    {
        if (_currentHeroData == null) return;
        
        string heroId = GetHeroId(_currentHeroData);
        
        // Obtener valor actual considerando cambios temporales
        float currentValue = HeroAttributeValidator.GetCurrentAttributeValue(_currentHeroData, statName);
        float currentTempValue = HeroTempAttributeService.GetTempAttributeValue(heroId, statName, currentValue);
        float newValue = currentTempValue + change;
        
        // Validar la modificación usando el validador
        if (!HeroAttributeValidator.CanModifyAttribute(_currentHeroData, statName, newValue, change))
            return;
        
        // Aplicar cambio temporal
        HeroTempAttributeService.ApplyTempChange(heroId, statName, newValue);
        
        // Actualizar UI
        PopulateBasicStats();
        
        // Mostrar panel detallado automáticamente si se modifica un stat
        if (!_isDetailedPanelVisible)
            ToggleDetailedPanel();
        else
            UpdateDetailedAttributesPanel();
        
        ShowUnsavedChangesIndicator();
    }

    private void ResetTempChanges()
    {
        if (_currentHeroData == null) return;
        
        string heroId = GetHeroId(_currentHeroData);
        HeroTempAttributeService.ClearTempChanges(heroId);
        
        PopulateBasicStats();
        UpdateDetailedAttributesPanel();
        HideUnsavedChangesIndicator();
    }

    #endregion

    #region Save/Cancel Operations

    /// <summary>
    /// Guarda todos los cambios temporales aplicándolos permanentemente al HeroData.
    /// </summary>
    public void SaveChanges()
    {
        if (_currentHeroData == null) return;
        
        string heroId = GetHeroId(_currentHeroData);
        
        if (!HeroTempAttributeService.HasTempChanges(heroId))
            return;
        
        ApplyTempChangesToHeroData();        
        SaveSystem.SavePlayer(PlayerSessionService.CurrentPlayer);        
        HeroTempAttributeService.ClearTempChanges(heroId);
        DataCacheService.RecalculateAttributes(PlayerSessionService.CurrentPlayer);
        
        // Refrescar UI
        PopulateBasicStats();
        HideUnsavedChangesIndicator();
        
        // Cerrar panel de detalles automáticamente después de guardar
        if (_isDetailedPanelVisible) ToggleDetailedPanel();
    }

    /// <summary>
    /// Cancela todos los cambios temporales sin persistir.
    /// </summary>
    public void CancelChanges()
    {
        if (_currentHeroData == null) return;
        
        string heroId = GetHeroId(_currentHeroData);
        HeroTempAttributeService.ClearTempChanges(heroId);
        
        PopulateBasicStats();
        UpdateDetailedAttributesPanel();
        HideUnsavedChangesIndicator();
    }

    /// <summary>
    /// Aplica los cambios temporales directamente al HeroData.
    /// </summary>
    private void ApplyTempChangesToHeroData()
    {
        if (_currentHeroData == null) return;
        
        string heroId = GetHeroId(_currentHeroData);
        var tempChanges = HeroTempAttributeService.GetTempChanges(heroId);
        
        if (tempChanges == null) return;
        
        // Calcular puntos gastados antes de aplicar cambios
        int pointsUsed = HeroTempAttributeService.CalculateUsedTempPoints(heroId, _currentHeroData);
        
        foreach (var change in tempChanges)
        {
            ApplyAttributeChangeToHeroData(change.Key, change.Value);
        }
        
        // Reducir puntos de atributo disponibles
        _currentHeroData.attributePoints = Mathf.Max(0, _currentHeroData.attributePoints - pointsUsed);        
    }

    /// <summary>
    /// Aplica un cambio específico de atributo al HeroData.
    /// </summary>
    private void ApplyAttributeChangeToHeroData(string attributeName, float newValue)
    {
        switch (attributeName.ToLower())
        {
            case "fuerza":
            case "strength":
                _currentHeroData.fuerza = (int)newValue;
                break;
            case "destreza":
            case "dexterity":
                _currentHeroData.destreza = (int)newValue;
                break;
            case "armadura":
            case "armor":
                _currentHeroData.armadura = (int)newValue;
                break;
            case "vitalidad":
            case "vitality":
                _currentHeroData.vitalidad = (int)newValue;
                break;
            default:
                Debug.LogWarning($"[HeroDetailUIController] Atributo no reconocido para persistir: {attributeName}");
                break;
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Inicializa el panel de equipamiento si no está ya inicializado.
    /// </summary>
    private void InitializeEquipmentPanel()
    {
        var panelType = System.Type.GetType("HeroEquipmentPanel");
        if (panelType != null)
        {
            equipmentPanel = GetComponentInChildren(panelType) as MonoBehaviour;
            
            if (equipmentPanel == null)
            {
                // Si no existe, buscar en los slots para crear uno dinámicamente
                var equipmentParent = FindEquipmentParent();
                if (equipmentParent != null)
                {
                    equipmentPanel = equipmentParent.AddComponent(panelType) as MonoBehaviour;
                }
            }
            
            if (equipmentPanel != null)
            {
                // Inicializar el panel usando reflexión
                var initMethod = equipmentPanel.GetType().GetMethod("InitializePanel");
                if (initMethod != null)
                {
                    initMethod.Invoke(equipmentPanel, null);
                }
            }
        }
        else
        {
            Debug.LogWarning("[HeroDetailUIController] HeroEquipmentPanel class not found - may need Unity compilation");
        }
    }

    /// <summary>
    /// Encuentra el GameObject padre que contiene los slots de equipamiento.
    /// </summary>
    private GameObject FindEquipmentParent()
    {
        // Intentar encontrar un padre común de los slots de equipamiento
        if (helmetSlot != null) return helmetSlot.transform.parent?.gameObject;
        if (torsoSlot != null) return torsoSlot.transform.parent?.gameObject;
        if (weaponSlot != null) return weaponSlot.transform.parent?.gameObject;
        
        return gameObject; // Fallback al objeto principal
    }

    /// <summary>
    /// Obtiene el ID único del héroe para operaciones de cache y servicios.
    /// Utiliza el método centralizado de HeroAttributeValidator para consistencia.
    /// </summary>
    private string GetHeroId(HeroData heroData)
    {
        return HeroAttributeValidator.GetHeroId(heroData);
    }

    /// <summary>
    /// Muestra indicador visual de que hay cambios pendientes por guardar.
    /// </summary>
    private void ShowUnsavedChangesIndicator()
    {
        // Mostrar botones Save/Cancel
        if (saveButton != null) saveButton.gameObject.SetActive(true);
        if (cancelButton != null) cancelButton.gameObject.SetActive(true);
        
        Debug.Log("[HeroDetailUIController] Cambios pendientes - mostrar botones Save/Cancel");
    }

    /// <summary>
    /// Oculta el indicador visual de cambios pendientes.
    /// </summary>
    private void HideUnsavedChangesIndicator()
    {
        // Ocultar botones Save/Cancel
        if (saveButton != null) saveButton.gameObject.SetActive(false);
        if (cancelButton != null) cancelButton.gameObject.SetActive(false);
    }

    /// <summary>
    /// Formatea un valor de atributo mostrando el cambio si existe.
    /// </summary>
    /// <param name="baseValue">Valor base original</param>
    /// <param name="finalValue">Valor final con cambios aplicados</param>
    /// <param name="textComponent">Componente de texto a actualizar</param>
    /// <param name="decimals">Número de decimales a mostrar</param>
    private void FormatAttributeValueWithChange(float baseValue, float finalValue, TMP_Text textComponent, int decimals = 0)
    {
        if (textComponent == null) return;
        
        float difference = finalValue - baseValue;
        bool hasChanges = Mathf.Abs(difference) > 0.01f;
        
        if (hasChanges)
        {
            textComponent.color = Color.green;
            string format = decimals == 0 ? "F0" : $"F{decimals}";
            if (difference > 0)
            {
                textComponent.text = $"{baseValue.ToString(format)}+{difference.ToString(format)}";
            }
            else
            {
                textComponent.text = $"{baseValue.ToString(format)}{difference.ToString(format)}"; // Ya incluye el signo negativo
            }
        }
        else
        {
            textComponent.color = Color.white;
            string format = decimals == 0 ? "F0" : $"F{decimals}";
            textComponent.text = finalValue.ToString(format);
        }
    }

    #endregion
}

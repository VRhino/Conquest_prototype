using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Maneja el panel de estadísticas básicas del héroe con botones +/- para modificar atributos.
/// Especializado en la gestión de stats, validaciones, y persistencia temporal.
/// </summary>
public class HeroDetailStatsPanel : MonoBehaviour
{
    #region UI References

    [Header("Basic Stats Panel")]
    public GameObject basicStatsPanel;
    
    [Header("Available Points Display")]
    public GameObject availablePointsPanel;
    public TMP_Text availablePointsText;
    
    [Header("Leadership")]
    public TMP_Text leadershipValueText;
    public Button leadershipMoreButton;
    public Button leadershipMinusButton;
    
    [Header("Strength (Fuerza)")]
    public TMP_Text strengthValueText;
    public Button strengthMoreButton;
    public Button strengthMinusButton;
    
    [Header("Agility (Destreza)")]
    public TMP_Text agilityValueText;
    public Button agilityMoreButton;
    public Button agilityMinusButton;
    
    [Header("Armor")]
    public TMP_Text armorValueText;
    public Button armorMoreButton;
    public Button armorMinusButton;
    
    [Header("Toughness (Vitalidad)")]
    public TMP_Text toughnessValueText;
    public Button toughnessMoreButton;
    public Button toughnessMinusButton;
    
    [Header("Control Buttons")]
    public Button resetButton;
    public Button saveButton;
    public Button cancelButton;

    #endregion

    #region Events

    /// <summary>
    /// Evento disparado cuando se realizan cambios temporales en los stats.
    /// </summary>
    public System.Action OnTempChangesApplied;
    
    /// <summary>
    /// Evento disparado cuando se necesita mostrar el panel detallado automáticamente.
    /// </summary>
    public System.Action OnRequestDetailedPanelShow;

    /// <summary>
    /// Evento disparado cuando se necesita ocultar el panel detallado automáticamente.
    /// </summary>
    public System.Action OnRequestAttributesPanelHide;

    #endregion

    #region Private Fields

    private HeroData _currentHeroData;
    private bool _isInitialized = false;

    #endregion

    #region Unity Lifecycle

    void Start()
    {
        SetupButtonListeners();
        SubscribeToEvents();
    }

    void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Inicializa el panel con los datos del héroe.
    /// </summary>
    /// <param name="heroData">Datos del héroe</param>
    public void Initialize(HeroData heroData)
    {
        if (heroData == null)
        {
            Debug.LogError("[HeroDetailStatsPanel] HeroData es null");
            return;
        }

        _currentHeroData = heroData;
        _isInitialized = true;
        
        SetupButtonListeners();
        
        // Ocultar botones Save/Cancel inicialmente
        InitializeSaveCancelButtons();
        
        PopulateStats();
    }

    /// <summary>
    /// Actualiza la visualización de las estadísticas.
    /// </summary>
    public void PopulateStats()
    {
        if (!_isInitialized || _currentHeroData == null) return;
        
        // Obtener ID del héroe para cache y servicios temporales
        string heroId = GetHeroId(_currentHeroData);
        
        // Actualizar puntos disponibles
        UpdateAvailablePointsDisplay();
        
        // Usar nueva arquitectura: obtener atributos calculados con modificaciones temporales
        var attributes = HeroTempAttributeService.HasTempChanges(heroId) 
            ? DataCacheService.CalculateAttributes(_currentHeroData, HeroTempAttributeService.GetTempChangesAsEquipmentBonuses(heroId))
            : DataCacheService.CalculateAttributes(_currentHeroData);
        
        UpdateStatDisplay("leadership", attributes.leadership, leadershipValueText, leadershipMoreButton, leadershipMinusButton, false);
        UpdateStatDisplay("strength", attributes.finalStrength, strengthValueText, strengthMoreButton, strengthMinusButton);
        UpdateStatDisplay("dexterity", attributes.finalDexterity, agilityValueText, agilityMoreButton, agilityMinusButton);
        UpdateStatDisplay("armor", attributes.finalArmor, armorValueText, armorMoreButton, armorMinusButton);
        UpdateStatDisplay("vitality", attributes.finalVitality, toughnessValueText, toughnessMoreButton, toughnessMinusButton);
        
        // Actualizar visibilidad de botones Save/Cancel basado en si hay cambios temporales
        UpdateSaveCancelButtonsVisibility();
    }

    /// <summary>
    /// Resetea todos los cambios temporales.
    /// </summary>
    public void ResetTempChanges()
    {
        if (_currentHeroData == null) return;

        string heroId = GetHeroId(_currentHeroData);
        HeroTempAttributeService.ClearTempChanges(heroId);

        PopulateStats();
        UpdateSaveCancelButtonsVisibility();
        OnRequestAttributesPanelHide?.Invoke();
    }

    /// <summary>
    /// Guarda todos los cambios temporales aplicándolos permanentemente.
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
        DataCacheService.CacheAttributes(PlayerSessionService.SelectedHero); // Usar método específico para recalcular cache
        
        PopulateStats();
        UpdateSaveCancelButtonsVisibility();
        
        // Ocultar panel de atributos detallados al guardar
        OnRequestAttributesPanelHide?.Invoke();
        
        Debug.Log("[HeroDetailStatsPanel] Cambios guardados exitosamente");
    }

    /// <summary>
    /// Cancela todos los cambios temporales.
    /// </summary>
    public void CancelChanges()
    {
        if (_currentHeroData == null) return;
        
        string heroId = GetHeroId(_currentHeroData);
        HeroTempAttributeService.ClearTempChanges(heroId);
        
        PopulateStats();
        UpdateSaveCancelButtonsVisibility();
        
        // Ocultar panel de atributos detallados al cancelar
        OnRequestAttributesPanelHide?.Invoke();
        
        Debug.Log("[HeroDetailStatsPanel] Cambios cancelados");
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Actualiza el panel de puntos disponibles con la información actual.
    /// </summary>
    private void UpdateAvailablePointsDisplay()
    {
        if (_currentHeroData == null || availablePointsText == null) return;
        
        string heroId = GetHeroId(_currentHeroData);
        int availablePoints = HeroTempAttributeService.GetAvailablePoints(heroId, _currentHeroData);
        
        availablePointsText.text = $"{availablePoints}";
        availablePointsText.color = Color.white;
        
        // Mostrar/ocultar panel solo si hay puntos disponibles o cambios temporales
        bool showPanel = availablePoints > 0 || HeroTempAttributeService.HasTempChanges(heroId);
        if (availablePointsPanel != null) availablePointsPanel.SetActive(showPanel);
    }

    /// <summary>
    /// Actualiza la visibilidad de los botones Save/Cancel según si hay cambios temporales.
    /// </summary>
    private void UpdateSaveCancelButtonsVisibility()
    {
        if (_currentHeroData == null) return;
        
        string heroId = GetHeroId(_currentHeroData);
        bool hasChanges = HeroTempAttributeService.HasTempChanges(heroId);
        
        // Solo mostrar botones Save/Cancel cuando hay cambios temporales
        if (saveButton != null) saveButton.gameObject.SetActive(hasChanges);
        if (cancelButton != null) cancelButton.gameObject.SetActive(hasChanges);
        
        // También actualizar disponibilidad
        if (saveButton != null) saveButton.interactable = hasChanges;
        if (cancelButton != null) cancelButton.interactable = hasChanges;
    }

    /// <summary>
    /// Inicializa los botones Save/Cancel ocultos y desactivados.
    /// </summary>
    private void InitializeSaveCancelButtons()
    {
        // Ocultar botones Save/Cancel inicialmente
        if (saveButton != null) 
        {
            saveButton.gameObject.SetActive(false);
            saveButton.interactable = false;
        }
        if (cancelButton != null) 
        {
            cancelButton.gameObject.SetActive(false);
            cancelButton.interactable = false;
        }
    }

    private void UpdateStatDisplay(string statName, float displayValue, TMP_Text valueText, Button moreButton, Button minusButton, bool canModify = true)
    {
        if (valueText == null) return;
        
        string heroId = GetHeroId(_currentHeroData);
        
        // CORREGIDO: Usar nomenclatura clara
        float baseWithEquipment = DataCacheService.GetBaseWithEquipment(_currentHeroData, statName);
        float finalValue = HeroTempAttributeService.GetFinalAttributeValue(heroId, statName);
        
        bool hasChanges = Mathf.Abs(finalValue - baseWithEquipment) > 0.01f;
        
        // Actualizar texto con color
        if (hasChanges)
        {
            valueText.color = Color.green;
            valueText.text = $"{finalValue:F0}";
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

    private void SetupButtonListeners()
    {
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
    }

    private void SetupStatButtons()
    {
        SetupStatButtonPair("leadership", leadershipMoreButton, leadershipMinusButton);
        SetupStatButtonPair("strength", strengthMoreButton, strengthMinusButton);
        SetupStatButtonPair("dexterity", agilityMoreButton, agilityMinusButton);
        SetupStatButtonPair("armor", armorMoreButton, armorMinusButton);
        SetupStatButtonPair("vitality", toughnessMoreButton, toughnessMinusButton);
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

    private void ModifyStat(string statName, int change)
    {
        if (_currentHeroData == null) return;
        
        string heroId = GetHeroId(_currentHeroData);
        
        // CORREGIDO: Usar nomenclatura clara y eliminar doble conteo
        float currentValue = HeroTempAttributeService.GetFinalAttributeValue(heroId, statName);
        float newValue = currentValue + change;
        
        // Validar la modificación usando el validador
        if (!HeroAttributeValidator.CanModifyAttribute(_currentHeroData, statName, newValue, change))
            return;
        
        // Aplicar cambio temporal
        HeroTempAttributeService.ApplyTempChange(heroId, statName, newValue);
        
        // Actualizar UI
        PopulateStats();
        UpdateSaveCancelButtonsVisibility();
        
        // Disparar eventos
        OnTempChangesApplied?.Invoke();
        OnRequestDetailedPanelShow?.Invoke();
        
        Debug.Log($"[HeroDetailStatsPanel] Stat {statName} modificado a {newValue}");
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
    /// FIXED: Solo modifica los stats base, no incluye bonificaciones de equipamiento.
    /// </summary>
    private void ApplyAttributeChangeToHeroData(string attributeName, float newValue)
    {
        // CRITICAL FIX: newValue viene de modificaciones temporales que incluyen equipamiento
        // Necesitamos calcular solo la diferencia sobre el valor base original
        
        float currentBaseValue = GetCurrentBaseValue(attributeName);
        float equipmentBonus = GetEquipmentBonusForAttribute(attributeName);
        
        // El valor temporal incluye base + equipamiento + modificaciones
        // Necesitamos extraer solo las modificaciones: newValue - (base + equipamiento)
        float modificationOnly = newValue - (currentBaseValue + equipmentBonus);
        float newBaseValue = currentBaseValue + modificationOnly;
        
        switch (attributeName.ToLower())
        {
            case "strength":
                _currentHeroData.strength = Mathf.RoundToInt(newBaseValue);
                break;
            case "dexterity":
                _currentHeroData.dexterity = Mathf.RoundToInt(newBaseValue);
                break;
            case "armor":
                _currentHeroData.armor = Mathf.RoundToInt(newBaseValue);
                break;
            case "vitality":
                _currentHeroData.vitality = Mathf.RoundToInt(newBaseValue);
                break;
            default:
                Debug.LogWarning($"[HeroDetailStatsPanel] Atributo no reconocido para persistir: {attributeName}");
                break;
        }
        
        Debug.Log($"[HeroDetailStatsPanel] FIXED - {attributeName}: base {currentBaseValue} + equipment {equipmentBonus} + mod {modificationOnly} = new base {newBaseValue}");
    }

    /// <summary>
    /// Obtiene el valor base actual de un atributo sin bonificaciones.
    /// </summary>
    private float GetCurrentBaseValue(string attributeName)
    {
        switch (attributeName.ToLower())
        {
            case "strength": return _currentHeroData.strength;
            case "dexterity": return _currentHeroData.dexterity;
            case "armor": return _currentHeroData.armor;
            case "vitality": return _currentHeroData.vitality;
            default: return 0f;
        }
    }

    /// <summary>
    /// Obtiene la bonificación de equipamiento para un atributo específico.
    /// </summary>
    private float GetEquipmentBonusForAttribute(string attributeName)
    {
        var equipmentStats = EquipmentManagerService.CalculateTotalEquipmentStats();
        string capitalizedName = char.ToUpper(attributeName[0]) + attributeName.Substring(1).ToLower();
        return equipmentStats.ContainsKey(capitalizedName) ? equipmentStats[capitalizedName] : 0f;
    }
    /// <summary>
    /// Obtiene el ID único del héroe para operaciones de cache y servicios.
    /// </summary>
    private string GetHeroId(HeroData heroData)
    {
        return HeroAttributeValidator.GetHeroId(heroData);
    }

    #endregion

    #region Event Management

    /// <summary>
    /// Suscribe a eventos del sistema.
    /// </summary>
    private void SubscribeToEvents()
    {
        DataCacheService.OnHeroCacheUpdated += OnHeroCacheUpdated;
    }

    /// <summary>
    /// Desuscribe de eventos del sistema.
    /// </summary>
    private void UnsubscribeFromEvents()
    {
        DataCacheService.OnHeroCacheUpdated -= OnHeroCacheUpdated;
    }

    /// <summary>
    /// Maneja la actualización del cache de un héroe.
    /// </summary>
    private void OnHeroCacheUpdated(string updatedHeroId)
    {
        Debug.Log($"[HeroDetailStatsPanel] OnHeroCacheUpdated recibido para heroId: {updatedHeroId}");
        if (_currentHeroData == null) return;
        
        string currentHeroId = GetHeroId(_currentHeroData);
        
        // Solo actualizar si es el héroe actual
        if (currentHeroId == updatedHeroId)
        {
            Debug.Log($"[HeroDetailStatsPanel] Cache updated for current hero: {_currentHeroData.heroName}. Refreshing UI.");
            PopulateStats();
        }
    }

    #endregion
}

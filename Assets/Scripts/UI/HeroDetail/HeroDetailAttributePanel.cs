using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Maneja el panel de atributos detallados (health, stamina, damage types, etc.).
/// Panel oculto por defecto que se muestra con "More Details" o al modificar stats.
/// </summary>
public class HeroDetailAttributePanel : MonoBehaviour
{
    #region UI References

    [Header("Main Panel")]
    public GameObject attributesDetailPanel;
    
    [Header("Toggle Button")]
    public Button moreDetailsButton;
    
    [Header("Health and Stamina")]
    public TMP_Text healthText;
    public TMP_Text staminaText;
    
    [Header("Damage Types")]
    public TMP_Text piercingDamageText;
    public TMP_Text slashingDamageText;
    public TMP_Text bluntDamageText;
    
    [Header("Defense Types")]
    public TMP_Text piercingDefenseText;
    public TMP_Text slashingDefenseText;
    public TMP_Text bluntDefenseText;
    
    [Header("Penetration Types")]
    public TMP_Text piercingPenetrationText;
    public TMP_Text slashingPenetrationText;
    public TMP_Text bluntPenetrationText;
    
    [Header("Block Stats")]
    public TMP_Text blockText;
    public TMP_Text blockRegenText;

    #endregion

    #region Private Fields

    private HeroData _currentHeroData;
    private bool _isDetailedPanelVisible = false;
    private bool _isInitialized = false;

    #endregion

    #region Unity Lifecycle

    void Start()
    {
        InitializePanel();
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
            Debug.LogError("[HeroDetailAttributePanel] HeroData es null");
            return;
        }

        _currentHeroData = heroData;
        _isInitialized = true;
        
        SetupButtonListeners();
        
        // Solo actualizar si el panel está visible
        if (_isDetailedPanelVisible)
        {
            UpdatePanel();
        }
    }

    /// <summary>
    /// Actualiza el contenido del panel con los atributos calculados actuales.
    /// </summary>
    public void UpdatePanel()
    {
        if (!_isInitialized || _currentHeroData == null) return;
        
        if (!_isDetailedPanelVisible || attributesDetailPanel == null) return;
        
        string heroId = GetHeroId(_currentHeroData);
        
        // Usar nueva arquitectura: obtener atributos base y con modificaciones temporales
        var baseAttributes = DataCacheService.CalculateAttributes(_currentHeroData);
        var currentAttributes = HeroTempAttributeService.HasTempChanges(heroId)
            ? DataCacheService.CalculateAttributes(_currentHeroData, HeroTempAttributeService.GetTempChangesAsEquipmentBonuses(heroId))
            : baseAttributes;
        
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

    /// <summary>
    /// Alterna la visibilidad del panel detallado.
    /// </summary>
    public void TogglePanel()
    {
        if (attributesDetailPanel == null) return;
        
        _isDetailedPanelVisible = !_isDetailedPanelVisible;
        attributesDetailPanel.SetActive(_isDetailedPanelVisible);
        
        if (_isDetailedPanelVisible) UpdatePanel();
        
        Debug.Log($"[HeroDetailAttributePanel] Panel detallado: {(_isDetailedPanelVisible ? "Mostrado" : "Oculto")}");
    }

    /// <summary>
    /// Muestra automáticamente el panel si no está visible.
    /// </summary>
    public void ShowPanelIfHidden()
    {
        if (!_isDetailedPanelVisible)
        {
            TogglePanel();
        }
    }

    /// <summary>
    /// Oculta automáticamente el panel si está visible.
    /// </summary>
    public void HidePanelIfShown()
    {
        if (_isDetailedPanelVisible)
        {
            TogglePanel();
        }
    }

    /// <summary>
    /// Verifica si el panel detallado está actualmente visible.
    /// </summary>
    public bool IsPanelVisible => _isDetailedPanelVisible;

    #endregion

    #region Private Methods

    private void InitializePanel()
    {
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
            moreDetailsButton.onClick.AddListener(TogglePanel);
        }
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
        Debug.Log("[HeroDetailAttributePanel] Suscribiéndose a eventos de DataCacheService");
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
        if (_currentHeroData == null) return;
        
        string currentHeroId = GetHeroId(_currentHeroData);
        
        // Solo actualizar si es el héroe actual y el panel está visible
        if (currentHeroId == updatedHeroId && _isDetailedPanelVisible)
        {
            Debug.Log($"[HeroDetailAttributePanel] Cache updated for current hero: {_currentHeroData.heroName}. Refreshing detailed panel.");
            UpdatePanel();
        }
    }

    #endregion
}

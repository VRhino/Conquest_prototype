using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Data.Items;

/// <summary>
/// Controlador principal para el panel de detalles del héroe.
/// Maneja la apertura/cierre del panel y coordina todos los sub-componentes.
/// Refactorizado para delegar responsabilidades específicas a paneles especializados.
/// </summary>
public class HeroDetailUIController : MonoBehaviour, IFullscreenPanel
{
    #region Singleton Pattern
    
    public static HeroDetailUIController Instance { get; private set; }

    #endregion

    #region UI References - Panel Principal
    
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

    #endregion

    #region Sub-Panel References
    
    [Header("Sub-Panels")]
    [SerializeField] private HeroDetailStatsPanel _statsPanel;
    [SerializeField] private HeroDetailAttributePanel _attributesPanel;
    [SerializeField] private HeroEquipmentPanel _equipmentPanel;
    [SerializeField] private HeroDetail3DPreview _previewSystem;

    #endregion

    #region Equipment Slots (Legacy - para compatibilidad)
    
    [Header("Equipment Slots (Legacy)")]
    public GameObject helmetSlot;
    public GameObject torsoSlot;
    public GameObject glovesSlot;
    public GameObject pantsSlot;
    public GameObject bootsSlot;
    public GameObject weaponSlot;
    
    [Header("Repair Buttons")]
    public Button repairButton;
    public Button repairAllButton;

    #endregion

    #region Private Fields

    private HeroData _currentHeroData;

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
        SetupSubPanels();
        SetupButtonListeners();
    }

    void OnDestroy()
    {
        // Limpiar singleton
        if (Instance == this)
        {
            Instance = null;
        }
        
        // Limpiar eventos de sub-paneles
        CleanupSubPanelEvents();
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
        if (!InventoryManager.IsInitialized)
        {
            InventoryManager.Initialize(heroData);
        }
        
        if (mainPanel != null)
        {
            mainPanel.SetActive(true);
            PopulateUI();
        }
        
        var globalTooltipManager = FindObjectOfType<InventoryTooltipManager>();
        if (globalTooltipManager != null)
        {
            _equipmentPanel.SetTooltipManager(globalTooltipManager);
        }
        Debug.Log($"[HeroDetailUIController] Panel abierto para héroe: {heroData.heroName}");
    }

    /// <summary>
    /// Cierra el panel de detalles del héroe.
    /// </summary>
    public void ClosePanel()
    {
        DisablePreviewSystem();
        if (_equipmentPanel != null) _equipmentPanel.SetTooltipManager(null);
        if (mainPanel != null) mainPanel.SetActive(false);
        
        _currentHeroData = null;
    }

    /// <summary>
    /// Alterna la visibilidad del panel.
    /// </summary>
    public void TogglePanel()
    {
        if (mainPanel != null && mainPanel.activeSelf)
            ClosePanel();
        else
            OpenPanel();

        // No llamar ToggleUIInteraction aquí - el FullscreenPanelManager se encarga
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
        PopulateSubPanels();
        EnablePreviewSystem();
    }

    private void PopulateHeroStatus()
    {
        if (_currentHeroData == null) return;
        
        // Nombre del héroe
        if (heroNameText != null) heroNameText.text = _currentHeroData.heroName;
        
        var expInfo = HeroExperienceCalculator.GetExperienceInfo(_currentHeroData);

        if (levelText != null) levelText.text = expInfo.currentLevel.ToString();
        
        if (expText != null) expText.text = expInfo.GetProgressText();

        if (expBarForeground != null) expBarForeground.fillAmount = expInfo.progressToNextLevel;
        
        if (actualLevelText != null) actualLevelText.text = expInfo.currentLevel.ToString();
        
        if (nextLevelText != null)
        {
            if (expInfo.isMaxLevel)
                nextLevelText.text = "MAX";
            else
                nextLevelText.text = (expInfo.currentLevel + 1).ToString();
        }
    }

    private void PopulateSubPanels()
    {
        // Inicializar y poblar panel de estadísticas
        if (_statsPanel != null) _statsPanel.Initialize(_currentHeroData);

        // Inicializar panel de atributos detallados
        if (_attributesPanel != null) _attributesPanel.Initialize(_currentHeroData);
        
        PopulateEquipmentSlots();
    }

    private void PopulateEquipmentSlots()
    {
        if (_equipmentPanel == null) InitializeEquipmentPanel();

        if (_equipmentPanel != null)
        {
            // Usar reflexión para evitar errores de compilación temporales
            var populateMethod = _equipmentPanel.GetType().GetMethod("PopulateFromSelectedHero");
            if (populateMethod != null)
            {
                populateMethod.Invoke(_equipmentPanel, null);
            }
            else Debug.LogWarning("[HeroDetailUIController] PopulateFromSelectedHero method not found on equipment panel");
        }
        else
        {
            if (_currentHeroData?.equipment != null) 
                Debug.Log($"[HeroDetailUIController] Equipment loaded - Helmet: {_currentHeroData.equipment.helmet?.itemId ?? "None"}");
        }
    }

    #endregion

    #region Sub-Panel Setup

    private void SetupSubPanels()
    {
        // Auto-detectar paneles si no están asignados
        if (_statsPanel == null) _statsPanel = GetComponentInChildren<HeroDetailStatsPanel>();

        if (_attributesPanel == null) _attributesPanel = GetComponentInChildren<HeroDetailAttributePanel>();

        if (_equipmentPanel == null) _equipmentPanel = GetComponentInChildren<HeroEquipmentPanel>();

        SetupSubPanelEvents();
    }

    private void SetupSubPanelEvents()
    {
        // Stats Panel Events
        if (_statsPanel != null)
        {
            _statsPanel.OnTempChangesApplied += OnStatsChanged;
            _statsPanel.OnRequestDetailedPanelShow += OnRequestShowDetailedPanel;
            _statsPanel.OnRequestAttributesPanelHide += OnRequestHideDetailedPanel;
        }
    }

    private void CleanupSubPanelEvents()
    {
        // Stats Panel Events
        if (_statsPanel != null)
        {
            _statsPanel.OnTempChangesApplied -= OnStatsChanged;
            _statsPanel.OnRequestDetailedPanelShow -= OnRequestShowDetailedPanel;
            _statsPanel.OnRequestAttributesPanelHide -= OnRequestHideDetailedPanel;
        }
    }

    #endregion

    #region Event Handlers
    private void SetupButtonListeners()
    {
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
    /// Manejador para cuando se modifican los stats en el panel de estadísticas.
    /// </summary>
    private void OnStatsChanged()
    {
        // Actualizar panel de atributos detallados si está visible
        if (_attributesPanel != null && _attributesPanel.IsPanelVisible)
            _attributesPanel.UpdatePanel();
    }

    /// <summary>
    /// Manejador para cuando se solicita mostrar el panel detallado automáticamente.
    /// </summary>
    private void OnRequestShowDetailedPanel()
    {
        if (_attributesPanel != null) _attributesPanel.ShowPanelIfHidden();
    }

    /// <summary>
    /// Manejador para cuando se solicita ocultar el panel detallado automáticamente.
    /// </summary>
    private void OnRequestHideDetailedPanel()
    {
        if (_attributesPanel != null)  _attributesPanel.HidePanelIfShown();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Habilita el sistema de preview 3D.
    /// </summary>
    private void EnablePreviewSystem()
    {
        if (_previewSystem != null)
        {
            _previewSystem.EnablePreview();
            Debug.Log("[HeroDetailUIController] 3D Preview system enabled");
        }
        else
        {
            Debug.LogWarning("[HeroDetailUIController] Preview system not assigned");
            
            // Intentar encontrar el componente automáticamente
            _previewSystem = GetComponentInChildren<HeroDetail3DPreview>();
            if (_previewSystem != null)
            {
                _previewSystem.EnablePreview();
                Debug.Log("[HeroDetailUIController] Found and enabled preview system automatically");
            }
        }
    }

    /// <summary>
    /// Deshabilita el sistema de preview 3D.
    /// </summary>
    private void DisablePreviewSystem()
    {
        if (_previewSystem != null)
        {
            _previewSystem.DisablePreview();
            Debug.Log("[HeroDetailUIController] 3D Preview system disabled");
        }
    }

    /// <summary>
    /// Inicializa el panel de equipamiento si no está ya inicializado.
    /// </summary>
    private void InitializeEquipmentPanel()
    {
        var panelType = System.Type.GetType("HeroEquipmentPanel");
        if (panelType != null)
        {
            _equipmentPanel = GetComponentInChildren(panelType) as HeroEquipmentPanel;
            
            if (_equipmentPanel == null)
            {
                // Si no existe, buscar en los slots para crear uno dinámicamente
                var equipmentParent = FindEquipmentParent();
                if (equipmentParent != null)
                {
                    _equipmentPanel = equipmentParent.AddComponent(panelType) as HeroEquipmentPanel;
                }
            }
            
            if (_equipmentPanel != null)
            {
                // Inicializar el panel usando reflexión
                var initMethod = _equipmentPanel.GetType().GetMethod("InitializePanel");
                if (initMethod != null)
                {
                    initMethod.Invoke(_equipmentPanel, null);
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

    #endregion
}

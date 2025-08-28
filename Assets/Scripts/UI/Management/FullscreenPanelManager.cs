using System;
using System.Collections.Generic;
using ConquestTactics.Dialogue;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gestor centralizado para paneles de pantalla completa que garantiza
/// exclusividad mutua (solo un panel abierto a la vez) y maneja la
/// coordinación con botones del HUD y el control de UI interaction.
/// </summary>
public class FullscreenPanelManager : MonoBehaviour
{
    // Exclusivity flag for reward dialog
    [SerializeField] public bool IsRewardDialogOpen { get; private set; } = false;

    /// <summary>
    /// Call this when the reward dialog is opened to block other panels.
    /// </summary>
    public void NotifyRewardDialogOpened()
    {
        IsRewardDialogOpen = true;
    }

    /// <summary>
    /// Call this when the reward dialog is closed to unblock other panels.
    /// </summary>
    public void NotifyRewardDialogClosed()
    {
        IsRewardDialogOpen = false;
    }
    #region Singleton Pattern

    public static FullscreenPanelManager Instance { get; private set; }

    #endregion

    #region Serialized Fields - Panel Controllers

    [SerializeField] private bool isUIModeEnabled = false;

    [Header("Panel Controllers")]
    [SerializeField] private InventoryPanelController inventoryPanel;
    [SerializeField] private HeroDetailUIController heroDetailPanel;
    [SerializeField] private BarracksMenuUIController barracksPanel;
    [SerializeField] private UIStoreController storePanel;
    [SerializeField] private NpcDialogueUIController dialoguePanel;

    #endregion

    #region Serialized Fields - HUD Buttons

    [Header("HUD Buttons (Optional)")]
    [SerializeField] private Button inventoryHUDButton;
    [SerializeField] private Button heroDetailHUDButton;
    [SerializeField] private Button barracksHUDButton;

    #endregion

    #region Private Fields

    private Dictionary<System.Type, IFullscreenPanel> _registeredPanels;
    private IFullscreenPanel _currentActivePanel;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        // Implementar patrón Singleton
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeManager();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    #endregion

    #region Initialization

    private void InitializeManager()
    {
        _registeredPanels = new Dictionary<System.Type, IFullscreenPanel>();
        _currentActivePanel = null;

        // Registrar paneles automáticamente si están asignados
        RegisterPanelsFromSerializedFields();

        // Configurar botones del HUD si están asignados
        SetupHUDButtons();

        Debug.Log("[FullscreenPanelManager] Manager initialized successfully");
    }

    private void RegisterPanelsFromSerializedFields()
    {
        if (inventoryPanel != null && inventoryPanel is IFullscreenPanel)
        {
            RegisterPanel<InventoryPanelController>(inventoryPanel as IFullscreenPanel);
        }

        if (heroDetailPanel != null && heroDetailPanel is IFullscreenPanel)
        {
            RegisterPanel<HeroDetailUIController>(heroDetailPanel as IFullscreenPanel);
        }

        if (barracksPanel != null && barracksPanel is IFullscreenPanel)
        {
            RegisterPanel<BarracksMenuUIController>(barracksPanel as IFullscreenPanel);
        }

        if (storePanel != null && storePanel is IFullscreenPanel)
        {
            RegisterPanel<UIStoreController>(storePanel as IFullscreenPanel);
        }

        if (dialoguePanel != null && dialoguePanel is IFullscreenPanel)
        {
            RegisterPanel<NpcDialogueUIController>(dialoguePanel as IFullscreenPanel);
        }
    }

    private void SetupHUDButtons()
    {
        if (inventoryHUDButton != null)
        {
            inventoryHUDButton.onClick.AddListener(() => RequestPanelToggle<InventoryPanelController>());
        }

        if (heroDetailHUDButton != null)
        {
            heroDetailHUDButton.onClick.AddListener(() => RequestPanelToggle<HeroDetailUIController>());
        }

        if (barracksHUDButton != null)
        {
            barracksHUDButton.onClick.AddListener(() => RequestPanelToggle<BarracksMenuUIController>());
        }
    }

    #endregion

    #region Panel Registration

    /// <summary>
    /// Registra un panel para gestión centralizada.
    /// </summary>
    public void RegisterPanel<T>(IFullscreenPanel panel) where T : MonoBehaviour
    {
        if (panel == null)
        {
            Debug.LogWarning($"[FullscreenPanelManager] Attempted to register null panel of type {typeof(T).Name}");
            return;
        }

        var panelType = typeof(T);
        if (_registeredPanels.ContainsKey(panelType))
        {
            Debug.LogWarning($"[FullscreenPanelManager] Panel of type {panelType.Name} already registered. Replacing...");
        }

        _registeredPanels[panelType] = panel;
        Debug.Log($"[FullscreenPanelManager] Panel {panelType.Name} registered successfully");
    }

    /// <summary>
    /// Desregistra un panel del gestor.
    /// </summary>
    public void UnregisterPanel<T>() where T : MonoBehaviour
    {
        var panelType = typeof(T);
        if (_registeredPanels.ContainsKey(panelType))
        {
            if (_currentActivePanel == _registeredPanels[panelType])
            {
                _currentActivePanel = null;
            }
            _registeredPanels.Remove(panelType);
            Debug.Log($"[FullscreenPanelManager] Panel {panelType.Name} unregistered");
        }
    }

    #endregion

    #region Panel Management - Public API

    /// <summary>
    /// Abre un panel específico, cerrando cualquier otro que esté abierto.
    /// </summary>
    public void OpenPanel<T>() where T : MonoBehaviour
    {
        if (IsRewardDialogOpen) return;
        var panelType = typeof(T);
        if (!_registeredPanels.TryGetValue(panelType, out var targetPanel))
        {
            Debug.LogWarning($"[FullscreenPanelManager] Panel of type {panelType.Name} is not registered");
            return;
        }

        // Cerrar panel actual si hay uno diferente abierto
        if (_currentActivePanel != null && _currentActivePanel != targetPanel)
        {
            _currentActivePanel.ClosePanel();
            SetUIInteractionState(false); // Desactivar antes de cambiar
        }

        // Abrir el panel solicitado
        targetPanel.OpenPanel();
        _currentActivePanel = targetPanel;
        SetUIInteractionState(true); // Activar para el nuevo panel

        Debug.Log($"[FullscreenPanelManager] Opened panel: {panelType.Name}");
    }

    /// <summary>
    /// Cierra un panel específico.
    /// </summary>
    public void ClosePanel<T>() where T : MonoBehaviour
    {
        if (IsRewardDialogOpen) return;
        var panelType = typeof(T);
        if (!_registeredPanels.TryGetValue(panelType, out var targetPanel))
        {
            Debug.LogWarning($"[FullscreenPanelManager] Panel of type {panelType.Name} is not registered");
            return;
        }

        if (_currentActivePanel == targetPanel)
        {
            targetPanel.ClosePanel();
            _currentActivePanel = null;
            SetUIInteractionState(false); // Desactivar UI interaction
            Debug.Log($"[FullscreenPanelManager] Closed panel: {panelType.Name}");
        }
    }

    /// <summary>
    /// Alterna el estado de un panel específico.
    /// </summary>
    public void TogglePanel<T>() where T : MonoBehaviour
    {
       if (IsRewardDialogOpen) return;

        var panelType = typeof(T);
        if (!_registeredPanels.TryGetValue(panelType, out var targetPanel))
        {
            Debug.LogWarning($"[FullscreenPanelManager] Panel of type {panelType.Name} is not registered");
            return;
        }

        if (targetPanel.IsPanelOpen)
        {
            ClosePanel<T>();
        }
        else
        {
            OpenPanel<T>();
        }
    }

    #endregion

    #region Input Handling - Called by HeroInputSystem

    /// <summary>
    /// Maneja la entrada de teclado para alternar paneles.
    /// Llamado por HeroInputSystem.
    /// </summary>
    public void HandleInventoryKeyPress()
    {
        RequestPanelToggle<InventoryPanelController>();
    }

    public void HandleHeroDetailKeyPress()
    {
        RequestPanelToggle<HeroDetailUIController>();
    }

    public void HandleBarracksKeyPress()
    {
        RequestPanelToggle<BarracksMenuUIController>();
    }

    public void HandleStoreOpen()
    {
        RequestPanelToggle<UIStoreController>();
    }

    public void HandleDialogueOpen(NpcDialogueData dialogueData, Action<DialogueOption> onOptionSelected)
    {
        dialoguePanel.setData(dialogueData, onOptionSelected);
        RequestPanelToggle<NpcDialogueUIController>();
    }

    public void HandleEscapeKeyPress()
    {
        if (_currentActivePanel != null)
        {
            var panelType = _currentActivePanel.GetType();
            var method = typeof(FullscreenPanelManager).GetMethod("ClosePanel");
            var genericMethod = method.MakeGenericMethod(panelType);
            genericMethod.Invoke(this, null);
        }
    }

    private void RequestPanelToggle<T>() where T : MonoBehaviour
    {
        TogglePanel<T>();
    }

    #endregion

    #region UI Interaction Control

    /// <summary>
    /// Controla el estado de interacción con la UI (cursor y cámara).
    /// Centraliza la funcionalidad que antes estaba en cada controlador.
    /// </summary>
    public void SetUIInteractionState(bool enableUIMode)
    {
        isUIModeEnabled = enableUIMode;
        if (enableUIMode)
        {
            // Activar modo UI: cursor visible y cámara deshabilitada
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            DialogueUIState.IsDialogueOpen = true;
            // Deshabilitar la cámara del héroe
            if (HeroCameraController.Instance != null)
            {
                HeroCameraController.Instance.SetCameraFollowEnabled(false);
            }
        }
        else
        {
            // Desactivar modo UI: cursor bloqueado y cámara habilitada
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            DialogueUIState.IsDialogueOpen = false;
            // Habilitar la cámara del héroe
            if (HeroCameraController.Instance != null)
            {
                HeroCameraController.Instance.SetCameraFollowEnabled(true);
            }
        }
    }

    #endregion
}

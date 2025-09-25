using UnityEngine;
using UnityEngine.UI;
using System;
/// <summary>
/// Controller UI para puntos de spawn que maneja la selección visual y eventos de click.
/// Se encarga únicamente de activar/desactivar el componente selection interno y disparar eventos.
/// </summary>
public class SpawnPointControllerUI : MonoBehaviour
{
    [Header("Data")]
    public string spawnPointId;
    public Side spawnPointType;
    [Header("Selection Visual")]
    [SerializeField] private GameObject selection;

    [Header("Input Component")]
    [SerializeField] private Button clickButton;

    // Estado interno
    private bool _isSelected = false;
    private bool _active = false;

    // Evento público para comunicación externa
    private event Action<SpawnPointControllerUI> OnSpawnPointClicked;

    #region Public API

    /// <summary>
    /// Obtiene el estado actual de selección.
    /// </summary>
    public bool IsSelected => _isSelected;

    /// <summary>
    /// Establece el estado de selección externamente (sin disparar evento).
    /// </summary>
    /// <param name="selected">Nuevo estado de selección</param>
    public void SetSelected(bool selected)
    {
        if (_isSelected == selected) return;

        _isSelected = selected;
        UpdateSelectionVisual();
        // NO dispara evento - solo control externo
    }

    #endregion

    #region Unity Events

    void Start()
    {
        _active = true;
        // Setup button listener
        if (clickButton == null) clickButton = GetComponent<Button>();

        if (clickButton != null) clickButton.onClick.AddListener(OnClick);
        else Debug.LogWarning($"[SpawnPointControllerUI] No Button component found on {gameObject.name}");

        // Estado inicial
        UpdateSelectionVisual();
    }

    void OnDestroy()
    {
        // Cleanup listener
        if (clickButton != null) clickButton.onClick.RemoveListener(OnClick);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Maneja el click del usuario en el spawn point.
    /// </summary>
    private void OnClick()
    {
        if (!_active) return;
        // Activar selección
        _isSelected = true;
        UpdateSelectionVisual();

        // Disparar evento para sistemas externos
        OnSpawnPointClicked?.Invoke(this);

        Debug.Log($"[SpawnPointControllerUI] Spawn point clicked: {gameObject.name}");
    }

    /// <summary>
    /// Actualiza la visualización del estado de selección.
    /// </summary>
    private void UpdateSelectionVisual()
    {
        if (selection != null) selection.SetActive(_isSelected);
        else Debug.LogWarning($"[SpawnPointControllerUI] Selection GameObject is null on {gameObject.name}");
    }

    #endregion

    #region Events Handling
    public void DisconnectAllEvents()
    {
        _active = false;
        OnSpawnPointClicked = null;
    }

    public void ConnectOnClickEvent(Action<SpawnPointControllerUI> onClickAction)
    {
        _active = true;
        OnSpawnPointClicked += onClickAction;
    }
    #endregion

    #region Validation

    /// <summary>
    /// Valida la configuración del componente en el editor.
    /// </summary>
    private void OnValidate()
    {
        // Verificar referencias en el editor
        if (selection == null) Debug.LogWarning($"[SpawnPointControllerUI] Selection GameObject not assigned on {gameObject.name}");

        if (clickButton == null)
        {
            clickButton = GetComponent<Button>();
            if (clickButton == null) Debug.LogWarning($"[SpawnPointControllerUI] No Button component found on {gameObject.name}");
        }
    }
    
    #endregion
}
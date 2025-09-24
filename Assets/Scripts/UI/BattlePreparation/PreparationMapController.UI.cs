using System.Collections.Generic;
using UnityEngine;
public enum Side
{
    Attackers,
    Defenders,
    None
}
/// <summary>
/// Manager para puntos de spawn que maneja la selección única entre múltiples SpawnPointControllerUI.
/// Comportamiento radio button simple - solo permite una selección activa a la vez.
/// </summary>
public class PreparationMapControllerUI : MonoBehaviour
{
    [Header("Spawn Points")]
    [SerializeField] private List<SpawnPointControllerUI> spawnPoints = new List<SpawnPointControllerUI>();
    
    [Header("Supply Points")]
    [SerializeField] private List<SupplyPointIconControllerUI> supplyPoints = new List<SupplyPointIconControllerUI>();
    
    [Header("Capture Points")]
    [SerializeField] private List<CapturePointIconControllerUI> capturePoints = new List<CapturePointIconControllerUI>();
    
    // Estado actual
    private SpawnPointControllerUI _currentSelectedSpawnPoint;
    public Side side;
    // Evento público para selección
    public event System.Action<SpawnPointControllerUI> OnSpawnPointSelected;
    
    #region Public API
    
    /// <summary>
    /// Obtiene el spawn point actualmente seleccionado.
    /// </summary>
    public SpawnPointControllerUI CurrentSelectedSpawnPoint => _currentSelectedSpawnPoint;
    
    /// <summary>
    /// Selecciona un spawn point específico externamente.
    /// </summary>
    /// <param name="spawnPoint">Spawn point a seleccionar</param>
    public void SelectSpawnPoint(SpawnPointControllerUI spawnPoint)
    {
        // Si ya está seleccionado, no hacer nada
        if (_currentSelectedSpawnPoint == spawnPoint) return;
        
        // Validar que el spawn point esté en la lista
        if (spawnPoint != null && !spawnPoints.Contains(spawnPoint))
        {
            Debug.LogWarning($"[PreparationMapControllerUI] Trying to select spawn point '{spawnPoint.name}' that is not in the managed list");
            return;
        }
        
        // Deseleccionar actual
        if (_currentSelectedSpawnPoint != null) _currentSelectedSpawnPoint.SetSelected(false);
        
        // Seleccionar nuevo
        _currentSelectedSpawnPoint = spawnPoint;
        if (spawnPoint != null)
        {
            spawnPoint.SetSelected(true);
            OnSpawnPointSelected?.Invoke(spawnPoint);
        }
        
        Debug.Log($"[PreparationMapControllerUI] Spawn point selected: {(spawnPoint != null ? spawnPoint.name : "none")}");
    }
    
    #endregion
    
    #region Unity Events

    public void Initialize(Side side)
    {
        this.side = side;

        // Initialize spawn points
        foreach (var spawnPoint in spawnPoints)
        {
            if (side == spawnPoint.spawnPointType) SubscribeToSpawnPoint(spawnPoint);
            else spawnPoint.gameObject.SetActive(false);
        }
        
        // Initialize supply points
        foreach (var supplyPoint in supplyPoints)
        {
            supplyPoint.Initialize(side);
        }
        
        // Initialize capture points
        foreach (var capturePoint in capturePoints)
        {
            capturePoint.Initialize(side);
        }
    }
    
    void OnDestroy()
    {
        // Cleanup subscriptions
        foreach (var spawnPoint in spawnPoints)
            UnsubscribeFromSpawnPoint(spawnPoint);
    }
    
    #endregion
    
    #region Private Methods
    
    /// <summary>
    /// Maneja el click del usuario en cualquier spawn point.
    /// </summary>
    /// <param name="clickedSpawnPoint">Spawn point que fue clickeado</param>
    private void OnSpawnPointClicked(SpawnPointControllerUI clickedSpawnPoint)
    {
        if (_currentSelectedSpawnPoint == clickedSpawnPoint) return;

        if (_currentSelectedSpawnPoint != null) _currentSelectedSpawnPoint.SetSelected(false);

        _currentSelectedSpawnPoint = clickedSpawnPoint;
        OnSpawnPointSelected?.Invoke(clickedSpawnPoint);
        
        Debug.Log($"[PreparationMapControllerUI] User clicked spawn point: {clickedSpawnPoint.name}");
    }
    
    /// <summary>
    /// Se suscribe a los eventos de un spawn point.
    /// </summary>
    /// <param name="spawnPoint">Spawn point al que suscribirse</param>
    private void SubscribeToSpawnPoint(SpawnPointControllerUI spawnPoint)
    {
        if (spawnPoint != null) spawnPoint.OnSpawnPointClicked += OnSpawnPointClicked;
    }
    
    /// <summary>
    /// Se desuscribe de los eventos de un spawn point.
    /// </summary>
    /// <param name="spawnPoint">Spawn point del que desuscribirse</param>
    private void UnsubscribeFromSpawnPoint(SpawnPointControllerUI spawnPoint)
    {
        if (spawnPoint != null) spawnPoint.OnSpawnPointClicked -= OnSpawnPointClicked;
    }
    
    #endregion
    
    #region Validation
    
    /// <summary>
    /// Valida la configuración del componente en el editor.
    /// </summary>
    private void OnValidate()
    {
    }
    
    #endregion
}
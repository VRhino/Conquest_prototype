using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Controlador especializado para la gestión de timers en el juego.
/// Maneja la lógica de countdown, visualización y notificación de eventos.
/// </summary>
public class TimerController : MonoBehaviour
{
    #region Events

    /// <summary>
    /// Se dispara cuando el timer llega a cero.
    /// </summary>
    public event Action OnTimerFinished;

    /// <summary>
    /// Se dispara cuando el timer se actualiza (cada segundo).
    /// </summary>
    public event Action<int> OnTimerUpdated;

    #endregion

    #region Dependencies

    private BattleData _battleData;
    private TextMeshProUGUI _timerDisplay;

    #endregion

    #region State

    private Coroutine _countdownCoroutine;
    private bool _isRunning = false;

    #endregion

    #region Public API

    /// <summary>
    /// Inicializa el controlador con las dependencias necesarias.
    /// </summary>
    /// <param name="battleData">Datos de batalla que contienen el timer</param>
    /// <param name="timerDisplay">Componente UI para mostrar el timer</param>
    public void Initialize(BattleData battleData, TextMeshProUGUI timerDisplay)
    {
        _battleData = battleData;
        _timerDisplay = timerDisplay;

        if (_battleData == null)
        {
            Debug.LogError("[TimerController] BattleData no puede ser null");
            return;
        }

        UpdateTimerDisplay();
    }

    /// <summary>
    /// Establece el tiempo de preparación en segundos.
    /// </summary>
    /// <param name="seconds">Tiempo en segundos</param>
    public void SetPreparationTimer(int seconds)
    {
        if (_battleData == null)
        {
            Debug.LogError("[TimerController] No inicializado correctamente");
            return;
        }
        if (seconds < 0) seconds = 0;
        _battleData.PreparationTimer = seconds;
        UpdateTimerDisplay();

        // Reiniciar countdown si está corriendo
        if (_isRunning) StopCountdown();

        // Iniciar countdown si hay tiempo
        if (_battleData.PreparationTimer > 0) StartCountdown();
        else OnTimerFinished?.Invoke();
    }

    /// <summary>
    /// Reduce el tiempo del timer.
    /// </summary>
    /// <param name="seconds">Segundos a reducir</param>
    public void DecreasePreparationTimer(int seconds)
    {
        if (_battleData == null || seconds < 0) return;

        _battleData.PreparationTimer -= seconds;
        if (_battleData.PreparationTimer < 0) _battleData.PreparationTimer = 0;

        UpdateTimerDisplay();
        OnTimerUpdated?.Invoke(_battleData.PreparationTimer);

        if (_battleData.PreparationTimer == 0)
        {
            StopCountdown();
            OnTimerFinished?.Invoke();
        }
    }

    /// <summary>
    /// Pausa el countdown del timer.
    /// </summary>
    public void PauseTimer()
    {
        if (_isRunning)
        {
            StopCountdown();
        }
    }

    /// <summary>
    /// Reanuda el countdown del timer.
    /// </summary>
    public void ResumeTimer()
    {
        if (!_isRunning && _battleData != null && _battleData.PreparationTimer > 0)
        {
            StartCountdown();
        }
    }

    /// <summary>
    /// Detiene completamente el timer.
    /// </summary>
    public void StopTimer()
    {
        StopCountdown();
        if (_battleData != null)
        {
            _battleData.PreparationTimer = 0;
            UpdateTimerDisplay();
        }
    }

    /// <summary>
    /// Obtiene el tiempo restante en segundos.
    /// </summary>
    /// <returns>Tiempo restante</returns>
    public int GetRemainingTime()
    {
        return _battleData?.PreparationTimer ?? 0;
    }

    /// <summary>
    /// Verifica si el timer está corriendo.
    /// </summary>
    /// <returns>True si está corriendo</returns>
    public bool IsRunning() { return _isRunning; }

    #endregion

    #region Private Methods

    /// <summary>
    /// Inicia el countdown del timer.
    /// </summary>
    private void StartCountdown()
    {
        if (_countdownCoroutine != null) StopCoroutine(_countdownCoroutine);

        _isRunning = true;
        _countdownCoroutine = StartCoroutine(CountdownCoroutine());
    }

    /// <summary>
    /// Detiene el countdown del timer.
    /// </summary>
    private void StopCountdown()
    {
        if (_countdownCoroutine != null)
        {
            StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = null;
        }
        _isRunning = false;
    }

    /// <summary>
    /// Coroutine que maneja el countdown segundo a segundo.
    /// </summary>
    private IEnumerator CountdownCoroutine()
    {
        while (_battleData.PreparationTimer > 0 && _isRunning)
        {
            yield return new WaitForSeconds(1);
            if (_isRunning) DecreasePreparationTimer(1);
        }
    }

    /// <summary>
    /// Actualiza la visualización del timer en la UI.
    /// </summary>
    private void UpdateTimerDisplay()
    {
        if (_timerDisplay != null && _battleData != null)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(_battleData.PreparationTimer);
            _timerDisplay.text = timeSpan.ToString(@"mm\:ss");
        }
    }

    #endregion

    #region Unity Lifecycle

    private void OnDestroy()
    {
        StopCountdown();
    }

    #endregion
}

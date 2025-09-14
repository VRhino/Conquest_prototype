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
    private int secondsRemaining = 0;
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
    public void Initialize(int seconds, TextMeshProUGUI timerDisplay)
    {
        if (seconds >= 0) secondsRemaining = seconds;
        else secondsRemaining = 1;

        _timerDisplay = timerDisplay;

        if (_timerDisplay == null )
        {
            Debug.LogError("[TimerController] TimerDisplay no puede ser null");
            return;
        }

        UpdateTimerDisplay();
    }

    /// <summary>
    /// Establece el tiempo de preparación en segundos.
    /// </summary>
    /// <param name="seconds">Tiempo en segundos</param>
    public void SetCountDownSecs(int seconds)
    {
        if (_timerDisplay == null)
        {
            Debug.LogError("[TimerController] No inicializado correctamente");
            return;
        }
        if (seconds < 0) seconds = 0;
        secondsRemaining = seconds;
        UpdateTimerDisplay();

        // Reiniciar countdown si está corriendo
        if (_isRunning) StopCountdown();

        // Iniciar countdown si hay tiempo
        if (secondsRemaining > 0) StartCountdown();
        else OnTimerFinished?.Invoke();
    }

    /// <summary>
    /// Reduce el tiempo del timer.
    /// </summary>
    /// <param name="seconds">Segundos a reducir</param>
    public void ReduceTimerSeconds(int seconds)
    {
        if (_timerDisplay == null || seconds < 0) return;

        secondsRemaining -= seconds;
        if (secondsRemaining < 0) secondsRemaining = 0;

        UpdateTimerDisplay();
        OnTimerUpdated?.Invoke(secondsRemaining);

        if (secondsRemaining == 0)
        {
            StopCountdown();
            OnTimerFinished?.Invoke();
        }
    }
    
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
        while (secondsRemaining > 0 && _isRunning)
        {
            yield return new WaitForSeconds(1);
            if (_isRunning) ReduceTimerSeconds(1);
        }
    }

    /// <summary>
    /// Actualiza la visualización del timer en la UI.
    /// </summary>
    private void UpdateTimerDisplay()
    {
        if (_timerDisplay != null)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(secondsRemaining);
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

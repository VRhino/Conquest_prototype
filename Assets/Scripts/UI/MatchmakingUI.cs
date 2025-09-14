using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Componente de UI para gestionar la interfaz del matchmaking.
/// Maneja botones, estados visuales y countdown de transición.
/// </summary>
public class MatchmakingUI : MonoBehaviour
{
    #region UI References

    [Header("Main Elements")]
    [SerializeField] private Button matchmakingButton;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject matchmakingPanel;

    [Header("Countdown Elements")]
    [SerializeField] private GameObject countdownPanel;
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private TimerController countdownTimer;

    #endregion

    #region Private Fields

    private MatchmakingService.MatchmakingState _currentState = MatchmakingService.MatchmakingState.Idle;

    #endregion

    #region Unity Lifecycle

    public void Initialize()
    {
        InitializeUI();
        SubscribeToEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    #endregion

    #region Initialization

    private void InitializeUI()
    {
        if (matchmakingPanel != null)  matchmakingPanel.SetActive(false);

        if (countdownPanel != null) countdownPanel.SetActive(false);

        UpdateUIState(MatchmakingService.MatchmakingState.Idle);
    }

    private void SubscribeToEvents()
    {
        Debug.Log("[MatchmakingUI] Subscribing to events");
        if (MatchmakingService.Instance != null)
        {
            Debug.Log("[MatchmakingUI] MatchmakingService instance found and subscribing to its events");
            MatchmakingService.Instance.OnMatchmakingStarted += OnMatchmakingStarted;
            MatchmakingService.Instance.OnMatchmakingCancelled += OnMatchmakingCancelled;
            MatchmakingService.Instance.OnBattleAssigned += OnBattleAssigned;
            MatchmakingService.Instance.OnMatchmakingFailed += OnMatchmakingFailed;
        }

        if (matchmakingButton != null) matchmakingButton.onClick.AddListener(OnMatchmakingButtonPressed);

        if (countdownTimer != null) countdownTimer.OnTimerFinished += OnCountdownFinished;
    }

    private void UnsubscribeFromEvents()
    {
        if (MatchmakingService.Instance != null)
        {
            MatchmakingService.Instance.OnMatchmakingStarted -= OnMatchmakingStarted;
            MatchmakingService.Instance.OnMatchmakingCancelled -= OnMatchmakingCancelled;
            MatchmakingService.Instance.OnBattleAssigned -= OnBattleAssigned;
            MatchmakingService.Instance.OnMatchmakingFailed -= OnMatchmakingFailed;
        }

        if (matchmakingButton != null) matchmakingButton.onClick.RemoveListener(OnMatchmakingButtonPressed);

        if (countdownTimer != null) countdownTimer.OnTimerFinished -= OnCountdownFinished;
    }

    #endregion

    #region Event Handlers

    private void OnMatchmakingButtonPressed()
    {
        if (MatchmakingService.Instance == null)
        {
            Debug.LogError("[MatchmakingUI] MatchmakingService not found");
            return;
        }

        HeroData selectedHero = PlayerSessionService.SelectedHero;
        if (selectedHero == null)
        {
            Debug.LogError("[MatchmakingUI] No hero selected");
            return;
        }

        MatchmakingService.Instance.toggleMatchmaking(selectedHero);
    }

    private void OnMatchmakingStarted()
    {
        Debug.Log("[MatchmakingUI] Matchmaking started");
        UpdateUIState(MatchmakingService.MatchmakingState.Searching);
    }

    private void OnMatchmakingCancelled()
    {
        Debug.Log("[MatchmakingUI] Matchmaking cancelled");
        UpdateUIState(MatchmakingService.MatchmakingState.Idle);
    }

    private void OnBattleAssigned(BattleData battleData)
    {
        Debug.Log("[MatchmakingUI] Battle assigned");
        UpdateUIState(MatchmakingService.MatchmakingState.Assigned);
        StartTransitionCountdown(battleData);
    }

    private void OnMatchmakingFailed(string reason)
    {
        Debug.LogError("[MatchmakingUI] Matchmaking failed");
        UpdateUIState(MatchmakingService.MatchmakingState.Idle);
        Debug.LogError($"[MatchmakingUI] Matchmaking failed: {reason}");

        if (statusText != null) statusText.text = $"Error: {reason}";
    }

    private void OnCountdownFinished()
    {
        // La transición se maneja en BattleAssignmentHandler
        Debug.Log("[MatchmakingUI] Countdown finished");
    }

    #endregion

    #region UI State Management

    private void UpdateUIState(MatchmakingService.MatchmakingState newState)
    {
        _currentState = newState;

        switch (_currentState)
        {
            case MatchmakingService.MatchmakingState.Idle:
                ShowIdleState();
                break;

            case MatchmakingService.MatchmakingState.Searching:
                ShowSearchingState();
                break;

            case MatchmakingService.MatchmakingState.Assigned:
                ShowAssignedState();
                break;

            case MatchmakingService.MatchmakingState.Transitioning:
                ShowTransitioningState();
                break;
        }
    }

    private void ShowIdleState()
    {
        Debug.Log("[MatchmakingUI] ShowIdleState");
        if (matchmakingPanel != null) matchmakingPanel.SetActive(false);

        if (countdownPanel != null) countdownPanel.SetActive(false);

        if (statusText != null) statusText.text = "Listo para buscar partida";
    }

    private void ShowSearchingState()
    {
        Debug.Log("[MatchmakingUI] ShowSearchingState");
        if (matchmakingPanel != null) matchmakingPanel.SetActive(true);

        if (countdownPanel != null) countdownPanel.SetActive(false);

        if (matchmakingButton != null) matchmakingButton.GetComponentInChildren<TextMeshProUGUI>().text = "Cancelar Búsqueda";

        if (statusText != null) statusText.text = "Buscando partida...";
    }

    private void ShowAssignedState()
    {
        Debug.Log("[MatchmakingUI] ShowAssignedState");
        if (matchmakingPanel != null) matchmakingPanel.SetActive(true);

        if (countdownPanel != null) countdownPanel.SetActive(false);

        if (matchmakingButton != null) matchmakingButton.GetComponentInChildren<TextMeshProUGUI>().text = "Cancelar";

        if (statusText != null) statusText.text = "¡Partida encontrada!";
    }

    private void ShowTransitioningState()
    {
        Debug.Log("[MatchmakingUI] ShowTransitioningState");
        if (countdownPanel != null) countdownPanel.SetActive(true);
    }

    #endregion

    #region Countdown Management

    private void StartTransitionCountdown(BattleData battleData)
    {
        if (countdownTimer == null)
        {
            Debug.LogError("[MatchmakingUI] CountdownTimer not assigned");
            return;
        }

        UpdateUIState(MatchmakingService.MatchmakingState.Transitioning);

        // Configurar countdown de 10 segundos
        countdownTimer.Initialize(10, countdownText);
        countdownTimer.OnTimerFinished += () => OnTransitionCountdownFinished(battleData);
        countdownTimer.SetCountDownSecs(10);
    }

    private void OnTransitionCountdownFinished(BattleData battleData)
    {
        // Pasar datos a la siguiente escena y transicionar
        BattleTransitionData.Instance.SetBattleData(battleData);
        SceneTransitionService.LoadScene("BattlePrepScene");
    }

    #endregion
}
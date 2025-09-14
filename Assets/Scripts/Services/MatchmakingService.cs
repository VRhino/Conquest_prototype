using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Servicio centralizado para gestionar el matchmaking de batallas.
/// Maneja el estado del matchmaking, comunicación con backend y eventos.
/// </summary>
public class MatchmakingService : MonoBehaviour
{
    #region Singleton Pattern

    public static MatchmakingService Instance { get; private set; }

    #endregion

    #region Events

    /// <summary>
    /// Se dispara cuando se inicia el matchmaking.
    /// </summary>
    public event Action OnMatchmakingStarted;

    /// <summary>
    /// Se dispara cuando se cancela el matchmaking.
    /// </summary>
    public event Action OnMatchmakingCancelled;

    /// <summary>
    /// Se dispara cuando el usuario es asignado a una batalla.
    /// </summary>
    public event Action<BattleData> OnBattleAssigned;

    /// <summary>
    /// Se dispara cuando falla el matchmaking.
    /// </summary>
    public event Action<string> OnMatchmakingFailed;

    #endregion

    #region State

    public enum MatchmakingState
    {
        Idle,
        Searching,
        Assigned,
        Transitioning
    }

    private MatchmakingState _currentState = MatchmakingState.Idle;
    private Coroutine _matchmakingCoroutine;
    private HeroData _currentHero;
    [SerializeField] private MatchmakingUI _matchmakingUI;

    #endregion

    #region Public Properties

    public MatchmakingState CurrentState => _currentState;
    public bool IsInQueue => _currentState == MatchmakingState.Searching;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);

        if (_matchmakingUI == null) Debug.LogWarning("[MatchmakingService] MatchmakingUI reference is not set in the inspector.");

        _matchmakingUI?.Initialize();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    #endregion

    #region Public API

    public void toggleMatchmaking(HeroData selectedHero)
    {
        if (IsInQueue) CancelMatchmaking();
        else StartMatchmaking(selectedHero);
    }

    /// <summary>
    /// Inicia el proceso de matchmaking con el héroe especificado.
    /// </summary>
    /// <param name="heroData">Datos del héroe que entra en cola</param>
    public void StartMatchmaking(HeroData heroData)
    {
        if (_currentState != MatchmakingState.Idle)
        {
            Debug.LogWarning("[MatchmakingService] Matchmaking already in progress");
            return;
        }

        if (heroData == null)
        {
            Debug.LogError("[MatchmakingService] HeroData cannot be null");
            OnMatchmakingFailed?.Invoke("Invalid hero data");
            return;
        }

        _currentHero = heroData;
        _currentState = MatchmakingState.Searching;

        // Actualizar GamePhase
        SceneTransitionService.UpdateGamePhase(GamePhase.Matchmaking);

        // Iniciar proceso de matchmaking
        _matchmakingCoroutine = StartCoroutine(MatchmakingProcess());

        OnMatchmakingStarted?.Invoke();
        Debug.Log($"[MatchmakingService] Started matchmaking for hero: {heroData.heroName}");
    }

    /// <summary>
    /// Cancela el proceso de matchmaking actual.
    /// </summary>
    public void CancelMatchmaking()
    {
        if (_currentState == MatchmakingState.Idle)
        {
            return;
        }

        if (_matchmakingCoroutine != null)
        {
            StopCoroutine(_matchmakingCoroutine);
            _matchmakingCoroutine = null;
        }

        _currentState = MatchmakingState.Idle;
        _currentHero = null;

        // Reset GamePhase
        SceneTransitionService.UpdateGamePhase(GamePhase.Feudo);

        OnMatchmakingCancelled?.Invoke();
        Debug.Log("[MatchmakingService] Matchmaking cancelled");
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Coroutine que simula el proceso de matchmaking.
    /// </summary>
    private IEnumerator MatchmakingProcess()
    {
        // Simular delay del backend (2-5 segundos)
        float waitTime = UnityEngine.Random.Range(2f, 5f);
        yield return new WaitForSeconds(waitTime);

        // Verificar que no se canceló mientras esperaba
        if (_currentState != MatchmakingState.Searching)
        {
            yield break;
        }

        try
        {
            // Simular respuesta del backend usando BattleDebugCreator
            BattleData battleData = BattleDebugCreator.CreateBattleWithLocalHero(_currentHero);

            if (battleData != null)
            {
                _currentState = MatchmakingState.Assigned;
                OnBattleAssigned?.Invoke(battleData);
                Debug.Log($"[MatchmakingService] Battle assigned successfully: {battleData.battleID}");
            }
            else
            {
                _currentState = MatchmakingState.Idle;
                OnMatchmakingFailed?.Invoke("Failed to create battle data");
                Debug.LogError("[MatchmakingService] Failed to create battle data");
            }
        }
        catch (Exception ex)
        {
            _currentState = MatchmakingState.Idle;
            OnMatchmakingFailed?.Invoke($"Exception: {ex.Message}");
            Debug.LogError($"[MatchmakingService] Exception during matchmaking: {ex.Message}");
        }
    }

    #endregion
}
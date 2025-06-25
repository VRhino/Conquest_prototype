using Unity.Entities;
using UnityEngine;
using TMPro;

/// <summary>
/// Singleton controller that manages the visibility of UI screens
/// based on the current <see cref="GamePhase"/>.
/// </summary>
public class UIManager : MonoBehaviour
{
    /// <summary>Global access instance.</summary>
    public static UIManager Instance { get; private set; }

    [Header("Screens")]
    [SerializeField] GameObject _loginScreen;
    [SerializeField] GameObject _feudoScreen;
    [SerializeField] GameObject _loadoutScreen;
    [SerializeField] GameObject _hudScreen;
    [SerializeField] GameObject _postPartidaScreen;

    [Header("Optional message UI")]
    [SerializeField] CanvasGroup _messageGroup;
    [SerializeField] TMP_Text _messageText;

    GamePhase _currentPhase;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        UpdateFromGameState();
    }

    void Update()
    {
        if (!SystemAPI.TryGetSingleton<GameStateComponent>(out var state))
            return;

        if (state.currentPhase != _currentPhase)
            ShowScreen(state.currentPhase);
    }

    /// <summary>
    /// Shows the UI screen associated with the given phase and hides the rest.
    /// </summary>
    /// <param name="phase">Target phase to display.</param>
    public void ShowScreen(GamePhase phase)
    {
        HideAll();

        switch (phase)
        {
            case GamePhase.Login:
                if (_loginScreen != null) _loginScreen.SetActive(true);
                break;
            case GamePhase.Feudo:
                if (_feudoScreen != null) _feudoScreen.SetActive(true);
                break;
            case GamePhase.Barracon:
            case GamePhase.Preparacion:
                if (_loadoutScreen != null) _loadoutScreen.SetActive(true);
                break;
            case GamePhase.Combate:
                if (_hudScreen != null) _hudScreen.SetActive(true);
                break;
            case GamePhase.PostPartida:
                if (_postPartidaScreen != null) _postPartidaScreen.SetActive(true);
                break;
        }

        _currentPhase = phase;
    }

    /// <summary>Hides all registered screens.</summary>
    public void HideAll()
    {
        if (_loginScreen != null) _loginScreen.SetActive(false);
        if (_feudoScreen != null) _feudoScreen.SetActive(false);
        if (_loadoutScreen != null) _loadoutScreen.SetActive(false);
        if (_hudScreen != null) _hudScreen.SetActive(false);
        if (_postPartidaScreen != null) _postPartidaScreen.SetActive(false);
    }

    /// <summary>
    /// Displays a simple text message using the configured message UI.
    /// </summary>
    /// <param name="text">Message text to show.</param>
    public void ShowMessage(string text)
    {
        if (_messageText != null)
            _messageText.text = text;

        if (_messageGroup != null)
            _messageGroup.alpha = 1f;
    }

    void UpdateFromGameState()
    {
        if (SystemAPI.TryGetSingleton<GameStateComponent>(out var state))
            ShowScreen(state.currentPhase);
    }
}

using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Handles input and display for the in-game chat window.
/// Messages are stored in a dynamic buffer by <see cref="ChatSystem"/> and
/// displayed here.
/// </summary>
public class ChatUIController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] CanvasGroup chatGroup;
    [SerializeField] TMP_InputField inputField;
    [SerializeField] ScrollRect scrollRect;
    [SerializeField] RectTransform messageContainer;
    [SerializeField] TMP_Text messagePrefab;
    [SerializeField] int maxMessages = 30;

    EntityManager _em;
    Entity _historyEntity = Entity.Null;
    int _lastCount;
    bool _inputActive;

    void Awake()
    {
        _em = World.DefaultGameObjectInjectionWorld.EntityManager;
    }

    void Start()
    {
        TryResolveHistory();
        if (chatGroup != null)
            chatGroup.alpha = 0f;
        if (inputField != null)
            inputField.gameObject.SetActive(false);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null)
            return;

        if (kb.tKey != null && kb.tKey.wasPressedThisFrame)
        {
            if (chatGroup != null)
                chatGroup.alpha = chatGroup.alpha > 0f ? 0f : 1f;
        }

        if (kb.enterKey != null && kb.enterKey.wasPressedThisFrame)
        {
            if (!_inputActive)
                OpenInput();
            else
                SubmitMessage();
        }
    }

    void LateUpdate()
    {
        if (!TryResolveHistory())
            return;

        DynamicBuffer<ChatHistoryElement> buffer = _em.GetBuffer<ChatHistoryElement>(_historyEntity);
        while (_lastCount < buffer.Length)
        {
            var entry = buffer[_lastCount];
            AppendMessage(entry.senderName.ToString(), entry.message.ToString());
            _lastCount++;
        }
    }

    void OpenInput()
    {
        if (chatGroup != null)
            chatGroup.alpha = 1f;
        if (inputField != null)
        {
            inputField.gameObject.SetActive(true);
            inputField.text = string.Empty;
            inputField.ActivateInputField();
        }
        _inputActive = true;
    }

    void CloseInput()
    {
        if (inputField != null)
            inputField.DeactivateInputField();
        _inputActive = false;
    }

    void SubmitMessage()
    {
        string text = inputField != null ? inputField.text : string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            CloseInput();
            return;
        }

        FixedString64Bytes sender = GetPlayerName();
        var msg = new ChatMessageComponent
        {
            senderName = sender,
            message = new FixedString128Bytes(text),
            teamOnly = true
        };

        Entity e = _em.CreateEntity(typeof(ChatMessageComponent));
        _em.SetComponentData(e, msg);

        if (inputField != null)
            inputField.text = string.Empty;

        CloseInput();
    }

    void AppendMessage(string sender, string message)
    {
        if (messagePrefab == null || messageContainer == null)
            return;

        TMP_Text txt = Instantiate(messagePrefab, messageContainer);
        txt.text = $"<b>{sender}:</b> {message}";
        txt.color = GetTeamColor();
        LayoutRebuilder.ForceRebuildLayoutImmediate(messageContainer);
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 0f;

        if (messageContainer.childCount > maxMessages)
            Destroy(messageContainer.GetChild(0).gameObject);
    }

    FixedString64Bytes GetPlayerName()
    {
        var q = _em.CreateEntityQuery(ComponentType.ReadOnly<DataContainerComponent>());
        if (!q.IsEmptyIgnoreFilter)
        {
            var data = q.GetSingleton<DataContainerComponent>();
            return data.playerName;
        }
        return new FixedString64Bytes("Player");
    }

    Color GetTeamColor()
    {
        var q = _em.CreateEntityQuery(ComponentType.ReadOnly<DataContainerComponent>());
        if (!q.IsEmptyIgnoreFilter)
        {
            var data = q.GetSingleton<DataContainerComponent>();
            return ((Team)data.teamID) switch
            {
                Team.TeamA => Color.blue,
                Team.TeamB => Color.red,
                _ => Color.white
            };
        }
        return Color.white;
    }

    bool TryResolveHistory()
    {
        if (_historyEntity != Entity.Null && _em.Exists(_historyEntity))
            return true;

        var q = _em.CreateEntityQuery(ComponentType.ReadOnly<ChatHistoryState>());
        if (!q.IsEmptyIgnoreFilter)
        {
            _historyEntity = q.GetSingletonEntity();
            return true;
        }

        return false;
    }
}

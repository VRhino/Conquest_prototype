using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using TMPro;
using Data.Items;

/// <summary>One acceptance per opening; delivery and consumption complete together or remain pending.</summary>
public class RewardDialogUIController : MonoBehaviour
{
    [Header("Dialog Data")]
    public RewardDialogDataSO dialogData;
    [Header("UI References")]
    public GameObject mainPanel;
    public TMP_Text titleText;
    public TMP_Text descriptionText;
    public Transform rewardItemsContainer;
    public GameObject inventoryItemCellPrefab;
    public Button acceptButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private float buttonSpacing = 12;
    [SerializeField] private float rewardCellScale = 1.375f;
    private readonly List<string> selectedItemIds = new();
    private HeroData _currentHero;
    private Func<List<string>, bool> onAccept;
    private Action onCancel, confirmation;
    private bool subscribed, ownsData, accepting;
    public bool IsOpen { get; private set; }

    private void Awake() => Initialize();
    public void Initialize()
    {
        if (!subscribed)
        {
            InventoryEventService.OnEquipmentConfirmationRequired += ShowConfirmation;
            InventoryManager.OnInventoryChanged += ValidateContext;
            subscribed = true;
        }
        if (acceptButton != null)
        {
            acceptButton.onClick.RemoveListener(OnAcceptButtonPressed);
            acceptButton.onClick.AddListener(OnAcceptButtonPressed);
        }
    }
    private void EnsureCancelButton()
    {
        if (cancelButton == null && acceptButton != null)
        {
            cancelButton = Instantiate(acceptButton, acceptButton.transform.parent);
            cancelButton.name = "CancelInventoryAction";
            cancelButton.onClick = new Button.ButtonClickedEvent();
            var rect = (RectTransform)cancelButton.transform;
            var acceptRect = (RectTransform)acceptButton.transform;
            rect.anchoredPosition = acceptRect.anchoredPosition + Vector2.left * (acceptRect.rect.width + buttonSpacing);
            var text = cancelButton.GetComponentInChildren<TMP_Text>();
            if (text != null) text.text = "Cancelar";
        }
        if (cancelButton != null)
        {
            cancelButton.gameObject.SetActive(true);
            cancelButton.onClick.RemoveListener(Close);
            cancelButton.onClick.AddListener(Close);
        }
    }
    private void Update()
    {
        if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
    }
    private void OnDisable() { if (IsOpen) Close(); }
    private void OnDestroy()
    {
        if (subscribed)
        {
            InventoryEventService.OnEquipmentConfirmationRequired -= ShowConfirmation;
            InventoryManager.OnInventoryChanged -= ValidateContext;
        }
        Close();
    }

    private void ValidateContext()
    {
        if (IsOpen && !ReferenceEquals(_currentHero, InventoryManager.GetCurrentHero())) Close();
    }

    public void Open(RewardDialogDataSO data, HeroData hero, Func<List<string>, bool> accepted = null,
        Action cancelled = null, bool ownRuntimeData = false)
    {
        if (data == null || hero == null) return;
        if (IsOpen) Close();
        Initialize();
        dialogData = data;
        _currentHero = hero;
        onAccept = accepted;
        onCancel = cancelled;
        ownsData = ownRuntimeData;
        IsOpen = true;
        accepting = false;
        mainPanel.SetActive(true);
        EnsureCancelButton();
        rewardItemsContainer.gameObject.SetActive(true);
        titleText.text = data.title;
        descriptionText.text = data.description;
        acceptButton.GetComponentInChildren<TMP_Text>().text = data.buttonText;
        acceptButton.interactable = !data.allowSelection || data.minSelection <= 0;
        PopulateRewardItems();
        FullscreenPanelManager.Instance?.NotifyRewardDialogOpened();
    }

    private void ShowConfirmation(InventoryItem item, string message, UnityAction confirm, UnityAction cancel)
    {
        if (IsOpen) { cancel?.Invoke(); return; }
        Initialize();
        confirmation = () => confirm?.Invoke();
        _currentHero = InventoryManager.GetCurrentHero();
        onCancel = () => cancel?.Invoke();
        IsOpen = true;
        accepting = false;
        mainPanel.SetActive(true);
        EnsureCancelButton();
        rewardItemsContainer.gameObject.SetActive(false);
        titleText.text = "Confirmar";
        descriptionText.text = message;
        acceptButton.GetComponentInChildren<TMP_Text>().text = "Aceptar";
        acceptButton.interactable = true;
        FullscreenPanelManager.Instance?.NotifyRewardDialogOpened();
    }

    private void PopulateRewardItems()
    {
        foreach (Transform child in rewardItemsContainer) Destroy(child.gameObject);
        selectedItemIds.Clear();
        foreach (var id in dialogData.rewardItemIds)
        {
            var definition = ItemService.GetItemById(id);
            if (definition == null) continue;
            var cellObject = Instantiate(inventoryItemCellPrefab, rewardItemsContainer);
            cellObject.transform.localScale = Vector3.one * rewardCellScale;
            var cell = cellObject.GetComponent<BaseItemCellController>();
            if (cell == null) { Destroy(cellObject); continue; }
            cell.SetContext(ItemInteractionContext.Reward);
            cell.SetSelected(false);
            cell.SetPreviewItem(definition);
            var button = cellObject.GetComponent<Button>();
            if (dialogData.allowSelection && button != null)
                button.onClick.AddListener(() => OnItemCellClicked(id, cell));
        }
    }
    private void OnItemCellClicked(string id, BaseItemCellController cell)
    {
        if (!IsOpen || dialogData == null) return;
        if (selectedItemIds.Contains(id)) { selectedItemIds.Remove(id); cell.SetSelected(false); }
        else if (selectedItemIds.Count < dialogData.maxSelection) { selectedItemIds.Add(id); cell.SetSelected(true); }
        acceptButton.interactable = selectedItemIds.Count >= dialogData.minSelection;
    }

    public void OnAcceptButtonPressed()
    {
        if (!IsOpen || accepting) return;
        accepting = true;
        if (confirmation != null)
        {
            var action = confirmation;
            CloseCore(false);
            action();
            return;
        }
        if (dialogData == null || !ReferenceEquals(_currentHero, InventoryManager.GetCurrentHero()))
        { accepting = false; return; }
        if (dialogData.allowSelection && (selectedItemIds.Count < dialogData.minSelection || selectedItemIds.Count > dialogData.maxSelection))
        { accepting = false; return; }
        var ids = new List<string>(dialogData.allowSelection ? selectedItemIds : dialogData.rewardItemIds);
        bool delivered = onAccept != null ? onAccept(ids) : dialogData.TryDeliverRewards(ids, _currentHero);
        if (delivered) CloseCore(false);
        else
        {
            accepting = false;
            descriptionText.text = "No se pudo completar la entrega. Revisa el espacio disponible o cancela para intentarlo después.";
        }
    }
    public void Close() => CloseCore(true);
    private void CloseCore(bool cancelled)
    {
        if (!IsOpen) return;
        IsOpen = false;
        var cancellation = onCancel;
        if (ownsData && dialogData != null) Destroy(dialogData);
        ownsData = accepting = false;
        dialogData = null;
        _currentHero = null;
        onAccept = null;
        onCancel = confirmation = null;
        selectedItemIds.Clear();
        if (mainPanel != null) mainPanel.SetActive(false);
        FullscreenPanelManager.Instance?.NotifyRewardDialogClosed();
        if (cancelled) cancellation?.Invoke();
    }
}

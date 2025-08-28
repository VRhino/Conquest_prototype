using System;
using System.Collections.Generic;
using ConquestTactics.Dialogue;
using TMPro;
using Unity.Entities.UniversalDelegates;
using UnityEngine;
using UnityEngine.UI;

public class NpcDialogueUIController : MonoBehaviour, IFullscreenPanel
{
    [Header("Referencias UI")]
    public GameObject dialoguePanel;
    public TMP_Text npcNameText;
    public Image npcImage;
    public TMP_Text dialogueText;
    public Transform optionsContainer;
    public GameObject optionButtonPrefab;

    private Action<DialogueOption> _onOptionSelected;
    private NpcDialogueData _currentData;

    public static NpcDialogueUIController Instance { get; private set; }

    public bool IsPanelOpen => dialoguePanel.activeSelf;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
    }

    public void setData(NpcDialogueData data, Action<DialogueOption> onOptionSelected)
    {
        _currentData = data;
        _onOptionSelected = onOptionSelected;
    }

    public void Open()
    {
        if (_currentData == null)
        {
            Debug.LogWarning("Open called but no data is set.");
            return;
        }

        if (dialoguePanel != null)
            dialoguePanel.SetActive(true);
        if (npcNameText != null)
            npcNameText.text = _currentData.npcName;
        if (npcImage != null)
            npcImage.sprite = _currentData.npcImage;
        if (dialogueText != null)
            dialogueText.text = _currentData.dialogueText;
        ClearOptions();
        foreach (var option in _currentData.options)
        {
            var btnObj = Instantiate(optionButtonPrefab, optionsContainer);
            var btnText = btnObj.GetComponentInChildren<TMP_Text>();
            if (btnText != null) btnText.text = option.optionText;
            var btn = btnObj.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() => OnOptionClicked(option));
            }
        }
    }

    public void Close()
    {
        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
        ClearOptions();
        _currentData = null;
        _onOptionSelected = null;
    }

    private void ClearOptions()
    {
        foreach (Transform child in optionsContainer)
        {
            Destroy(child.gameObject);
        }
    }

    private void OnOptionClicked(DialogueOption option)
    {
        _onOptionSelected?.Invoke(option);
    }

    public void OpenPanel()
    {
        Open();
    }

    public void ClosePanel()
    {
        Close();
    }

    public void TogglePanel()
    {
        if (IsPanelOpen)
            ClosePanel();
        else
            OpenPanel();
    }
}

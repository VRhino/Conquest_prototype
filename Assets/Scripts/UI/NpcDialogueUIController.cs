using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ConquestTactics.Dialogue;
using System;
using System.Collections.Generic;

namespace ConquestTactics.UI
{
    public class NpcDialogueUIController : MonoBehaviour
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

        public void Open(NpcDialogueData data, Action<DialogueOption> onOptionSelected)
        {
            _currentData = data;
            _onOptionSelected = onOptionSelected;
            if (dialoguePanel != null)
                dialoguePanel.SetActive(true);
            if (npcNameText != null)
                npcNameText.text = data.npcName;
            if (npcImage != null)
                npcImage.sprite = data.npcImage;
            if (dialogueText != null)
                dialogueText.text = data.dialogueText;
            ClearOptions();
            foreach (var option in data.options)
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
            Close();
        }
    }
}

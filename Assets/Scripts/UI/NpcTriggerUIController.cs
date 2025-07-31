using UnityEngine;

namespace ConquestTactics.UI
{
    /// <summary>
    /// Controlador singleton para mostrar/ocultar el prompt de interacción de NPCs.
    /// </summary>
    public class NpcTriggerUIController : MonoBehaviour
    {
        public static NpcTriggerUIController Instance { get; private set; }

        [Header("UI Prompt")]
        public GameObject promptPanel;
        public TMPro.TextMeshProUGUI promptText;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (promptPanel != null)
                promptPanel.SetActive(false);
        }

        /// <summary>
        /// Muestra el prompt de interacción con el texto indicado.
        /// </summary>
        public static void ShowInteractionPrompt(string text, MonoBehaviour trigger)
        {
            if (Instance == null) return;
            if (Instance.promptPanel != null)
                Instance.promptPanel.SetActive(true);
            if (Instance.promptText != null)
                Instance.promptText.text = text;
        }

        /// <summary>
        /// Oculta el prompt de interacción.
        /// </summary>
        public static void HideInteractionPrompt(MonoBehaviour trigger)
        {
            if (Instance == null) return;
            if (Instance.promptPanel != null)
                Instance.promptPanel.SetActive(false);
        }
    }
}

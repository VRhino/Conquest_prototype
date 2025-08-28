using UnityEngine;
using System.Collections.Generic;

namespace ConquestTactics.Dialogue
{
    /// <summary>
    /// Parámetros opcionales para efectos de diálogo.
    /// </summary>
    [System.Serializable]
    public class DialogueParameters
    {
        public string stringParameter;
        public int intParameter;
        public float floatParameter;
        public bool boolParameter;
    }

    [CreateAssetMenu(fileName = "NpcDialogueData", menuName = "NPC/NPC Dialogue Data", order = 1)]
    public class NpcDialogueData : ScriptableObject
    {
        [Header("NPC Info")]
        public string npcName;
        public Sprite npcImage;
        [TextArea(2, 5)]
        public string dialogueText;

        [Header("Opciones de diálogo")]
        public List<DialogueOption> options = new List<DialogueOption>();
    }

    [System.Serializable]
    public class DialogueOption
    {
        [Header("Basic Settings")]
        public string optionText;
        public DialogueOptionType optionType;
        
        [Header("Dialogue Effects")]
        [Tooltip("IDs de los efectos de diálogo a ejecutar (nombres de los ScriptableObjects)")]
        public string[] dialogueEffectIds; // Id guardados en la dialogueEffectsDatabase
        [Tooltip("Parámetros opcionales para los efectos de diálogo, estos sobre escriben algunos de los definidos en el scriptable object")]
        public DialogueParameters effectParameters; // Parámetros opcionales para los efectos
        
        [Header("Advanced Settings")]
        [Tooltip("Si está marcado, la opción solo aparece si todos los efectos se pueden ejecutar")]
        public bool requireEffectsCanExecute = false;
    }
}

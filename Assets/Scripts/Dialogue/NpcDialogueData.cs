using UnityEngine;
using System.Collections.Generic;

namespace ConquestTactics.Dialogue
{
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
        public string optionText;
        public DialogueOptionType optionType;
        public string nextMenuId; // Por ejemplo: "barracks", "armory", etc.
        public string customEvent; // Para lógica personalizada si se requiere
    }
}

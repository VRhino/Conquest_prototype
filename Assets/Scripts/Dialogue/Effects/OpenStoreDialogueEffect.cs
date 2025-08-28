using ConquestTactics.Dialogue;
using UnityEngine;

namespace ConquestTactics.Dialogue
{
    /// <summary>
    /// Efecto de diálogo que agrega un ítem al inventario del héroe.
    /// Reemplaza el customEvent "Add_Item" con un sistema más flexible.
    /// </summary>
    [CreateAssetMenu(fileName = "OpenStoreDialogueEffect", menuName = "NPC/Dialogue/Effects/Open Store", order = 1)]
    public class OpenStoreDialogueEffect : DialogueEffect
    {
        [Header("Menu Settings")]
        [SerializeField] private string storeId;
        private StoreData storeData => StoreDatabase.Instance.GetStoreById(storeId);

        public override bool Execute(HeroData hero, string npcId = null, DialogueParameters parameters = null)
        {
            if (!CanExecute(hero, npcId))
            {
                return false;
            }
            FullscreenPanelManager.Instance.HandleStoreOpen(storeData);

            return true;
        }

        public override string GetPreviewText()
        {
            return $"Open {storeData.storeTitle}";
        }

        public override bool CanExecute(HeroData hero, string npcId = null)
        {
            return true;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
        }
    }
}

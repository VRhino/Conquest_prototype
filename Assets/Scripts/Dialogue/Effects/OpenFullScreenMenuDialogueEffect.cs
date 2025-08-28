using ConquestTactics.Dialogue;
using UnityEngine;

namespace ConquestTactics.Dialogue
{
    public enum FullScreenMenuType
    {
        Store,
        Inventory,
        Barracks,
        HeroDetails,
        None
    }
    /// <summary>
    /// Efecto de diálogo que agrega un ítem al inventario del héroe.
    /// Reemplaza el customEvent "Add_Item" con un sistema más flexible.
    /// </summary>
    [CreateAssetMenu(fileName = "OpenFullScreenMenuDialogueEffect", menuName = "NPC/Dialogue/Effects/Open Full Screen Menu", order = 1)]
    public class OpenFullScreenMenuDialogueEffect : DialogueEffect
    {
        [Header("Menu Settings")]
        [SerializeField] private FullScreenMenuType MenuId;

        public override bool Execute(HeroData hero, string npcId = null, DialogueParameters parameters = null)
        {
            if (!CanExecute(hero, npcId))
            {
                return false;
            }
            if (MenuId == FullScreenMenuType.None)
            {
                Debug.LogError($"[OpenFullScreenMenuDialogueEffect] No menu ID specified");
                return false;
            }
            switch (MenuId)
            {
                case FullScreenMenuType.Store:
                    FullscreenPanelManager.Instance.HandleStoreOpen();
                    break;
                case FullScreenMenuType.Inventory:
                    FullscreenPanelManager.Instance.HandleInventoryKeyPress();
                    break;
                case FullScreenMenuType.Barracks:
                    FullscreenPanelManager.Instance.HandleBarracksKeyPress();
                    break;
                case FullScreenMenuType.HeroDetails:
                    FullscreenPanelManager.Instance.HandleHeroDetailKeyPress();
                    break;
                default:
                    Debug.LogError($"[OpenFullScreenMenuDialogueEffect] Unknown menu ID: {MenuId}");
                    return false;
            }
            return true;
        }

        public override string GetPreviewText()
        {
            return $"Open {MenuId}";
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

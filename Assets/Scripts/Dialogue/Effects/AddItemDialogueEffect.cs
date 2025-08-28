using UnityEngine;
using ConquestTactics.Dialogue;

namespace ConquestTactics.Dialogue
{
    /// <summary>
    /// Efecto de diálogo que agrega un ítem al inventario del héroe.
    /// Reemplaza el customEvent "Add_Item" con un sistema más flexible.
    /// </summary>
    [CreateAssetMenu(fileName = "AddItemDialogueEffect", menuName = "Dialogue/Effects/Add Item", order = 1)]
    public class AddItemDialogueEffect : DialogueEffect
{
    [Header("Item Settings")]
    [SerializeField] private string itemId;
    [SerializeField] private int quantity = 1;
    [SerializeField] private bool showNotification = true;
    [SerializeField] private string customMessage;

    public override bool Execute(HeroData hero, string npcId = null, DialogueParameters parameters = null)
    {
        if (!CanExecute(hero, npcId))
        {
            return false;
        }

        string targetItemId = parameters?.stringParameter == "" ? itemId : parameters.stringParameter;
        int targetQuantity = parameters?.intParameter > 0 ? parameters.intParameter : quantity;

        if (string.IsNullOrEmpty(targetItemId))
            {
                Debug.LogError($"[AddItemDialogueEffect] No item ID specified");
                return false;
            }

            // Agregar el ítem usando InventoryManager
        bool success = InventoryManager.CreateAndAddItem(targetItemId, targetQuantity);

        if (success)
        {
            if (showNotification)
            {
                string message = !string.IsNullOrEmpty(customMessage) 
                    ? customMessage 
                    : $"Received {targetQuantity}x {targetItemId}";
                
                // Aquí podrías disparar un evento de notificación UI
                Debug.Log($"[Dialogue] {message}");
            }
            FullscreenPanelManager.Instance.ClosePanel<NpcDialogueUIController>();
            OnEffectExecuted(hero, npcId);
            return true;
        }
        else
        {            
            FullscreenPanelManager.Instance.ClosePanel<NpcDialogueUIController>();
            Debug.LogWarning($"[AddItemDialogueEffect] Failed to add item: {targetItemId}");
            return false;
        }
    }

    public override string GetPreviewText()
    {
        return $"Add {quantity}x {itemId} to inventory";
    }

    public override bool CanExecute(HeroData hero, string npcId = null)
    {
        if (!base.CanExecute(hero, npcId))
        {
            return false;
        }

        return InventoryManager.HasSpace();
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        
        if (quantity <= 0)
        {
            quantity = 1;
        }
    }
}
}

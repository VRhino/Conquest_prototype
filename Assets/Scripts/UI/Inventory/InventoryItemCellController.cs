using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Data.Items;
using System.Collections.Generic;

public class InventoryItemCellController : MonoBehaviour
{
    [Header("References")]
    public GameObject itemPanel;
    public Image itemBackground;
    public Image itemMiniature;
    public Image filler;
    public TMP_Text stackText;
    public Image selectedOverlay;

    [Header("Background Sprites")]
    [SerializeField] private Sprite backgroundSpriteCommon;
    [SerializeField] private Sprite backgroundSpriteUncommon;
    [SerializeField] private Sprite backgroundSpriteRare;
    [SerializeField] private Sprite backgroundSpriteEpic;
    [SerializeField] private Sprite backgroundSpriteLegendary;
    [SerializeField] private InventoryItemCellInteraction _interaction;
    // Componente de drag and drop (se agrega dinámicamente)
    private Component _dragHandler;
    
    // Índice de la celda en la grilla
    [SerializeField] private int _cellIndex = -1;

    void Awake()
    {
        // Inicialización del componente de interacción
        _interaction = gameObject.AddComponent<InventoryItemCellInteraction>();
        if (_interaction == null)
        {
            Debug.Log($"[InventoryItemCellController] Interacción no configurada para la celda {_cellIndex}");
        }

        // Inicializar componente de drag and drop dinámicamente
        InitializeDragHandler();
    }
    
    private void InitializeDragHandler()
    {
        // Intentar obtener o agregar el componente de drag handler
        var dragHandlerType = System.Type.GetType("InventoryDragHandler");
        if (dragHandlerType != null)
        {
            _dragHandler = GetComponent(dragHandlerType);
            if (_dragHandler == null)
                _dragHandler = gameObject.AddComponent(dragHandlerType);
        }
    }

    /// <summary>
    /// Asigna un item a la celda. Si item es null, muestra solo el placeholder.
    /// </summary>
    public void SetItem(InventoryItem item, ItemData itemData)
    {
        bool hasItem = item != null && itemData != null;

        itemPanel.SetActive(hasItem);

        if (!hasItem) 
        {
            // Limpiar drag handler si no hay ítem
            CallDragHandlerMethod("ClearItemData");
            return;
        }

        SetItemVisuals(itemData);

        // Actualizar componente de interacción si existe
        if (_interaction != null)
            _interaction.SetItem(item, itemData);
        
        // Actualizar drag handler
        CallDragHandlerMethod("SetItemData", item, itemData, _cellIndex);

        // Stack
        if (itemData.stackable && item.quantity > 1)
        {
            stackText.gameObject.SetActive(true);
            stackText.text = item.quantity.ToString();
        }
        else
        {
            stackText.gameObject.SetActive(false);
        }
    }

    public void SetPreviewItem(ItemData itemPreviewData)
    {
        itemPanel.SetActive(true);
        stackText.gameObject.SetActive(false);
        SetItemVisuals(itemPreviewData);
    }

    private void SetItemVisuals(ItemData itemData)
    {
        // Asignar sprites
        Sprite itemSprite = itemData.iconPath != null ? Resources.Load<Sprite>(itemData.iconPath) : null;
        
        if (itemSprite == null)
        {
            Debug.LogWarning($"[InventoryItemCellController] No se pudo cargar sprite de ítem: {itemData.iconPath}");
            itemMiniature.gameObject.SetActive(false);
        }
        else
        {
            Debug.Log($"[InventoryItemCellController] Cargando sprite de ítem: {itemData.iconPath}");
            itemMiniature.gameObject.SetActive(true);
            itemMiniature.sprite = itemSprite;
        }

        itemBackground.sprite = itemData.rarity switch
        {
            ItemRarity.Common => backgroundSpriteCommon,
            ItemRarity.Uncommon => backgroundSpriteUncommon,
            ItemRarity.Rare => backgroundSpriteRare,
            ItemRarity.Epic => backgroundSpriteEpic,
            ItemRarity.Legendary => backgroundSpriteLegendary,
            _ => backgroundSpriteCommon // Default case
        };

        // Asignar color de rareza
        filler.color = InventoryUtils.GetRarityColor(itemData.rarity);
    }

    /// <summary>
    /// Limpia la celda y muestra solo el placeholder.
    /// </summary>
    public void Clear()
    {
        itemPanel.SetActive(false);

        // Limpiar interacción si existe
        if (_interaction != null)
            _interaction.ClearItem();

        // Limpiar drag handler
        CallDragHandlerMethod("ClearItemData");
    }
    
    /// <summary>
    /// Establece el índice de esta celda en la grilla.
    /// </summary>
    /// <param name="index">Índice de la celda</param>
    public void SetCellIndex(int index)
    {
        _cellIndex = index;
        CallDragHandlerMethod("SetItemData", null, null, _cellIndex);
    }
    
    /// <summary>
    /// Obtiene el índice de esta celda en la grilla.
    /// </summary>
    /// <returns>Índice de la celda</returns>
    public int GetCellIndex()
    {
        return _cellIndex;
    }
    
    /// <summary>
    /// Llama un método del drag handler usando reflexión para evitar dependencias de compilación.
    /// </summary>
    /// <param name="methodName">Nombre del método a llamar</param>
    /// <param name="parameters">Parámetros del método</param>
    private void CallDragHandlerMethod(string methodName, params object[] parameters)
    {
        if (_dragHandler == null) return;
        
        try
        {
            var method = _dragHandler.GetType().GetMethod(methodName);
            if (method != null)
            {
                method.Invoke(_dragHandler, parameters);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[InventoryItemCellController] Error calling drag handler method {methodName}: {ex.Message}");
        }
    }
}
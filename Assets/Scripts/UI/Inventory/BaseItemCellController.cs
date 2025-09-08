using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Data.Items;
using System;
using System.Numerics;

/// <summary>
/// Clase base abstracta para celdas y slots de inventario/equipamiento.
/// Centraliza la lógica y referencias visuales comunes.
/// </summary>
public abstract class BaseItemCellController : MonoBehaviour
{
    private string cellId = Guid.NewGuid().ToString();
    // Referencias visuales comunes
    public GameObject itemPanel;
    public Image itemBackground;
    public Image itemMiniature;
    public TMP_Text stackText;
    public Image selectedOverlay;
    public Image filler; // Referencia directa, no reflexión

    protected InventoryItem _currentItem;
    protected ItemData _currentItemData;

    // Sprites de fondo por rareza (pueden ser null en subclases)
    [Header("Background Sprites")]
    [SerializeField] protected Sprite backgroundSpriteCommon;
    [SerializeField] protected Sprite backgroundSpriteUncommon;
    [SerializeField] protected Sprite backgroundSpriteRare;
    [SerializeField] protected Sprite backgroundSpriteEpic;
    [SerializeField] protected Sprite backgroundSpriteLegendary;

    // Componente de interacción
    protected BaseItemCellInteraction _interaction;

    // Tipo de interacción que debe usar esta celda/slot
    protected virtual System.Type InteractionType  => typeof(BaseItemCellInteraction);
    private bool _isInitialized = false;

    public string CellId => cellId;


    public virtual void Initialize()
    {
        _interaction = (BaseItemCellInteraction)GetComponent(InteractionType);
        if (_interaction == null)
            _interaction = (BaseItemCellInteraction)gameObject.AddComponent(InteractionType); 
        if (_interaction != null)
        {
            _interaction.Initialize(cellId);
            _interaction.SetItem(_currentItem, _currentItemData, cellId);
        }
        _isInitialized = true;
    }

    /// <summary>
    /// Asigna un item a la celda. Si item es null, muestra solo el placeholder.
    /// </summary>
    public virtual void SetItem(InventoryItem item, ItemData itemData)
    {
        if(_isInitialized == false) Initialize();

        _currentItem = item;
        _currentItemData = itemData;

        bool hasItem = item != null && itemData != null;
        itemPanel.SetActive(hasItem);

        if (!hasItem)
            return;

        SetItemVisuals(itemData);

        // Actualizar componente de interacción si existe
        if (_interaction != null) _interaction.SetItem(item, itemData, cellId);

        // Stack
        if (itemData.stackable && item.quantity > 1)
        {
            stackText.gameObject.SetActive(true);
            stackText.text = item.quantity.ToString();
        }
        else stackText.gameObject.SetActive(false);
    }

    public virtual void ConnectWithTooltips(
        Action<InventoryItem, ItemData, UnityEngine.Vector3, string> OnItemHoverEnter,
        Action<InventoryItem, ItemData, UnityEngine.Vector3> OnItemHoverExit,
        Action<InventoryItem, ItemData, UnityEngine.Vector3> OnItemHoverMove,
        Action<InventoryItem, ItemData, string> OnSetItem,
        Action<InventoryItem, ItemData, string> OnClearItem
    )
    {
        if (_isInitialized == false) Initialize();
        _interaction?.ConnectWithTooltips(OnItemHoverEnter, OnItemHoverExit, OnItemHoverMove, OnSetItem, OnClearItem);
    }
    
    public virtual void DisconnectFromTooltips(
        Action<InventoryItem, ItemData, UnityEngine.Vector3, string> OnItemHoverEnter,
        Action<InventoryItem, ItemData, UnityEngine.Vector3> OnItemHoverExit,
        Action<InventoryItem, ItemData, UnityEngine.Vector3> OnItemHoverMove,
        Action<InventoryItem, ItemData, string> OnSetItem,
        Action<InventoryItem, ItemData, string> OnClearItem
    )
    {
        _interaction?.DisconnectFromTooltips(OnItemHoverEnter, OnItemHoverExit, OnItemHoverMove, OnSetItem, OnClearItem);
    }
    public virtual void SetSelected(bool isSelected)
    {
        if (selectedOverlay != null)
            selectedOverlay.gameObject.SetActive(isSelected);
    }

    /// <summary>
    /// Asigna los eventos de interacción al componente de interacción.
    /// </summary>
    public void SetEvents(System.Action<InventoryItem, ItemData> onItemClicked, System.Action<InventoryItem, ItemData> onItemRightClicked)
    {
        if (_isInitialized == false) Initialize();

        if (_interaction != null) _interaction.SetEvents(onItemClicked, onItemRightClicked);
        else Debug.LogWarning($"[BaseItemCellController] No interaction component found on cell {cellId} to set events.");
    }

    /// <summary>
    /// Remueve los eventos asignados al componente de interacción.
    /// </summary>
    public void RemoveEvents()
    {
        if (_interaction != null) _interaction.RemoveEvents();
    }

    /// <summary>
    /// Asigna un item de preview (sin cantidad ni interacción)
    /// </summary>
    public virtual void SetPreviewItem(ItemData itemPreviewData)
    {
        itemPanel.SetActive(true);
        stackText.gameObject.SetActive(false);
        SetItemVisuals(itemPreviewData);
    }

    /// <summary>
    /// Actualiza los elementos visuales según el item
    /// </summary>
    protected virtual void SetItemVisuals(ItemData itemData)
    {
        Sprite itemSprite = itemData.iconPath != null ? Resources.Load<Sprite>(itemData.iconPath) : null;
        if (itemSprite == null)
        {
            itemMiniature.gameObject.SetActive(false);
        }
        else
        {
            itemMiniature.gameObject.SetActive(true);
            itemMiniature.sprite = itemSprite;
        }

        if (itemBackground != null)
        {
            itemBackground.sprite = itemData.rarity switch
            {
                ItemRarity.Common => backgroundSpriteCommon,
                ItemRarity.Uncommon => backgroundSpriteUncommon,
                ItemRarity.Rare => backgroundSpriteRare,
                ItemRarity.Epic => backgroundSpriteEpic,
                ItemRarity.Legendary => backgroundSpriteLegendary,
                _ => backgroundSpriteCommon
            };
        }

        // Asignar color de rareza directamente
        if (filler != null)
            filler.color = InventoryUtils.GetRarityColor(itemData.rarity);
    }

    /// <summary>
    /// Limpia la celda y muestra solo el placeholder.
    /// </summary>
    public virtual void Clear()
    {
        itemPanel.SetActive(false);
        if (_interaction != null)
            _interaction.ClearItem();
    }
}

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

    [Header("Background Sprites")]
    [SerializeField] private Sprite backgroundSpriteCommon;
    [SerializeField] private Sprite backgroundSpriteUncommon;
    [SerializeField] private Sprite backgroundSpriteRare;
    [SerializeField] private Sprite backgroundSpriteEpic;
    [SerializeField] private Sprite backgroundSpriteLegendary;

    // Componente de interacción (se inicializa en runtime)
    // private InventoryItemCellInteraction _interaction;

    void Awake()
    {
        // Inicialización del componente de interacción
        // TODO: Agregar InventoryItemCellInteraction component cuando se necesite
    }

    /// <summary>
    /// Asigna un item a la celda. Si item es null, muestra solo el placeholder.
    /// </summary>
    public void SetItem(InventoryItem item, ItemData itemData)
    {
        bool hasItem = item != null && itemData != null;

        itemPanel.SetActive(hasItem);

        if (!hasItem) return;

        // Asignar sprites
        Sprite itemSprite = itemData.iconPath != null ? Resources.Load<Sprite>(itemData.iconPath) : null;
        itemBackground.sprite = itemData.rarity switch
        {
            ItemRarity.Common => backgroundSpriteCommon,
            ItemRarity.Uncommon => backgroundSpriteUncommon,
            ItemRarity.Rare => backgroundSpriteRare,
            ItemRarity.Epic => backgroundSpriteEpic,
            ItemRarity.Legendary => backgroundSpriteLegendary,
            _ => backgroundSpriteCommon // Default case
        };
        itemMiniature.sprite = itemSprite;

        // Asignar color de rareza
        filler.color = InventoryUtils.GetRarityColor(item);

        // Actualizar componente de interacción si existe
        // if (_interaction != null)
        //     _interaction.SetItem(item, itemData);

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

    /// <summary>
    /// Limpia la celda y muestra solo el placeholder.
    /// </summary>
    public void Clear()
    {
        itemPanel.SetActive(false);
        
        // Limpiar interacción si existe
        // if (_interaction != null)
        //     _interaction.ClearItem();
    }
}
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Data.Items;

/// <summary>
/// Renderizador de contenido para tooltips que maneja la población visual de información de ítems.
/// Componente interno responsable de mostrar título, contenido, iconos y texto de interacción.
/// </summary>
public class TooltipContentRenderer : ITooltipComponent
{
    private InventoryTooltipController _controller;

    // Referencias UI del controller
    private GameObject _titlePanel;
    private GameObject _contentPanel;
    private GameObject _interactionPanel;
    private Image _backgroundImage;
    private Image _dividerImage;
    private TMP_Text _title;
    private Image _miniatureImage;
    private TMP_Text _descriptionText;
    private TMP_Text _armorText;
    private TMP_Text _categoryText;
    private TMP_Text _durabilityText;
    private TMP_Text _actionText;

    #region ITooltipComponent Implementation

    public void Initialize(InventoryTooltipController controller)
    {
        _controller = controller;

        // Obtener referencias de los componentes UI del controller
        _titlePanel = controller.TitlePanel;
        _contentPanel = controller.ContentPanel;
        _interactionPanel = controller.InteractionPanel;
        _backgroundImage = controller.BackgroundImage;
        _dividerImage = controller.DividerImage;
        _title = controller.Title;
        _miniatureImage = controller.MiniatureImage;
        _descriptionText = controller.DescriptionText;
        _armorText = controller.ArmorText;
        _categoryText = controller.CategoryText;
        _durabilityText = controller.DurabilityText;
        _actionText = controller.ActionText;
    }

    public void Cleanup()
    {
        _controller = null;
        
        // Limpiar referencias UI
        _titlePanel = null;
        _contentPanel = null;
        _interactionPanel = null;
        _backgroundImage = null;
        _dividerImage = null;
        _title = null;
        _miniatureImage = null;
        _descriptionText = null;
        _armorText = null;
        _categoryText = null;
        _durabilityText = null;
        _actionText = null;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Puebla el contenido completo del tooltip con información del ítem.
    /// </summary>
    public void PopulateContent(InventoryItem item, ItemData itemData)
    {
        if (item == null || itemData == null) return;

        ClearContent();
        SetupTitlePanel(item, itemData);
        SetupContentPanel(item, itemData);
        SetupInteractionPanel(item, itemData);

        // Configurar stats usando el sistema de stats
        _controller.StatsSystem?.SetupStats(item, itemData);
    }

    /// <summary>
    /// Limpia todo el contenido del tooltip.
    /// </summary>
    public void ClearContent()
    {
        // Limpiar textos principales
        if (_title != null)
            _title.text = "";

        if (_descriptionText != null)
            _descriptionText.text = "";

        if (_categoryText != null)
            _categoryText.text = "";

        if (_durabilityText != null)
            _durabilityText.text = "";

        if (_actionText != null)
            _actionText.text = "";

        if (_armorText != null)
            _armorText.text = "";

        // Limpiar imagen
        if (_miniatureImage != null)
            _miniatureImage.sprite = null;

        // Limpiar stats usando el sistema de stats
        _controller.StatsSystem?.ClearStats();
    }

    #endregion

    #region Panel Setup Methods

    /// <summary>
    /// Configura el panel de título con nombre, icono y rareza.
    /// </summary>
    private void SetupTitlePanel(InventoryItem item, ItemData itemData)
    {
        // Configurar título
        if (_title != null)
            _title.text = itemData.name;

        // Configurar icono del ítem
        SetItemIcon(itemData);

        // Configurar background y divider por rareza (PRESERVAR rarityBackground)
        SetRarityVisuals(itemData.rarity);
    }

    /// <summary>
    /// Configura el panel de contenido con descripción, categoría y durabilidad.
    /// </summary>
    private void SetupContentPanel(InventoryItem item, ItemData itemData)
    {
        // Configurar descripción
        if (_descriptionText != null && !string.IsNullOrEmpty(itemData.description))
            _descriptionText.text = itemData.description;

        // Configurar categoría
        if (_categoryText != null)
            _categoryText.text = TooltipFormattingUtils.GetItemTypeDisplayName(itemData.itemType);

        // Configurar información de armadura si aplica
        if (_armorText != null && IsArmorItem(itemData.itemType))
        {
            _armorText.text = TooltipFormattingUtils.GetArmorTypeInfo(itemData.itemType);
            _armorText.gameObject.SetActive(true);
        }
        else if (_armorText != null)
        {
            _armorText.gameObject.SetActive(false);
        }

        // Configurar durabilidad (placeholder - implementar cuando esté el sistema)
        if (_durabilityText != null)
        {
            // Por ahora ocultar durabilidad
            _durabilityText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Configura el panel de interacción con texto de acciones disponibles.
    /// </summary>
    private void SetupInteractionPanel(InventoryItem item, ItemData itemData)
    {
        if (_actionText == null) return;

        string actionString = TooltipFormattingUtils.GetActionText(itemData, item);

        if (!string.IsNullOrEmpty(actionString))
        {
            _actionText.text = actionString;
            _interactionPanel?.SetActive(true);
        }
        else
        {
            _interactionPanel?.SetActive(false);
        }
    }

    #endregion

    #region Visual Configuration

    /// <summary>
    /// Configura el icono del ítem.
    /// </summary>
    private void SetItemIcon(ItemData itemData)
    {
        if (_miniatureImage == null) return;

        if (!string.IsNullOrEmpty(itemData.iconPath))
        {
            Sprite itemSprite = Resources.Load<Sprite>(itemData.iconPath);
            if (itemSprite != null)
            {
                _miniatureImage.sprite = itemSprite;
                _miniatureImage.gameObject.SetActive(true);
            }
            else
            {
                Debug.LogWarning($"[TooltipContentRenderer] No se encontró sprite en: {itemData.iconPath}");
                _miniatureImage.gameObject.SetActive(false);
            }
        }
        else
        {
            _miniatureImage.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Configura los elementos visuales basados en la rareza del ítem.
    /// IMPORTANTE: Preserva la funcionalidad rarityBackground como se solicitó.
    /// </summary>
    private void SetRarityVisuals(ItemRarity rarity)
    {
        // Configurar background sprite por rareza (PRESERVAR rarityBackground)
        if (_backgroundImage != null)
        {
            Sprite backgroundSprite = GetBackgroundSpriteByRarity(rarity);
            if (backgroundSprite != null)
            {
                _backgroundImage.sprite = backgroundSprite;
            }
        }

        // Configurar color del divider por rareza
        if (_dividerImage != null)
        {
            Color rarityColor = InventoryUtils.GetRarityColor(rarity);
            _dividerImage.color = rarityColor;
        }
    }

    /// <summary>
    /// Obtiene el sprite de background según la rareza.
    /// PRESERVADO: Funcionalidad rarityBackground original.
    /// </summary>
    private Sprite GetBackgroundSpriteByRarity(ItemRarity rarity)
    {
        return rarity switch
        {
            ItemRarity.Common => _controller.CommonBackgroundSprite,
            ItemRarity.Uncommon => _controller.UncommonBackgroundSprite,
            ItemRarity.Rare => _controller.RareBackgroundSprite,
            ItemRarity.Epic => _controller.EpicBackgroundSprite,
            ItemRarity.Legendary => _controller.LegendaryBackgroundSprite,
            _ => _controller.CommonBackgroundSprite
        };
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Verifica si un tipo de ítem es armadura.
    /// </summary>
    private bool IsArmorItem(ItemType itemType)
    {
        return itemType == ItemType.Helmet ||
               itemType == ItemType.Torso ||
               itemType == ItemType.Gloves ||
               itemType == ItemType.Pants ||
               itemType == ItemType.Boots;
    }

    #endregion
}

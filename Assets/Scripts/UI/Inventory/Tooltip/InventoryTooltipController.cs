using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Data.Items;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Controlador principal del tooltip del inventario que muestra información detallada de los ítems.
/// Se activa cuando el usuario pone el cursor sobre un ítem en el inventario.
/// </summary>
public class InventoryTooltipController : MonoBehaviour
{
    [Header("Main Tooltip")]
    public GameObject tooltipPanel;
    public Canvas tooltipCanvas;

    [Header("Title Panel")]
    public GameObject titlePanel;
    public Image backgroundImage;
    public Image dividerImage;
    public TMP_Text title;
    public Image miniatureImage;

    [Header("Content Panel")]
    public GameObject contentPanel;
    public TMP_Text descriptionText;
    public TMP_Text armorText;
    public TMP_Text categoryText;
    public TMP_Text durabilityText;

    [Header("Stats Panel")]
    public GameObject statsPanel;
    public Transform statsContainer;
    public GameObject statEntryPrefab;

    [Header("Interaction Panel")]
    public GameObject interactionPanel;
    public TMP_Text actionText;

    [Header("Background Sprites por Rareza")]
    [SerializeField] private Sprite commonBackgroundSprite;
    [SerializeField] private Sprite uncommonBackgroundSprite;
    [SerializeField] private Sprite rareBackgroundSprite;
    [SerializeField] private Sprite epicBackgroundSprite;
    [SerializeField] private Sprite legendaryBackgroundSprite;

    [Header("Tooltip Configuration")]
    [SerializeField] private TooltipType tooltipType = TooltipType.Primary;
    [SerializeField] private Vector2 comparisonPositionOffset = new Vector2(300f, 0f);

    [Header("Settings")]
    [SerializeField] private Vector2 tooltipOffset = new Vector2(15f, 15f); // Aumentado para mayor separación
    [SerializeField] private float showDelay = 0.2f;
    [SerializeField] private bool followMouse = true;

    /// <summary>
    /// Tipos de tooltip disponibles para configuración.
    /// </summary>
    public enum TooltipType
    {
        Primary,     // Tooltip principal - muestra item del inventario con comparación
        Secondary    // Tooltip secundario - muestra item equipado como referencia
    }

    /// <summary>
    /// Tipo actual del tooltip configurado en este controlador.
    /// </summary>
    public TooltipType CurrentTooltipType => tooltipType;

    // Control de estado
    private bool _isShowing = false;
    
    /// <summary>
    /// Indica si el tooltip está actualmente visible.
    /// </summary>
    public bool IsShowing => _isShowing;
    private float _showTimer = 0f;
    private InventoryItem _currentItem;
    private ItemData _currentItemData;
    private Vector3 _lastMousePosition = Vector3.zero;
    private List<GameObject> _statEntries = new List<GameObject>();

    void Awake()
    {
        InitializeTooltip();
    }

    void Start()
    {
        HideTooltip();
    }

    void Update()
    {
        // Manejar delay de aparición
        if (_showTimer > 0f)
        {
            _showTimer -= Time.unscaledDeltaTime;
            if (_showTimer <= 0f && _currentItem != null)
            {
                ShowTooltipImmediate();
            }
        }
    }

    /// <summary>
    /// Inicializa los componentes del tooltip.
    /// </summary>
    private void InitializeTooltip()
    {
        if (tooltipCanvas == null)
            tooltipCanvas = GetComponentInParent<Canvas>();

        if (tooltipCanvas == null)
        {
            Debug.LogWarning("[InventoryTooltipController] No se encontró Canvas para el tooltip");
        }

        // Configurar el pivot del tooltip para que la esquina superior izquierda sea el punto de referencia
        if (tooltipPanel != null)
        {
            RectTransform rectTransform = tooltipPanel.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                // Pivot en esquina superior izquierda (0, 1)
                rectTransform.pivot = new Vector2(0f, 1f);
            }
        }

        ValidateComponents();
    }

    /// <summary>
    /// Muestra el tooltip para un ítem específico con delay.
    /// </summary>
    /// <param name="item">Ítem del inventario</param>
    /// <param name="itemData">Datos del ítem</param>
    public void ShowTooltip(InventoryItem item, ItemData itemData)
    {
        if (item == null || itemData == null)
        {
            HideTooltip();
            return;
        }

        _currentItem = item;
        _currentItemData = itemData;
        _showTimer = showDelay;
    }

    /// <summary>
    /// Muestra el tooltip para un ítem específico con delay y posición del mouse.
    /// </summary>
    /// <param name="item">Ítem del inventario</param>
    /// <param name="itemData">Datos del ítem</param>
    /// <param name="mousePosition">Posición del mouse en coordenadas de pantalla</param>
    public void ShowTooltip(InventoryItem item, ItemData itemData, Vector3 mousePosition)
    {
        if (item == null || itemData == null)
        {
            HideTooltip();
            return;
        }

        _currentItem = item;
        _currentItemData = itemData;
        _lastMousePosition = mousePosition;
        _showTimer = showDelay;
    }

    /// <summary>
    /// Muestra el tooltip inmediatamente sin delay.
    /// </summary>
    private void ShowTooltipImmediate()
    {
        if (_currentItem == null || _currentItemData == null) return;

        StartCoroutine(ShowTooltipCoroutine());
    }

    /// <summary>
    /// Corrutina para mostrar el tooltip con rebuild de layout adecuado.
    /// </summary>
    private System.Collections.IEnumerator ShowTooltipCoroutine()
    {
        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(true);
            
            // SOLUCIÓN CRITICA: Deshabilitar raycasting en el tooltip para evitar bucle infinito
            // El tooltip no debe interferir con el raycasting de las celdas
            DisableRaycastTargetsInTooltip();
        }

        // Poblar contenido
        PopulateTooltipContent();

        // Esperar un frame para que el layout se calcule
        yield return null;

        // Forzar otro rebuild después del frame
        ForceLayoutRebuild();

        // Esperar otro frame para asegurar que todo esté calculado
        yield return null;

        // Ahora posicionar el tooltip
        if (_lastMousePosition != Vector3.zero)
        {
            UpdateTooltipPosition(_lastMousePosition);
        }
        else
        {
            Vector3 defaultPos = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            UpdateTooltipPosition(defaultPos);
        }

        _isShowing = true;
        _showTimer = 0f;
    }

    /// <summary>
    /// Oculta el tooltip.
    /// </summary>
    public void HideTooltip()
    {
        // Detener cualquier corrutina de mostrar tooltip
        StopAllCoroutines();

        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);

        _isShowing = false;
        _showTimer = 0f;
        _currentItem = null;
        _currentItemData = null;
    }

    /// <summary>
    /// Verifica si actualmente se está mostrando un ítem específico.
    /// </summary>
    /// <param name="itemId">ID del ítem a verificar</param>
    /// <returns>True si el tooltip está mostrando este ítem</returns>
    public bool IsShowingItem(string itemId)
    {
        return _isShowing && _currentItem != null && _currentItem.itemId == itemId;
    }

    /// <summary>
    /// Verifica si actualmente se está mostrando un ítem específico por instanceId (para equipment).
    /// </summary>
    /// <param name="instanceId">Instance ID del ítem a verificar</param>
    /// <returns>True si el tooltip está mostrando este ítem</returns>
    public bool IsShowingItemInstance(string instanceId)
    {
        return _isShowing && _currentItem != null && _currentItem.instanceId == instanceId;
    }

    /// <summary>
    /// Valida si el tooltip actual sigue siendo válido y lo actualiza o oculta según corresponda.
    /// </summary>
    public void ValidateAndRefreshTooltip()
    {
        if (!_isShowing || _currentItem == null) return;
        
        var currentHero = PlayerSessionService.SelectedHero;
        if (currentHero?.inventory == null) 
        {
            HideTooltip();
            return;
        }
        
        // Verificar si el item sigue siendo válido
        if (!InventoryUtils.IsTooltipItemValid(_currentItem, currentHero.inventory))
        {
            HideTooltip();
            return;
        }
        
        // Buscar item actualizado
        var updatedItem = InventoryUtils.GetUpdatedTooltipItem(_currentItem, currentHero.inventory);
        if (updatedItem != null && InventoryUtils.HasTooltipRelevantChanges(_currentItem, updatedItem))
        {
            // Refrescar tooltip con datos actualizados
            var itemData = InventoryUtils.GetItemData(updatedItem.itemId);
            if (itemData != null)
            {
                _currentItem = updatedItem;
                _currentItemData = itemData;
                PopulateTooltipContent(); // Método existente
            }
        }
    }

    /// <summary>
    /// Valida y oculta el tooltip si el ítem especificado ya no es válido.
    /// </summary>
    /// <param name="removedItemId">ID del ítem que fue removido</param>
    public void ValidateTooltipForRemovedItem(string removedItemId)
    {
        if (!_isShowing || _currentItem == null) return;
        
        // Si el tooltip está mostrando el ítem removido, ocultarlo
        if (_currentItem.itemId == removedItemId)
        {
            // Para equipment, verificar también por instanceId para mayor precisión
            if (!string.IsNullOrEmpty(_currentItem.instanceId))
            {
                var currentHero = PlayerSessionService.SelectedHero;
                if (currentHero?.inventory != null)
                {
                    // Si el item con este instanceId ya no existe, ocultar tooltip
                    bool stillExists = currentHero.inventory.Any(item => 
                        item.instanceId == _currentItem.instanceId);
                    
                    if (!stillExists)
                    {
                        HideTooltip();
                    }
                }
            }
            else
            {
                // Para stackables, hacer validación completa
                ValidateAndRefreshTooltip();
            }
        }
    }

    /// <summary>
    /// Rellena el contenido del tooltip con la información del ítem.
    /// </summary>
    private void PopulateTooltipContent()
    {
        if (_currentItem == null || _currentItemData == null) return;

        // Limpiar contenido previo para evitar problemas de tamaño
        ClearTooltipContent();

        // Configurar título y apariencia por rareza
        SetupTitlePanel();

        // Configurar contenido principal
        SetupContentPanel();

        // Configurar panel de stats si aplica
        SetupStatsPanel();

        // Configurar panel de interacción
        SetupInteractionPanel();

        // Forzar rebuild del layout para evitar problemas de tamaño
        ForceLayoutRebuild();
    }

    /// <summary>
    /// Limpia el contenido previo del tooltip.
    /// </summary>
    private void ClearTooltipContent()
    {
        // Limpiar stats previas
        ClearStatEntries();

        // Ocultar paneles que pueden no ser necesarios
        if (armorText != null)
            armorText.gameObject.SetActive(false);

        if (statsPanel != null)
            statsPanel.SetActive(false);

        // Limpiar textos
        if (descriptionText != null)
            descriptionText.text = "";

        if (categoryText != null)
            categoryText.text = "";

        if (durabilityText != null)
            durabilityText.text = "";

        if (actionText != null)
            actionText.text = "";
    }

    /// <summary>
    /// Fuerza la reconstrucción del layout del tooltip para evitar problemas de tamaño.
    /// </summary>
    private void ForceLayoutRebuild()
    {
        if (tooltipPanel == null) return;

        // Forzar rebuild de todos los layout groups
        Canvas.ForceUpdateCanvases();
        
        // Rebuild específico de cada panel
        if (titlePanel != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(titlePanel.GetComponent<RectTransform>());
        }

        if (contentPanel != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentPanel.GetComponent<RectTransform>());
        }

        if (statsPanel != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(statsPanel.GetComponent<RectTransform>());
        }

        if (interactionPanel != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(interactionPanel.GetComponent<RectTransform>());
        }

        // Rebuild final del panel principal
        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipPanel.GetComponent<RectTransform>());
    }

    /// <summary>
    /// Configura el panel de título con colores y sprites por rareza.
    /// </summary>
    private void SetupTitlePanel()
    {
        if (titlePanel != null)
            titlePanel.SetActive(true);

        // Configurar background por rareza
        if (backgroundImage != null)
        {
            backgroundImage.sprite = GetBackgroundSpriteForRarity(_currentItemData.rarity);
        }

        // Configurar divider con color de rareza
        if (dividerImage != null)
        {
            dividerImage.color = InventoryUtils.GetRarityColor(_currentItem);
        }

        // Configurar título por rareza
        if (title != null)
        {
            title.text = _currentItemData.name;
        }

        // Configurar miniatura del ítem
        if (miniatureImage != null)
        {
            SetItemIcon();
        }
    }

    /// <summary>
    /// Configura el panel de contenido principal.
    /// </summary>
    private void SetupContentPanel()
    {
        if (contentPanel != null)
            contentPanel.SetActive(true);

        // Descripción
        if (descriptionText != null)
        {
            descriptionText.text = !string.IsNullOrEmpty(_currentItemData.description) 
                ? _currentItemData.description 
                : "Sin descripción disponible.";
        }

        // Armadura (solo para equipment de protección)
        if (armorText != null)
        {
            bool isArmorItem = IsArmorItem(_currentItemData.itemType);
            armorText.gameObject.SetActive(isArmorItem);
            
            if (isArmorItem)
            {
                string armorType = GetArmorType(_currentItemData.itemType);
                armorText.text = $"Armadura: {armorType}";
            }
        }

        // Categoría
        if (categoryText != null)
        {
            categoryText.text = $"{GetItemTypeDisplayName(_currentItemData.itemType)}";
        }

        // Durabilidad (placeholder - se puede implementar en el futuro)
        if (durabilityText != null)
        {
            durabilityText.text = $"100/100";
        }
    }

    /// <summary>
    /// Configura el panel de estadísticas para equipment.
    /// </summary>
    private void SetupStatsPanel()
    {
        if (statsPanel == null || statsContainer == null) return;

        // Limpiar entradas anteriores
        ClearStatEntries();

        bool isEquipment = _currentItem.IsEquipment && _currentItem.GeneratedStats != null && _currentItem.GeneratedStats.Count > 0;
        statsPanel.SetActive(isEquipment);

        if (isEquipment)
        {
            // Primary tooltip muestra comparación SI hay equipado del mismo tipo
            if (tooltipType == TooltipType.Primary && HasEquippedItemForComparison())
            {
                SetupComparisonStats();
            }
            // Secondary tooltip (o Primary sin equipado) muestra stats normales
            else
            {
                foreach (var stat in _currentItem.GeneratedStats)
                {
                    CreateStatEntry(stat.Key, stat.Value);
                }
            }
        }
    }

    /// <summary>
    /// Verifica si hay un ítem equipado del mismo tipo para mostrar comparación.
    /// </summary>
    /// <returns>True si se puede mostrar comparación</returns>
    private bool HasEquippedItemForComparison()
    {
        if (_currentItemData == null || !_currentItemData.IsEquipment) return false;
        
        return ComparisonTooltipUtils.ShouldShowComparison(_currentItemData) &&
               ComparisonTooltipUtils.GetEquippedItemForComparison(_currentItemData.itemType) != null;
    }

    /// <summary>
    /// Configura el panel de interacción.
    /// </summary>
    private void SetupInteractionPanel()
    {
        // Solo mostrar panel de interacción en tooltips primarios
        if (tooltipType == TooltipType.Secondary)
        {
            if (interactionPanel != null)
                interactionPanel.SetActive(false);
            return;
        }

        if (interactionPanel != null)
            interactionPanel.SetActive(true);

        if (actionText != null)
        {
            string actionString = GetActionText();
            actionText.text = actionString;
        }
    }

    /// <summary>
    /// Crea una entrada de estadística en el panel de stats.
    /// </summary>
    /// <param name="statName">Nombre de la estadística</param>
    /// <param name="statValue">Valor de la estadística</param>
    private void CreateStatEntry(string statName, float statValue)
    {
        if (statEntryPrefab == null || statsContainer == null) return;

        GameObject entry = Instantiate(statEntryPrefab, statsContainer);
        _statEntries.Add(entry);

        // Buscar componentes de texto en el prefab
        TMP_Text[] texts = entry.GetComponentsInChildren<TMP_Text>();
        
        if (texts.Length >= 2)
        {
            texts[0].text = GetStatDisplayName(statName);
            texts[1].text = FormatStatValue(statValue);
        }
        else if (texts.Length == 1)
        {
            texts[0].text = $"{GetStatDisplayName(statName)}: {FormatStatValue(statValue)}";
        }
    }

    /// <summary>
    /// Configura las estadísticas para el tooltip de comparación.
    /// </summary>
    private void SetupComparisonStats()
    {
        if (_currentItemData?.IsEquipment != true) return;

        // Obtener el ítem equipado correspondiente
        var equippedItem = ComparisonTooltipUtils.GetEquippedItemForComparison(_currentItemData.itemType);
        if (equippedItem?.GeneratedStats == null) return;

        // Comparar las estadísticas usando los InventoryItems
        var comparisons = StatComparisonUtils.CompareItemStats(_currentItem, equippedItem);

        // Crear entradas para cada comparación significativa
        foreach (var comparison in comparisons)
        {
            if (StatComparisonUtils.IsSignificantStat(comparison))
            {
                CreateComparisonStatEntry(comparison);
            }
        }
    }

    /// <summary>
    /// Crea una entrada de estadística con comparación.
    /// </summary>
    /// <param name="comparison">Comparación de la estadística</param>
    private void CreateComparisonStatEntry(StatComparison comparison)
    {
        if (statEntryPrefab == null || statsContainer == null) return;

        GameObject entry = Instantiate(statEntryPrefab, statsContainer);
        _statEntries.Add(entry);

        // Buscar componentes de texto en el prefab
        TMP_Text[] texts = entry.GetComponentsInChildren<TMP_Text>();
        
        if (texts.Length >= 2)
        {
            // Nombre de la estadística
            texts[0].text = GetStatDisplayName(comparison.statName);
            
            // Valor con comparación
            texts[1].text = StatComparisonUtils.FormatComparisonValue(comparison);
            texts[1].color = StatComparisonUtils.GetComparisonColor(comparison);
        }
        else if (texts.Length == 1)
        {
            texts[0].text = $"{GetStatDisplayName(comparison.statName)}: {StatComparisonUtils.FormatComparisonValue(comparison)}";
            texts[0].color = StatComparisonUtils.GetComparisonColor(comparison);
        }
    }

    /// <summary>
    /// Limpia todas las entradas de estadísticas.
    /// </summary>
    private void ClearStatEntries()
    {
        foreach (GameObject entry in _statEntries)
        {
            if (entry != null)
                DestroyImmediate(entry);
        }
        _statEntries.Clear();
    }

    /// <summary>
    /// Actualiza la posición del tooltip en una posición específica.
    /// </summary>
    /// <param name="screenPosition">Posición en coordenadas de pantalla</param>
    public void UpdateTooltipPosition(Vector3 screenPosition)
    {
        if (tooltipPanel == null) return;

        RectTransform rectTransform = tooltipPanel.GetComponent<RectTransform>();
        if (rectTransform == null) return;

        // Asegurar que tenemos un Canvas válido
        Canvas canvas = tooltipCanvas ?? rectTransform.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        // Convertir posición de pantalla a posición local del Canvas
        Vector2 localPosition;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform, 
            screenPosition, 
            canvas.worldCamera, 
            out localPosition))
        {
            // Con pivot en (0,1), la posición será la esquina superior izquierda del tooltip
            // Aplicar offset para separar del cursor
            Vector2 offset = tooltipOffset;
            
            // Si es tooltip secundario, aplicar el offset adicional
            if (tooltipType == TooltipType.Secondary)
            {
                offset += comparisonPositionOffset;
            }
            
            localPosition += new Vector2(offset.x, -offset.y);
            
            // Obtener dimensiones para validación de límites
            Vector2 tooltipSize = rectTransform.sizeDelta;
            Vector2 canvasSize = (canvas.transform as RectTransform).sizeDelta;
            float canvasHalfWidth = canvasSize.x * 0.5f;
            float canvasHalfHeight = canvasSize.y * 0.5f;
            
            // Ajustar si se sale del canvas
            // Si se sale por la derecha, mover a la izquierda del cursor
            if (localPosition.x + tooltipSize.x > canvasHalfWidth)
            {
                localPosition.x = localPosition.x - tooltipSize.x - offset.x * 2;
            }
            
            // Si se sale por abajo, mover arriba del cursor  
            if (localPosition.y - tooltipSize.y < -canvasHalfHeight)
            {
                localPosition.y = localPosition.y + tooltipSize.y + offset.y * 2;
            }
            
            // Asegurar límites mínimos
            localPosition.x = Mathf.Max(-canvasHalfWidth + 5, localPosition.x);
            localPosition.y = Mathf.Min(canvasHalfHeight - 5, localPosition.y);
            
            rectTransform.localPosition = localPosition;
            
        }
        else
        {
            // Fallback: posición directa con offset
            Vector3 adjustedPosition = screenPosition + new Vector3(tooltipOffset.x, -tooltipOffset.y, 0f);
            rectTransform.position = adjustedPosition;
        }
    }

    /// <summary>
    /// Configura el icono del ítem en la miniatura.
    /// </summary>
    private void SetItemIcon()
    {
        if (miniatureImage == null) return;

        // Intentar cargar el sprite del ítem
        if (!string.IsNullOrEmpty(_currentItemData.iconPath))
        {
           
            Sprite itemSprite = Resources.Load<Sprite>(_currentItemData.iconPath);
            if (itemSprite != null)
            {
                miniatureImage.sprite = itemSprite;
                miniatureImage.gameObject.SetActive(true);
            }
           
        }
        else
        {
            // Si no se encontró sprite, ocultar miniatura
            miniatureImage.gameObject.SetActive(false);
            Debug.LogWarning($"[InventoryTooltipController] No se pudo cargar sprite de ítem: {_currentItemData.iconPath}");
        }

    }

    /// <summary>
    /// Deshabilita el raycasting en todos los elementos UI del tooltip para evitar interferencia.
    /// CRÍTICO: Esto previene el bucle infinito donde el tooltip bloquea el hover de las celdas.
    /// </summary>
    private void DisableRaycastTargetsInTooltip()
    {
        if (tooltipPanel == null) return;

        // Deshabilitar raycasting en todos los Graphic (Image, Text, etc.) del tooltip
        UnityEngine.UI.Graphic[] graphics = tooltipPanel.GetComponentsInChildren<UnityEngine.UI.Graphic>(includeInactive: true);
        
        foreach (var graphic in graphics)
        {
            graphic.raycastTarget = false;
        }

    }

    /// <summary>
    /// Método para testing: Verifica el estado de raycast targets en el tooltip.
    /// </summary>
    [ContextMenu("Debug Raycast Targets")]
    private void DebugRaycastTargets()
    {
        if (tooltipPanel == null)
        {
            Debug.Log("[InventoryTooltipController] Tooltip panel is null");
            return;
        }

        UnityEngine.UI.Graphic[] graphics = tooltipPanel.GetComponentsInChildren<UnityEngine.UI.Graphic>(includeInactive: true);
        int enabledTargets = 0;
        int disabledTargets = 0;

        foreach (var graphic in graphics)
        {
            if (graphic.raycastTarget)
            {
                enabledTargets++;
            }
            else
            {
                disabledTargets++;
            }
        }

        
        if (enabledTargets > 0)
        {
            Debug.LogWarning($"[InventoryTooltipController] WARNING: {enabledTargets} elements still have raycast enabled - this may cause hover interference!");
        }
    }

    #region Helper Methods

    /// <summary>
    /// Obtiene el sprite de fondo según la rareza.
    /// </summary>
    private Sprite GetBackgroundSpriteForRarity(ItemRarity rarity)
    {
        return rarity switch
        {
            ItemRarity.Common => commonBackgroundSprite,
            ItemRarity.Uncommon => uncommonBackgroundSprite,
            ItemRarity.Rare => rareBackgroundSprite,
            ItemRarity.Epic => epicBackgroundSprite,
            ItemRarity.Legendary => legendaryBackgroundSprite,
            _ => commonBackgroundSprite
        };
    }

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

    /// <summary>
    /// Obtiene el tipo de armadura según el slot.
    /// </summary>
    private string GetArmorType(ItemType itemType)
    {
        return itemType switch
        {
            ItemType.Helmet => "Casco",
            ItemType.Torso => "Pechera",
            ItemType.Gloves => "Guantes",
            ItemType.Pants => "Pantalones",
            ItemType.Boots => "Botas",
            _ => "Desconocido"
        };
    }

    /// <summary>
    /// Obtiene el nombre de display para un tipo de ítem.
    /// </summary>
    private string GetItemTypeDisplayName(ItemType itemType)
    {
        return itemType switch
        {
            ItemType.Weapon => "Arma",
            ItemType.Helmet => "Casco",
            ItemType.Torso => "Armadura",
            ItemType.Gloves => "Guantes",
            ItemType.Pants => "Pantalones",
            ItemType.Boots => "Botas",
            ItemType.Consumable => "Consumible",
            ItemType.Visual => "Cosmético",
            _ => itemType.ToString()
        };
    }

    /// <summary>
    /// Obtiene el nombre de display para una estadística.
    /// </summary>
    private string GetStatDisplayName(string statName)
    {
        return statName switch
        {
            "PiercingDamage" => "Daño Perforante",
            "SlashingDamage" => "Daño Cortante",
            "BluntDamage" => "Daño Contundente",
            "PiercingDefense" => "Defensa Perforante",
            "SlashingDefense" => "Defensa Cortante",
            "BluntDefense" => "Defensa Contundente",
            "PiercingPenetration" => "Penetración Perforante",
            "SlashingPenetration" => "Penetración Cortante",
            "BluntPenetration" => "Penetración Contundente",
            "Health" => "Salud",
            "Armor" => "Armadura",
            "Vitality" => "Vitalidad",
            "Strength" => "Fuerza",
            "Dexterity" => "Destreza",
            _ => statName
        };
    }

    /// <summary>
    /// Formatea el valor de una estadística para mostrar.
    /// </summary>
    private string FormatStatValue(float value)
    {
        return value % 1 == 0 ? value.ToString("F0") : value.ToString("F1");
    }

    /// <summary>
    /// Obtiene el texto de acción según el tipo de ítem.
    /// </summary>
    private string GetActionText()
    {
        if (_currentItemData.IsEquipment)
        {
            return "Clic derecho: Equipar ";
        }
        else if (_currentItemData.IsConsumable)
        {
            return "Clic derecho: Usar ";
        }
        else
        {
            return "Arrastrar: Mover";
        }
    }

    /// <summary>
    /// Valida que todos los componentes estén asignados.
    /// </summary>
    private void ValidateComponents()
    {
        if (tooltipPanel == null) Debug.LogWarning("[InventoryTooltipController] tooltipPanel no asignado");
        if (titlePanel == null) Debug.LogWarning("[InventoryTooltipController] titlePanel no asignado");
        if (contentPanel == null) Debug.LogWarning("[InventoryTooltipController] contentPanel no asignado");
        if (interactionPanel == null) Debug.LogWarning("[InventoryTooltipController] interactionPanel no asignado");
    }

    #endregion

    #region Public API

    /// <summary>
    /// Muestra el tooltip para un ítem específico inmediatamente (sin delay).
    /// </summary>
    public void ShowTooltipInstant(InventoryItem item, ItemData itemData)
    {
        _currentItem = item;
        _currentItemData = itemData;
        _showTimer = 0f;
        ShowTooltipImmediate();
    }

    /// <summary>
    /// Muestra el tooltip para un ítem específico inmediatamente con posición específica.
    /// </summary>
    public void ShowTooltipInstant(InventoryItem item, ItemData itemData, Vector3 mousePosition)
    {
        if (item == null || itemData == null)
        {
            HideTooltip();
            return;
        }

        _currentItem = item;
        _currentItemData = itemData;
        _lastMousePosition = mousePosition;
        _showTimer = 0f;
        ShowTooltipImmediate();
    }

    /// <summary>
    /// Configura el delay de aparición del tooltip.
    /// </summary>
    public void SetShowDelay(float delay)
    {
        showDelay = Mathf.Max(0f, delay);
    }

    /// <summary>
    /// Configura si el tooltip debe seguir el mouse.
    /// </summary>
    public void SetFollowMouse(bool follow)
    {
        followMouse = follow;
    }

    /// <summary>
    /// Configura el tipo de tooltip.
    /// </summary>
    /// <param name="type">Tipo de tooltip</param>
    public void SetTooltipType(TooltipType type)
    {
        tooltipType = type;
    }

    #endregion
}

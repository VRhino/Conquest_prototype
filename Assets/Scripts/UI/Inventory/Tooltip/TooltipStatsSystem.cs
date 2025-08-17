using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Data.Items;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Sistema de estadísticas para tooltips que maneja la visualización de stats y comparaciones.
/// Componente interno responsable de mostrar estadísticas de equipamiento y comparaciones.
/// </summary>
public class TooltipStatsSystem : ITooltipComponent
{
    private InventoryTooltipController _controller;

    // Referencias UI para stats
    private GameObject _statsPanel;
    private Transform _statsContainer;
    private GameObject _statEntryPrefab;

    // Lista de entries activas
    private List<GameObject> _statEntries = new List<GameObject>();

    #region ITooltipComponent Implementation

    public void Initialize(InventoryTooltipController controller)
    {
        _controller = controller;

        // Obtener referencias del controller
        _statsPanel = controller.StatsPanel;
        _statsContainer = controller.StatsContainer;
        _statEntryPrefab = controller.StatEntryPrefab;

        _statEntries = new List<GameObject>();
    }

    public void Cleanup()
    {
        ClearStats();
        _controller = null;
        _statsPanel = null;
        _statsContainer = null;
        _statEntryPrefab = null;
        _statEntries = null;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Configura las estadísticas del tooltip para un ítem.
    /// </summary>
    public void SetupStats(InventoryItem item, ItemData itemData)
    {
        if (item == null || itemData == null) return;

        ClearStats();

        // Solo mostrar stats para equipment
        if (!itemData.IsEquipment || item.GeneratedStats == null || item.GeneratedStats.Count == 0)
        {
            _statsPanel?.SetActive(false);
            return;
        }

        _statsPanel?.SetActive(true);

        // Verificar si hay comparación disponible
        bool hasComparison = ComparisonTooltipUtils.ShouldShowComparison(itemData);

        if (hasComparison && _controller.CurrentTooltipType == TooltipType.Primary)
        {
            SetupComparisonStats(item, itemData);
        }
        else
        {
            SetupRegularStats(item, itemData);
        }
    }

    /// <summary>
    /// Limpia todas las estadísticas mostradas.
    /// </summary>
    public void ClearStats()
    {
        // Destruir todas las entries de stats
        foreach (GameObject entry in _statEntries)
        {
            if (entry != null)
                Object.Destroy(entry);
        }
        _statEntries.Clear();

        if (_statsPanel != null)
            _statsPanel.SetActive(false);
    }

    #endregion

    #region Stats Setup Methods

    /// <summary>
    /// Configura estadísticas regulares sin comparación.
    /// </summary>
    private void SetupRegularStats(InventoryItem item, ItemData itemData)
    {
        foreach (var stat in item.GeneratedStats)
        {
            CreateStatEntry(stat.Key, stat.Value);
        }
    }

    /// <summary>
    /// Configura estadísticas con comparación a ítem equipado.
    /// </summary>
    private void SetupComparisonStats(InventoryItem item, ItemData itemData)
    {
        // Obtener ítem equipado para comparación
        InventoryItem equippedItem = ComparisonTooltipUtils.GetEquippedItemForComparison(itemData.itemType, itemData.itemCategory);

        if (equippedItem == null || equippedItem.GeneratedStats == null)
        {
            // Si no hay ítem equipado, mostrar stats regulares
            SetupRegularStats(item, itemData);
            return;
        }

        // Crear comparaciones de estadísticas
        var comparisons = StatComparisonUtils.CompareItemStats(item, equippedItem);

        foreach (var comparison in comparisons)
        {
            CreateComparisonStatEntry(comparison);
        }
    }

    #endregion

    #region Stat Entry Creation

    /// <summary>
    /// Crea una entrada de estadística regular.
    /// </summary>
    private void CreateStatEntry(string statName, float statValue)
    {
        if (_statEntryPrefab == null || _statsContainer == null) return;

        GameObject entry = Object.Instantiate(_statEntryPrefab, _statsContainer);
        _statEntries.Add(entry);

        // Configurar textos de la entry
        TMP_Text[] texts = entry.GetComponentsInChildren<TMP_Text>();
        if (texts.Length >= 2)
        {
            // Primer texto: nombre de la estadística
            texts[0].text = TooltipFormattingUtils.GetStatDisplayName(statName);
            texts[0].color = Color.white;

            // Segundo texto: valor de la estadística
            texts[1].text = TooltipFormattingUtils.FormatStatValue(statValue);
            texts[1].color = Color.white;
        }
        else if (texts.Length == 1)
        {
            // Un solo texto: combinar nombre y valor
            texts[0].text = $"{TooltipFormattingUtils.GetStatDisplayName(statName)}: {TooltipFormattingUtils.FormatStatValue(statValue)}";
            texts[0].color = Color.white;
        }

        entry.SetActive(true);
    }

    /// <summary>
    /// Crea una entrada de estadística con comparación.
    /// </summary>
    private void CreateComparisonStatEntry(StatComparison comparison)
    {
        if (_statEntryPrefab == null || _statsContainer == null) return;

        GameObject entry = Object.Instantiate(_statEntryPrefab, _statsContainer);
        _statEntries.Add(entry);

        // Configurar textos de la entry
        TMP_Text[] texts = entry.GetComponentsInChildren<TMP_Text>();
        if (texts.Length >= 2)
        {
            // Primer texto: nombre de la estadística
            texts[0].text = TooltipFormattingUtils.GetStatDisplayName(comparison.statName);
            texts[0].color = Color.white;

            // Segundo texto: valor con comparación
            string comparisonText = StatComparisonUtils.FormatComparisonValue(comparison);
            texts[1].text = comparisonText;
            texts[1].color = comparison.displayColor;
        }
        else if (texts.Length == 1)
        {
            // Un solo texto: combinar nombre y comparación
            string comparisonText = StatComparisonUtils.FormatComparisonValue(comparison);
            texts[0].text = $"{TooltipFormattingUtils.GetStatDisplayName(comparison.statName)}: {comparisonText}";
            texts[0].color = comparison.displayColor;
        }

        entry.SetActive(true);
    }

    #endregion
}

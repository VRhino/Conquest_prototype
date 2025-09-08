using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Data.Items;

/// <summary>
/// Utilidades centralizadas para UI del sistema de inventario.
/// Evita duplicación de código y centraliza operaciones UI comunes.
/// Contiene funciones movidas desde InventoryPanelController y InventoryTooltipManager.
/// </summary>
public static class InventoryUIUtils
{
    #region UI Update Methods
    
    /// <summary>
    /// Actualiza completamente la UI del inventario (items + monedas + filtros).
    /// Método principal que debe llamarse cuando cambia el inventario.
    /// </summary>
    /// <param name="controller">Controlador del panel de inventario</param>
    public static void RefreshFullUI(InventoryPanelController controller)
    {
        if (controller == null)
        {
            Debug.LogError("[InventoryUIUtils] RefreshFullUI: controller is null");
            return;
        }

        var currentHero = InventoryManager.GetCurrentHero();
        if (currentHero == null)
        {
            Debug.LogWarning("[InventoryUIUtils] RefreshFullUI: currentHero is null");
            return;
        }

        // Actualizar todas las partes de la UI
        UpdateInventoryGrid(controller, currentHero);
        UpdateMoneyDisplay(controller, currentHero);
        UpdateFilterButtons(controller, controller.CurrentFilter);

        Debug.Log("[InventoryUIUtils] Full UI refresh completed");
    }
    
    /// <summary>
    /// Actualiza solo la visualización de monedas del héroe.
    /// Movido desde InventoryPanelController.UpdateMoneyDisplay().
    /// </summary>
    /// <param name="controller">Controlador del panel de inventario</param>
    /// <param name="hero">Datos del héroe</param>
    public static void UpdateMoneyDisplay(InventoryPanelController controller, HeroData hero)
    {
        if (controller == null || hero == null) return;

        if (controller.bronzeText != null)
            controller.bronzeText.text = hero.bronze.ToString();

        if (controller.silverText != null)
            controller.silverText.text = hero.silver.ToString();

        if (controller.goldText != null)
            controller.goldText.text = hero.gold.ToString();

        Debug.Log($"[InventoryUIUtils] Money display updated: Bronze={hero.bronze}, Silver={hero.silver}, Gold={hero.gold}");
    }
    
    /// <summary>
    /// Actualiza solo el grid de items del inventario.
    /// Basado en InventoryPanelController.UpdateInventoryDisplay().
    /// </summary>
    /// <param name="controller">Controlador del panel de inventario</param>
    /// <param name="hero">Datos del héroe</param>
    public static void UpdateInventoryGrid(InventoryPanelController controller, HeroData hero)
    {
        if (controller == null || hero == null) return;
        if (controller.CellControllers.Count == 0) return;

        int totalCells = controller.GridWidth * controller.GridHeight;
        
        // Limpiar array visual
        System.Array.Clear(controller.SlotItems, 0, controller.SlotItems.Length);

        // Colocar cada item del inventario persistente en su posición visual correspondiente
        foreach (var item in hero.inventory)
        {
            if (item != null && item.slotIndex >= 0 && item.slotIndex < totalCells)
            {
                controller.SlotItems[item.slotIndex] = item;
            }
        }

        // Actualizar cada celda visual basándose en slotItems
        UpdateCellsBatch(controller.CellControllers, controller.SlotItems);

        Debug.Log($"[InventoryUIUtils] Inventory grid updated with {hero.inventory.Count} items");
    }
    
    /// <summary>
    /// Actualiza el estado de los botones de filtro.
    /// Movido desde InventoryPanelController.UpdateFilterButtons().
    /// </summary>
    /// <param name="controller">Controlador del panel de inventario</param>
    /// <param name="currentFilter">Filtro actualmente seleccionado</param>
    public static void UpdateFilterButtons(InventoryPanelController controller, InventoryPanelController.ItemFilter currentFilter)
    {
        if (controller == null) return;

        // Resetear interactividad de todos los botones
        if (controller.allItemsButton != null)
            controller.allItemsButton.interactable = currentFilter != InventoryPanelController.ItemFilter.All;
        
        if (controller.equipmentButton != null)
            controller.equipmentButton.interactable = currentFilter != InventoryPanelController.ItemFilter.Equipment;
        
        if (controller.stackableButton != null)
            controller.stackableButton.interactable = currentFilter != InventoryPanelController.ItemFilter.Stackable;

        Debug.Log($"[InventoryUIUtils] Filter buttons updated for filter: {currentFilter}");
    }
    
    #endregion
    
    #region Cell Management
    /// <summary>
    /// Actualiza múltiples celdas en batch para mejor performance.
    /// Optimización para evitar llamadas individuales.
    /// </summary>
    /// <param name="cells">Lista de controladores de celdas</param>
    /// <param name="slotItems">Array de items por slot</param>
    public static void UpdateCellsBatch(List<InventoryItemCellController> cells, InventoryItem[] slotItems)
    {
        if (cells == null || slotItems == null) return;

        int totalCells = Mathf.Min(cells.Count, slotItems.Length);
        
        for (int i = 0; i < totalCells; i++)
        {
            var cellController = cells[i];
            if (cellController == null) continue;

            cellController.SetCellIndex(i);
            
            var item = slotItems[i];
            if (item != null)
            {
                var itemData = InventoryUtils.GetItemData(item.itemId);
                cellController.SetItem(item, itemData);
            }
            else
            {
                cellController.Clear();
            }
        }

        Debug.Log($"[InventoryUIUtils] Batch updated {totalCells} cells");
    }
    
    #endregion
    
    #region Tooltip Integration
    
    /// <summary>
    /// Conecta el sistema de tooltips con las celdas del inventario.
    /// Movido desde InventoryTooltipManager.ConnectToInventoryCells().
    /// </summary>
    /// <param name="tooltipManager">Manager de tooltips</param>
    /// <param name="inventoryPanel">Panel de inventario</param>
    public static void ConnectTooltipsToInventory(TooltipManager tooltipManager, InventoryPanelController inventoryPanel)
    {
        if (tooltipManager == null || inventoryPanel == null) return;

        // Buscar todas las celdas de inventario
        InventoryItemCellController[] cells = Object.FindObjectsOfType<InventoryItemCellController>();
        
        foreach (var cell in cells)
        {
            ConnectCellToTooltip(tooltipManager, cell);
        }

        Debug.Log($"[InventoryUIUtils] Connected {cells.Length} cells to tooltip system");
    }
    /// <summary>
    /// Conecta una celda específica al sistema de tooltips.
    /// Helper method movido desde InventoryTooltipManager.
    /// </summary>
    /// <param name="tooltipManager">Manager de tooltips</param>
    /// <param name="cell">Celda a conectar</param>
    private static void ConnectCellToTooltip(TooltipManager tooltipManager, InventoryItemCellController cell)
    {
        if (tooltipManager == null || cell == null) return;

        // Delegar la conexión real al tooltip manager
        // (mantenemos la lógica específica en el manager)
        tooltipManager.ConnectCellToTooltip(cell);
    }
    
    #endregion
}

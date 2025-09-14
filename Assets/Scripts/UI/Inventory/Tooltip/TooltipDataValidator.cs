using UnityEngine;
using Data.Items;
using System.Linq;

/// <summary>
/// Validador de datos para tooltips que maneja la sincronización y validación de información.
/// Componente interno responsable de verificar consistencia de datos y actualizaciones.
/// </summary>
public class TooltipDataValidator : ITooltipComponent
{
    private InventoryTooltipController _controller;

    #region ITooltipComponent Implementation

    public void Initialize(InventoryTooltipController controller)
    {
        _controller = controller;
    }

    public void Cleanup()
    {
        _controller = null;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Valida si el tooltip actual sigue siendo válido y lo actualiza o oculta según corresponda.
    /// </summary>
    public void ValidateAndRefreshTooltip()
    {
        if (!_controller.LifecycleManager.IsShowing)
            return;

        if (_controller.LifecycleManager.IsShowing && _controller.LifecycleManager.CurrentItem == null)
        {
            _controller.LifecycleManager.HideTooltip();
            return;
        }

        var currentHero = PlayerSessionService.SelectedHero;
        if (currentHero?.inventory == null)
        {
            _controller.LifecycleManager.HideTooltip();
            return;
        }

        var currentItem = _controller.LifecycleManager.CurrentItem;
        var currentItemData = _controller.LifecycleManager.CurrentItemData;

        // Buscar item actualizado
        var updatedItem = InventoryUtils.GetUpdatedTooltipItem(currentItem, currentHero.inventory);
        if (updatedItem != null && InventoryUtils.HasTooltipRelevantChanges(currentItem, updatedItem))
        {
            // Refrescar tooltip con datos actualizados
            var itemData = InventoryUtils.GetItemData(updatedItem.itemId);
            if (itemData != null)
            {
                // Actualizar contenido usando el renderer
                _controller.ContentRenderer?.PopulateContent(updatedItem, itemData);
            }
        }
    }

    /// <summary>
    /// Valida y oculta el tooltip si el ítem especificado ya no es válido.
    /// </summary>
    public void ValidateTooltipForRemovedItem(string removedItemId)
    {
        if (!_controller.LifecycleManager.IsShowing || _controller.LifecycleManager.CurrentItem == null) 
            return;

        var currentItem = _controller.LifecycleManager.CurrentItem;

        // Si el tooltip está mostrando el ítem removido, ocultarlo
        if (currentItem.itemId == removedItemId)
        {
            // Para equipment, verificar también por instanceId para mayor precisión
            if (!string.IsNullOrEmpty(currentItem.instanceId))
            {
                var currentHero = PlayerSessionService.SelectedHero;
                if (currentHero?.inventory != null)
                {
                    // Si el item con este instanceId ya no existe, ocultar tooltip
                    bool stillExists = currentHero.inventory.Any(item =>
                        item.instanceId == currentItem.instanceId);

                    if (!stillExists)
                    {
                        _controller.LifecycleManager.HideTooltip();
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
    /// Verifica si actualmente se está mostrando un ítem específico.
    /// </summary>
    public bool IsShowingItem(string itemId)
    {
        return _controller.LifecycleManager.IsShowingItem(itemId);
    }

    /// <summary>
    /// Verifica si actualmente se está mostrando un ítem específico por instanceId.
    /// </summary>
    public bool IsShowingItemInstance(string instanceId)
    {
        return _controller.LifecycleManager.IsShowingItemInstance(instanceId);
    }

    #endregion
}

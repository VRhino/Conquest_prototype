using System.Linq;
using UnityEngine;
using Data.Items;

/// <summary>
/// Sistema encargado de ejecutar los efectos de ítems consumibles.
/// Maneja la validación, ejecución y notificación de efectos.
/// </summary>
public static class ItemEffectSystem
{
    /// <summary>
    /// Usa un ítem consumible aplicando todos sus efectos.
    /// </summary>
    /// <param name="item">Ítem consumible a usar</param>
    /// <param name="hero">Héroe sobre el que aplicar los efectos</param>
    /// <returns>True si se usó exitosamente</returns>
    public static bool UseConsumableItem(InventoryItem item, HeroData hero)
    {
        if (item == null)
        {
            LogError("Cannot use null item");
            return false;
        }

        if (hero == null)
        {
            LogError("Cannot use item on null hero");
            return false;
        }

        var protoItem = InventoryUtils.GetItemData(item.itemId);
        if (protoItem == null)
        {
            LogError($"ProtoItem not found: {item.itemId}");
            return false;
        }

        if (!protoItem.IsConsumable)
        {
            LogError($"Item {item.itemId} is not consumable");
            return false;
        }

        // Verificar si todos los efectos se pueden ejecutar
        foreach (var effect in protoItem.effects)
        {
            if (effect == null)
            {
                LogWarning($"Null effect found in item: {item.itemId}");
                continue;
            }

            if (!effect.CanExecute(hero))
            {
                LogInfo($"Cannot execute effect: {effect.DisplayName} on hero: {hero.heroName}");
                return false;
            }
        }

        // Ordenar efectos por prioridad (mayor prioridad primero)
        var sortedEffects = protoItem.effects
            .Where(e => e != null)
            .OrderByDescending(e => e.GetExecutionPriority())
            .ToArray();

        // Ejecutar todos los efectos
        bool allSucceeded = true;
        foreach (var effect in sortedEffects)
        {
            try
            {
                if (!effect.Execute(hero, 1))
                {
                    allSucceeded = false;
                    LogError($"Failed to execute effect: {effect.DisplayName}");
                    break; // Detener ejecución si un efecto falla
                }
                else
                {
                    LogInfo($"Successfully executed effect: {effect.DisplayName} on {hero.heroName}");
                }
            }
            catch (System.Exception ex)
            {
                allSucceeded = false;
                LogError($"Exception executing effect {effect.DisplayName}: {ex.Message}");
                break;
            }
        }

        // Consumir el ítem si se ejecutaron todos los efectos exitosamente
        if (allSucceeded && protoItem.consumeOnUse)
        {
            bool removed = InventoryService.RemoveItem(item.itemId, 1);
            if (!removed)
            {
                LogWarning($"Could not remove consumed item: {item.itemId}");
            }
        }

        // Emitir eventos de efectos ejecutados
        if (allSucceeded)
        {
            foreach (var effect in sortedEffects)
            {
                InventoryEvents.OnItemEffectExecuted?.Invoke(effect.EffectId, hero);
            }
            
            InventoryEvents.OnItemUsed?.Invoke(item.itemId, hero);
        }

        return allSucceeded;
    }

    /// <summary>
    /// Obtiene una descripción completa de todos los efectos de un ítem.
    /// </summary>
    /// <param name="itemId">ID del ítem</param>
    /// <param name="quantity">Cantidad a aplicar</param>
    /// <returns>Descripción textual de los efectos</returns>
    public static string GetItemEffectsPreview(string itemId, int quantity = 1)
    {
        var protoItem = InventoryUtils.GetItemData(itemId);
        if (protoItem?.effects == null || protoItem.effects.Length == 0)
            return "No effects";

        var previews = protoItem.effects
            .Where(effect => effect != null)
            .Select(effect => effect.GetPreviewText(quantity));

        return string.Join("\n", previews);
    }

    /// <summary>
    /// Verifica si un ítem se puede usar en el héroe especificado.
    /// </summary>
    /// <param name="itemId">ID del ítem</param>
    /// <param name="hero">Héroe objetivo</param>
    /// <returns>True si se puede usar</returns>
    public static bool CanUseItem(string itemId, HeroData hero)
    {
        var protoItem = InventoryUtils.GetItemData(itemId);
        if (protoItem?.effects == null)
            return false;

        // Verificar que todos los efectos se pueden ejecutar
        return protoItem.effects
            .Where(effect => effect != null)
            .All(effect => effect.CanExecute(hero));
    }

    /// <summary>
    /// Obtiene información detallada de los efectos de un ítem para debugging.
    /// </summary>
    /// <param name="itemId">ID del ítem</param>
    /// <returns>Información detallada</returns>
    public static string GetEffectsDebugInfo(string itemId)
    {
        var protoItem = InventoryUtils.GetItemData(itemId);
        if (protoItem?.effects == null)
            return "No effects defined";

        var info = $"Effects for {itemId}:\n";
        for (int i = 0; i < protoItem.effects.Length; i++)
        {
            var effect = protoItem.effects[i];
            if (effect == null)
            {
                info += $"  [{i}] NULL EFFECT\n";
            }
            else
            {
                info += $"  [{i}] {effect.DisplayName} (Priority: {effect.GetExecutionPriority()})\n";
                info += $"      ID: {effect.EffectId}\n";
                info += $"      Description: {effect.Description}\n";
            }
        }

        return info;
    }

    #region Logging
    private static void LogInfo(string message)
    {
        Debug.Log($"[ItemEffectSystem] {message}");
    }

    private static void LogWarning(string message)
    {
        Debug.LogWarning($"[ItemEffectSystem] {message}");
    }

    private static void LogError(string message)
    {
        Debug.LogError($"[ItemEffectSystem] {message}");
    }
    #endregion
}

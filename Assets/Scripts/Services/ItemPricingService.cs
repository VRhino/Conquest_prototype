using UnityEngine;
using Data.Items;

/// <summary>
/// Servicio estático encargado de calcular precios de ítems usando configuraciones ScriptableObject.
/// Centraliza toda la lógica de pricing y proporciona métodos de conveniencia.
/// </summary>
public static class ItemPricingService
{
    /// <summary>
    /// Precio por defecto usado cuando no se puede calcular un precio específico.
    /// </summary>
    private const int DEFAULT_FALLBACK_PRICE = 25;

    /// <summary>
    /// Multiplicador por defecto para precios de venta (50% del precio de compra).
    /// </summary>
    private const float DEFAULT_SELL_RATIO = 0.5f;

    /// <summary>
    /// Calcula el precio de compra de un ítem basado en su configuración.
    /// </summary>
    /// <param name="itemData">Datos del prototipo del ítem</param>
    /// <param name="inventoryItem">Instancia específica del ítem (opcional)</param>
    /// <returns>Precio de compra calculado</returns>
    public static int CalculateItemPrice(ItemDataSO itemData, InventoryItem inventoryItem = null)
    {
        if (itemData == null)
        {
            LogError("Cannot calculate price for null ItemData");
            return DEFAULT_FALLBACK_PRICE;
        }

        // Si el ítem ya tiene precio calculado y almacenado, usarlo
        if (inventoryItem != null && inventoryItem.price > 0)
        {
            LogInfo($"Using cached price for '{itemData.name}': {inventoryItem.price}");
            return inventoryItem.price;
        }

        // Si el ítem tiene configuración de pricing específica, usarla
        if (itemData.pricingConfig != null)
        {
            try
            {
                // Cast a PricingConfigurationSO
                var pricingConfig = itemData.pricingConfig as PricingConfigurationSO;
                if (pricingConfig != null)
                {
                    int calculatedPrice = pricingConfig.CalculatePrice(itemData, inventoryItem);
                    LogInfo($"Calculated price for '{itemData.name}' using config '{pricingConfig.DisplayName}': {calculatedPrice}");
                    return calculatedPrice;
                }
                else
                {
                    LogWarning($"PricingConfig for '{itemData.name}' is not a valid PricingConfigurationSO");
                    return GetDefaultPriceForItem(itemData);
                }
            }
            catch (System.Exception ex)
            {
                LogError($"Error calculating price for '{itemData.name}' using config: {ex.Message}");
                return GetDefaultPriceForItem(itemData);
            }
        }

        // Si no tiene configuración específica, usar precio por defecto basado en rareza
        int defaultPrice = GetDefaultPriceForItem(itemData);
        LogInfo($"Using default price for '{itemData.name}': {defaultPrice}");
        return defaultPrice;
    }

    /// <summary>
    /// Calcula el precio de venta de un ítem (generalmente menor que el precio de compra).
    /// </summary>
    /// <param name="itemData">Datos del ítem</param>
    /// <param name="inventoryItem">Instancia del ítem</param>
    /// <param name="sellRatio">Ratio de venta (por defecto 0.5 = 50%)</param>
    /// <returns>Precio de venta calculado</returns>
    public static int CalculateItemSellPrice(ItemDataSO itemData, InventoryItem inventoryItem = null, float sellRatio = DEFAULT_SELL_RATIO)
    {
        if (sellRatio < 0f || sellRatio > 1f)
        {
            LogWarning($"Invalid sell ratio: {sellRatio}. Using default: {DEFAULT_SELL_RATIO}");
            sellRatio = DEFAULT_SELL_RATIO;
        }

        int buyPrice = CalculateItemPrice(itemData, inventoryItem);
        int sellPrice = Mathf.RoundToInt(buyPrice * sellRatio);
        
        // Asegurar precio mínimo de venta
        sellPrice = Mathf.Max(1, sellPrice);

        LogInfo($"Sell price for '{itemData.name}': {sellPrice} (ratio: {sellRatio:P0})");
        return sellPrice;
    }

    /// <summary>
    /// Obtiene un precio por defecto basado en la rareza y tipo del ítem.
    /// Usado como fallback cuando no hay configuración específica.
    /// </summary>
    /// <param name="itemData">Datos del ítem</param>
    /// <returns>Precio por defecto basado en rareza</returns>
    public static int GetDefaultPriceForItem(ItemDataSO itemData)
    {
        if (itemData == null)
            return DEFAULT_FALLBACK_PRICE;

        // Precio base según rareza
        int basePrice = itemData.rarity switch
        {
            ItemRarity.Common => 20,
            ItemRarity.Uncommon => 50,
            ItemRarity.Rare => 100,
            ItemRarity.Epic => 250,
            ItemRarity.Legendary => 500,
            _ => DEFAULT_FALLBACK_PRICE
        };

        // Modificador según tipo de ítem
        float typeMultiplier = itemData.itemType switch
        {
            ItemType.Weapon => 1.5f,
            ItemType.Armor => 1.2f,
            ItemType.Consumable => 0.8f,
            _ => 1.0f
        };

        int finalPrice = Mathf.RoundToInt(basePrice * typeMultiplier);
        return Mathf.Max(1, finalPrice); // Precio mínimo de 1
    }

    /// <summary>
    /// Verifica si un ítem tiene una configuración de pricing personalizada.
    /// </summary>
    /// <param name="itemData">Datos del ítem</param>
    /// <returns>True si tiene configuración personalizada</returns>
    public static bool HasCustomPricing(ItemDataSO itemData)
    {
        return itemData?.pricingConfig != null && itemData.pricingConfig is PricingConfigurationSO;
    }

    /// <summary>
    /// Obtiene información de debugging sobre el pricing de un ítem.
    /// </summary>
    /// <param name="itemData">Datos del ítem</param>
    /// <param name="inventoryItem">Instancia del ítem (opcional)</param>
    /// <returns>String con información detallada de pricing</returns>
    public static string GetPricingDebugInfo(ItemDataSO itemData, InventoryItem inventoryItem = null)
    {
        if (itemData == null)
            return "ItemData is null";

        var info = $"Pricing Debug for '{itemData.name}' (ID: {itemData.id})\n";
        info += $"- Rarity: {itemData.rarity}\n";
        info += $"- Type: {itemData.itemType}\n";
        info += $"- Default Price: {GetDefaultPriceForItem(itemData)}\n";
        
        if (inventoryItem?.price > 0)
        {
            info += $"- Cached Price: {inventoryItem.price}\n";
        }

        if (HasCustomPricing(itemData))
        {
            var pricingConfig = itemData.pricingConfig as PricingConfigurationSO;
            info += $"- Custom Config: {pricingConfig.DisplayName}\n";
            info += $"- Calculated Price: {pricingConfig.CalculatePrice(itemData, inventoryItem)}\n";
        }
        else
        {
            info += "- No custom pricing config\n";
        }

        info += $"- Final Buy Price: {CalculateItemPrice(itemData, inventoryItem)}\n";
        info += $"- Final Sell Price: {CalculateItemSellPrice(itemData, inventoryItem)}";

        return info;
    }

    #region Logging
    private static void LogInfo(string message)
    {
        Debug.Log($"[ItemPricingService] {message}");
    }

    private static void LogWarning(string message)
    {
        Debug.LogWarning($"[ItemPricingService] {message}");
    }

    private static void LogError(string message)
    {
        Debug.LogError($"[ItemPricingService] {message}");
    }
    #endregion
}

using System;
using System.Collections.Generic;
using UnityEngine;
using Data.Items;

/// <summary>
/// Configuración de precios simples basada en una lista de precios fijos por itemId.
/// Ideal para ítems con precios estáticos que no dependen de stats o rareza.
/// </summary>
[CreateAssetMenu(menuName = "Items/Pricing/Simple Pricing Config", fileName = "SimplePricingConfig")]
public class SimplePricingConfig : PricingConfigurationSO
    {
        [Header("Price Configuration")]
        [SerializeField] private List<ItemPriceEntry> itemPrices = new List<ItemPriceEntry>();
        [SerializeField] private int defaultPrice = 50;

        /// <summary>
        /// Entrada individual de precio para un ítem específico.
        /// </summary>
        [Serializable]
        public struct ItemPriceEntry
        {
            [Tooltip("ID del ítem al que aplicar este precio")]
            public string itemId;
            [Tooltip("Precio base del ítem en monedas")]
            public int basePrice;

            public ItemPriceEntry(string itemId, int basePrice)
            {
                this.itemId = itemId;
                this.basePrice = basePrice;
            }
        }

        /// <summary>
        /// Calcula el precio buscando en la lista de precios configurados.
        /// </summary>
        /// <param name="itemData">Datos del ítem</param>
        /// <param name="inventoryItem">Instancia del ítem (no usado en precios simples)</param>
        /// <returns>Precio configurado o precio por defecto si no se encuentra</returns>
        public override int CalculatePrice(ItemDataSO itemData, InventoryItem inventoryItem = null)
        {
            if (itemData == null)
            {
                LogWarning("ItemData is null, using default price");
                return defaultPrice;
            }

            // Buscar precio específico para este ítem
            foreach (var priceEntry in itemPrices)
            {
                if (priceEntry.itemId == itemData.id)
                {
                    return priceEntry.basePrice;
                }
            }

            // Si no se encontró, usar precio por defecto
            LogInfo($"No specific price found for item '{itemData.id}', using default price: {defaultPrice}");
            return defaultPrice;
        }

        /// <summary>
        /// Obtiene vista previa de los precios configurados.
        /// </summary>
        /// <returns>String con información de precios</returns>
        public override string GetPricePreview()
        {
            var preview = $"{base.GetPricePreview()}\n";
            preview += $"Configured prices: {itemPrices.Count}\n";
            preview += $"Default price: {defaultPrice}";
            
            if (itemPrices.Count > 0)
            {
                preview += "\nSample prices:\n";
                int maxSamples = Mathf.Min(3, itemPrices.Count);
                for (int i = 0; i < maxSamples; i++)
                {
                    var entry = itemPrices[i];
                    preview += $"- {entry.itemId}: {entry.basePrice}\n";
                }
                if (itemPrices.Count > 3)
                {
                    preview += $"... and {itemPrices.Count - 3} more";
                }
            }
            
            return preview;
        }

        /// <summary>
        /// Valida que la configuración sea correcta.
        /// </summary>
        /// <returns>True si es válida</returns>
        public override bool IsValid()
        {
            if (!base.IsValid())
                return false;

            if (defaultPrice < 0)
            {
                LogError("Default price cannot be negative");
                return false;
            }

            // Verificar que no hay IDs duplicados
            var seenIds = new HashSet<string>();
            foreach (var entry in itemPrices)
            {
                if (string.IsNullOrEmpty(entry.itemId))
                {
                    LogError("Found empty itemId in price entries");
                    return false;
                }

                if (entry.basePrice < 0)
                {
                    LogError($"Negative price for item '{entry.itemId}': {entry.basePrice}");
                    return false;
                }

                if (seenIds.Contains(entry.itemId))
                {
                    LogError($"Duplicate itemId found: '{entry.itemId}'");
                    return false;
                }

                seenIds.Add(entry.itemId);
            }

            return true;
        }

        /// <summary>
        /// Agrega un nuevo precio para un ítem específico.
        /// Útil para configuración programática.
        /// </summary>
        /// <param name="itemId">ID del ítem</param>
        /// <param name="price">Precio a asignar</param>
        public void AddItemPrice(string itemId, int price)
        {
            // Verificar si ya existe
            for (int i = 0; i < itemPrices.Count; i++)
            {
                if (itemPrices[i].itemId == itemId)
                {
                    // Actualizar precio existente
                    var entry = itemPrices[i];
                    entry.basePrice = price;
                    itemPrices[i] = entry;
                    LogInfo($"Updated price for '{itemId}': {price}");
                    return;
                }
            }

            // Agregar nuevo precio
            itemPrices.Add(new ItemPriceEntry(itemId, price));
            LogInfo($"Added new price for '{itemId}': {price}");
        }

        /// <summary>
        /// Obtiene el precio configurado para un ítem específico.
        /// </summary>
        /// <param name="itemId">ID del ítem</param>
        /// <returns>Precio configurado o -1 si no existe</returns>
        public int GetItemPrice(string itemId)
        {
            foreach (var entry in itemPrices)
            {
                if (entry.itemId == itemId)
                    return entry.basePrice;
            }
            return -1; // No encontrado
        }

        #region Logging
        private void LogInfo(string message)
        {
            Debug.Log($"[SimplePricingConfig] {message}");
        }

        private void LogWarning(string message)
        {
            Debug.LogWarning($"[SimplePricingConfig] {message}");
        }

        private void LogError(string message)
        {
            Debug.LogError($"[SimplePricingConfig] {message}");
        }
        #endregion
    }

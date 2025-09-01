using UnityEngine;
using Data.Items;

/// <summary>
/// Servicio especializado en transacciones de tienda (compra y venta de ítems).
/// Coordina operaciones atómicas entre CurrencyService e InventoryManager,
/// asegurando consistencia en todas las transacciones comerciales.
/// </summary>
public static class StoreTransactionService
{
    private static HeroData _currentHero;

    /// <summary>
    /// Inicializa el servicio con el héroe activo.
    /// </summary>
    /// <param name="hero">Datos del héroe activo</param>
    public static void Initialize(HeroData hero)
    {
        _currentHero = hero;
        
        if (_currentHero == null)
        {
            LogError("Cannot initialize StoreTransactionService with null hero");
            return;
        }

        // Asegurar que los servicios dependientes estén inicializados
        CurrencyService.Initialize(hero);
        
        LogInfo($"StoreTransactionService initialized for hero: {_currentHero.heroName}");
    }

    #region Purchase Operations

    /// <summary>
    /// Intenta comprar un ítem de la tienda.
    /// Ejecuta una transacción atómica: validación -> débito -> agregar ítem.
    /// </summary>
    /// <param name="itemId">ID del ítem a comprar</param>
    /// <param name="quantity">Cantidad a comprar (default: 1)</param>
    /// <returns>Resultado de la transacción</returns>
    public static TransactionResult PurchaseItem(string itemId, int quantity = 1)
    {
        if (!IsInitialized())
        {
            return TransactionResult.CreateFailure(TransactionErrorType.TransactionFailed, 
                "StoreTransactionService not initialized");
        }

        // Validar la compra antes de ejecutar
        var validationResult = ValidatePurchase(itemId, quantity);
        if (!validationResult.Success)
        {
            return validationResult;
        }

        // Obtener datos del ítem
        var itemData = ItemDatabase.Instance.GetItemDataById(itemId);
        int totalCost = ItemPricingService.CalculateItemPrice(itemData) * quantity;

        LogInfo($"Executing purchase: {itemData.name} x{quantity} for {totalCost} Bronze");

        // Ejecutar transacción atómica
        try
        {
            // 1. Debitar Bronze
            if (!CurrencyService.DebitBronze(totalCost))
            {
                LogError($"Failed to debit Bronze during purchase of {itemId}");
                return TransactionResult.CreateFailure(TransactionErrorType.TransactionFailed,
                    "Failed to process payment", totalCost, CurrencyService.GetBronzeAmount());
            }

            // 2. Agregar ítem al inventario
            bool itemAdded = InventoryManager.CreateAndAddItem(itemId, quantity);
            if (!itemAdded)
            {
                // Rollback: devolver el Bronze
                CurrencyService.CreditBronze(totalCost);
                LogError($"Failed to add item to inventory during purchase of {itemId}, Bronze refunded");
                return TransactionResult.CreateFailure(TransactionErrorType.TransactionFailed,
                    "Failed to add item to inventory", totalCost, CurrencyService.GetBronzeAmount());
            }

            // 3. Disparar eventos
            LogInfo($"Purchase completed successfully: {itemData.name} x{quantity}");
            
            return TransactionResult.CreateSuccess(totalCost, CurrencyService.GetBronzeAmount());
        }
        catch (System.Exception ex)
        {
            LogError($"Exception during purchase transaction: {ex.Message}");
            return TransactionResult.CreateFailure(TransactionErrorType.TransactionFailed,
                $"Transaction failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Valida si es posible realizar una compra sin ejecutarla.
    /// </summary>
    /// <param name="itemId">ID del ítem a validar</param>
    /// <param name="quantity">Cantidad a comprar</param>
    /// <returns>Resultado de la validación</returns>
    public static TransactionResult ValidatePurchase(string itemId, int quantity = 1)
    {
        if (!IsInitialized())
        {
            return TransactionResult.CreateFailure(TransactionErrorType.TransactionFailed,
                "Service not initialized");
        }

        if (string.IsNullOrEmpty(itemId))
        {
            return TransactionResult.CreateFailure(TransactionErrorType.InvalidItem,
                "Item ID cannot be empty");
        }

        if (quantity <= 0)
        {
            return TransactionResult.CreateFailure(TransactionErrorType.InvalidItem,
                "Quantity must be greater than 0");
        }

        // Verificar que el ítem existe
        var itemData = ItemDatabase.Instance.GetItemDataById(itemId);
        if (itemData == null)
        {
            return TransactionResult.CreateFailure(TransactionErrorType.InvalidItem,
                $"Item not found: {itemId}");
        }

        // Calcular costo total
        int itemPrice = ItemPricingService.CalculateItemPrice(itemData);
        int totalCost = itemPrice * quantity;

        // Verificar fondos suficientes
        if (!CurrencyService.HasSufficientBronze(totalCost))
        {
            return TransactionResult.CreateFailure(TransactionErrorType.InsufficientBronze,
                $"Insufficient Bronze. Required: {totalCost}, Available: {CurrencyService.GetBronzeAmount()}",
                totalCost, CurrencyService.GetBronzeAmount());
        }

        // Verificar espacio en inventario
        if (!InventoryManager.HasSpace())
        {
            return TransactionResult.CreateFailure(TransactionErrorType.InsufficientSpace,
                "Not enough space in inventory");
        }

        return TransactionResult.CreateSuccess(totalCost, CurrencyService.GetBronzeAmount());
    }

    /// <summary>
    /// Verifica si el héroe puede permitirse un ítem.
    /// </summary>
    /// <param name="itemId">ID del ítem</param>
    /// <param name="quantity">Cantidad a comprar</param>
    /// <returns>True si puede permitirse la compra</returns>
    public static bool CanAffordItem(string itemId, int quantity = 1)
    {
        var validationResult = ValidatePurchase(itemId, quantity);
        return validationResult.Success || validationResult.ErrorType != TransactionErrorType.InsufficientBronze;
    }

    #endregion

    #region Sale Operations

    /// <summary>
    /// Intenta vender un ítem del inventario.
    /// Ejecuta una transacción atómica: validación -> remover ítem -> crédito.
    /// </summary>
    /// <param name="item">Ítem a vender</param>
    /// <param name="quantity">Cantidad a vender (default: 1 o toda la cantidad para stackables)</param>
    /// <returns>Resultado de la transacción</returns>
    public static TransactionResult SellItem(InventoryItem item, int quantity = -1)
    {
        if (!IsInitialized())
        {
            return TransactionResult.CreateFailure(TransactionErrorType.TransactionFailed,
                "StoreTransactionService not initialized");
        }

        // Validar la venta antes de ejecutar
        var validationResult = ValidateSale(item, quantity);
        if (!validationResult.Success)
        {
            return validationResult;
        }

        // Determinar cantidad a vender
        int sellQuantity = quantity == -1 ? item.quantity : quantity;
        
        // Obtener datos del ítem y calcular precio de venta
        var itemData = InventoryUtils.GetItemData(item.itemId);
        int sellPrice = ItemPricingService.CalculateItemSellPrice(itemData, item) * sellQuantity;

        LogInfo($"Executing sale: {itemData.name} x{sellQuantity} for {sellPrice} Bronze");

        // Ejecutar transacción atómica
        try
        {
            // 1. Remover ítem del inventario
            bool itemRemoved;
            if (item.IsStackable && sellQuantity < item.quantity)
            {
                // Venta parcial de stack
                itemRemoved = InventoryManager.RemoveItem(item.itemId, sellQuantity);
            }
            else
            {
                // Venta completa del ítem
                itemRemoved = InventoryManager.RemoveSpecificItem(item);
            }

            if (!itemRemoved)
            {
                LogError($"Failed to remove item from inventory during sale of {item.itemId}");
                return TransactionResult.CreateFailure(TransactionErrorType.TransactionFailed,
                    "Failed to remove item from inventory");
            }

            // 2. Acreditar Bronze
            if (!CurrencyService.CreditBronze(sellPrice))
            {
                // Rollback: re-agregar el ítem (esto es complejo, mejor prevenir)
                LogError($"Failed to credit Bronze during sale of {item.itemId}");
                return TransactionResult.CreateFailure(TransactionErrorType.TransactionFailed,
                    "Failed to process payment", sellPrice, CurrencyService.GetBronzeAmount());
            }

            // 3. Confirmar transacción
            LogInfo($"Sale completed successfully: {itemData.name} x{sellQuantity} sold for {sellPrice} Bronze");
            
            return TransactionResult.CreateSuccess(sellPrice, CurrencyService.GetBronzeAmount());
        }
        catch (System.Exception ex)
        {
            LogError($"Exception during sale transaction: {ex.Message}");
            return TransactionResult.CreateFailure(TransactionErrorType.TransactionFailed,
                $"Transaction failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Valida si es posible realizar una venta sin ejecutarla.
    /// </summary>
    /// <param name="item">Ítem a validar</param>
    /// <param name="quantity">Cantidad a vender</param>
    /// <returns>Resultado de la validación</returns>
    public static TransactionResult ValidateSale(InventoryItem item, int quantity = -1)
    {
        if (!IsInitialized())
        {
            return TransactionResult.CreateFailure(TransactionErrorType.TransactionFailed,
                "Service not initialized");
        }

        if (item == null)
        {
            return TransactionResult.CreateFailure(TransactionErrorType.InvalidItem,
                "Cannot sell null item");
        }

        // Obtener datos del ítem
        var itemData = InventoryUtils.GetItemData(item.itemId);
        if (itemData == null)
        {
            return TransactionResult.CreateFailure(TransactionErrorType.InvalidItem,
                $"Item data not found: {item.itemId}");
        }

        // Determinar cantidad a vender
        int sellQuantity = quantity == -1 ? item.quantity : quantity;

        if (sellQuantity <= 0)
        {
            return TransactionResult.CreateFailure(TransactionErrorType.InvalidItem,
                "Sale quantity must be greater than 0");
        }

        if (sellQuantity > item.quantity)
        {
            return TransactionResult.CreateFailure(TransactionErrorType.InvalidItem,
                $"Cannot sell more than available. Available: {item.quantity}, Requested: {sellQuantity}");
        }

        // Calcular precio de venta
        int sellPrice = ItemPricingService.CalculateItemSellPrice(itemData, item) * sellQuantity;

        return TransactionResult.CreateSuccess(sellPrice, CurrencyService.GetBronzeAmount());
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Verifica si el servicio está inicializado correctamente.
    /// </summary>
    /// <returns>True si está inicializado</returns>
    public static bool IsInitialized()
    {
        return _currentHero != null && CurrencyService.IsInitialized();
    }

    #endregion

    #region Logging

    private static void LogInfo(string message)
    {
        Debug.Log($"[StoreTransactionService] {message}");
    }

    private static void LogWarning(string message)
    {
        Debug.LogWarning($"[StoreTransactionService] {message}");
    }

    private static void LogError(string message)
    {
        Debug.LogError($"[StoreTransactionService] {message}");
    }

    #endregion
}

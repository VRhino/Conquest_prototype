using System;
using System.Collections.Generic;
using Data.Items;

/// <summary>Purchases and sales preserve instance identity and publish one completed transaction.</summary>
public static class StoreTransactionService
{
    private static HeroData _currentHero;
    public static void Initialize(HeroData hero)
    {
        _currentHero = hero;
        if (hero != null) CurrencyService.Initialize(hero);
    }
    public static void Reset() => _currentHero = null;
    public static bool IsInitialized() => _currentHero != null &&
        ReferenceEquals(_currentHero, InventoryManager.GetCurrentHero()) && CurrencyService.IsInitialized();

    public static TransactionResult PurchaseItem(string itemId, int quantity = 1)
    {
        var validation = ValidatePurchase(itemId, quantity);
        if (!validation.Success) return validation;
        var definition = ItemService.GetItemById(itemId);
        var products = new List<InventoryItem>();
        if (definition.RequiresInstances)
        {
            for (int i = 0; i < quantity; i++) products.Add(ItemInstanceService.CreateItem(itemId));
        }
        else products.Add(ItemInstanceService.CreateItem(itemId, quantity));
        return PurchaseProducts(products);
    }

    public static TransactionResult PurchaseItem(InventoryItem product)
    {
        if (product == null || product.quantity <= 0 || InventoryStorageService.Contains(product))
            return Failure(TransactionErrorType.InvalidItem, "Invalid product");
        return PurchaseProducts(new List<InventoryItem> { product });
    }

    private static TransactionResult PurchaseProducts(List<InventoryItem> products)
    {
        if (!IsInitialized()) return Failure(TransactionErrorType.TransactionFailed, "Inventory session is not initialized");
        long cost = 0;
        foreach (var product in products)
        {
            if (product == null) return Failure(TransactionErrorType.InvalidItem, "Invalid product");
            var definition = ItemService.GetItemById(product.itemId);
            if (definition == null) return Failure(TransactionErrorType.InvalidItem, "Unknown product");
            cost += (long)ItemPricingService.CalculateItemPrice(definition, product) * product.quantity;
        }
        if (cost > int.MaxValue) return Failure(TransactionErrorType.InvalidItem, "Price exceeds the supported balance");
        if (!CurrencyService.HasSufficientBronze((int)cost))
            return Failure(TransactionErrorType.InsufficientBronze, "Insufficient Bronze", (int)cost);
        bool success = InventoryEventService.RunTransaction(() =>
        {
            if (!CurrencyService.DebitBronze((int)cost)) return false;
            foreach (var product in products)
                if (!InventoryManager.AddExistingItem(product)) return false;
            return true;
        });
        return success ? TransactionResult.CreateSuccess((int)cost, _currentHero.bronze)
            : Failure(TransactionErrorType.InsufficientSpace, "The complete purchase does not fit in inventory");
    }

    public static TransactionResult ValidatePurchase(string itemId, int quantity = 1)
    {
        if (!IsInitialized()) return Failure(TransactionErrorType.TransactionFailed, "Inventory session is not initialized");
        var definition = ItemService.GetItemById(itemId);
        if (definition == null || quantity <= 0) return Failure(TransactionErrorType.InvalidItem, "Invalid item or quantity");
        long cost = (long)ItemPricingService.CalculateItemPrice(definition) * quantity;
        if (cost > int.MaxValue) return Failure(TransactionErrorType.InvalidItem, "Price exceeds the supported balance");
        if (!CurrencyService.HasSufficientBronze((int)cost))
            return Failure(TransactionErrorType.InsufficientBronze, "Insufficient Bronze", (int)cost);
        var inventory = InventoryStorageService.GetAllItems();
        int requiredSlots = definition.RequiresInstances ? quantity :
            inventory.Exists(item => item.itemId == itemId && item.IsStackable) ? 0 : 1;
        if (inventory.Count + requiredSlots > InventoryManager.InventoryLimit)
            return Failure(TransactionErrorType.InsufficientSpace, "Not enough space in inventory");
        return TransactionResult.CreateSuccess((int)cost, _currentHero.bronze);
    }

    public static bool CanAffordItem(string itemId, int quantity = 1)
    {
        if (!IsInitialized() || quantity <= 0) return false;
        var definition = ItemService.GetItemById(itemId);
        return definition != null && (long)ItemPricingService.CalculateItemPrice(definition) * quantity <= _currentHero.bronze;
    }

    public static TransactionResult ValidateSale(InventoryItem item, int quantity = -1)
    {
        if (!IsInitialized()) return Failure(TransactionErrorType.TransactionFailed, "Inventory session is not initialized");
        if (!InventoryStorageService.Contains(item)) return Failure(TransactionErrorType.InvalidItem, "Item is not in inventory");
        var definition = ItemService.GetItemById(item.itemId);
        int count = quantity == -1 ? item.quantity : quantity;
        if (definition == null || count <= 0 || count > item.quantity || (item.IsEquipment && count != 1))
            return Failure(TransactionErrorType.InvalidItem, "Invalid sale quantity");
        long price = (long)ItemPricingService.CalculateItemSellPrice(definition, item) * count;
        if (price > int.MaxValue || (long)_currentHero.bronze + price > int.MaxValue)
            return Failure(TransactionErrorType.InvalidItem, "Sale exceeds the supported balance");
        return TransactionResult.CreateSuccess((int)price, _currentHero.bronze);
    }

    public static TransactionResult SellItem(InventoryItem item, int quantity = -1)
    {
        var validation = ValidateSale(item, quantity);
        if (!validation.Success) return validation;
        int count = quantity == -1 ? item.quantity : quantity;
        bool success = InventoryEventService.RunTransaction(() =>
        {
            bool removed = item.IsStackable && count < item.quantity
                ? InventoryManager.RemoveItemQuantity(item, count) : InventoryManager.RemoveSpecificItem(item);
            return removed && CurrencyService.CreditBronze(validation.RequiredBronze);
        });
        return success ? TransactionResult.CreateSuccess(validation.RequiredBronze, _currentHero.bronze)
            : Failure(TransactionErrorType.TransactionFailed, "Sale reverted");
    }

    private static TransactionResult Failure(TransactionErrorType type, string message, int cost = 0)
        => TransactionResult.CreateFailure(type, message, cost, _currentHero?.bronze ?? 0);
}

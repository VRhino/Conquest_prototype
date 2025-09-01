using UnityEngine;

/// <summary>
/// Servicio estático especializado en operaciones de moneda Bronze.
/// Proporciona métodos seguros para débito, crédito y validación de fondos del héroe.
/// Centraliza toda la lógica de manejo de Bronze para transacciones de tienda.
/// </summary>
public static class CurrencyService
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
            LogError("Cannot initialize CurrencyService with null hero");
            return;
        }

        LogInfo($"CurrencyService initialized. Current Bronze: {_currentHero.bronze}");
    }

    /// <summary>
    /// Verifica si el héroe tiene suficiente Bronze para una transacción.
    /// </summary>
    /// <param name="cost">Cantidad de Bronze requerida</param>
    /// <returns>True si tiene fondos suficientes</returns>
    public static bool HasSufficientBronze(int cost)
    {
        if (!ValidateOperation(cost))
            return false;

        bool hasSufficient = _currentHero.bronze >= cost;
        
        LogInfo($"Checking Bronze sufficiency: Required={cost}, Available={_currentHero.bronze}, Sufficient={hasSufficient}");
        return hasSufficient;
    }

    /// <summary>
    /// Debita una cantidad específica de Bronze del héroe de forma segura.
    /// </summary>
    /// <param name="amount">Cantidad a debitar</param>
    /// <returns>True si el débito fue exitoso</returns>
    public static bool DebitBronze(int amount)
    {
        if (!ValidateOperation(amount))
            return false;

        if (!HasSufficientBronze(amount))
        {
            LogWarning($"Cannot debit Bronze: insufficient funds. Required={amount}, Available={_currentHero.bronze}");
            return false;
        }

        int previousAmount = _currentHero.bronze;
        _currentHero.bronze -= amount;
        
        // Asegurar que nunca sea negativo
        _currentHero.bronze = Mathf.Max(0, _currentHero.bronze);

        LogInfo($"Bronze debited successfully: {previousAmount} -> {_currentHero.bronze} (debited: {amount})");
        
        // Disparar evento de cambio de inventario para persistencia
        InventoryEventService.TriggerInventoryChanged();
        
        return true;
    }

    /// <summary>
    /// Acredita una cantidad específica de Bronze al héroe de forma segura.
    /// </summary>
    /// <param name="amount">Cantidad a acreditar</param>
    /// <returns>True si el crédito fue exitoso</returns>
    public static bool CreditBronze(int amount)
    {
        if (!ValidateOperation(amount))
            return false;

        int previousAmount = _currentHero.bronze;
        _currentHero.bronze += amount;

        LogInfo($"Bronze credited successfully: {previousAmount} -> {_currentHero.bronze} (credited: {amount})");
        
        // Disparar evento de cambio de inventario para persistencia
        InventoryEventService.TriggerInventoryChanged();
        
        return true;
    }

    /// <summary>
    /// Obtiene la cantidad actual de Bronze del héroe.
    /// </summary>
    /// <returns>Cantidad de Bronze disponible</returns>
    public static int GetBronzeAmount()
    {
        if (_currentHero == null)
        {
            LogError("Cannot get Bronze amount: hero not initialized");
            return 0;
        }

        return _currentHero.bronze;
    }

    /// <summary>
    /// Obtiene la cantidad de Bronze de un héroe específico.
    /// </summary>
    /// <param name="hero">Héroe del cual obtener el Bronze</param>
    /// <returns>Cantidad de Bronze del héroe especificado</returns>
    public static int GetBronzeAmount(HeroData hero)
    {
        if (hero == null)
        {
            LogError("Cannot get Bronze amount: hero is null");
            return 0;
        }

        return hero.bronze;
    }

    /// <summary>
    /// Verifica si el servicio está inicializado correctamente.
    /// </summary>
    /// <returns>True si está inicializado</returns>
    public static bool IsInitialized()
    {
        return _currentHero != null;
    }

    #region Private Helper Methods

    /// <summary>
    /// Valida que una operación de moneda sea válida.
    /// </summary>
    /// <param name="amount">Cantidad a validar</param>
    /// <returns>True si la operación es válida</returns>
    private static bool ValidateOperation(int amount)
    {
        if (_currentHero == null)
        {
            LogError("CurrencyService not initialized with a hero");
            return false;
        }

        if (amount < 0)
        {
            LogError($"Invalid amount for currency operation: {amount}. Amount must be non-negative");
            return false;
        }

        if (amount == 0)
        {
            LogWarning("Currency operation with amount 0 - no action needed");
            return false;
        }

        return true;
    }

    #endregion

    #region Logging

    private static void LogInfo(string message)
    {
        Debug.Log($"[CurrencyService] {message}");
    }

    private static void LogWarning(string message)
    {
        Debug.LogWarning($"[CurrencyService] {message}");
    }

    private static void LogError(string message)
    {
        Debug.LogError($"[CurrencyService] {message}");
    }

    #endregion
}

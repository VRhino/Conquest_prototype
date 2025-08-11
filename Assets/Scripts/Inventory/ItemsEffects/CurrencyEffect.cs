using UnityEngine;
using Data.Items;

/// <summary>
/// Efecto de item que otorga una cantidad aleatoria de monedas al héroe.
/// Implementa correctamente la estructura base de ItemEffect.
/// </summary>
[CreateAssetMenu(fileName = "CurrencyEffect", menuName = "Items/Effects/Currency", order = 1)]
public class CurrencyEffect : ItemEffect
{
    [Header("Currency Settings")]
    [SerializeField] private CurrencyType currencyType = CurrencyType.Gold;
    [SerializeField] private IntRange currencyAmount = new IntRange(10, 50);
    
    [Header("Validation")]
    [SerializeField] private bool hasMaximumLimit = false;
    [SerializeField] private int maximumCurrencyLimit = 999999;

    /// <summary>
    /// Ejecuta el efecto de moneda en el héroe
    /// </summary>
    public override bool Execute(HeroData hero, int quantity = 1)
    {
        if (!CanExecute(hero))
        {
            return false;
        }
        
        // Generar cantidad aleatoria de monedas por cada quantity
        int totalAmount = 0;
        for (int i = 0; i < quantity; i++)
        {
            totalAmount += currencyAmount.GetRandomValue();
        }
        
        // Aplicar límite máximo si está habilitado
        if (hasMaximumLimit)
        {
            int currentCurrency = GetCurrentCurrencyAmount(hero);
            int remainingSpace = maximumCurrencyLimit - currentCurrency;
            totalAmount = Mathf.Min(totalAmount, remainingSpace);
        }
        
        // Añadir monedas al héroe
        bool success = AddCurrencyToHero(hero, totalAmount);
        
        if (success && totalAmount > 0)
        {
            // Llamar al método base para logging
            OnEffectExecuted(hero, totalAmount);
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// Verifica si el efecto puede ser ejecutado
    /// </summary>
    public override bool CanExecute(HeroData hero)
    {
        if (!base.CanExecute(hero))
        {
            return false;
        }
        
        // Verificar si el héroe puede recibir más monedas
        if (hasMaximumLimit)
        {
            int currentCurrency = GetCurrentCurrencyAmount(hero);
            if (currentCurrency >= maximumCurrencyLimit)
            {
                Debug.LogWarning($"CurrencyEffect: Hero has reached maximum {currencyType} limit ({maximumCurrencyLimit})");
                return false;
            }
        }
        
        return true;
    }

    /// <summary>
    /// Texto de preview para mostrar en la UI
    /// </summary>
    public override string GetPreviewText(int quantity = 1)
    {
        int minTotal = currencyAmount.min * quantity;
        int maxTotal = currencyAmount.max * quantity;
        
        string currencyName = GetCurrencyDisplayName(currencyType);
        
        if (minTotal == maxTotal)
        {
            return $"+{minTotal} {currencyName}";
        }
        else
        {
            return $"+{minTotal}-{maxTotal} {currencyName}";
        }
    }

    /// <summary>
    /// Obtiene la prioridad de ejecución (las monedas tienen baja prioridad)
    /// </summary>
    public override int GetExecutionPriority()
    {
        return -10; // Baja prioridad - se ejecuta después de otros efectos
    }

    #region Helper Methods

    /// <summary>
    /// Añade monedas al héroe según el tipo especificado
    /// </summary>
    private bool AddCurrencyToHero(HeroData hero, int amount)
    {
        if (amount <= 0) return false;

        switch (currencyType)
        {
            case CurrencyType.Gold:
                hero.gold = Mathf.Max(0, hero.gold + amount);
                return true;

            case CurrencyType.Silver:
                hero.silver = Mathf.Max(0, hero.silver + amount);
                return true;

            case CurrencyType.Bronze:
                hero.bronze = Mathf.Max(0, hero.bronze + amount);
                return true;

            default:
                Debug.LogError($"CurrencyEffect: Unsupported currency type: {currencyType}");
                return false;
        }
    }

    /// <summary>
    /// Obtiene la cantidad actual de monedas del héroe
    /// </summary>
    private int GetCurrentCurrencyAmount(HeroData hero)
    {
        switch (currencyType)
        {
            case CurrencyType.Gold:
                return hero.gold;
            case CurrencyType.Silver:
                return hero.silver;
            case CurrencyType.Bronze:
                return hero.bronze;
            default:
                return 0;
        }
    }

    /// <summary>
    /// Obtiene el nombre a mostrar para el tipo de moneda
    /// </summary>
    private string GetCurrencyDisplayName(CurrencyType type)
    {
        switch (type)
        {
            case CurrencyType.Gold: return "Gold";
            case CurrencyType.Silver: return "Silver";
            case CurrencyType.Bronze: return "Bronze";
            default: return type.ToString();
        }
    }

    #endregion

    #region Validation

    /// <summary>
    /// Validación en el editor
    /// </summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        
        // Asegurar que el rango sea válido
        if (currencyAmount.min < 0)
        {
            currencyAmount.min = 0;
        }
        
        if (currencyAmount.max < currencyAmount.min)
        {
            currencyAmount.max = currencyAmount.min;
        }
        
        // Asegurar que el límite máximo sea razonable
        if (maximumCurrencyLimit < 0)
        {
            maximumCurrencyLimit = 999999;
        }
    }

    #endregion
}

/// <summary>
/// Enumeración de tipos de moneda soportados
/// </summary>
[System.Serializable]
public enum CurrencyType
{
    Gold,
    Silver,
    Bronze
}
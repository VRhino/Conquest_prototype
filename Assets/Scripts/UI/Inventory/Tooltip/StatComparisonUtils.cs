using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Estructura que representa la comparación de una estadística entre dos ítems.
/// </summary>
[Serializable]
public struct StatComparison
{
    public string statName;
    public float inventoryValue;
    public float equippedValue;
    public float difference;
    public ComparisonResult result;
    public Color displayColor;
    public string displaySymbol;

    public StatComparison(string statName, float inventoryValue, float equippedValue)
    {
        this.statName = statName;
        this.inventoryValue = inventoryValue;
        this.equippedValue = equippedValue;
        this.difference = inventoryValue - equippedValue;
        
        // Determinar el resultado de la comparación
        if (Mathf.Abs(difference) < 0.01f) // Considerar valores muy pequeños como iguales
        {
            this.result = ComparisonResult.Equal;
            this.displayColor = Color.white;
            this.displaySymbol = "";
        }
        else if (difference > 0)
        {
            this.result = ComparisonResult.Better;
            this.displayColor = Color.green;
            this.displaySymbol = "↑";
        }
        else
        {
            this.result = ComparisonResult.Worse;
            this.displayColor = Color.red;
            this.displaySymbol = "↓";
        }
    }
}

/// <summary>
/// Resultado de la comparación entre estadísticas.
/// </summary>
public enum ComparisonResult
{
    Better,   // El ítem del inventario es mejor
    Worse,    // El ítem del inventario es peor
    Equal     // Ambos ítems tienen el mismo valor
}

/// <summary>
/// Utilidades para comparar estadísticas entre ítems de equipamiento.
/// </summary>
public static class StatComparisonUtils
{
    // Colores para los diferentes tipos de comparación
    public static readonly Color BetterColor = new Color(0.2f, 0.8f, 0.2f, 1f);    // Verde
    public static readonly Color WorseColor = new Color(0.8f, 0.2f, 0.2f, 1f);     // Rojo
    public static readonly Color EqualColor = new Color(0.8f, 0.8f, 0.8f, 1f);     // Gris claro

    /// <summary>
    /// Compara las estadísticas de dos ítems de equipamiento.
    /// </summary>
    /// <param name="inventoryItem">Ítem del inventario</param>
    /// <param name="equippedItem">Ítem equipado</param>
    /// <returns>Lista de comparaciones de estadísticas</returns>
    public static List<StatComparison> CompareItemStats(InventoryItem inventoryItem, InventoryItem equippedItem)
    {
        var comparisons = new List<StatComparison>();

        if (inventoryItem?.GeneratedStats == null || equippedItem?.GeneratedStats == null)
            return comparisons;

        var inventoryStats = inventoryItem.GeneratedStats;
        var equippedStats = equippedItem.GeneratedStats;

        // Obtener todas las estadísticas únicas de ambos ítems
        var allStatNames = new HashSet<string>();
        
        foreach (var stat in inventoryStats)
            allStatNames.Add(stat.Key);
        
        foreach (var stat in equippedStats)
            allStatNames.Add(stat.Key);

        // Comparar cada estadística
        foreach (string statName in allStatNames)
        {
            float inventoryValue = GetStatValue(inventoryStats, statName);
            float equippedValue = GetStatValue(equippedStats, statName);

            var comparison = new StatComparison(statName, inventoryValue, equippedValue);
            comparisons.Add(comparison);
        }

        return comparisons;
    }

    /// <summary>
    /// Obtiene el valor de una estadística específica de un diccionario de stats.
    /// </summary>
    /// <param name="stats">Diccionario de estadísticas</param>
    /// <param name="statName">Nombre de la estadística</param>
    /// <returns>Valor de la estadística o 0 si no se encuentra</returns>
    private static float GetStatValue(Dictionary<string, float> stats, string statName)
    {
        if (stats == null) return 0f;

        return stats.TryGetValue(statName, out float value) ? value : 0f;
    }

    /// <summary>
    /// Formatea el valor de una estadística para mostrar en el tooltip de comparación.
    /// </summary>
    /// <param name="comparison">Comparación de la estadística</param>
    /// <returns>String formateado para mostrar</returns>
    public static string FormatComparisonValue(StatComparison comparison)
    {
        string formattedValue = FormatStatValue(comparison.inventoryValue);
        
        if (comparison.result != ComparisonResult.Equal)
        {
            string differenceText = "";
            if (comparison.difference > 0)
                differenceText = $" (+{FormatStatValue(comparison.difference)})";
            else
                differenceText = $" ({FormatStatValue(comparison.difference)})";

            return $"{formattedValue}{differenceText} {comparison.displaySymbol}";
        }

        return formattedValue;
    }

    /// <summary>
    /// Formatea un valor de estadística individual.
    /// </summary>
    /// <param name="value">Valor a formatear</param>
    /// <returns>String formateado</returns>
    private static string FormatStatValue(float value)
    {
        // Si es un número entero, mostrarlo sin decimales
        if (Mathf.Abs(value - Mathf.RoundToInt(value)) < 0.01f)
            return Mathf.RoundToInt(value).ToString();
        
        // Si tiene decimales, mostrar hasta 2 decimales
        return value.ToString("F1");
    }

    /// <summary>
    /// Obtiene el color para mostrar una comparación.
    /// </summary>
    /// <param name="comparison">Comparación de la estadística</param>
    /// <returns>Color para la comparación</returns>
    public static Color GetComparisonColor(StatComparison comparison)
    {
        switch (comparison.result)
        {
            case ComparisonResult.Better:
                return BetterColor;
            case ComparisonResult.Worse:
                return WorseColor;
            case ComparisonResult.Equal:
            default:
                return EqualColor;
        }
    }

    /// <summary>
    /// Determina si una estadística es significativa para mostrar en la comparación.
    /// </summary>
    /// <param name="comparison">Comparación de la estadística</param>
    /// <returns>True si la estadística es significativa</returns>
    public static bool IsSignificantStat(StatComparison comparison)
    {
        // Filtrar estadísticas que no aportan información útil
        if (string.IsNullOrEmpty(comparison.statName))
            return false;

        // Si ambos valores son 0, no mostrar
        if (comparison.inventoryValue == 0 && comparison.equippedValue == 0)
            return false;

        return true;
    }

    /// <summary>
    /// Obtiene una descripción textual del resultado de la comparación.
    /// </summary>
    /// <param name="comparison">Comparación de la estadística</param>
    /// <returns>Descripción del resultado</returns>
    public static string GetComparisonDescription(StatComparison comparison)
    {
        switch (comparison.result)
        {
            case ComparisonResult.Better:
                return $"Mejor en {FormatStatValue(comparison.difference)}";
            case ComparisonResult.Worse:
                return $"Peor en {FormatStatValue(Mathf.Abs(comparison.difference))}";
            case ComparisonResult.Equal:
                return "Igual";
            default:
                return "";
        }
    }
}

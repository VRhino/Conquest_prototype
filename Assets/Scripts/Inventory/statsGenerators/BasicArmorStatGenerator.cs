using UnityEngine;
using System.Collections.Generic;
using Data.Items;

/// <summary>
/// Generador de stats específico para armaduras.
/// </summary>
[CreateAssetMenu(fileName = "BasicArmorStatGenerator", menuName = "Items/Stat Generators/BasicArmor", order = 1)]
public class BasicArmorStatGenerator : ItemStatGenerator
{
    [Header("Defense Stats")]
    [SerializeField] private FloatRange piercingDefense = new FloatRange(8f, 15f);
    [SerializeField] private FloatRange slashingDefense = new FloatRange(10f, 18f);
    [SerializeField] private FloatRange bluntDefense = new FloatRange(12f, 20f);
    [Header("Basic Stats")]
    [SerializeField] private FloatRange health = new FloatRange(50f, 100f);
    [SerializeField] private FloatRange armor = new FloatRange(30f, 60f);
    [SerializeField] private FloatRange vitality = new FloatRange(30f, 60f);

    [Header("Generation Settings")]
    [SerializeField] private bool allowZeroValues = false;
    
    /// <summary>
    /// Genera todos los stats del arma con valores aleatorios
    /// </summary>
    public override Dictionary<string, float> GenerateStats()
    {
        var stats = new Dictionary<string, float>();
        
        // Generar defense stats
        stats["PiercingDefense"] = RoundToInt(piercingDefense.GetRandomValue());
        stats["SlashingDefense"] = RoundToInt(slashingDefense.GetRandomValue());
        stats["BluntDefense"] = RoundToInt(bluntDefense.GetRandomValue());

        // Generar basic stats
        stats["Health"] = RoundToInt(health.GetRandomValue());
        stats["Armor"] = RoundToInt(armor.GetRandomValue());
        stats["Vitality"] = RoundToInt(vitality.GetRandomValue());

        // Aplicar reglas de validación si es necesario
        if (!allowZeroValues)
        {
            ValidateMinimumValues(stats);
        }
        
        return stats;
    }
    
    /// <summary>
    /// Muestra preview de los stats que se pueden generar
    /// </summary>
    public override string GetStatsPreview()
    {
        return $"Defense: P({piercingDefense.min}-{piercingDefense.max}) " +
               $"S({slashingDefense.min}-{slashingDefense.max}) " +
               $"B({bluntDefense.min}-{bluntDefense.max})\n" +
               $"Health: {health.min}-{health.max}\n" +
               $"Armor: {armor.min}-{armor.max}\n" +
               $"Vitality: {vitality.min}-{vitality.max}\n";
    }
    
    #region Helper Methods
    
    /// <summary>
    /// Redondea un valor al número entero más cercano
    /// </summary>
    private float RoundToInt(float value)
    {
        return Mathf.Round(value);
    }
    
    /// <summary>
    /// Asegura que ningún stat sea 0 si allowZeroValues está deshabilitado
    /// </summary>
    private void ValidateMinimumValues(Dictionary<string, float> stats)
    {
        var keysToUpdate = new List<string>();
        
        foreach (var kvp in stats)
        {
            if (kvp.Value <= 0f)
            {
                keysToUpdate.Add(kvp.Key);
            }
        }
        
        foreach (var key in keysToUpdate)
        {
            stats[key] = GetMinimumValueForStat(key);
        }
    }
    
    /// <summary>
    /// Obtiene el valor mínimo permitido para un stat específico
    /// </summary>
    private float GetMinimumValueForStat(string statName)
    {
        switch (statName)
        {
            case "PiercingDefense": return Mathf.Max(0.1f, piercingDefense.min);
            case "SlashingDefense": return Mathf.Max(0.1f, slashingDefense.min);
            case "BluntDefense": return Mathf.Max(0.1f, bluntDefense.min);
            case "Health": return Mathf.Max(0.1f, health.min);
            case "Armor": return Mathf.Max(0.1f, armor.min);
            case "Vitality": return Mathf.Max(0.1f, vitality.min);
            default: return 0.1f;
        }
    }
    
    #endregion
    

    public override List<string> GetStatNames()
    {
        return new List<string>
        {
            "PiercingDefense",
            "SlashingDefense",
            "BluntDefense",
            "Health",
            "Armor",
            "Vitality"
        };
    }
}
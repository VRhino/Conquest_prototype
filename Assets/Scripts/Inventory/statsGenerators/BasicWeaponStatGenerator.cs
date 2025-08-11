using UnityEngine;
using System.Collections.Generic;
using Data.Items;

/// <summary>
/// Generador de stats específico para armas.
/// </summary>
[CreateAssetMenu(fileName = "BasicWeaponStatGenerator", menuName = "Items/Stat Generators/BasicWeapon", order = 2)]
public class BasicWeaponStatGenerator : ItemStatGenerator
{
    [Header("Damage Stats")]
    [SerializeField] private FloatRange piercingDamage = new FloatRange(8f, 15f);
    [SerializeField] private FloatRange slashingDamage = new FloatRange(10f, 18f);
    [SerializeField] private FloatRange bluntDamage = new FloatRange(12f, 20f);
    
    [Header("Penetration Stats")]
    [SerializeField] private FloatRange piercingPenetration = new FloatRange(2f, 8f);
    [SerializeField] private FloatRange slashingPenetration = new FloatRange(1f, 6f);
    [SerializeField] private FloatRange bluntPenetration = new FloatRange(3f, 10f);

    [Header("Basic Stats")]
    [SerializeField] private FloatRange strength = new FloatRange(10f, 20f);
    [SerializeField] private FloatRange dexterity = new FloatRange(10f, 20f);

    [Header("Generation Settings")]
    [SerializeField] private bool allowZeroValues = false;
    
    /// <summary>
    /// Genera todos los stats del arma con valores aleatorios
    /// </summary>
    public override Dictionary<string, float> GenerateStats()
    {
        var stats = new Dictionary<string, float>();
        
        // Generar damage stats
        stats["PiercingDamage"] = RoundToInt(piercingDamage.GetRandomValue());
        stats["SlashingDamage"] = RoundToInt(slashingDamage.GetRandomValue());
        stats["BluntDamage"] = RoundToInt(bluntDamage.GetRandomValue());
        
        // Generar penetration stats
        stats["PiercingPenetration"] = RoundToInt(piercingPenetration.GetRandomValue());
        stats["SlashingPenetration"] = RoundToInt(slashingPenetration.GetRandomValue());
        stats["BluntPenetration"] = RoundToInt(bluntPenetration.GetRandomValue());

        // Generar basic stats
        stats["Strength"] = RoundToInt(strength.GetRandomValue());
        stats["Dexterity"] = RoundToInt(dexterity.GetRandomValue());

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
        return $"Damage: P({piercingDamage.min}-{piercingDamage.max}) " +
               $"S({slashingDamage.min}-{slashingDamage.max}) " +
               $"B({bluntDamage.min}-{bluntDamage.max})\n" +
               $"Penetration: P({piercingPenetration.min}-{piercingPenetration.max}) " +
               $"S({slashingPenetration.min}-{slashingPenetration.max}) " +
               $"B({bluntPenetration.min}-{bluntPenetration.max})\n" +
               $"Strength: {strength.min}-{strength.max}\n" +
               $"Dexterity: {dexterity.min}-{dexterity.max}";
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
            case "PiercingDamage": return Mathf.Max(0.1f, piercingDamage.min);
            case "SlashingDamage": return Mathf.Max(0.1f, slashingDamage.min);
            case "BluntDamage": return Mathf.Max(0.1f, bluntDamage.min);
            case "PiercingPenetration": return Mathf.Max(0.1f, piercingPenetration.min);
            case "SlashingPenetration": return Mathf.Max(0.1f, slashingPenetration.min);
            case "BluntPenetration": return Mathf.Max(0.1f, bluntPenetration.min);
            case "Strength": return Mathf.Max(0.1f, strength.min);
            case "Dexterity": return Mathf.Max(0.1f, dexterity.min);
            default: return 0.1f;
        }
    }
    
    #endregion
    

    public override List<string> GetStatNames()
    {
        return new List<string>
        {
            "PiercingDamage",
            "SlashingDamage",
            "BluntDamage",
            "PiercingPenetration",
            "SlashingPenetration",
            "BluntPenetration",
            "Strength",
            "Dexterity"
        };
    }
}
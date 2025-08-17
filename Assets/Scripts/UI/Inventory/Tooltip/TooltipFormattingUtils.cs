using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Data.Items;
using Unity.Entities.UniversalDelegates;

/// <summary>
/// Utilidades centralizadas para formateo de tooltips.
/// Contiene todas las funciones de formateo movidas desde InventoryTooltipController.
/// </summary>
public static class TooltipFormattingUtils
{
    #region Stat Display Names
    
    private static readonly Dictionary<string, string> StatDisplayNames = new()
    {
        // Sistema en inglés
        { "armor", "Armor" },
        { "damage", "Damage" },
        { "health", "Health" },
        { "mana", "Mana" },
        { "strength", "Strength" },
        { "dexterity", "Dexterity" },
        { "intelligence", "Intelligence" },
        { "vitality", "Vitality" },
        { "criticalChance", "Critical Chance" },
        { "criticalDamage", "Critical Damage" },
        { "attackSpeed", "Attack Speed" },
        { "movementSpeed", "Movement Speed" },
        { "magicResistance", "Magic Resistance" },
        { "physicalResistance", "Physical Resistance" },
        
        // Sistema en español (para compatibilidad con el controller actual)
        { "PiercingDamage", "Daño Perforante" },
        { "SlashingDamage", "Daño Cortante" },
        { "BluntDamage", "Daño Contundente" },
        { "PiercingDefense", "Defensa Perforante" },
        { "SlashingDefense", "Defensa Cortante" },
        { "BluntDefense", "Defensa Contundente" },
        { "PiercingPenetration", "Penetración Perforante" },
        { "SlashingPenetration", "Penetración Cortante" },
        { "BluntPenetration", "Penetración Contundente" },
        { "Health", "Salud" },
        { "Armor", "Armadura" },
        { "Vitality", "Vitalidad" },
        { "Strength", "Fuerza" },
        { "Dexterity", "Destreza" }
    };

    /// <summary>
    /// Obtiene el nombre de display formateado para una estadística.
    /// </summary>
    /// <param name="statName">Nombre interno de la estadística</param>
    /// <returns>Nombre formateado para mostrar</returns>
    public static string GetStatDisplayName(string statName)
    {
        if (string.IsNullOrEmpty(statName)) return "Unknown";
        
        return StatDisplayNames.TryGetValue(statName, out string displayName) 
            ? displayName 
            : FormatCamelCase(statName);
    }

    /// <summary>
    /// Convierte un string en camelCase a formato legible.
    /// Ejemplo: "criticalChance" -> "Critical Chance"
    /// </summary>
    /// <param name="camelCase">String en camelCase</param>
    /// <returns>String formateado</returns>
    private static string FormatCamelCase(string camelCase)
    {
        if (string.IsNullOrEmpty(camelCase)) return "";
        
        var result = "";
        for (int i = 0; i < camelCase.Length; i++)
        {
            char c = camelCase[i];
            if (i > 0 && char.IsUpper(c))
                result += " ";
            result += i == 0 ? char.ToUpper(c) : c;
        }
        return result;
    }

    #endregion

    #region Stat Value Formatting

    /// <summary>
    /// Formatea el valor de una estadística para display.
    /// </summary>
    /// <param name="statName">Nombre de la estadística</param>
    /// <param name="value">Valor de la estadística</param>
    /// <returns>Valor formateado para mostrar</returns>
    public static string FormatStatValue(string statName, float value)
    {
        // Estadísticas que se muestran como porcentajes
        if (IsPercentageStat(statName))
        {
            return $"{value:F1}%";
        }
        
        // Estadísticas que se muestran como enteros
        if (IsIntegerStat(statName))
        {
            return $"{Mathf.RoundToInt(value)}";
        }
        
        // Estadísticas con decimales
        return $"{value:F1}";
    }

    /// <summary>
    /// Formatea el valor de una estadística para display (sobrecarga simple para compatibilidad).
    /// </summary>
    /// <param name="value">Valor de la estadística</param>
    /// <returns>Valor formateado para mostrar</returns>
    public static string FormatStatValue(float value)
    {
        // Comportamiento por defecto: entero si no tiene decimales, float si los tiene
        return value % 1 == 0 ? value.ToString("F0") : value.ToString("F1");
    }

    /// <summary>
    /// Verifica si una estadística debe mostrarse como porcentaje.
    /// </summary>
    /// <param name="statName">Nombre de la estadística</param>
    /// <returns>True si es porcentaje</returns>
    private static bool IsPercentageStat(string statName)
    {
        var percentageStats = new HashSet<string>
        {
            "criticalChance",
            "criticalDamage",
            "magicResistance",
            "physicalResistance",
            "blockChance",
            "dodgeChance"
        };
        
        return percentageStats.Contains(statName);
    }

    /// <summary>
    /// Verifica si una estadística debe mostrarse como entero.
    /// </summary>
    /// <param name="statName">Nombre de la estadística</param>
    /// <returns>True si es entero</returns>
    private static bool IsIntegerStat(string statName)
    {
        var integerStats = new HashSet<string>
        {
            "armor",
            "damage",
            "health",
            "mana",
            "strength",
            "dexterity",
            "intelligence",
            "vitality"
        };
        
        return integerStats.Contains(statName);
    }

    #endregion

    #region Item Type Display

    /// <summary>
    /// Obtiene el nombre de display para un tipo de ítem.
    /// </summary>
    /// <param name="itemType">Tipo de ítem</param>
    /// <param name="itemCategory">Categoría de ítem</param>
    /// <returns>Nombre formateado para mostrar</returns>
    public static string GetItemTypeDisplayName(ItemType itemType, ItemCategory itemCategory)
    {
        if (itemType == ItemType.Weapon)
        {
            return itemCategory switch
            {
                ItemCategory.Bow => "Arco",
                ItemCategory.Spear => "Lanza",
                ItemCategory.TwoHandedSword => "Espada a Dos Manos",
                ItemCategory.SwordAndShield => "Espada y Escudo",
                _ => "Arma"
            };
        }
        else if (itemType == ItemType.Armor)
        {
            return itemCategory switch
            {
                ItemCategory.Helmet => "Casco",
                ItemCategory.Torso => "Torso",
                ItemCategory.Gloves => "Guantes",
                ItemCategory.Pants => "Pantalones",
                ItemCategory.Boots => "Botas",
                _ => "Armadura"
            };
        }
        else if (itemType == ItemType.Consumable)
        {
            return "Consumible";
        }
        else if (itemType == ItemType.Visual)
        {
            return "Cosmético";
        }
        else
        {
            return "Sin Tipo";
        }
    }

    /// <summary>
    /// Obtiene información específica de armadura para tipos de armadura.
    /// </summary>
    /// <param name="itemType">Tipo de ítem</param>
    /// <returns>Información de armadura específica</returns>
    public static string GetArmorTypeInfo(ArmorType armorType)
    {
        return armorType switch
        {
           ArmorType.Light => "Armadura Ligera",
           ArmorType.Medium => "Armadura Media",
           ArmorType.Heavy => "Armadura Pesada",
            _ => "Desconocido"
        };
    }

    #endregion

    #region Durability Formatting

    /// <summary>
    /// Formatea la durabilidad de un ítem para display.
    /// </summary>
    /// <param name="currentDurability">Durabilidad actual</param>
    /// <param name="maxDurability">Durabilidad máxima</param>
    /// <returns>String de durabilidad formateado</returns>
    public static string FormatDurability(float currentDurability, float maxDurability)
    {
        if (maxDurability <= 0) return "N/A";
        
        int current = Mathf.RoundToInt(currentDurability);
        int max = Mathf.RoundToInt(maxDurability);
        
        return $"{current}/{max}";
    }

    /// <summary>
    /// Obtiene el color de durabilidad basado en el porcentaje restante.
    /// </summary>
    /// <param name="currentDurability">Durabilidad actual</param>
    /// <param name="maxDurability">Durabilidad máxima</param>
    /// <returns>Color para la durabilidad</returns>
    public static Color GetDurabilityColor(float currentDurability, float maxDurability)
    {
        if (maxDurability <= 0) return Color.gray;
        
        float percentage = currentDurability / maxDurability;
        
        return percentage switch
        {
            >= 0.8f => Color.green,
            >= 0.6f => Color.yellow,
            >= 0.3f => new Color(1f, 0.5f, 0f), // Orange
            _ => Color.red
        };
    }

    #endregion

    #region Action Text Formatting

    /// <summary>
    /// Obtiene el texto de acción apropiado para un ítem.
    /// </summary>
    /// <param name="itemData">Datos del ítem</param>
    /// <returns>Texto de acción formateado</returns>
    public static string GetActionText(ItemData itemData, TooltipType currentTooltipType)
    {
        if (itemData == null) return "";
        if (currentTooltipType == TooltipType.Secondary)
        {
            return "Equipado";
        }
            
        if (itemData.IsEquipment)
            {
                // Usar el texto en español para compatibilidad con el controller actual
                return "Clic derecho: Equipar ";
            }
        
        if (itemData.IsConsumable)
        {
            // Usar el texto en español para compatibilidad con el controller actual
            return "Clic derecho: Usar ";
        }
        
        return "Arrastrar: Mover";
    }

    #endregion

    #region Validation

    /// <summary>
    /// Valida que los datos necesarios para el formateo estén presentes.
    /// </summary>
    /// <param name="itemData">Datos del ítem</param>
    /// <param name="inventoryItem">Instancia del ítem</param>
    /// <returns>True si los datos son válidos</returns>
    public static bool ValidateFormattingData(ItemData itemData, InventoryItem inventoryItem)
    {
        if (itemData == null)
        {
            Debug.LogWarning("[TooltipFormattingUtils] ItemData is null");
            return false;
        }
        
        if (inventoryItem == null)
        {
            Debug.LogWarning("[TooltipFormattingUtils] InventoryItem is null");
            return false;
        }
        
        if (string.IsNullOrEmpty(itemData.id))
        {
            Debug.LogWarning("[TooltipFormattingUtils] ItemData ID is null or empty");
            return false;
        }
        
        return true;
    }

    #endregion
}

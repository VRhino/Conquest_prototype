using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Servicio para manejar modificaciones temporales de atributos del héroe.
/// Mantiene un cache temporal superpuesto sobre el cache principal de DataCacheService.
/// Permite modificar atributos sin alterar los datos persistidos hasta que se confirmen los cambios.
/// REFACTORED: Usa nueva arquitectura de separación base/calculado/temporal.
/// </summary>
public static class HeroTempAttributeService
{
    /// <summary>
    /// Cache de modificaciones temporales por héroe.
    /// Key: heroId, Value: Dictionary de cambios (nombreAtributo -> valorModificado)
    /// </summary>
    private static readonly Dictionary<string, Dictionary<string, float>> _tempChanges = new();

    /// <summary>
    /// Aplica una modificación temporal a un atributo específico del héroe.
    /// </summary>
    /// <param name="heroId">ID del héroe</param>
    /// <param name="attributeName">Nombre del atributo (ej: "strength", "dexterity")</param>
    /// <param name="newValue">Nuevo valor temporal</param>
    public static void ApplyTempChange(string heroId, string attributeName, float newValue)
    {
        if (string.IsNullOrEmpty(heroId) || string.IsNullOrEmpty(attributeName))
        {
            Debug.LogWarning("[HeroTempAttributeService] HeroId o attributeName es inválido.");
            return;
        }

        if (!_tempChanges.ContainsKey(heroId)) _tempChanges[heroId] = new Dictionary<string, float>();
        
        _tempChanges[heroId][attributeName] = newValue;
    }

    /// <summary>
    /// Obtiene los atributos calculados con las modificaciones temporales aplicadas.
    /// Usa la nueva arquitectura de separación base/calculado/temporal.
    /// </summary>
    /// <param name="heroId">ID del héroe</param>
    /// <returns>HeroCalculatedAttributes con modificaciones temporales aplicadas</returns>
    public static HeroCalculatedAttributes GetAttributesWithTempChanges(string heroId)
    {
        var heroData = PlayerSessionService.SelectedHero;
        if (heroData == null || heroData.heroName != heroId)
        {
            Debug.LogWarning($"[HeroTempAttributeService] No se encontró hero data para: {heroId}");
            return HeroCalculatedAttributes.Empty;
        }

        // Si no hay cambios temporales, usar cálculo normal
        if (!HasTempChanges(heroId)) return DataCacheService.GetHeroCalculatedAttributes(heroId);

        // Convertir cambios temporales a EquipmentBonuses para la nueva arquitectura
        var tempMods = GetTempChangesAsEquipmentBonuses(heroId);
        
        return DataCacheService.CalculateAttributes(heroData, tempMods);
    }

    /// <summary>
    /// Convierte las modificaciones temporales a formato EquipmentBonuses.
    /// CORREGIDO: Ahora calcula correctamente la diferencia entre temporal y (base + equipamiento).
    /// </summary>
    /// <param name="heroId">ID del héroe</param>
    /// <returns>Modificaciones temporales en formato de bonificaciones</returns>
    public static EquipmentBonuses GetTempChangesAsEquipmentBonuses(string heroId)
    {
        if (!_tempChanges.ContainsKey(heroId))
        {
            return EquipmentBonuses.Empty;
        }

        var changes = _tempChanges[heroId];
        var heroData = PlayerSessionService.SelectedHero;
        if (heroData == null) return EquipmentBonuses.Empty;

        // CORREGIDO: Calcular diferencias entre valores temporales y valores con equipamiento
        var result = new EquipmentBonuses();
        
        if (changes.ContainsKey("strength"))
        {
            var baseWithEquipment = DataCacheService.GetBaseWithEquipment(heroData, "strength");
            result.strengthBonus = (int)(changes["strength"] - baseWithEquipment);
        }
            
        if (changes.ContainsKey("dexterity"))
        {
            var baseWithEquipment = DataCacheService.GetBaseWithEquipment(heroData, "dexterity");
            result.dexterityBonus = (int)(changes["dexterity"] - baseWithEquipment);
        }
            
        if (changes.ContainsKey("armor"))
        {
            var baseWithEquipment = DataCacheService.GetBaseWithEquipment(heroData, "armor");
            result.armorBonus = (int)(changes["armor"] - baseWithEquipment);
        }
            
        if (changes.ContainsKey("vitality"))
        {
            var baseWithEquipment = DataCacheService.GetBaseWithEquipment(heroData, "vitality");
            result.vitalityBonus = (int)(changes["vitality"] - baseWithEquipment);
        }

        return result;
    }

    /// <summary>
    /// Verifica si un héroe tiene modificaciones temporales pendientes.
    /// </summary>
    /// <param name="heroId">ID del héroe</param>
    /// <returns>True si hay cambios temporales, False si no</returns>
    public static bool HasTempChanges(string heroId)
    {
        return !string.IsNullOrEmpty(heroId) && 
               _tempChanges.ContainsKey(heroId) && 
               _tempChanges[heroId].Count > 0;
    }

    /// <summary>
    /// Obtiene la lista de modificaciones temporales para un héroe.
    /// </summary>
    /// <param name="heroId">ID del héroe</param>
    /// <returns>Dictionary de cambios temporales, o null si no hay</returns>
    public static Dictionary<string, float> GetTempChanges(string heroId)
    {
        if (string.IsNullOrEmpty(heroId) || !_tempChanges.ContainsKey(heroId))
            return null;
        return new Dictionary<string, float>(_tempChanges[heroId]);
    }

    /// <summary>
    /// Limpia todas las modificaciones temporales de un héroe.
    /// </summary>
    /// <param name="heroId">ID del héroe</param>
    public static void ClearTempChanges(string heroId)
    {
        if (!string.IsNullOrEmpty(heroId) && _tempChanges.ContainsKey(heroId))
        {
            _tempChanges[heroId].Clear();
        }
    }

    /// <summary>
    /// Obtiene el valor final de un atributo considerando modificaciones temporales.
    /// CORREGIDO: Usa la nueva nomenclatura clara para evitar confusión.
    /// </summary>
    /// <param name="heroId">ID del héroe</param>
    /// <param name="attributeName">Nombre del atributo</param>
    /// <returns>Valor con temporales aplicadas, o valor base+equipamiento si no hay temporales</returns>
    public static float GetFinalAttributeValue(string heroId, string attributeName)
    {
        var heroData = PlayerSessionService.SelectedHero;
        if (heroData == null) return 0f;

        if (!HasTempChanges(heroId))
            return DataCacheService.GetBaseWithEquipment(heroData, attributeName);
            
        var changes = _tempChanges[heroId];
        if (changes.ContainsKey(attributeName))
            return changes[attributeName];
            
        return DataCacheService.GetBaseWithEquipment(heroData, attributeName);
    }

    /// <summary>
    /// Calcula la cantidad de puntos de atributo usados en modificaciones temporales.
    /// </summary>
    /// <param name="heroId">ID del héroe</param>
    /// <param name="heroData">Datos base del héroe</param>
    /// <returns>Puntos usados en modificaciones temporales</returns>
    public static int CalculateUsedTempPoints(string heroId, HeroData heroData)
    {
        if (!HasTempChanges(heroId) || heroData == null) return 0;

        var changes = _tempChanges[heroId];
        int usedPoints = 0;

        foreach (var change in changes)
        {
            float BaseAndEquipment = DataCacheService.GetBaseWithEquipment(heroData, change.Key);
            float tempValue = change.Value;
            int difference = Mathf.RoundToInt(tempValue - BaseAndEquipment);
            if (difference > 0) usedPoints += difference;
        }

        return usedPoints;
    }

    /// <summary>
    /// Obtiene el valor original de un atributo desde HeroData.
    /// </summary>
    /// <param name="heroData">Datos del héroe</param>
    /// <param name="attributeName">Nombre del atributo</param>
    /// <returns>Valor original del atributo</returns>
    private static float GetOriginalValue(HeroData heroData, string attributeName)
    {
        switch (attributeName.ToLower())
        {
            case "strength": return heroData.strength;
            case "dexterity": return heroData.dexterity;
            case "armor": return heroData.armor;
            case "vitality": return heroData.vitality;
            default: return 0f;
        }
    }

    /// <summary>
    /// Calcula los puntos de atributo disponibles para un héroe considerando modificaciones temporales.
    /// </summary>
    /// <param name="heroId">ID del héroe</param>
    /// <param name="heroData">Datos del héroe</param>
    /// <returns>Puntos disponibles después de modificaciones temporales</returns>
    public static int GetAvailablePoints(string heroId, HeroData heroData)
    {
        if (heroData == null) return 0;
        
        int usedTempPoints = CalculateUsedTempPoints(heroId, heroData);
        return heroData.attributePoints - usedTempPoints;
    }
}

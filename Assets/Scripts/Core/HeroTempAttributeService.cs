using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Servicio para manejar modificaciones temporales de atributos del héroe.
/// Mantiene un cache temporal superpuesto sobre el cache principal de DataCacheService.
/// Permite modificar atributos sin alterar los datos persistidos hasta que se confirmen los cambios.
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
    /// <param name="attributeName">Nombre del atributo (ej: "fuerza", "destreza")</param>
    /// <param name="newValue">Nuevo valor temporal</param>
    public static void ApplyTempChange(string heroId, string attributeName, float newValue)
    {
        if (string.IsNullOrEmpty(heroId) || string.IsNullOrEmpty(attributeName))
        {
            Debug.LogWarning("[HeroTempAttributeService] HeroId o attributeName es inválido.");
            return;
        }

        if (!_tempChanges.ContainsKey(heroId))
        {
            _tempChanges[heroId] = new Dictionary<string, float>();
        }
        
        _tempChanges[heroId][attributeName] = newValue;
    }

    /// <summary>
    /// Obtiene los atributos calculados con las modificaciones temporales aplicadas.
    /// Si no hay modificaciones temporales, retorna los atributos del cache normal.
    /// </summary>
    /// <param name="heroId">ID del héroe</param>
    /// <returns>CalculatedAttributes con modificaciones temporales aplicadas</returns>
    public static CalculatedAttributes GetAttributesWithTempChanges(string heroId)
    {
        var baseAttributes = DataCacheService.GetCachedAttributes(heroId);
        
        if (baseAttributes == null)
        {
            Debug.LogWarning($"[HeroTempAttributeService] No se encontraron atributos base para hero: {heroId}");
            return null;
        }

        // Si no hay cambios temporales, devolver atributos base
        if (!_tempChanges.ContainsKey(heroId) || _tempChanges[heroId].Count == 0)
        {
            return baseAttributes;
        }

        // Crear una copia de los atributos base y aplicar cambios temporales
        var modifiedAttributes = new CalculatedAttributes
        {
            maxHealth = baseAttributes.maxHealth,
            stamina = baseAttributes.stamina,
            strength = baseAttributes.strength,
            dexterity = baseAttributes.dexterity,
            vitality = baseAttributes.vitality,
            armor = baseAttributes.armor,
            bluntDamage = baseAttributes.bluntDamage,
            slashingDamage = baseAttributes.slashingDamage,
            piercingDamage = baseAttributes.piercingDamage,
            bluntDefense = baseAttributes.bluntDefense,
            slashDefense = baseAttributes.slashDefense,
            pierceDefense = baseAttributes.pierceDefense,
            bluntPenetration = baseAttributes.bluntPenetration,
            slashPenetration = baseAttributes.slashPenetration,
            piercePenetration = baseAttributes.piercePenetration,
            blockPower = baseAttributes.blockPower,
            movementSpeed = baseAttributes.movementSpeed,
            leadership = baseAttributes.leadership
        };

        // Aplicar modificaciones temporales a atributos base
        var heroChanges = _tempChanges[heroId];
        foreach (var change in heroChanges)
        {
            ApplyTempChangeToCalculatedAttributes(modifiedAttributes, change.Key, change.Value);
        }
        // Recalcular atributos derivados basados en los nuevos valores base
        RecalculateDerivedAttributes(modifiedAttributes, heroId);

        return modifiedAttributes;
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
        if (string.IsNullOrEmpty(heroId))
            return;

        if (_tempChanges.ContainsKey(heroId))
        {
            _tempChanges[heroId].Clear();
        }
    }

    /// <summary>
    /// Obtiene el valor temporal de un atributo específico, o el valor original si no hay cambio temporal.
    /// </summary>
    /// <param name="heroId">ID del héroe</param>
    /// <param name="attributeName">Nombre del atributo</param>
    /// <param name="originalValue">Valor original del atributo</param>
    /// <returns>Valor temporal si existe, valor original si no</returns>
    public static float GetTempAttributeValue(string heroId, string attributeName, float originalValue)
    {
        if (string.IsNullOrEmpty(heroId) || string.IsNullOrEmpty(attributeName))
            return originalValue;

        if (_tempChanges.ContainsKey(heroId) && _tempChanges[heroId].ContainsKey(attributeName))
        {
            return _tempChanges[heroId][attributeName];
        }

        return originalValue;
    }

    /// <summary>
    /// Aplica un cambio temporal específico a un objeto CalculatedAttributes.
    /// </summary>
    private static void ApplyTempChangeToCalculatedAttributes(CalculatedAttributes attributes, string attributeName, float newValue)
    {
        switch (attributeName.ToLower())
        {
            case "strength":
                attributes.strength = newValue;
                break;
            case "dexterity":
                attributes.dexterity = newValue;
                break;
            case "armor":
                attributes.armor = newValue;
                break;
            case "vitality":
                attributes.vitality = newValue;
                break;
            case "leadership":
                attributes.leadership = newValue;
                break;
            default:
                Debug.LogWarning($"[HeroTempAttributeService] Atributo no reconocido para modificación temporal: {attributeName}");
                break;
        }
    }

    /// <summary>
    /// Recalcula los atributos derivados basados en los atributos base modificados temporalmente.
    /// Utiliza la función centralizada de DataCacheService para mantener consistencia total.
    /// </summary>
    private static void RecalculateDerivedAttributes(CalculatedAttributes attributes, string heroId)
    {
        // Obtener la definición de clase del héroe
        var heroData = PlayerSessionService.SelectedHero;
        if (heroData == null)
        {
            Debug.LogWarning($"[HeroTempAttributeService] No se pudo obtener datos del héroe para {heroId}");
            return;
        }

        var classData = HeroClassManager.GetClassDefinition(heroData.classId);
        if (classData == null)
        {
            Debug.LogWarning($"[HeroTempAttributeService] No se pudo obtener definición de clase para {heroData.classId}");
            return;
        }

        // Usar la función centralizada de DataCacheService para calcular atributos derivados
        var calculatedResults = DataCacheService.CalculateDerivedAttributes(classData, 
            attributes.strength, attributes.dexterity, attributes.armor, attributes.vitality, attributes.leadership);

        // Aplicar los resultados calculados a nuestro objeto attributes
        attributes.maxHealth = calculatedResults.maxHealth;
        attributes.stamina = calculatedResults.stamina;
        // Atributos de daño
        attributes.bluntDamage = calculatedResults.bluntDamage;
        attributes.slashingDamage = calculatedResults.slashingDamage;
        attributes.piercingDamage = calculatedResults.piercingDamage;
        // Atributos de penetración
        attributes.bluntPenetration = calculatedResults.bluntPenetration;
        attributes.slashPenetration = calculatedResults.slashPenetration;
        attributes.piercePenetration = calculatedResults.piercePenetration;
        // Atributos de defensa
        attributes.bluntDefense = calculatedResults.bluntDefense;
        attributes.slashDefense = calculatedResults.slashDefense;
        attributes.pierceDefense = calculatedResults.pierceDefense;
        attributes.blockPower = calculatedResults.blockPower;
        attributes.movementSpeed = calculatedResults.movementSpeed;
    }

    /// <summary>
    /// Calcula cuántos puntos de atributo se han usado en los cambios temporales.
    /// </summary>
    /// <param name="heroId">ID del héroe</param>
    /// <param name="heroData">Datos del héroe para obtener valores base</param>
    /// <returns>Número total de puntos usados temporalmente</returns>
    public static int CalculateUsedTempPoints(string heroId, HeroData heroData)
    {
        if (string.IsNullOrEmpty(heroId) || heroData == null || !_tempChanges.ContainsKey(heroId))
        {
            return 0;
        }

        int totalUsedPoints = 0;
        var heroChanges = _tempChanges[heroId];

        foreach (var change in heroChanges)
        {
            float baseValue = HeroAttributeValidator.GetCurrentAttributeValue(heroData, change.Key);
            float tempValue = change.Value;
            int pointsUsed = Mathf.RoundToInt(tempValue - baseValue);
            
            // Solo contamos puntos positivos (incrementos)
            if (pointsUsed > 0)
                totalUsedPoints += pointsUsed;
        }

        return totalUsedPoints;
    }

    /// <summary>
    /// Calcula cuántos puntos de atributo están disponibles considerando cambios temporales.
    /// </summary>
    /// <param name="heroId">ID del héroe</param>
    /// <param name="heroData">Datos del héroe</param>
    /// <returns>Puntos disponibles después de considerar cambios temporales</returns>
    public static int GetAvailablePoints(string heroId, HeroData heroData)
    {
        if (heroData == null)
            return 0;

        int usedTempPoints = CalculateUsedTempPoints(heroId, heroData);
        int available = Mathf.Max(0, heroData.attributePoints - usedTempPoints);
        
        return available;
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Provides cached access to dynamic hero data such as calculated attributes,
/// unlocked formations and active perks. This avoids recalculating values on
/// every scene load or gameplay tick.
/// </summary>
public static class DataCacheService
{
    // Constants used for attribute calculations. These values should be tuned
    // according to the GDD formulas and game balance requirements.
    static class Constants
    {
        public const float BASE_HEALTH = 100f;
        public const float BASE_STAMINA = 100f;
        public const float BASE_DAMAGE = 5f;
        public const float BASE_ARMOR = 0f;

        public const float BLUNT_DAMAGE_MULTIPLIER = 2f;
        public const float PIERCING_DAMAGE_MULTIPLIER = 2f;

        public const float PENETRATION_BASE = 0f;
        public const float BLOCK_POWER_MULTIPLIER = 0.5f;
        public const float MOVEMENT_SPEED_BASE = 5f;
        public const float MOVEMENT_SPEED_DEX_MULTIPLIER = 0.1f;
    }
    // Cache of calculated attributes by hero identifier.
    static readonly Dictionary<string, CalculatedAttributes> _attributeCache = new();

    // Cache of unlocked formation IDs per squad instance.
    static readonly Dictionary<string, List<string>> _formationCache = new();

    // Cache of active perk identifiers by hero.
    static readonly Dictionary<string, List<string>> _perkCache = new();

    /// <summary>
    /// Clears all cached information. Useful when reloading player data.
    /// </summary>
    public static void Clear()
    {
        _attributeCache.Clear();
        _formationCache.Clear();
        _perkCache.Clear();
    }

    /// <summary>
    /// Calculates and stores the derived attributes for the given hero.
    /// </summary>
    /// <param name="heroData">Hero progression data.</param>
    public static void CacheAttributes(HeroData heroData)
    {
        if (heroData == null)
            return;

        var calculated = CalculateAttributes(heroData);
        string key = GetHeroKey(heroData);
        _attributeCache[key] = calculated;
    }

    /// <summary>
    /// Retrieves previously cached attributes for a hero.
    /// </summary>
    /// <param name="heroId">Unique identifier or name of the hero.</param>
    public static CalculatedAttributes GetCachedAttributes(string heroId)
    {
        if (string.IsNullOrEmpty(heroId))
            return null;
        _attributeCache.TryGetValue(heroId, out var attributes);
        return attributes;
    }

    /// <summary>
    /// Stores the list of unlocked formation IDs for a squad instance.
    /// </summary>
    public static void CacheFormations(string squadId, List<string> formations)
    {
        if (string.IsNullOrEmpty(squadId) || formations == null)
            return;
        _formationCache[squadId] = new List<string>(formations);
    }

    /// <summary>
    /// Returns the cached formation IDs for the requested squad.
    /// </summary>
    public static List<string> GetUnlockedFormations(string squadId)
    {
        if (string.IsNullOrEmpty(squadId))
            return null;
        _formationCache.TryGetValue(squadId, out var list);
        return list;
    }

    /// <summary>
    /// Stores the list of currently active perk IDs for a hero.
    /// </summary>
    public static void CachePerks(string heroId, List<string> perks)
    {
        if (string.IsNullOrEmpty(heroId) || perks == null)
            return;
        _perkCache[heroId] = new List<string>(perks);
    }

    /// <summary>
    /// Returns the cached active perk IDs for the requested hero.
    /// </summary>
    public static List<string> GetActivePerks(string heroId)
    {
        if (string.IsNullOrEmpty(heroId))
            return null;
        _perkCache.TryGetValue(heroId, out var list);
        return list;
    }

    // Recalcula los atributos cacheados para todos los héroes del jugador.
    public static void RecalculateAttributes(PlayerData data)
    {
        Clear();
        if (data == null || data.heroes == null)
            return;
        foreach (var hero in data.heroes)
        {
            CacheAttributes(hero);
        }
    }

    // Helper --------------------------------------------------------------

    // Generates a unique key for the hero based on available data.
    static string GetHeroKey(HeroData heroData)
    {
        // If the project later introduces an explicit ID use it here.
        return string.IsNullOrEmpty(heroData.heroName) ? heroData.classId : heroData.heroName;
    }

    // Performs the actual attribute calculations using simple formulas
    // described in the GDD. Real implementation should include equipment
    // and perk modifiers.
    static CalculatedAttributes CalculateAttributes(HeroData hero)
    {
        var result = new CalculatedAttributes();

        // Base values (could come from the hero class definition)
        const float baseHealth = Constants.BASE_HEALTH;
        const float baseStamina = Constants.BASE_STAMINA;
        const float baseDamage = Constants.BASE_DAMAGE;
        const float baseArmor = Constants.BASE_ARMOR;

        // Primary attributes
        float fuerza = hero.fuerza;
        float destreza = hero.destreza;
        float armadura = hero.armadura;
        float vitalidad = hero.vitalidad;

        // Derived stats following GDD section 5.2
        result.maxHealth = baseHealth + vitalidad;
        result.stamina = baseStamina + destreza;

        result.strength = fuerza;
        result.dexterity = destreza;
        result.vitality = vitalidad;
        result.armor = baseArmor + armadura;

        result.bluntDamage = baseDamage + (Constants.BLUNT_DAMAGE_MULTIPLIER * fuerza);
        result.slashingDamage = baseDamage + fuerza + destreza;
        result.piercingDamage = baseDamage + (Constants.PIERCING_DAMAGE_MULTIPLIER * destreza);

        result.bluntDefense = baseArmor + armadura;
        result.slashDefense = baseArmor + armadura;
        result.pierceDefense = baseArmor + armadura;

        result.bluntPenetration = Constants.PENETRATION_BASE;
        result.slashPenetration = Constants.PENETRATION_BASE;
        result.piercePenetration = Constants.PENETRATION_BASE;

        result.blockPower = fuerza * Constants.BLOCK_POWER_MULTIPLIER;
        result.movementSpeed = Constants.MOVEMENT_SPEED_BASE + destreza * Constants.MOVEMENT_SPEED_DEX_MULTIPLIER;

        return result;
    }
}

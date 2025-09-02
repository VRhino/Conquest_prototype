using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MonoBehaviour de debugging para visualizar el cache de atributos de héroes en el Inspector.
/// Útil para development y testing. Solo debe usarse en builds de desarrollo.
/// </summary>
[System.Serializable]
public class CachedHeroInfo
{
    [Header("Hero Identity")]
    public string heroId;
    public string heroName;
    public string classId;

    [Header("Primary Attributes")]
    public float strength;
    public float dexterity;
    public float vitality;
    public float armor;

    [Header("Health & Stamina")]
    public float maxHealth;
    public float stamina;

    [Header("Damage Stats")]
    public float bluntDamage;
    public float slashingDamage;
    public float piercingDamage;

    [Header("Defense Stats")]
    public float bluntDefense;
    public float slashDefense;
    public float pierceDefense;

    [Header("Penetration Stats")]
    public float bluntPenetration;
    public float slashPenetration;
    public float piercePenetration;

    [Header("Other Stats")]
    public float blockPower;
    public float movementSpeed;
    public float leadership;

    public void UpdateFromCalculatedAttributes(string id, CalculatedAttributes attributes)
    {
        heroId = id;
        
        if (PlayerSessionService.SelectedHero != null && PlayerSessionService.SelectedHero.heroName == id)
        {
            heroName = PlayerSessionService.SelectedHero.heroName;
            classId = PlayerSessionService.SelectedHero.classId;
        }

        if (attributes != null)
        {
            strength = attributes.strength;
            dexterity = attributes.dexterity;
            vitality = attributes.vitality;
            armor = attributes.armor;
            maxHealth = attributes.maxHealth;
            stamina = attributes.stamina;
            bluntDamage = attributes.bluntDamage;
            slashingDamage = attributes.slashingDamage;
            piercingDamage = attributes.piercingDamage;
            bluntDefense = attributes.bluntDefense;
            slashDefense = attributes.slashDefense;
            pierceDefense = attributes.pierceDefense;
            bluntPenetration = attributes.bluntPenetration;
            slashPenetration = attributes.slashPenetration;
            piercePenetration = attributes.piercePenetration;
            blockPower = attributes.blockPower;
            movementSpeed = attributes.movementSpeed;
            leadership = attributes.leadership;
        }
    }
}

public class HeroCacheDebugger : MonoBehaviour
{
    [Header("Cache Debug Info")]
    [SerializeField] private bool enableAutoRefresh = true;
    [SerializeField] private float refreshInterval = 1.0f;
    [SerializeField] private bool showOnlySelectedHero = true;

    [Header("Current Hero Cache")]
    [SerializeField] private CachedHeroInfo currentHero = new();

    [Header("All Cached Heroes")]
    [SerializeField] private List<CachedHeroInfo> allCachedHeroes = new();

    [Header("Debug Actions")]
    [SerializeField] private bool refreshNow = false;
    [SerializeField] private bool clearCache = false;

    private float _lastRefreshTime;

    void Start()
    {
        RefreshCacheData();
    }

    void Update()
    {
        // Handle debug actions
        if (refreshNow)
        {
            refreshNow = false;
            RefreshCacheData();
        }

        if (clearCache)
        {
            clearCache = false;
            DataCacheService.Clear();
            RefreshCacheData();
        }

        // Auto-refresh
        if (enableAutoRefresh && Time.time - _lastRefreshTime > refreshInterval)
        {
            RefreshCacheData();
        }
    }

    void RefreshCacheData()
    {
        _lastRefreshTime = Time.time;

        // Clear previous data
        allCachedHeroes.Clear();

        // Get selected hero data
        if (PlayerSessionService.SelectedHero != null)
        {
            string selectedHeroId = string.IsNullOrEmpty(PlayerSessionService.SelectedHero.heroName) 
                ? PlayerSessionService.SelectedHero.classId 
                : PlayerSessionService.SelectedHero.heroName;

            var selectedAttributes = DataCacheService.GetCachedAttributes(selectedHeroId);
            currentHero.UpdateFromCalculatedAttributes(selectedHeroId, selectedAttributes);

            if (showOnlySelectedHero)
            {
                if (selectedAttributes != null)
                {
                    var heroInfo = new CachedHeroInfo();
                    heroInfo.UpdateFromCalculatedAttributes(selectedHeroId, selectedAttributes);
                    allCachedHeroes.Add(heroInfo);
                }
            }
        }

        // If showing all heroes or no selected hero, try to get all cached data
        if (!showOnlySelectedHero || PlayerSessionService.SelectedHero == null)
        {
            // Since DataCacheService doesn't expose all cached keys, we'll work with available player data
            if (PlayerSessionService.CurrentPlayer?.heroes != null)
            {
                foreach (var hero in PlayerSessionService.CurrentPlayer.heroes)
                {
                    string heroKey = string.IsNullOrEmpty(hero.heroName) ? hero.classId : hero.heroName;
                    var attributes = DataCacheService.GetCachedAttributes(heroKey);
                    
                    if (attributes != null)
                    {
                        var heroInfo = new CachedHeroInfo();
                        heroInfo.UpdateFromCalculatedAttributes(heroKey, attributes);
                        heroInfo.heroName = hero.heroName;
                        heroInfo.classId = hero.classId;
                        allCachedHeroes.Add(heroInfo);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Fuerza un recálculo completo del cache de atributos.
    /// </summary>
    [ContextMenu("Force Recalculate All Attributes")]
    public void ForceRecalculateAttributes()
    {
        if (PlayerSessionService.SelectedHero != null)
        {
            DataCacheService.RecalculateAttributes(PlayerSessionService.SelectedHero);
            RefreshCacheData();
            Debug.Log("[HeroCacheDebugger] Forced recalculation of all hero attributes");
        }
        else
        {
            Debug.LogWarning("[HeroCacheDebugger] No player data available for recalculation");
        }
    }

    /// <summary>
    /// Limpia el cache y lo recalcula.
    /// </summary>
    [ContextMenu("Clear and Refresh Cache")]
    public void ClearAndRefreshCache()
    {
        DataCacheService.Clear();
        ForceRecalculateAttributes();
    }

    /// <summary>
    /// Compara atributos temporales vs cacheados para el héroe seleccionado.
    /// </summary>
    [ContextMenu("Compare Temp vs Cached Attributes")]
    public void CompareTempVsCached()
    {
        if (PlayerSessionService.SelectedHero == null)
        {
            Debug.LogWarning("[HeroCacheDebugger] No selected hero for comparison");
            return;
        }

        string heroId = string.IsNullOrEmpty(PlayerSessionService.SelectedHero.heroName) 
            ? PlayerSessionService.SelectedHero.classId 
            : PlayerSessionService.SelectedHero.heroName;

        var cachedAttributes = DataCacheService.GetCachedAttributes(heroId);
        var tempAttributes = HeroTempAttributeService.GetAttributesWithTempChanges(heroId);
        
        Debug.Log($"[HeroCacheDebugger] Comparison for hero {heroId}:");
        
        if (cachedAttributes != null && tempAttributes != null)
        {
            Debug.Log($"  Health - Cached: {cachedAttributes.maxHealth:F1}, Temp: {tempAttributes.maxHealth:F1}");
            Debug.Log($"  Stamina - Cached: {cachedAttributes.stamina:F1}, Temp: {tempAttributes.stamina:F1}");
            Debug.Log($"  Strength - Cached: {cachedAttributes.strength:F1}, Temp: {tempAttributes.strength:F1}");
            Debug.Log($"  Has Temp Changes: {HeroTempAttributeService.HasTempChanges(heroId)}");
        }
        else
        {
            Debug.LogWarning("  Missing cached or temp attributes data");
        }
    }

    void OnValidate()
    {
        // Ensure refresh interval is reasonable
        refreshInterval = Mathf.Clamp(refreshInterval, 0.1f, 10f);
    }
}

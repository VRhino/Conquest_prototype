using System;
using UnityEngine;

/// <summary>
/// Loads the in-repo test hero pool from Resources/Debug/TestHeroPool.json.
/// Used by BattleDebugCreator to provide real HeroData for remote hero spawning in test environments.
/// </summary>
public static class TestHeroPool
{
    [Serializable]
    private class Wrapper { public HeroData[] heroes; }

    public static HeroData[] Load()
    {
        var asset = Resources.Load<TextAsset>("Debug/TestHeroPool");
        if (asset == null)
        {
            Debug.LogWarning("[TestHeroPool] TestHeroPool.json not found in Resources/Debug/");
            return Array.Empty<HeroData>();
        }
        var wrapper = JsonUtility.FromJson<Wrapper>(asset.text);
        return wrapper?.heroes ?? Array.Empty<HeroData>();
    }

    public static HeroData GetRandom()
    {
        var heroes = Load();
        if (heroes.Length == 0) return null;
        return heroes[UnityEngine.Random.Range(0, heroes.Length)];
    }
}

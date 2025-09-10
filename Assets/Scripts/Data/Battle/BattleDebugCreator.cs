using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Utility class for creating BattleData instances for debugging purposes.
/// Provides methods to quickly set up battles with local heroes and test scenarios.
/// </summary>
public static class BattleDebugCreator
{
    /// <summary>
    /// Creates a BattleData instance with the specified local hero as the first attacker.
    /// Converts the HeroData to BattleHeroData and places it at index 0 of attackers.
    /// </summary>
    /// <param name="localHero">The hero data to convert and add as the first attacker</param>
    /// <returns>A new BattleData instance with the hero configured as attacker</returns>
    public static BattleData CreateBattleWithLocalHero(HeroData localHero)
    {
        if (localHero == null)
        {
            Debug.LogError("BattleDebugCreator: localHero cannot be null");
            return null;
        }

        // Create new battle data
        BattleData battleData = CreateTestBattle();

        // Convert HeroData to BattleHeroData
        BattleHeroData battleHero = ConvertHeroToBattleHero(localHero);

        // Add as first attacker
        battleData.attackers.Insert(0, battleHero);

        Debug.Log($"BattleDebugCreator: Created battle with hero '{battleHero.heroName}' as first attacker");
        return battleData;
    }

    /// <summary>
    /// Converts a HeroData instance to BattleHeroData for battle scenarios.
    /// </summary>
    /// <param name="heroData">The hero data to convert</param>
    /// <returns>A new BattleHeroData instance with converted data</returns>
    private static BattleHeroData ConvertHeroToBattleHero(HeroData heroData)
    {
        BattleHeroData battleHero = new BattleHeroData
        {
            heroName = heroData.heroName,
            classID = heroData.classId,
            level = heroData.level,
            squadInstances = new List<SquadInstanceData>()
        };

        // Convert active Loadout to SquadInstances
        if (heroData.loadouts != null && heroData.loadouts.Count > 0)
        {
            LoadoutSaveData activeLoadout = heroData.loadouts[0]; // Assuming first loadout is active
            foreach (var squadInstanceID in activeLoadout.squadInstanceIDs)
            {
                // Find the SquadInstanceData by ID and add it to the battle hero
                SquadInstanceData squadInstance = heroData.squadProgress.Find(s => s.id == squadInstanceID);
                if (squadInstance != null) battleHero.squadInstances.Add(squadInstance);
            }
        }

        return battleHero;
    }

    /// <summary>
    /// Creates a test battle with mock data for debugging purposes.
    /// </summary>
    /// <returns>A BattleData instance with test data</returns>
    public static BattleData CreateTestBattle()
    {
        BattleData battleData = new BattleData();
        battleData.battleID = Guid.NewGuid().ToString();

        // Create mock attackers
        for (int i = 0; i < 14; i++)
        {
            BattleHeroData attacker = CreateRandomMockBattleHero();
            battleData.attackers.Add(attacker);
        }
        // Create mock defenders
        for (int i = 0; i < 15; i++)
        {
            BattleHeroData defender = CreateRandomMockBattleHero();
            battleData.defenders.Add(defender);
        }

        Debug.Log("BattleDebugCreator: Created test battle with mock data");
        return battleData;
    }
    public static void GenerateRandomSquadInstances(List<string> validSquadIDs, out List<SquadInstanceData> randomSquads)
    {
        randomSquads = new List<SquadInstanceData>();
        int squadCount = UnityEngine.Random.Range(1, 4); // 1 to 3 squads

        for (int i = 0; i < squadCount; i++)
        {
            string randomSquadID = validSquadIDs[UnityEngine.Random.Range(0, validSquadIDs.Count)];
            int randomLevel = UnityEngine.Random.Range(1, 5);
            int randomUnits = UnityEngine.Random.Range(5, 21);

            SquadInstanceData squadInstance = new SquadInstanceData
            {
                id = System.Guid.NewGuid().ToString(),
                baseSquadID = randomSquadID,
                level = randomLevel,
                unitsInSquad = randomUnits,
                experience = 0,
                unlockedAbilities = new List<string>(),
                permittedFormationIndexes = new List<int> { 0 },
                selectedFormationIndex = 0
            };

            randomSquads.Add(squadInstance);
        }
    }

    public static BattleHeroData CreateRandomMockBattleHero()
    {
        List<string> validSquadIDs = new List<string> { "sqd01", "arc01", "spm01" };
        List<string> validClassIDs = new List<string> { "SwordAndShield", "TwoHandedSword", "Bow", "Spear" };
        string randomHeroName = $"Hero_{UnityEngine.Random.Range(1000, 9999)}";
        string randomClassID = validClassIDs[UnityEngine.Random.Range(0, validClassIDs.Count)];
        int randomLevel = UnityEngine.Random.Range(1, 10);

        GenerateRandomSquadInstances(validSquadIDs, out List<SquadInstanceData> randomSquads);

        BattleHeroData battleHero = new BattleHeroData
        {
            heroName = randomHeroName,
            classID = randomClassID,
            level = randomLevel,
            squadInstances = new List<SquadInstanceData>(randomSquads)
        };

        return battleHero;
    }

}

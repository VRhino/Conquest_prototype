using System;
using System.Collections.Generic;
using Data.Maps;
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
        => CreateBattleWithLocalHero(localHero, Team.TeamA, 4, 5, EnemySquadMode.Random, null);

    public static BattleData CreateBattleWithLocalHero(HeroData localHero, Team playerTeam, int attackerCount, int defenderCount)
        => CreateBattleWithLocalHero(localHero, playerTeam, attackerCount, defenderCount, EnemySquadMode.Random, null);

    public static BattleData CreateBattleWithLocalHero(HeroData localHero, Team playerTeam, int attackerCount, int defenderCount, EnemySquadMode squadMode, SquadData squadOverride)
    {
        if (localHero == null)
        {
            Debug.LogError("BattleDebugCreator: localHero cannot be null");
            return null;
        }

        List<string> validSquadIDs;
        if (squadMode == EnemySquadMode.Fixed && squadOverride != null)
            validSquadIDs = new List<string> { squadOverride.id };
        else
            validSquadIDs = SquadDataService.GetAllSquads().ConvertAll(s => s.id);

        BattleData battleData = CreateTestBattle(attackerCount, defenderCount, validSquadIDs);
        BattleHeroData battleHero = ConvertHeroToBattleHero(localHero);

        if (playerTeam == Team.TeamB)
        {
            battleHero.spawnPointId = "4"; // First defender spawn
            battleData.defenders.Insert(0, battleHero);
        }
        else
        {
            battleHero.spawnPointId = "1"; // First attacker spawn
            battleData.attackers.Insert(0, battleHero);
        }

        string teamName = playerTeam == Team.TeamB ? "defender" : "attacker";
        Debug.Log($"BattleDebugCreator: Created battle with hero '{battleHero.heroName}' as first {teamName}");
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
            squadInstances = new List<SquadInstanceData>(),
            spawnPointId = string.Empty, // Assigned by caller based on team
            avatar = heroData.avatar,
            equipment = heroData.equipment,
            gender = heroData.gender
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
    public static BattleData CreateTestBattle() => CreateTestBattle(4, 5);

    public static BattleData CreateTestBattle(int attackerCount, int defenderCount)
        => CreateTestBattle(attackerCount, defenderCount, SquadDataService.GetAllSquads().ConvertAll(s => s.id));

    private static BattleData CreateTestBattle(int attackerCount, int defenderCount, List<string> validSquadIDs)
    {
        BattleData battleData = new BattleData();
        battleData.battleID = Guid.NewGuid().ToString();

        for (int i = 0; i < attackerCount; i++)
            battleData.attackers.Add(CreateRandomMockBattleHero(1, 3, validSquadIDs)); // Attacker spawns 1-3

        for (int i = 0; i < defenderCount; i++)
            battleData.defenders.Add(CreateRandomMockBattleHero(4, 6, validSquadIDs)); // Defender spawns 4-6

        battleData.mapData = MapService.GetMapById("default");
        battleData.BattleTimer = battleData.mapData != null ? battleData.mapData.battleDuration : 900;
        Debug.Log($"BattleDebugCreator: Created test battle with map '{battleData.mapData.name}'");
        return battleData;
    }
    public static void GenerateRandomSquadInstances(List<string> validSquadIDs, out List<SquadInstanceData> randomSquads)
    {
        randomSquads = new List<SquadInstanceData>();
        int squadCount = UnityEngine.Random.Range(1, 4); // 1 to 3 squads

        for (int i = 0; i < squadCount; i++)
        {
            string randomSquadID = validSquadIDs[UnityEngine.Random.Range(0, validSquadIDs.Count)];
            var squadInstance = SquadDataService.CreateSquadInstance(randomSquadID);
            if (squadInstance == null) continue;

            // Sobrescribir valores con datos aleatorios para debug
            squadInstance.level = UnityEngine.Random.Range(1, 5);
            squadInstance.unitsInSquad = UnityEngine.Random.Range(5, 21);

            randomSquads.Add(squadInstance);
        }
    }

    public static BattleHeroData CreateRandomMockBattleHero(int minSpawnId = 1, int maxSpawnId = 3)
        => CreateRandomMockBattleHero(minSpawnId, maxSpawnId, SquadDataService.GetAllSquads().ConvertAll(s => s.id));

    public static BattleHeroData CreateRandomMockBattleHero(int minSpawnId, int maxSpawnId, List<string> validSquadIDs)
    {
        HeroData poolHero = TestHeroPool.GetRandom();

        string heroName = poolHero != null ? poolHero.heroName : $"Hero_{UnityEngine.Random.Range(1000, 9999)}";
        string classID  = poolHero != null ? poolHero.classId  : "SwordAndShield";
        int    level    = UnityEngine.Random.Range(1, 10);

        GenerateRandomSquadInstances(validSquadIDs, out List<SquadInstanceData> randomSquads);

        BattleHeroData battleHero = new BattleHeroData
        {
            heroName      = heroName,
            classID       = classID,
            level         = level,
            squadInstances = new List<SquadInstanceData>(randomSquads),
            spawnPointId  = UnityEngine.Random.Range(minSpawnId, maxSpawnId + 1).ToString(),
            avatar        = poolHero?.avatar    ?? new AvatarParts(),
            equipment     = poolHero?.equipment ?? new Equipment(),
            gender        = poolHero?.gender    ?? "Male"
        };

        return battleHero;
    }

}

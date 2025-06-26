using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Simple utility that stores and loads player progress in JSON format
/// under the application's persistent data path.
/// </summary>
public static class LocalSaveSystem
{
    /// <summary>Container for all player progress data.</summary>
    [Serializable]
    public class PlayerProgressData
    {
        public List<LoadoutData> loadouts = new();
        public HeroProgressData heroProgress = new();
    }

    /// <summary>Serializable representation of the hero progression.</summary>
    [Serializable]
    public class HeroProgressData
    {
        public int level = 1;
        public int currentXP = 0;
        public int xpToNextLevel = 100;
        public int perkPoints = 0;
    }

    /// <summary>Serializable representation of a loadout.</summary>
    [Serializable]
    public class LoadoutData
    {
        public int loadoutID;
        public List<int> squadIDs = new();
        public List<int> perkIDs = new();
        public int leadershipUsed;
    }

    static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");

    /// <summary>Loads the player progress file if it exists.</summary>
    public static PlayerProgressData LoadGame()
    {
        if (!File.Exists(FilePath))
            return new PlayerProgressData();

        try
        {
            string json = File.ReadAllText(FilePath);
            return JsonUtility.FromJson<PlayerProgressData>(json);
        }
        catch
        {
            return new PlayerProgressData();
        }
    }

    /// <summary>Saves the given progress data to disk.</summary>
    public static void SaveGame(PlayerProgressData data)
    {
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(FilePath, json);
    }
}


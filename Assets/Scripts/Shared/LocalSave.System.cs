using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Utility used to persist the player's progression locally in JSON format.
/// </summary>
public static class LocalSaveSystem
{
    /// <summary>Structure containing the player's progress.</summary>
    [Serializable]
    public class PlayerProgressData
    {
        public int level = 1;
        public int currentXP = 0;
        public int perkPoints = 0;
        public List<LoadoutData> loadouts = new();
        /// <summary>Progress for each squad instance owned by the hero.</summary>
        public List<SquadInstanceData> squads = new();
    }

    /// <summary>Serializable representation of a loadout.</summary>
    [Serializable]
    public class LoadoutData
    {
        public string name = string.Empty;
        public List<int> squadIDs = new();
        public List<int> perkIDs = new();
        public int totalLeadership = 0;
    }

    /// <summary>Persistent state for a particular squad instance.</summary>
    [Serializable]
    public class SquadInstanceData
    {
        public int id;
        public SquadType squadType;
        public int level = 1;
        public float currentXP = 0f;
        public float armorPercent = 100f;
        public List<int> unlockedAbilities = new();
        public List<FormationType> unlockedFormations = new();
    }

    static string FilePath => Path.Combine(Application.persistentDataPath, "player_progress.json");

    /// <summary>Loads the player progress file or returns a new instance.</summary>
    public static PlayerProgressData LoadProgress()
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

    /// <summary>Serializes the given data to disk.</summary>
    public static void SaveProgress(PlayerProgressData data)
    {
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(FilePath, json);
    }
}


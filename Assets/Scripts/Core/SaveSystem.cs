using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Provides methods to persist <see cref="PlayerData"/> locally in JSON format.
/// </summary>
public static class SaveSystem
{
    /// <summary>Serializes the player data to disk.</summary>
    /// <param name="data">Data to save.</param>
    public static void SavePlayer(PlayerData data)
    {
        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SaveFileConfig.FilePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save player data: {e}");
        }
    }
}

/// <summary>
/// Internal helper holding save path configuration.
/// </summary>
static class SaveFileConfig
{
    const string FileName = "player_save.json";
    internal static string FilePath => Path.Combine(Application.persistentDataPath, FileName);
}

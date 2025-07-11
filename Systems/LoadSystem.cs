using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Handles loading of <see cref="PlayerData"/> from persistent storage.
/// </summary>
public static class LoadSystem
{
    /// <summary>Loads player data if a save file exists.</summary>
    /// <returns>Deserialized <see cref="PlayerData"/> or null.</returns>
    public static PlayerData LoadPlayer()
    {
        if (!File.Exists(SaveFileConfig.FilePath))
            return null;

        try
        {
            string json = File.ReadAllText(SaveFileConfig.FilePath);
            return JsonUtility.FromJson<PlayerData>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load player data: {e}");
            return null;
        }
    }
}

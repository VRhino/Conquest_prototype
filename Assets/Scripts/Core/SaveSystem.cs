
using System;
using System.IO;
using UnityEngine;

namespace Core.Persistence
{
    public interface ISaveProvider
    {
        void Save(PlayerData data);
        PlayerData Load();
    }

    public class LocalSaveProvider : ISaveProvider
    {
        private readonly string filePath;
        public LocalSaveProvider(string customPath = null)
        {
            filePath = customPath ?? Path.Combine(Application.persistentDataPath, "player_save.json");
        }
        public void Save(PlayerData data)
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(filePath, json);
        }
        public PlayerData Load()
        {
            if (!File.Exists(filePath)) return null;
            string json = File.ReadAllText(filePath);
            return JsonUtility.FromJson<PlayerData>(json);
        }
    }
}

public static class SaveSystem
{
    private static Core.Persistence.ISaveProvider _provider = new Core.Persistence.LocalSaveProvider();

    /// <summary>
    /// Permite cambiar el proveedor de guardado (ej: para CloudSaveProvider en el futuro).
    /// </summary>
    public static void SetProvider(Core.Persistence.ISaveProvider provider)
    {
        _provider = provider ?? new Core.Persistence.LocalSaveProvider();
    }

    /// <summary>Serializa y guarda los datos del jugador usando el proveedor actual.</summary>
    public static void SavePlayer(PlayerData data)
    {
        if (data == null)
        {
            Debug.LogError("No se puede guardar: PlayerData es null.");
            return;
        }
        try
        {
            data.ClearCachedAttributes();
            _provider.Save(data);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save player data: {e}");
        }
    }
}

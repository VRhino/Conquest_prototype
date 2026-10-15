using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Atomic JSON storage with field-scoped transactions and injectable file location.</summary>
public sealed class ProgressFileStore
{
    readonly string path;
    readonly Core.Persistence.LocalSaveProvider profile;
    static object Gate => Core.Persistence.LocalSaveProvider.Gate;
    public ProgressFileStore(string filePath, string profilePath = null)
    {
        path = Path.GetFullPath(filePath);
        if (profilePath != null) profile = new Core.Persistence.LocalSaveProvider(profilePath, path);
    }

    public LocalSaveSystem.PlayerProgressData Load()
    {
        lock (Gate)
        {
            var canonical = profile?.Load();
            if (canonical != null) return canonical.battleProgress;
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return new LocalSaveSystem.PlayerProgressData();
            try { return Read(path); }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is InvalidDataException)
            {
                if (File.Exists(path + ".bak")) return Read(path + ".bak");
                throw new InvalidDataException("Cannot read progress; refusing to replace it with an empty save.", error);
            }
        }
    }

    static LocalSaveSystem.PlayerProgressData Read(string file)
    {
        var data = JsonUtility.FromJson<LocalSaveSystem.PlayerProgressData>(File.ReadAllText(file));
        if (data == null) throw new InvalidDataException("Empty progress document.");
        data.squads ??= new List<LocalSaveSystem.SquadInstanceData>();
        data.loadouts ??= new List<LocalSaveSystem.LoadoutData>();
        return data;
    }

    public void Save(LocalSaveSystem.PlayerProgressData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        lock (Gate)
        {
            if (profile != null && profile.SaveBattleProgress(data)) return;
            AtomicJsonFile.Write(path, data);
        }
    }

    public void Update(Action<LocalSaveSystem.PlayerProgressData> update)
    {
        if (update == null) throw new ArgumentNullException(nameof(update));
        lock (Gate)
        {
            var latest = Load();
            update(latest);
            Save(latest);
        }
    }
}

public static class AtomicJsonFile
{
    public static void Write(string filePath, object data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        string path = Path.GetFullPath(filePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonUtility.ToJson(data, true));
        if (File.Exists(path)) File.Replace(temporaryPath, path, path + ".bak");
        else File.Move(temporaryPath, path);
    }
}

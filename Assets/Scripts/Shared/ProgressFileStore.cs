using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Atomic JSON storage with field-scoped transactions and injectable file location.</summary>
public sealed class ProgressFileStore
{
    static readonly object Gate = new object();
    readonly string path;
    public ProgressFileStore(string filePath) => path = Path.GetFullPath(filePath);

    public LocalSaveSystem.PlayerProgressData Load()
    {
        lock (Gate)
        {
            if (!File.Exists(path)) return new LocalSaveSystem.PlayerProgressData();
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

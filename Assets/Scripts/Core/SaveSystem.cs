
using System;
using System.IO;
using System.Collections.Generic;
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
        // Reuse the neutral hero DTO to inspect serialized IDs without HeroData's GUID initializer.
        [Serializable]
        sealed class SerializedHeroIds { public List<BattleHeroData> heroes; }
        public const int CurrentVersion = 3;
        public const string ProfileFileName = "player_save.json";
        public const string LegacyProgressFileName = "player_progress.json";
        internal static readonly object Gate = new object();
        private readonly string filePath;
        private readonly string progressPath;
        public LocalSaveProvider(string customPath = null, string legacyProgressPath = null)
        {
            filePath = Path.GetFullPath(customPath ?? Path.Combine(Application.persistentDataPath, ProfileFileName));
            progressPath = legacyProgressPath ?? Path.Combine(Path.GetDirectoryName(filePath), LegacyProgressFileName);
        }
        public void Save(PlayerData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            lock (Gate)
            {
                ValidateVersion(data);
                // Inventory may hold an older snapshot: progression has its own transaction.
                var latest = Load();
                if (data.heroes != null)
                    foreach (var hero in data.heroes)
                    {
                        if (hero == null) continue;
                        var saved = HeroDataService.FindById(latest, hero.heroId);
                        if (saved == null) continue;
                        if (hero.progressRevision < saved.progressRevision) CopyProgress(saved, hero);
                        if (hero.squadProgress == null) continue;
                        foreach (var squad in hero.squadProgress)
                        {
                            if (squad == null) continue;
                            var savedSquad = SquadDataService.FindInstanceById(saved, squad.id);
                            if (savedSquad != null && squad.progressRevision < savedSquad.progressRevision)
                                CopySquadProgress(savedSquad, squad);
                        }
                    }
                var progress = latest?.battleProgress ?? data.battleProgress ?? LoadLegacyProgress();
                data.saveVersion = CurrentVersion;
                data.battleProgress = progress;
                AtomicJsonFile.Write(filePath, data);
                data.requiresIdentitySave = false;
            }
        }
        public PlayerData Load()
        {
            lock (Gate)
            {
                if (!File.Exists(filePath) && !File.Exists(filePath + ".bak")) return null;
                PlayerData data;
                try { data = ReadProfile(filePath); }
                catch (Exception error) when (error is IOException || error is ArgumentException)
                {
                    if (!File.Exists(filePath + ".bak"))
                        throw new InvalidDataException("Cannot read profile; refusing to replace it with an empty save.", error);
                    data = ReadProfile(filePath + ".bak");
                }
                // Unity JSON materializes a default nested object even for a null/missing field.
                // The version, rather than null alone, decides whether this block was imported.
                if (data.saveVersion == 0) data.battleProgress = LoadLegacyProgress();
                else data.battleProgress ??= LoadLegacyProgress();
                data.saveVersion = CurrentVersion;
                return data;
            }
        }

        internal bool SaveBattleProgress(LocalSaveSystem.PlayerProgressData progress)
        {
            lock (Gate)
            {
                var latest = Load();
                if (latest == null) return false; // No profile yet: keep the legacy writer available.
                latest.battleProgress = progress;
                AtomicJsonFile.Write(filePath, latest);
                return true;
            }
        }

        LocalSaveSystem.PlayerProgressData LoadLegacyProgress() => new ProgressFileStore(progressPath).Load();

        static PlayerData ReadProfile(string path)
        {
            string json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<PlayerData>(json);
            if (data == null) throw new InvalidDataException("Empty profile document.");
            ValidateVersion(data);
            var identities = JsonUtility.FromJson<SerializedHeroIds>(json);
            if (identities?.heroes != null)
                foreach (var hero in identities.heroes)
                    if (hero != null && string.IsNullOrEmpty(hero.heroId)) data.requiresIdentitySave = true;
            return data;
        }

        static void ValidateVersion(PlayerData data)
        {
            if (data.saveVersion < 0 || data.saveVersion > CurrentVersion)
                throw new NotSupportedException("Unsupported player save version: " + data.saveVersion);
        }

        internal static void CopyProgress(HeroData source, HeroData target)
        {
            target.level = source.level;
            target.currentXP = source.currentXP;
            target.perkPoints = source.perkPoints;
            target.progressRevision = source.progressRevision;
        }

        internal static void CopySquadProgress(SquadInstanceData source, SquadInstanceData target)
        {
            target.level = source.level;
            target.experience = source.experience;
            target.progressRevision = source.progressRevision;
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

    /// <summary>Commits only the addressed hero's progression; unknown/ambiguous IDs never fall back to selection.</summary>
    public static bool TryUpdateHeroProgress(string heroId, int level, int currentXP, int perkPoints)
    {
        if (level < 1 || currentXP < 0 || perkPoints < 0) return false;
        lock (Core.Persistence.LocalSaveProvider.Gate)
        {
            var player = _provider.Load();
            var hero = HeroDataService.FindById(player, heroId);
            if (hero == null) return false;
            // Providers may return their live object; stage changes so a failed save cannot mutate it.
            player = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(player));
            hero = HeroDataService.FindById(player, heroId);
            hero.level = level;
            hero.currentXP = currentXP;
            hero.perkPoints = perkPoints;
            hero.progressRevision = checked(hero.progressRevision + 1);
            _provider.Save(player);
            var sessionHero = HeroDataService.FindById(PlayerSessionService.CurrentPlayer, heroId);
            if (sessionHero != null)
            {
                bool levelChanged = sessionHero.level != hero.level;
                Core.Persistence.LocalSaveProvider.CopyProgress(hero, sessionHero);
                if (levelChanged) DataCacheService.CacheAttributes(sessionHero);
            }
            return true;
        }
    }

    public static bool TryUpdateSquadProgress(string heroId, string instanceId, int level, int currentXP)
    {
        if (level < 1 || currentXP < 0) return false;
        lock (Core.Persistence.LocalSaveProvider.Gate)
        {
            var player = _provider.Load();
            var squad = SquadDataService.FindInstanceById(HeroDataService.FindById(player, heroId), instanceId);
            if (squad == null) return false;
            player = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(player));
            squad = SquadDataService.FindInstanceById(HeroDataService.FindById(player, heroId), instanceId);
            squad.level = level;
            squad.experience = currentXP;
            squad.progressRevision = checked(squad.progressRevision + 1);
            _provider.Save(player);
            var sessionSquad = SquadDataService.FindInstanceById(
                HeroDataService.FindById(PlayerSessionService.CurrentPlayer, heroId), instanceId);
            if (sessionSquad != null) Core.Persistence.LocalSaveProvider.CopySquadProgress(squad, sessionSquad);
            return true;
        }
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
            if (data.heroes != null)
                foreach (var hero in data.heroes)
                    if (hero != null) HeroAttributeValidator.GetHeroId(hero);
            _provider.Save(data);
            data.requiresIdentitySave = false;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save player data: {e}");
        }
    }
}

using Conquest.WorldMap.Editor;
public static class WorldMapBuildV9
{
    public static string Run()
    {
        WorldMapMountainTrial.Build("C:/Users/VRINO/.codex/worktrees/worldgen-relieve/BronzeAgeFase0/exports-regional/campaign-anatolia-v9-seed42/manifest.json");
        return WorldMapMountainTrial.ScenePath;
    }
}

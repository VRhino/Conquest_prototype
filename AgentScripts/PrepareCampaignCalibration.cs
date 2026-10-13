using Conquest.WorldMap.Editor;
public static class PrepareCampaignCalibration
{
    public static string Run() { WorldMapCalibrationBuilder.Build(); return WorldMapCalibrationBuilder.TrialScene; }
}

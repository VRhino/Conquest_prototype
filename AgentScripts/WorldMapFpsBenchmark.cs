using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Conquest.WorldMap;
using Conquest.WorldMap.Editor;

public static class WorldMapFpsBenchmark
{
    public static string Run()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Enter Play Mode first");
        new GameObject("Temporary world map FPS benchmark").AddComponent<WorldMapFpsProbe>();
        return "Sampling four views: 5 seconds warmup + 15 seconds per view";
    }
    public static string Build()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play Mode first");
        string folder = Path.GetFullPath("Builds/WorldMapV9Fps");
        Directory.CreateDirectory(folder);
        try {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { WorldMapMountainTrial.ScenePath },
                locationPathName = Path.Combine(folder,"WorldMapV9.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            string result = report.summary.result + " | errors=" + report.summary.totalErrors + " | bytes=" + report.summary.totalSize;
            File.WriteAllText(Path.Combine(folder,"BuildResult.txt"),result);
            return result;
        } catch (Exception ex) {
            File.WriteAllText(Path.Combine(folder,"BuildResult.txt"),ex.ToString());
            throw;
        }
    }
}


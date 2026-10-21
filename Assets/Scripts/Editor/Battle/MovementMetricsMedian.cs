using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Combines the latest MovementBaseline runs of every scenario into a median summary (M10), written next to
/// them in Logs/MovementMetrics as <c>&lt;scenario&gt;_median_summary.txt</c>.
/// </summary>
public static class MovementMetricsMedian
{
    public const int DefaultRuns = 3;

    [MenuItem("Tools/Movement/Median Of Last 3 Runs")]
    public static void WriteMenu() => Debug.Log($"[MovementMetricsMedian]\n{Write(DefaultRuns)}");

    /// <summary>Median of the last <paramref name="runs"/> runs per scenario; returns the combined report.</summary>
    public static string Write(int runs)
    {
        string directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "MovementMetrics");
        if (!Directory.Exists(directory)) return "No Logs/MovementMetrics folder yet.";
        var byScenario = new Dictionary<string, List<string>>();
        // Names are <scenario>_<yyyyMMdd>_<HHmmss>_summary.json, so ordinal order is chronological.
        foreach (string file in Directory.GetFiles(directory, "*_summary.json").OrderBy(f => f, System.StringComparer.Ordinal))
        {
            string scenario = Path.GetFileName(file).Split('_')[0];
            if (!byScenario.TryGetValue(scenario, out var list)) byScenario[scenario] = list = new List<string>();
            list.Add(file);
        }
        var report = new System.Text.StringBuilder();
        foreach (var pair in byScenario.OrderBy(p => p.Key, System.StringComparer.Ordinal))
        {
            var latest = pair.Value.Skip(System.Math.Max(0, pair.Value.Count - runs))
                .Select(f => JsonUtility.FromJson<MovementMetricsSummary>(File.ReadAllText(f))).ToList();
            var median = MovementMetricsAnalyzer.Median(latest);
            File.WriteAllText(Path.Combine(directory, $"{pair.Key}_median_summary.txt"), median.ToString());
            report.AppendLine(median.ToString());
        }
        return report.Length > 0 ? report.ToString() : "No JSON summaries yet (runs before M10 only wrote text).";
    }
}

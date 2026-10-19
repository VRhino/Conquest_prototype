// DEBUG ONLY — movement metrics for the formation baseline (Docs/Design/Movimiento_Formaciones.md §5).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

/// <summary>One unit observed in one frame.</summary>
public struct MovementUnitSample
{
    public int frame;
    public float time;
    public int squad;
    public int unit;
    public UnitFormationState state;
    public float slotDistance;
}

/// <summary>Squad-level and global values observed in one frame.</summary>
public struct MovementFrameSample
{
    public int frame;
    public float time;
    public float deltaTime;
    public int squad;
    public bool anchorMoving;
    public float anchorX;
    public float anchorZ;
    public int setDestinationCalls;
    public int projectionRequests;
    public double formationStateMs;
    public double navMeshMs;
}

/// <summary>Aggregated baseline metrics. Negative values mean "not observed".</summary>
public sealed class MovementMetricsSummary
{
    public string label;
    public int frames;
    public float duration;
    public int units;
    public int stops;
    public int starts;
    public float meanExtraTransitionsAfterArrival;
    public int maxExtraTransitionsAfterArrival;
    public float meanTimeToFormed;
    public float maxTimeToFormed;
    public float meanStartSpread;
    public float maxLastStart;
    public float setDestinationPerUnitPerSecond;
    public float meanSlotErrorMarching;
    public float p95SlotErrorMarching;
    public double meanFormationStateMs;
    public double meanNavMeshMs;

    public override string ToString()
    {
        var c = CultureInfo.InvariantCulture;
        var s = new StringBuilder();
        s.AppendLine($"[{label}] frames={frames} duration={duration.ToString("F1", c)}s units={units} stops={stops} starts={starts}");
        s.AppendLine($"  extra transitions after arrival (3 s): mean={meanExtraTransitionsAfterArrival.ToString("F2", c)} max={maxExtraTransitionsAfterArrival}");
        s.AppendLine($"  time to formed (95 %): mean={meanTimeToFormed.ToString("F2", c)}s max={maxTimeToFormed.ToString("F2", c)}s");
        s.AppendLine($"  start: mean spread={meanStartSpread.ToString("F2", c)}s, last unit after={maxLastStart.ToString("F2", c)}s");
        s.AppendLine($"  SetDestination per unit per second={setDestinationPerUnitPerSecond.ToString("F2", c)}");
        s.AppendLine($"  slot error while marching: mean={meanSlotErrorMarching.ToString("F2", c)}m p95={p95SlotErrorMarching.ToString("F2", c)}m");
        s.Append($"  ms: UnitFormationState={meanFormationStateMs.ToString("F3", c)} UnitNavMesh={meanNavMeshMs.ToString("F3", c)}");
        return s.ToString();
    }
}

/// <summary>
/// Pure analysis of recorded samples. A stop is the anchor going from moving to still for at least
/// <see cref="MinStillDuration"/>; a start is the reverse. All windows use recorded simulation time.
/// </summary>
public static class MovementMetricsAnalyzer
{
    public const float ArrivalWindow = 3f;      // Transitions counted after a stop.
    public const float FormedWindow = 6f;       // Longest wait for 95 % of units to be Formed.
    public const float FormedFraction = 0.95f;
    public const float StartWindow = 3f;        // Longest wait for a unit to leave its slot.
    public const float MinStillDuration = 0.5f; // Ignores single-frame anchor pauses.

    public static MovementMetricsSummary Analyze(string label,
        IReadOnlyList<MovementFrameSample> frames, IReadOnlyList<MovementUnitSample> units)
    {
        var summary = new MovementMetricsSummary { label = label };
        if (frames.Count == 0) return summary;

        var frameNumbers = frames.Select(f => f.frame).Distinct().ToList();
        summary.frames = frameNumbers.Count;
        summary.duration = frames.Max(f => f.time) - frames.Min(f => f.time);
        summary.units = units.Select(u => (u.squad, u.unit)).Distinct().Count();

        var global = frames.GroupBy(f => f.frame).Select(g => g.First()).ToList();
        if (summary.units > 0 && summary.duration > 0f)
            summary.setDestinationPerUnitPerSecond = global.Sum(f => f.setDestinationCalls) / (summary.units * summary.duration);
        var stateMs = global.Where(f => f.formationStateMs >= 0).Select(f => f.formationStateMs).ToList();
        var navMs = global.Where(f => f.navMeshMs >= 0).Select(f => f.navMeshMs).ToList();
        summary.meanFormationStateMs = stateMs.Count > 0 ? stateMs.Average() : -1;
        summary.meanNavMeshMs = navMs.Count > 0 ? navMs.Average() : -1;

        var marching = new HashSet<(int, int)>(frames.Where(f => f.anchorMoving).Select(f => (f.squad, f.frame)));
        var marchingErrors = units.Where(u => marching.Contains((u.squad, u.frame)))
            .Select(u => u.slotDistance).OrderBy(d => d).ToList();
        summary.meanSlotErrorMarching = marchingErrors.Count > 0 ? marchingErrors.Average() : -1f;
        summary.p95SlotErrorMarching = marchingErrors.Count > 0
            ? marchingErrors[Math.Min(marchingErrors.Count - 1, (int)(0.95f * marchingErrors.Count))] : -1f;

        var extra = new List<int>();
        var toFormed = new List<float>();
        var spreads = new List<float>();
        var lastStarts = new List<float>();

        foreach (var squadFrames in frames.GroupBy(f => f.squad))
        {
            var series = squadFrames.OrderBy(f => f.frame).ToList();
            var squadUnits = units.Where(u => u.squad == squadFrames.Key)
                .GroupBy(u => u.unit).ToDictionary(g => g.Key, g => g.OrderBy(u => u.frame).ToList());
            if (squadUnits.Count == 0) continue;

            foreach (float stop in Transitions(series, fromMoving: true))
            {
                summary.stops++;
                foreach (var unitSeries in squadUnits.Values)
                    extra.Add(Math.Max(0, CountTransitions(unitSeries, stop, stop + ArrivalWindow) - 1));
                float formed = TimeUntilFormed(squadUnits.Values, stop);
                if (formed >= 0f) toFormed.Add(formed);
            }

            foreach (float start in Transitions(series, fromMoving: false))
            {
                summary.starts++;
                var departures = squadUnits.Values
                    .Select(s => FirstStateTime(s, UnitFormationState.Moving, start, start + StartWindow))
                    .Where(t => t >= 0f).ToList();
                if (departures.Count == 0) continue;
                spreads.Add(departures.Max() - departures.Min());
                lastStarts.Add(departures.Max() - start);
            }
        }

        summary.meanExtraTransitionsAfterArrival = extra.Count > 0 ? (float)extra.Average() : -1f;
        summary.maxExtraTransitionsAfterArrival = extra.Count > 0 ? extra.Max() : -1;
        summary.meanTimeToFormed = toFormed.Count > 0 ? toFormed.Average() : -1f;
        summary.maxTimeToFormed = toFormed.Count > 0 ? toFormed.Max() : -1f;
        summary.meanStartSpread = spreads.Count > 0 ? spreads.Average() : -1f;
        summary.maxLastStart = lastStarts.Count > 0 ? lastStarts.Max() : -1f;
        return summary;
    }

    /// <summary>Times where the anchor changes state and keeps the new state for MinStillDuration.</summary>
    static IEnumerable<float> Transitions(List<MovementFrameSample> series, bool fromMoving)
    {
        for (int i = 1; i < series.Count; i++)
        {
            if (series[i - 1].anchorMoving != fromMoving || series[i].anchorMoving == fromMoving) continue;
            float t = series[i].time;
            bool sustained = true;
            for (int j = i; j < series.Count && series[j].time < t + MinStillDuration; j++)
                if (series[j].anchorMoving == fromMoving) { sustained = false; break; }
            if (sustained) yield return t;
        }
    }

    static int CountTransitions(List<MovementUnitSample> series, float from, float to)
    {
        int count = 0;
        for (int i = 1; i < series.Count; i++)
            if (series[i].time > from && series[i].time <= to && series[i].state != series[i - 1].state)
                count++;
        return count;
    }

    static float TimeUntilFormed(IEnumerable<List<MovementUnitSample>> unitSeries, float from)
    {
        var all = unitSeries.SelectMany(s => s).Where(u => u.time >= from && u.time <= from + FormedWindow)
            .GroupBy(u => u.frame).OrderBy(g => g.Key);
        foreach (var frame in all)
        {
            int total = frame.Count();
            int formed = frame.Count(u => u.state == UnitFormationState.Formed);
            if (total > 0 && formed >= FormedFraction * total) return frame.First().time - from;
        }
        return -1f;
    }

    static float FirstStateTime(List<MovementUnitSample> series, UnitFormationState state, float from, float to)
    {
        foreach (var sample in series)
            if (sample.time > from && sample.time <= to && sample.state == state) return sample.time;
        return -1f;
    }
}

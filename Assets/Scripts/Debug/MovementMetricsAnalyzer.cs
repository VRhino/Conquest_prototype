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
    public float x, z;            // Horizontal position.
    public float yaw;             // Facing, radians.
    public int team;
    public int target;            // Combat target entity index, -1 without target.
    public float targetDistance;  // Horizontal distance to the target, -1 without target.
    public bool attacking;
    public bool reacting;         // Squad order comes from a combat reaction.
    public int gear;              // MovementGear, -1 when unknown.
    public float speed;           // Agent horizontal speed.
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
    public double followFormationMs;
    public double avoidanceMs;
    public int allyOverlaps;            // Allied pairs closer than the overlap distance (all squads).
    public int enemyOverlaps;           // Enemy pairs closer than the overlap distance.
    public int persistentEnemyOverlaps; // Enemy pairs overlapping for longer than the persistence window.
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
    public double meanFollowFormationMs;
    public double meanAvoidanceMs;
    public float setDestinationMarchingPerUnitPerSecond;
    public float projectionsMarchingPerUnitPerSecond;
    public float meanAllyOverlaps;
    public int maxAllyOverlaps;
    public float meanAllyOverlapsMarching;
    public int maxPersistentEnemyOverlaps;
    public float meanContactDistance;
    public float minContactDistance;
    public int maxAttackersPerTarget;
    public float p95AttackersPerTarget;
    public int leashViolations;
    public int turnRateViolationFrames;
    public int gearChanges;

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
        s.AppendLine($"  marching per unit per second: SetDestination={setDestinationMarchingPerUnitPerSecond.ToString("F2", c)} projections={projectionsMarchingPerUnitPerSecond.ToString("F2", c)}");
        s.AppendLine($"  ally overlaps per frame: mean={meanAllyOverlaps.ToString("F2", c)} max={maxAllyOverlaps} marching mean={meanAllyOverlapsMarching.ToString("F2", c)}");
        s.AppendLine($"  enemy pairs overlapping longer than {MovementMetricsAnalyzer.EnemyOverlapPersistence.ToString("F1", c)} s: max={maxPersistentEnemyOverlaps}");
        s.AppendLine($"  contact distance while attacking: mean={meanContactDistance.ToString("F2", c)}m min={minContactDistance.ToString("F2", c)}m");
        s.AppendLine($"  attackers per target: max={maxAttackersPerTarget} p95={p95AttackersPerTarget.ToString("F1", c)}");
        s.AppendLine($"  leash violations (reacting, beyond the leash > {MovementMetricsAnalyzer.LeashPersistence.ToString("F1", c)} s): {leashViolations}");
        s.AppendLine($"  combat turn frames above the cap: {turnRateViolationFrames}; gear changes: {gearChanges}");
        s.Append($"  ms: UnitFormationState={meanFormationStateMs.ToString("F3", c)} UnitNavMesh={meanNavMeshMs.ToString("F3", c)} UnitFollowFormation={meanFollowFormationMs.ToString("F3", c)} UnitAvoidance={meanAvoidanceMs.ToString("F3", c)}");
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
    public const float LeashPersistence = 0.5f; // Seconds beyond the leash before counting a violation.
    public const float EnemyOverlapPersistence = 0.3f; // Enemy pairs overlapping longer than this are counted.
    public const float ContactRange = 2.5f;     // Units this close to their target count as attackers on it.

    /// <summary>Contact-phase thresholds the summary is evaluated against.</summary>
    public struct ContactLimits
    {
        public float leashDistance;
        public float combatTurnRateDegrees; // Frames turning faster than this (with a target) are counted.
        public static ContactLimits Default => new ContactLimits { leashDistance = 6f, combatTurnRateDegrees = 450f };
    }

    public static MovementMetricsSummary Analyze(string label,
        IReadOnlyList<MovementFrameSample> frames, IReadOnlyList<MovementUnitSample> units)
        => Analyze(label, frames, units, ContactLimits.Default);

    public static MovementMetricsSummary Analyze(string label,
        IReadOnlyList<MovementFrameSample> frames, IReadOnlyList<MovementUnitSample> units, ContactLimits limits)
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
        summary.meanFollowFormationMs = Mean(global.Select(f => f.followFormationMs));
        summary.meanAvoidanceMs = Mean(global.Select(f => f.avoidanceMs));
        AnalyzeContact(summary, frames, units, global, limits);

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

    static double Mean(IEnumerable<double> values)
    {
        var valid = values.Where(v => v >= 0).ToList();
        return valid.Count > 0 ? valid.Average() : -1;
    }

    /// <summary>Overlaps, contact distance, attackers per target, leash, combat turn rate and gear changes.</summary>
    static void AnalyzeContact(MovementMetricsSummary summary, IReadOnlyList<MovementFrameSample> frames,
        IReadOnlyList<MovementUnitSample> units, List<MovementFrameSample> global, ContactLimits limits)
    {
        var marchingFrames = new HashSet<int>(frames.Where(f => f.anchorMoving).Select(f => f.frame));
        var marching = global.Where(f => marchingFrames.Contains(f.frame)).ToList();
        float marchingTime = marching.Sum(f => f.deltaTime);
        if (summary.units > 0 && marchingTime > 0f)
        {
            summary.setDestinationMarchingPerUnitPerSecond = marching.Sum(f => f.setDestinationCalls) / (summary.units * marchingTime);
            summary.projectionsMarchingPerUnitPerSecond = marching.Sum(f => f.projectionRequests) / (summary.units * marchingTime);
        }
        summary.meanAllyOverlaps = global.Count > 0 ? (float)global.Average(f => f.allyOverlaps) : 0f;
        summary.maxAllyOverlaps = global.Count > 0 ? global.Max(f => f.allyOverlaps) : 0;
        summary.meanAllyOverlapsMarching = marching.Count > 0 ? (float)marching.Average(f => f.allyOverlaps) : 0f;
        summary.maxPersistentEnemyOverlaps = global.Count > 0 ? global.Max(f => f.persistentEnemyOverlaps) : 0;

        var contact = units.Where(u => u.attacking && u.targetDistance >= 0f).Select(u => u.targetDistance).ToList();
        summary.meanContactDistance = contact.Count > 0 ? contact.Average() : -1f;
        summary.minContactDistance = contact.Count > 0 ? contact.Min() : -1f;

        var perTarget = units.Where(u => u.target >= 0 && u.targetDistance >= 0f && u.targetDistance <= ContactRange)
            .GroupBy(u => (u.frame, u.target)).Select(g => g.Count()).OrderBy(n => n).ToList();
        summary.maxAttackersPerTarget = perTarget.Count > 0 ? perTarget.Max() : 0;
        summary.p95AttackersPerTarget = perTarget.Count > 0
            ? perTarget[Math.Min(perTarget.Count - 1, (int)(0.95f * perTarget.Count))] : 0f;

        float turnCap = limits.combatTurnRateDegrees * 1.1f * (float)(Math.PI / 180.0);
        foreach (var series in units.GroupBy(u => (u.squad, u.unit)).Select(g => g.OrderBy(u => u.frame).ToList()))
        {
            float beyondSince = -1f;
            bool counted = false;
            for (int i = 0; i < series.Count; i++)
            {
                var u = series[i];
                bool beyond = u.reacting && u.slotDistance > limits.leashDistance;
                if (!beyond) { beyondSince = -1f; counted = false; }
                else
                {
                    if (beyondSince < 0f) beyondSince = u.time;
                    if (!counted && u.time - beyondSince >= LeashPersistence) { summary.leashViolations++; counted = true; }
                }
                if (i == 0) continue;
                var p = series[i - 1];
                float dt = u.time - p.time;
                if (u.target >= 0 && p.target >= 0 && dt > 0f)
                {
                    float delta = Math.Abs(WrapAngle(u.yaw - p.yaw));
                    if (delta / dt > turnCap) summary.turnRateViolationFrames++;
                }
                if (u.gear >= 0 && p.gear >= 0 && u.gear != p.gear) summary.gearChanges++;
            }
        }
    }

    static float WrapAngle(float radians)
    {
        while (radians > Math.PI) radians -= (float)(2 * Math.PI);
        while (radians < -Math.PI) radians += (float)(2 * Math.PI);
        return radians;
    }
}

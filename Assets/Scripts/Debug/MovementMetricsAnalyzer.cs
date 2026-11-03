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
    public float destX, destZ;    // Formation destination the unit is heading for (after NavMesh validation).
    public int row;               // Formation rank from the front (0 = front row), -1 when unknown (S9).
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
    public float forwardX, forwardZ;    // Formation forward on the ground (S9 rank order).
    public int setDestinationCalls;
    public int projectionRequests;
    public double formationStateMs;
    public double navMeshMs;
    public double followFormationMs;
    public double avoidanceMs;
    public double slotResolveMs;
    public int allyOverlaps;            // Allied pairs closer than the overlap distance (all squads).
    public int enemyOverlaps;           // Enemy pairs closer than the overlap distance.
    public int persistentEnemyOverlaps; // Enemy pairs overlapping for longer than the persistence window.
    public int persistentEnemyOverlapsEngaged; // ... of which a unit of the pair has a combat target (S9).
    public int stackedDestinations;     // Squadmate pairs whose movement destinations share a formation cell (S7).
    public int detourDestinations;      // Units whose destination is reached only by a detour from the hero, or not at all (S7).
    public int relocatedSlots;          // Slots moved to another cell by the NavMesh validation (C13).
    public int unresolvedSlots;         // Slots the validation found no free cell for (C13).
    public int slotResolveQueries;      // NavMesh queries of the slot validation this frame (all squads).
    public int templateIndex;           // Template in use (F1); a change is a new layout for fewer soldiers.
    public int alliedSharedCells;       // Pairs of destinations of this squad and an allied squad sharing a cell (S5, F17).
    public int gapSlots;                // Soldiers outside the first n slots of the template, n = soldiers alive (gaps, L3-02).
}

/// <summary>Aggregated baseline metrics. Negative values mean "not observed".</summary>
[Serializable]
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
    public double meanSlotResolveMs;
    public float setDestinationMarchingPerUnitPerSecond;
    public float projectionsMarchingPerUnitPerSecond;
    public float meanAllyOverlaps;
    public int maxAllyOverlaps;
    public float meanAllyOverlapsMarching;
    public int maxPersistentEnemyOverlaps;
    /// <summary>Persistent enemy overlaps split by whether a unit of the pair has a combat target (S9, P7).</summary>
    public int maxPersistentEnemyOverlapsEngaged;
    public int maxPersistentEnemyOverlapsFree;
    /// <summary>Mean time from the start of the recording to each unit's first swing, s; -1 without swings (S9).</summary>
    public float meanTimeToFirstSwing;
    /// <summary>Mean agent speed while swinging (P5).</summary>
    public float meanSpeedWhileSwinging;
    /// <summary>Share of rear-rank samples in melee that stand ahead of their front rank's mean line (S9: the line holds).</summary>
    public float rearAheadOfFrontRatio;
    public float rearThroughFrontRatio;
    public float meanContactDistance;
    public float minContactDistance;
    public int maxAttackersPerTarget;
    public float p95AttackersPerTarget;
    public int leashViolations;
    public int turnRateViolationFrames;
    public int gearChanges;
    /// <summary>Changes from one enemy to another per unit and second with a target (L4-04 hysteresis).</summary>
    public float targetSwitchesPerUnitPerSecond;
    /// <summary>Share of unit samples whose acceleration exceeds <see cref="MovementMetricsAnalyzer.AccelerationSpikeLimit"/>.</summary>
    public float accelerationSpikeRatio;
    public float maxAcceleration;
    public float meanStackedDestinations;
    public int maxStackedDestinations;
    public float meanDetourDestinations;
    public int maxDetourDestinations;
    /// <summary>Episodes of a unit standing still far from its slot for longer than <see cref="MovementMetricsAnalyzer.StallPersistence"/>.</summary>
    public int stalls;
    public int maxRelocatedSlots;
    public int maxUnresolvedSlots;
    public float slotQueriesMarchingPerUnitPerSecond;
    /// <summary>Layout changes for the soldiers alive (F1), measured in the formation frame: the march does not count as travel.</summary>
    public int templateChanges;
    public float meanTemplateChangeTravel;
    public float maxTemplateChangeTravel;
    public float meanTemplateChangeCrossings;
    public int maxTemplateChangeCrossings;
    /// <summary>Time for 95 % of the soldiers to reach their new slot after a template change, s; -1 without changes.</summary>
    public float meanTemplateChangeReform;
    public float maxTemplateChangeReform;
    /// <summary>Destination pairs shared with an allied squad (S5, F17).</summary>
    public float meanAlliedSharedCells;
    public int maxAlliedSharedCells;
    public float alliedSharedCellFrameRatio;
    /// <summary>Soldiers outside the first n slots (one per gap left by casualties inside a template, L3-02).</summary>
    public float meanGapSlots;
    public int maxGapSlots;
    public float gapFrameRatio;

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
        s.AppendLine($"  of which engaged={maxPersistentEnemyOverlapsEngaged} free={maxPersistentEnemyOverlapsFree}; first swing after mean={meanTimeToFirstSwing.ToString("F2", c)}s; speed while swinging={meanSpeedWhileSwinging.ToString("F2", c)}m/s");
        s.AppendLine($"  rear ranks ahead of their front line in melee: {(rearAheadOfFrontRatio * 100f).ToString("F1", c)} % of samples");
        s.AppendLine($"  rear ranks through their front in melee: {(rearThroughFrontRatio * 100f).ToString("F1", c)} % of samples");
        s.AppendLine($"  contact distance while attacking: mean={meanContactDistance.ToString("F2", c)}m min={minContactDistance.ToString("F2", c)}m");
        s.AppendLine($"  attackers per target: max={maxAttackersPerTarget} p95={p95AttackersPerTarget.ToString("F1", c)}");
        s.AppendLine($"  leash violations (reacting, beyond the leash > {MovementMetricsAnalyzer.LeashPersistence.ToString("F1", c)} s): {leashViolations}");
        s.AppendLine($"  combat turn frames above the cap: {turnRateViolationFrames}; gear changes: {gearChanges}");
        s.AppendLine($"  target switches per unit per second with a target: {targetSwitchesPerUnitPerSecond.ToString("F3", c)}");
        s.AppendLine($"  acceleration above {MovementMetricsAnalyzer.AccelerationSpikeLimit.ToString("F0", c)} m/s2: {(accelerationSpikeRatio * 100f).ToString("F1", c)} % of samples, max {maxAcceleration.ToString("F0", c)} m/s2");
        s.AppendLine($"  destinations sharing a cell per frame: mean={meanStackedDestinations.ToString("F2", c)} max={maxStackedDestinations}; reached only by a detour: mean={meanDetourDestinations.ToString("F2", c)} max={maxDetourDestinations}");
        s.AppendLine($"  slot validation (C13): relocated max={maxRelocatedSlots} unresolved max={maxUnresolvedSlots} queries marching per unit per second={slotQueriesMarchingPerUnitPerSecond.ToString("F2", c)}");
        s.AppendLine($"  template changes: {templateChanges}; travel per soldier mean={meanTemplateChangeTravel.ToString("F2", c)}m max={maxTemplateChangeTravel.ToString("F2", c)}m; crossings mean={meanTemplateChangeCrossings.ToString("F2", c)} max={maxTemplateChangeCrossings}; reformed mean={meanTemplateChangeReform.ToString("F2", c)}s max={maxTemplateChangeReform.ToString("F2", c)}s");
        s.AppendLine($"  destinations sharing a cell with an allied squad: mean={meanAlliedSharedCells.ToString("F2", c)} max={maxAlliedSharedCells} in {(alliedSharedCellFrameRatio * 100f).ToString("F1", c)} % of frames");
        s.AppendLine($"  gaps (soldiers past the alive count): mean={meanGapSlots.ToString("F2", c)} max={maxGapSlots} in {(gapFrameRatio * 100f).ToString("F1", c)} % of frames");
        s.AppendLine($"  stalls (still beyond {MovementMetricsAnalyzer.StallDistance.ToString("F1", c)} m of the slot > {MovementMetricsAnalyzer.StallPersistence.ToString("F1", c)} s): {stalls}");
        s.Append($"  ms: UnitFormationState={meanFormationStateMs.ToString("F3", c)} UnitNavMesh={meanNavMeshMs.ToString("F3", c)} UnitFollowFormation={meanFollowFormationMs.ToString("F3", c)} UnitAvoidance={meanAvoidanceMs.ToString("F3", c)} FormationSlotResolve={meanSlotResolveMs.ToString("F3", c)}");
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
    public const float AccelerationSpikeLimit = 20f; // m/s²: well above any agent (8-12 m/s²), so only jolts count.
    public const float ThroughFrontMargin = 0.5f; // m past the foremost front-rank unit: a body radius, so standing level with it is not through.
    public const float EnemyOverlapPersistence = 0.3f; // Enemy pairs overlapping longer than this are counted.
    public const float ContactRange = 2.5f;     // Units this close to their target count as attackers on it.
    public const float StallSpeed = 0.3f;       // m/s: below this a unit counts as standing still.
    public const float StallDistance = 1f;      // m from its slot: a still unit closer than this has arrived.
    public const float StallPersistence = 0.5f; // Seconds standing still far from the slot before counting a stall.
    public const float TemplateChangeWindow = 4f; // Longest wait for a squad to reform after a template change, s.

    /// <summary>
    /// Field-by-field median of several runs of one scenario (M10): the melee in S6 diverges between runs from
    /// the first contacts on, so one run decides nothing. Even counts take the mean of the two middle values.
    /// </summary>
    public static MovementMetricsSummary Median(IReadOnlyList<MovementMetricsSummary> runs)
    {
        if (runs == null || runs.Count == 0) throw new ArgumentException("No runs to combine.", nameof(runs));
        var median = new MovementMetricsSummary { label = $"{runs[0].label} median of {runs.Count}" };
        foreach (var field in typeof(MovementMetricsSummary).GetFields())
        {
            if (field.FieldType == typeof(string)) continue;
            var values = runs.Select(r => Convert.ToDouble(field.GetValue(r), CultureInfo.InvariantCulture))
                .OrderBy(v => v).ToArray();
            int mid = values.Length / 2;
            double value = values.Length % 2 == 1 ? values[mid] : (values[mid - 1] + values[mid]) / 2.0;
            field.SetValue(median, Convert.ChangeType(field.FieldType == typeof(int) ? Math.Round(value) : value,
                field.FieldType, CultureInfo.InvariantCulture));
        }
        return median;
    }

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
        AnalyzeTemplateChanges(summary, frames, units);
        summary.meanAlliedSharedCells = (float)frames.Average(f => f.alliedSharedCells);
        summary.maxAlliedSharedCells = frames.Max(f => f.alliedSharedCells);
        summary.alliedSharedCellFrameRatio = (float)frames.Count(f => f.alliedSharedCells > 0) / frames.Count;
        summary.meanGapSlots = (float)frames.Average(f => f.gapSlots);
        summary.maxGapSlots = frames.Max(f => f.gapSlots);
        summary.gapFrameRatio = (float)frames.Count(f => f.gapSlots > 0) / frames.Count;

        var global = frames.GroupBy(f => f.frame).Select(g => g.First()).ToList();
        if (summary.units > 0 && summary.duration > 0f)
            summary.setDestinationPerUnitPerSecond = global.Sum(f => f.setDestinationCalls) / (summary.units * summary.duration);
        var stateMs = global.Where(f => f.formationStateMs >= 0).Select(f => f.formationStateMs).ToList();
        var navMs = global.Where(f => f.navMeshMs >= 0).Select(f => f.navMeshMs).ToList();
        summary.meanFormationStateMs = stateMs.Count > 0 ? stateMs.Average() : -1;
        summary.meanNavMeshMs = navMs.Count > 0 ? navMs.Average() : -1;
        summary.meanFollowFormationMs = Mean(global.Select(f => f.followFormationMs));
        summary.meanAvoidanceMs = Mean(global.Select(f => f.avoidanceMs));
        summary.meanSlotResolveMs = Mean(global.Select(f => f.slotResolveMs));
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
    /// <summary>
    /// Each change of template (F1): from the frame of the change until 95 % of the soldiers are back at a slot, or the
    /// window ends. Travel and crossings use positions relative to the anchor, so they hold while the squad marches.
    /// A soldier who dies meanwhile does not count.
    /// </summary>
    static void AnalyzeTemplateChanges(MovementMetricsSummary summary, IReadOnlyList<MovementFrameSample> frames,
        IReadOnlyList<MovementUnitSample> units)
    {
        var travels = new List<float>();
        var crossings = new List<int>();
        var reforms = new List<float>();
        foreach (var squad in frames.GroupBy(f => f.squad))
        {
            var series = squad.OrderBy(f => f.frame).ToList();
            var byFrame = units.Where(u => u.squad == squad.Key).GroupBy(u => u.frame).ToDictionary(g => g.Key, g => g.ToList());
            for (int i = 1; i < series.Count; i++)
            {
                if (series[i].templateIndex == series[i - 1].templateIndex) continue;
                float start = series[i].time;
                int end = i;
                float reform = -1f;
                for (int k = i; k < series.Count && series[k].time - start <= TemplateChangeWindow; k++)
                {
                    if (k > i && series[k].templateIndex != series[k - 1].templateIndex) break;
                    end = k;
                    if (!byFrame.TryGetValue(series[k].frame, out var present) || present.Count == 0) continue;
                    if (present.Count(u => u.slotDistance <= StallDistance) >= FormedFraction * present.Count)
                    {
                        reform = series[k].time - start;
                        break;
                    }
                }
                reforms.Add(reform >= 0f ? reform : TemplateChangeWindow);

                var paths = new Dictionary<int, GroundPath>();
                for (int k = i; k <= end; k++)
                {
                    if (!byFrame.TryGetValue(series[k].frame, out var present)) continue;
                    foreach (var u in present)
                    {
                        float x = u.x - series[k].anchorX, z = u.z - series[k].anchorZ;
                        paths[u.unit] = paths.TryGetValue(u.unit, out var path)
                            ? path.To(x, z) : new GroundPath { fromX = x, fromZ = z, toX = x, toZ = z };
                    }
                }
                var alive = byFrame.TryGetValue(series[end].frame, out var last)
                    ? last.Select(u => paths[u.unit]).ToList() : paths.Values.ToList();
                travels.AddRange(alive.Select(p => p.length));
                int crossed = 0;
                for (int a = 0; a < alive.Count; a++)
                for (int b = a + 1; b < alive.Count; b++)
                    if (GroundPath.Cross(alive[a], alive[b])) crossed++;
                crossings.Add(crossed);
            }
        }
        summary.templateChanges = reforms.Count;
        summary.meanTemplateChangeTravel = travels.Count > 0 ? travels.Average() : 0f;
        summary.maxTemplateChangeTravel = travels.Count > 0 ? travels.Max() : 0f;
        summary.meanTemplateChangeCrossings = crossings.Count > 0 ? (float)crossings.Average() : 0f;
        summary.maxTemplateChangeCrossings = crossings.Count > 0 ? crossings.Max() : 0;
        summary.meanTemplateChangeReform = reforms.Count > 0 ? reforms.Average() : -1f;
        summary.maxTemplateChangeReform = reforms.Count > 0 ? reforms.Max() : -1f;
    }

    /// <summary>A soldier's walk in the formation frame: walked length, and its ends as a straight segment.</summary>
    struct GroundPath
    {
        public float fromX, fromZ, toX, toZ, length;

        public GroundPath To(float x, float z)
        {
            float dx = x - toX, dz = z - toZ;
            return new GroundPath { fromX = fromX, fromZ = fromZ, toX = x, toZ = z,
                length = length + (float)Math.Sqrt(dx * dx + dz * dz) };
        }

        static float Side(in GroundPath p, float x, float z) =>
            (p.toX - p.fromX) * (z - p.fromZ) - (p.toZ - p.fromZ) * (x - p.fromX);

        public static bool Cross(in GroundPath a, in GroundPath b) =>
            Side(a, b.fromX, b.fromZ) * Side(a, b.toX, b.toZ) < 0f
            && Side(b, a.fromX, a.fromZ) * Side(b, a.toX, a.toZ) < 0f;
    }

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
            summary.slotQueriesMarchingPerUnitPerSecond = marching.Sum(f => f.slotResolveQueries) / (summary.units * marchingTime);
        }
        summary.meanAllyOverlaps = global.Count > 0 ? (float)global.Average(f => f.allyOverlaps) : 0f;
        summary.maxAllyOverlaps = global.Count > 0 ? global.Max(f => f.allyOverlaps) : 0;
        summary.meanAllyOverlapsMarching = marching.Count > 0 ? (float)marching.Average(f => f.allyOverlaps) : 0f;
        summary.maxPersistentEnemyOverlaps = global.Count > 0 ? global.Max(f => f.persistentEnemyOverlaps) : 0;
        summary.maxPersistentEnemyOverlapsEngaged = global.Count > 0 ? global.Max(f => f.persistentEnemyOverlapsEngaged) : 0;
        summary.maxPersistentEnemyOverlapsFree = global.Count > 0
            ? global.Max(f => f.persistentEnemyOverlaps - f.persistentEnemyOverlapsEngaged) : 0;
        AnalyzeLine(summary, frames, units);
        // Destination checks are per squad, so they use every frame sample, not only the global ones.
        summary.meanStackedDestinations = (float)frames.Average(f => f.stackedDestinations);
        summary.maxStackedDestinations = frames.Max(f => f.stackedDestinations);
        summary.meanDetourDestinations = (float)frames.Average(f => f.detourDestinations);
        summary.maxDetourDestinations = frames.Max(f => f.detourDestinations);
        summary.maxRelocatedSlots = frames.Max(f => f.relocatedSlots);
        summary.maxUnresolvedSlots = frames.Max(f => f.unresolvedSlots);

        var contact = units.Where(u => u.attacking && u.targetDistance >= 0f).Select(u => u.targetDistance).ToList();
        summary.meanContactDistance = contact.Count > 0 ? contact.Average() : -1f;
        summary.minContactDistance = contact.Count > 0 ? contact.Min() : -1f;

        var perTarget = units.Where(u => u.target >= 0 && u.targetDistance >= 0f && u.targetDistance <= ContactRange)
            .GroupBy(u => (u.frame, u.target)).Select(g => g.Count()).OrderBy(n => n).ToList();
        summary.maxAttackersPerTarget = perTarget.Count > 0 ? perTarget.Max() : 0;
        summary.p95AttackersPerTarget = perTarget.Count > 0
            ? perTarget[Math.Min(perTarget.Count - 1, (int)(0.95f * perTarget.Count))] : 0f;

        float turnCap = limits.combatTurnRateDegrees * 1.1f * (float)(Math.PI / 180.0);
        int accelerationSamples = 0, accelerationSpikes = 0;
        int targetSwitches = 0;
        float targetedTime = 0f;
        foreach (var series in units.GroupBy(u => (u.squad, u.unit)).Select(g => g.OrderBy(u => u.frame).ToList()))
        {
            float beyondSince = -1f;
            bool counted = false;
            float stillSince = -1f;
            bool stallCounted = false;
            for (int i = 0; i < series.Count; i++)
            {
                var u = series[i];
                bool still = u.speed < StallSpeed && u.slotDistance > StallDistance;
                if (!still) { stillSince = -1f; stallCounted = false; }
                else
                {
                    if (stillSince < 0f) stillSince = u.time;
                    if (!stallCounted && u.time - stillSince >= StallPersistence) { summary.stalls++; stallCounted = true; }
                }
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
                if (u.target >= 0 && p.target >= 0)
                {
                    targetedTime += dt;
                    if (u.target != p.target) targetSwitches++;
                }

                // Acceleration from three consecutive positions.
                if (i < 2 || dt <= 1e-4f) continue;
                var q = series[i - 2];
                float dtPrev = p.time - q.time;
                if (dtPrev <= 1e-4f) continue;
                float vx = (u.x - p.x) / dt, vz = (u.z - p.z) / dt;
                float pvx = (p.x - q.x) / dtPrev, pvz = (p.z - q.z) / dtPrev;
                float acceleration = (float)Math.Sqrt((vx - pvx) * (vx - pvx) + (vz - pvz) * (vz - pvz)) / dt;
                accelerationSamples++;
                if (acceleration > AccelerationSpikeLimit) accelerationSpikes++;
                summary.maxAcceleration = Math.Max(summary.maxAcceleration, acceleration);
            }
        }
        summary.accelerationSpikeRatio = accelerationSamples > 0 ? (float)accelerationSpikes / accelerationSamples : 0f;
        summary.targetSwitchesPerUnitPerSecond = targetedTime > 0f ? targetSwitches / targetedTime : 0f;
    }

    /// <summary>
    /// S9 (P7): time to each unit's first swing, speed while swinging, and rear ranks that stand ahead of their front
    /// rank's mean line along the formation forward, in frames where the squad has a unit with a target. That count
    /// includes rear units that extend the line at its flanks or fill a gap in it; "through" is only a rear unit within
    /// the front rank's width and clearly past its foremost unit.
    /// </summary>
    static void AnalyzeLine(MovementMetricsSummary summary, IReadOnlyList<MovementFrameSample> frames,
        IReadOnlyList<MovementUnitSample> units)
    {
        float start = frames.Min(f => f.time);
        var firstSwings = units.Where(u => u.attacking).GroupBy(u => (u.squad, u.unit))
            .Select(g => g.Min(u => u.time) - start).ToList();
        summary.meanTimeToFirstSwing = firstSwings.Count > 0 ? firstSwings.Average() : -1f;
        var swinging = units.Where(u => u.attacking).ToList();
        summary.meanSpeedWhileSwinging = swinging.Count > 0 ? swinging.Average(u => u.speed) : 0f;

        var forwards = new Dictionary<(int, int), (float x, float z)>();
        foreach (var f in frames) forwards[(f.frame, f.squad)] = (f.forwardX, f.forwardZ);
        int rear = 0, ahead = 0, through = 0;
        foreach (var group in units.GroupBy(u => (u.frame, u.squad)))
        {
            if (!forwards.TryGetValue(group.Key, out var fwd) || (fwd.x == 0f && fwd.z == 0f)) continue;
            if (!group.Any(u => u.target >= 0)) continue;
            var front = group.Where(u => u.row == 0).ToList();
            if (front.Count == 0) continue;
            float line = front.Average(u => u.x * fwd.x + u.z * fwd.z);
            float foremost = front.Max(u => u.x * fwd.x + u.z * fwd.z);
            float left = front.Min(u => u.x * fwd.z - u.z * fwd.x);
            float right = front.Max(u => u.x * fwd.z - u.z * fwd.x);
            foreach (var u in group)
            {
                if (u.row <= 0) continue;
                rear++;
                float depth = u.x * fwd.x + u.z * fwd.z;
                float lateral = u.x * fwd.z - u.z * fwd.x;
                if (depth > line) ahead++;
                if (depth > foremost + ThroughFrontMargin && lateral >= left && lateral <= right) through++;
            }
        }
        summary.rearAheadOfFrontRatio = rear > 0 ? (float)ahead / rear : 0f;
        summary.rearThroughFrontRatio = rear > 0 ? (float)through / rear : 0f;
    }

    static float WrapAngle(float radians)
    {
        while (radians > Math.PI) radians -= (float)(2 * Math.PI);
        while (radians < -Math.PI) radians += (float)(2 * Math.PI);
        return radians;
    }
}

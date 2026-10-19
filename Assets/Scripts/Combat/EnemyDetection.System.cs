using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;

/// <summary>
/// Detects enemy squads within detection range and populates the three combat
/// detection buffers every frame. Candidate discovery uses an exact-distance
/// check after an XZ spatial broadphase; output order follows the original ECS
/// query and dynamic-buffer order.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(SquadAISystem))]
[UpdateAfter(typeof(HeroRespawnSystem))]
public partial class EnemyDetectionSystem : SystemBase
{
    private struct DetectionCandidate
    {
        public Entity Entity;
        public Entity Squad;
        public Team Team;
        public float3 Position;
        public bool IsHero;
    }

    private struct DetectionObserver
    {
        public Entity Entity;
        public Team Team;
        public float3 Centroid;
        public float RangeSquared;
        public int2 MinCell;
        public int2 MaxCell;
        public bool UseGrid;
    }

    private ComponentLookup<LocalTransform> _transformLookup;
    private ComponentLookup<HeroLifeComponent> _heroLifeLookup;
    private NativeList<DetectionCandidate> _candidates;
    private NativeList<DetectionObserver> _observers;
    private NativeList<int> _fallbackCandidateIndices;
    private NativeList<int> _resultCandidateIndices;
    private NativeList<int> _scratchCandidateIndices;
    private NativeList<int2> _observerResultSegments;
    private NativeParallelMultiHashMap<int2, int> _spatialIndex;
    private NativeArray<int> _jobCounters;

    private static readonly ProfilerMarker DetectionMarker = new("Conquest.EnemyDetection");
    private static readonly ProfilerMarker SnapshotMarker = new("Conquest.EnemyDetection.Snapshot");
    private static readonly ProfilerMarker SearchMarker = new("Conquest.EnemyDetection.SearchAndWait");
    private static readonly ProfilerMarker PublishMarker = new("Conquest.EnemyDetection.Publish");

    public int DebugCandidateOccurrences { get; private set; }
    public int DebugObserverCount { get; private set; }
    public int DebugGridCellVisits { get; private set; }
    public int DebugExactDistanceChecks { get; private set; }
    public int DebugPublishedTargets { get; private set; }

    protected override void OnCreate()
    {
        _transformLookup = GetComponentLookup<LocalTransform>(true);
        _heroLifeLookup = GetComponentLookup<HeroLifeComponent>(true);
        _candidates = new NativeList<DetectionCandidate>(Allocator.Persistent);
        _observers = new NativeList<DetectionObserver>(Allocator.Persistent);
        _fallbackCandidateIndices = new NativeList<int>(Allocator.Persistent);
        _resultCandidateIndices = new NativeList<int>(Allocator.Persistent);
        _scratchCandidateIndices = new NativeList<int>(Allocator.Persistent);
        _observerResultSegments = new NativeList<int2>(Allocator.Persistent);
        _spatialIndex = new NativeParallelMultiHashMap<int2, int>(1, Allocator.Persistent);
        _jobCounters = new NativeArray<int>(2, Allocator.Persistent);
    }

    protected override void OnUpdate()
    {
        using var detectionSample = DetectionMarker.Auto();
        _transformLookup.Update(this);
        _heroLifeLookup.Update(this);

        DebugCandidateOccurrences = 0;
        DebugObserverCount = 0;
        DebugGridCellVisits = 0;
        DebugExactDistanceChecks = 0;
        DebugPublishedTargets = 0;

        using (SnapshotMarker.Auto())
        {
            BuildObservers();
            if (_observers.Length == 0)
                return;
            BuildCandidatesAndIndex();
        }

        _resultCandidateIndices.Clear();
        _scratchCandidateIndices.Clear();
        _observerResultSegments.ResizeUninitialized(_observers.Length);
        _jobCounters[0] = 0;
        _jobCounters[1] = 0;

        var job = new SearchCandidatesJob
        {
            Candidates = _candidates,
            Observers = _observers,
            FallbackCandidateIndices = _fallbackCandidateIndices,
            SpatialIndex = _spatialIndex,
            ResultCandidateIndices = _resultCandidateIndices,
            ScratchCandidateIndices = _scratchCandidateIndices,
            ObserverResultSegments = _observerResultSegments,
            Counters = _jobCounters
        };
        using (SearchMarker.Auto())
        {
            JobHandle handle = job.Schedule(Dependency);
            Dependency = handle;
            handle.Complete();
        }

        DebugGridCellVisits = _jobCounters[0];
        DebugExactDistanceChecks = _jobCounters[1];
        using (PublishMarker.Auto()) PublishResults();
    }

    protected override void OnDestroy()
    {
        Dependency.Complete();
        if (_candidates.IsCreated) _candidates.Dispose();
        if (_observers.IsCreated) _observers.Dispose();
        if (_fallbackCandidateIndices.IsCreated) _fallbackCandidateIndices.Dispose();
        if (_resultCandidateIndices.IsCreated) _resultCandidateIndices.Dispose();
        if (_scratchCandidateIndices.IsCreated) _scratchCandidateIndices.Dispose();
        if (_observerResultSegments.IsCreated) _observerResultSegments.Dispose();
        if (_spatialIndex.IsCreated) _spatialIndex.Dispose();
        if (_jobCounters.IsCreated) _jobCounters.Dispose();
    }

    private void BuildObservers()
    {
        _observers.Clear();
        foreach (var (definition, team, units, detectedEnemies, squadTargets, entity) in
                 SystemAPI.Query<
                         RefRO<SquadDefinitionComponent>,
                         RefRO<TeamComponent>,
                         DynamicBuffer<SquadUnitElement>,
                         DynamicBuffer<DetectedEnemy>,
                         DynamicBuffer<SquadTargetEntity>>()
                     .WithEntityAccess())
        {
            detectedEnemies.Clear();
            squadTargets.Clear();

            float3 centroid = float3.zero;
            int aliveCount = 0;
            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (!EntityManager.Exists(unit) || !_transformLookup.HasComponent(unit)
                    || EntityManager.HasComponent<IsDeadComponent>(unit))
                    continue;

                centroid += _transformLookup[unit].Position;
                aliveCount++;
            }

            if (aliveCount == 0)
                continue;

            centroid /= aliveCount;
            float range = definition.ValueRO.detectionRange;
            float rangeSquared = range * range;
            _observers.Add(new DetectionObserver
            {
                Entity = entity,
                Team = team.ValueRO.value,
                Centroid = centroid,
                RangeSquared = rangeSquared
            });
        }

        DebugObserverCount = _observers.Length;
    }

    private void BuildCandidatesAndIndex()
    {
        _candidates.Clear();
        _fallbackCandidateIndices.Clear();

        // Keep every live squad-member occurrence, including duplicate buffer entries.
        // This matches the previous nested scan's observable candidate multiplicity.
        foreach (var (team, units, squad) in
                 SystemAPI.Query<RefRO<TeamComponent>, DynamicBuffer<SquadUnitElement>>()
                     .WithAll<SquadDataComponent>()
                     .WithEntityAccess())
        {
            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (!EntityManager.Exists(unit) || !_transformLookup.HasComponent(unit)
                    || EntityManager.HasComponent<IsDeadComponent>(unit))
                    continue;

                _candidates.Add(new DetectionCandidate
                {
                    Entity = unit,
                    Squad = squad,
                    Team = team.ValueRO.value,
                    Position = _transformLookup[unit].Position
                });
            }
        }

        // Heroes remain a second pass, after all squad-member occurrences, and keep query order.
        foreach (var (team, transform, hero) in
                 SystemAPI.Query<RefRO<TeamComponent>, RefRO<LocalTransform>>()
                     .WithAll<DetectableEntityTag>()
                     .WithEntityAccess())
        {
            if (EntityManager.HasComponent<IsDeadComponent>(hero))
                continue;
            if (_heroLifeLookup.HasComponent(hero) && !_heroLifeLookup[hero].isAlive)
                continue;

            _candidates.Add(new DetectionCandidate
            {
                Entity = hero,
                Squad = Entity.Null,
                Team = team.ValueRO.value,
                Position = transform.ValueRO.Position,
                IsHero = true
            });
        }

        DebugCandidateOccurrences = _candidates.Length;
        _spatialIndex.Clear();
        if (_spatialIndex.Capacity < _candidates.Length)
            _spatialIndex.Capacity = _candidates.Length;

        float cellSize = 0f;
        for (int i = 0; i < _observers.Length; i++)
        {
            float rangeSquared = _observers[i].RangeSquared;
            if (IsFinite(rangeSquared) && rangeSquared > cellSize * cellSize)
                cellSize = math.sqrt(rangeSquared);
        }

        for (int i = 0; i < _candidates.Length; i++)
        {
            var candidate = _candidates[i];
            if (cellSize > 0f && TryGetCell(candidate.Position, cellSize, out int2 cell))
            {
                _spatialIndex.Add(cell, i);
            }
            else
            {
                _fallbackCandidateIndices.Add(i);
            }
        }

        for (int i = 0; i < _observers.Length; i++)
        {
            var observer = _observers[i];
            float radius = IsFinite(observer.RangeSquared) ? math.sqrt(observer.RangeSquared) : 0f;
            observer.UseGrid = cellSize > 0f
                && IsFinite(observer.RangeSquared)
                && TryGetCellBounds(observer.Centroid, radius, cellSize, out observer.MinCell, out observer.MaxCell);
            _observers[i] = observer;
        }
    }

    private void PublishResults()
    {
        for (int observerIndex = 0; observerIndex < _observers.Length; observerIndex++)
        {
            var observer = _observers[observerIndex];
            var segment = _observerResultSegments[observerIndex];
            var detectedEnemies = EntityManager.GetBuffer<DetectedEnemy>(observer.Entity);
            var squadTargets = EntityManager.GetBuffer<SquadTargetEntity>(observer.Entity);
            Entity lastSquad = Entity.Null;
            bool hasLastSquad = false;

            for (int resultIndex = segment.x; resultIndex < segment.y; resultIndex++)
            {
                var candidate = _candidates[_resultCandidateIndices[resultIndex]];
                if (candidate.Team == observer.Team)
                    continue;

                if (candidate.IsHero)
                {
                    detectedEnemies.Add(new DetectedEnemy { Value = candidate.Entity });
                }
                else if (!hasLastSquad || candidate.Squad != lastSquad)
                {
                    detectedEnemies.Add(new DetectedEnemy { Value = candidate.Squad });
                    lastSquad = candidate.Squad;
                    hasLastSquad = true;
                }

                squadTargets.Add(new SquadTargetEntity { Value = candidate.Entity });
                DebugPublishedTargets++;
            }
        }
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static bool TryGetCell(float3 position, float cellSize, out int2 cell)
    {
        cell = default;
        if (!math.all(math.isfinite(position)))
            return false;

        double x = Math.Floor((double)position.x / cellSize);
        double z = Math.Floor((double)position.z / cellSize);
        if (x < int.MinValue || x > int.MaxValue || z < int.MinValue || z > int.MaxValue)
            return false;

        cell = new int2((int)x, (int)z);
        return true;
    }

    private static bool TryGetCellBounds(float3 center, float radius, float cellSize, out int2 min, out int2 max)
    {
        min = default;
        max = default;
        if (!math.all(math.isfinite(center)) || !IsFinite(radius))
            return false;

        double minX = Math.Floor(((double)center.x - radius) / cellSize);
        double maxX = Math.Floor(((double)center.x + radius) / cellSize);
        double minZ = Math.Floor(((double)center.z - radius) / cellSize);
        double maxZ = Math.Floor(((double)center.z + radius) / cellSize);
        if (minX < int.MinValue || minX > int.MaxValue || maxX < int.MinValue || maxX > int.MaxValue
            || minZ < int.MinValue || minZ > int.MaxValue || maxZ < int.MinValue || maxZ > int.MaxValue)
            return false;

        min = new int2((int)minX, (int)minZ);
        max = new int2((int)maxX, (int)maxZ);
        return true;
    }

    [BurstCompile]
    private struct SearchCandidatesJob : IJob
    {
        [ReadOnly] public NativeList<DetectionCandidate> Candidates;
        [ReadOnly] public NativeList<DetectionObserver> Observers;
        [ReadOnly] public NativeList<int> FallbackCandidateIndices;
        [ReadOnly] public NativeParallelMultiHashMap<int2, int> SpatialIndex;
        public NativeList<int> ResultCandidateIndices;
        public NativeList<int> ScratchCandidateIndices;
        public NativeList<int2> ObserverResultSegments;
        public NativeArray<int> Counters;

        public void Execute()
        {
            for (int observerIndex = 0; observerIndex < Observers.Length; observerIndex++)
            {
                var observer = Observers[observerIndex];
                ScratchCandidateIndices.Clear();
                if (observer.UseGrid)
                {
                    // Include a one-cell guard band so float rounding at an exact
                    // detection boundary cannot remove a candidate from the broadphase.
                    for (long z = (long)observer.MinCell.y - 1; z <= (long)observer.MaxCell.y + 1; z++)
                    {
                        for (long x = (long)observer.MinCell.x - 1; x <= (long)observer.MaxCell.x + 1; x++)
                        {
                            if (x < int.MinValue || x > int.MaxValue || z < int.MinValue || z > int.MaxValue)
                                continue;
                            Counters[0]++;
                            var key = new int2((int)x, (int)z);
                            if (!SpatialIndex.TryGetFirstValue(key, out int candidateIndex, out var iterator))
                                continue;

                            do
                            {
                                CheckCandidate(observer, candidateIndex);
                            }
                            while (SpatialIndex.TryGetNextValue(out candidateIndex, ref iterator));
                        }
                    }

                    for (int i = 0; i < FallbackCandidateIndices.Length; i++)
                        CheckCandidate(observer, FallbackCandidateIndices[i]);
                }
                else
                {
                    for (int i = 0; i < Candidates.Length; i++)
                        CheckCandidate(observer, i);
                }

                ScratchCandidateIndices.Sort();
                int start = ResultCandidateIndices.Length;
                for (int i = 0; i < ScratchCandidateIndices.Length; i++)
                    ResultCandidateIndices.Add(ScratchCandidateIndices[i]);
                ObserverResultSegments[observerIndex] = new int2(start, ResultCandidateIndices.Length);
            }
        }

        private void CheckCandidate(DetectionObserver observer, int candidateIndex)
        {
            var candidate = Candidates[candidateIndex];
            if (candidate.Team == observer.Team)
                return;

            Counters[1]++;
            if (math.distancesq(observer.Centroid, candidate.Position) > observer.RangeSquared)
                return;

            ScratchCandidateIndices.Add(candidateIndex);
        }
    }
}

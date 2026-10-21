using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// Reciprocal velocity avoidance between allied bodies (C14, decision D1), on top of <see cref="OrcaSolver"/>.
/// Allies dodge each other by velocity instead of being pushed; enemies are skipped (they are kept apart by the
/// stop distance, D2). An agent shares the avoidance with a neighbour that also steers and takes all of it
/// against one that does not (a hero, or a unit of another squad holding its slot).
/// Within a squad only the units that move avoid each other, with a combined radius below the grid spacing:
/// slots are one body apart, and a row of formed squadmates would otherwise be a wall the unit cannot cross
/// on its way to its slot (ORCA has no path planning to go around it).
/// </summary>
public static class AllyAvoidance
{
    public struct Agent
    {
        public float2 position;
        public float2 velocity;
        public float2 preferred;   // Velocity the agent would take without neighbours.
        public float maxSpeed;
        public float timeHorizon;
        public float squadSpacing; // Formation spacing of its squad (m).
        public int team;
        public int squad;          // -1 for bodies outside any squad (heroes).
        public bool steered;       // Gets a new velocity and shares the avoidance.
    }

    public struct Settings
    {
        public float neighborRadius;
        public int maxNeighbors;
        public float combinedRadius;
        public float squadmateSpacingFraction; // Squadmates keep this fraction of their spacing apart.
        public float collisionResolveTime;
    }

    /// <summary>
    /// Velocity reachable this frame: moves from <paramref name="current"/> towards <paramref name="target"/> by at most
    /// <paramref name="maxDelta"/> (acceleration × dt), or <paramref name="maxBrakeDelta"/> when slowing down, so avoidance
    /// never changes speed faster than the agent can.
    /// </summary>
    public static float2 LimitAcceleration(float2 current, float2 target, float maxDelta, float maxBrakeDelta)
    {
        if (math.lengthsq(target) < math.lengthsq(current)) maxDelta = math.max(maxDelta, maxBrakeDelta);
        float2 delta = target - current;
        float lengthSq = math.lengthsq(delta);
        if (maxDelta <= 0f || lengthSq <= maxDelta * maxDelta) return target;
        return current + delta * (maxDelta / math.sqrt(lengthSq));
    }

    /// <summary>Fills <paramref name="velocities"/> with the avoiding velocity of every agent (unsteered ones keep theirs).</summary>
    public static void Run(NativeArray<Agent> agents, NativeArray<float2> velocities, in Settings settings)
    {
        using var grid = new NativeParallelMultiHashMap<int2, int>(math.max(1, agents.Length), Allocator.TempJob);
        Run(agents, velocities, grid, settings);
    }

    /// <summary>
    /// Same, reusing a caller-owned grid (M9): it is cleared and only grows, so a warm frame allocates nothing.
    /// </summary>
    public static void Run(NativeArray<Agent> agents, NativeArray<float2> velocities,
        NativeParallelMultiHashMap<int2, int> grid, in Settings settings)
    {
        float cellSize = math.max(0.5f, settings.neighborRadius);
        grid.Clear();
        if (grid.Capacity < agents.Length) grid.Capacity = agents.Length;
        new AllyAvoidanceGridJob { agents = agents, cellSize = cellSize, grid = grid.AsParallelWriter() }
            .Schedule(agents.Length, 64).Complete();
        new AllyAvoidanceSolveJob
        {
            agents = agents, grid = grid, cellSize = cellSize, settings = settings, velocities = velocities,
        }.Schedule(agents.Length, 32).Complete();
    }
}

/// <summary>Buckets the agents into a uniform grid of neighbour-radius cells.</summary>
[BurstCompile(CompileSynchronously = true)]
struct AllyAvoidanceGridJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<AllyAvoidance.Agent> agents;
    public float cellSize;
    public NativeParallelMultiHashMap<int2, int>.ParallelWriter grid;

    public void Execute(int i) => grid.Add((int2)math.floor(agents[i].position / cellSize), i);
}

/// <summary>Collects the nearest allies of each steered agent and solves its ORCA velocity.</summary>
[BurstCompile(CompileSynchronously = true)]
struct AllyAvoidanceSolveJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<AllyAvoidance.Agent> agents;
    [ReadOnly] public NativeParallelMultiHashMap<int2, int> grid;
    public float cellSize;
    public AllyAvoidance.Settings settings;
    [WriteOnly] public NativeArray<float2> velocities;

    public void Execute(int i)
    {
        AllyAvoidance.Agent self = agents[i];
        if (!self.steered) { velocities[i] = self.velocity; return; }

        // K nearest allies within the radius, kept sorted by distance.
        var nearest = new FixedList128Bytes<int>();
        var nearestDistSq = new FixedList128Bytes<float>();
        int maxNeighbors = math.clamp(settings.maxNeighbors, 0, 28);
        float radiusSq = settings.neighborRadius * settings.neighborRadius;
        int2 cell = (int2)math.floor(self.position / cellSize);
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            if (!grid.TryGetFirstValue(cell + new int2(dx, dy), out int j, out var it)) continue;
            do
            {
                if (j == i || agents[j].team != self.team) continue;
                if (self.squad >= 0 && agents[j].squad == self.squad && !agents[j].steered) continue;
                float distSq = math.distancesq(agents[j].position, self.position);
                if (distSq > radiusSq) continue;
                Insert(ref nearest, ref nearestDistSq, j, distSq, maxNeighbors);
            } while (grid.TryGetNextValue(out j, ref it));
        }

        var lines = new FixedList512Bytes<float4>();
        for (int n = 0; n < nearest.Length; n++)
        {
            AllyAvoidance.Agent other = agents[nearest[n]];
            float responsibility = other.steered ? 0.5f : 1f;
            float combinedRadius = self.squad >= 0 && other.squad == self.squad
                ? math.min(settings.combinedRadius, settings.squadmateSpacingFraction * self.squadSpacing)
                : settings.combinedRadius;
            lines.Add(OrcaSolver.Line(other.position - self.position, self.velocity - other.velocity,
                self.velocity, combinedRadius, self.timeHorizon, settings.collisionResolveTime,
                responsibility));
        }
        velocities[i] = OrcaSolver.Solve(ref lines, self.preferred, self.maxSpeed);
    }

    static void Insert(ref FixedList128Bytes<int> indices, ref FixedList128Bytes<float> distances,
        int index, float distSq, int capacity)
    {
        if (capacity == 0) return;
        if (indices.Length == capacity && distSq >= distances[indices.Length - 1]) return;
        if (indices.Length == capacity) { indices.RemoveAt(indices.Length - 1); distances.RemoveAt(distances.Length - 1); }
        int at = indices.Length;
        while (at > 0 && distances[at - 1] > distSq) at--;
        indices.Insert(at, index);
        distances.Insert(at, distSq);
    }
}

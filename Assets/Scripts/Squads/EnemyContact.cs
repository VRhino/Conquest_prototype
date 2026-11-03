using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// Enemy bodies as a constraint on the mover's velocity (M2, P1): "collide and slide", the reference's pattern. Before the
/// agent integrates, a unit that moves drops the part of its velocity that would close the gap to an enemy faster than
/// the gap allows and keeps the sideways part. A still unit is never pushed, and allies stay with ORCA
/// (<see cref="AllyAvoidance"/>): one writer per relation. Overlaps that already exist are left to the bodyblock.
/// </summary>
public static class EnemyContact
{
    /// <summary>Contact passes per agent: removing one approach can add another against a second enemy.</summary>
    public const int Passes = 3;
    /// <summary>Nearest enemies checked per agent.</summary>
    const int MaxEnemies = 8;
    /// <summary>cos 30°: a blocker lies within ±30° of where the unit wants to go (P4).</summary>
    public const float BlockCone = 0.866f;

    /// <summary>
    /// The velocity left after one enemy: the approach towards it may close the gap (contact distance minus distance)
    /// in no less than <paramref name="closeTime"/>, never more; inside the contact distance no approach at all.
    /// </summary>
    public static float2 Slide(float2 velocity, float2 toEnemy, float contactDistance, float closeTime)
    {
        float distance = math.length(toEnemy);
        if (distance < 1e-5f) return velocity;
        float2 normal = toEnemy / distance;
        float approach = math.dot(velocity, normal);
        float allowed = math.max(0f, distance - contactDistance) / math.max(1e-4f, closeTime);
        return approach > allowed ? velocity - normal * (approach - allowed) : velocity;
    }

    /// <summary>A displacement that may close the gap to the enemy (contact distance minus distance) but never pass it.</summary>
    public static float2 ClipStep(float2 step, float2 toEnemy, float contactDistance)
        => Slide(step, toEnemy, contactDistance, 1f);

    /// <summary>True when the enemy clips the velocity and lies within ±30° of the preferred direction: it stands in the way.</summary>
    public static bool Blocks(float2 preferred, float2 velocity, float2 toEnemy, float contactDistance, float closeTime)
    {
        float distance = math.length(toEnemy);
        if (distance < 1e-5f || math.lengthsq(preferred) < 1e-6f) return false;
        if (math.dot(math.normalize(preferred), toEnemy / distance) < BlockCone) return false;
        return math.any(Slide(velocity, toEnemy, contactDistance, closeTime) != velocity);
    }

    /// <summary>
    /// Clips the velocity of every steered agent against the enemies near it. Reuses the grid
    /// <see cref="AllyAvoidance.Run(NativeArray{AllyAvoidance.Agent}, NativeArray{float2}, NativeParallelMultiHashMap{int2, int}, in AllyAvoidance.Settings)"/>
    /// filled this frame, with its cell size; the contact distance plus one frame of travel must fit in a cell.
    /// </summary>
    public static void Run(NativeArray<AllyAvoidance.Agent> agents, NativeArray<float2> velocities,
        NativeParallelMultiHashMap<int2, int> grid, float cellSize, float contactDistance, float closeTime)
    {
        using var blockers = new NativeArray<int>(agents.Length, Allocator.TempJob);
        Run(agents, velocities, grid, cellSize, contactDistance, closeTime, blockers);
    }

    /// <summary>As above, and <paramref name="blockers"/> gets, per agent, the index of the nearest enemy that
    /// <see cref="Blocks"/> it, or -1 (P4).</summary>
    public static void Run(NativeArray<AllyAvoidance.Agent> agents, NativeArray<float2> velocities,
        NativeParallelMultiHashMap<int2, int> grid, float cellSize, float contactDistance, float closeTime,
        NativeArray<int> blockers)
    {
        new EnemyContactJob
        {
            agents = agents, grid = grid, cellSize = cellSize, contactDistance = contactDistance,
            closeTime = closeTime, velocities = velocities, blockers = blockers,
        }.Schedule(agents.Length, 32).Complete();
    }

    [BurstCompile(CompileSynchronously = true)]
    struct EnemyContactJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<AllyAvoidance.Agent> agents;
        [ReadOnly] public NativeParallelMultiHashMap<int2, int> grid;
        public float cellSize, contactDistance, closeTime;
        public NativeArray<float2> velocities;
        [WriteOnly] public NativeArray<int> blockers;

        public void Execute(int i)
        {
            blockers[i] = -1;
            var self = agents[i];
            // A pair collides only if both have the enemy collision on (P2).
            if (!self.steered || self.passesEnemies) return;
            float2 velocity = velocities[i];
            float selfRadius = AllyAvoidance.RadiusOf(self, contactDistance);
            float travel = math.length(velocity) * closeTime;
            var enemies = new FixedList64Bytes<int>();
            var contacts = new FixedList64Bytes<float>(); // Contact distance of each pair: both bodies (L5-5).
            int2 cell = (int2)math.floor(self.position / cellSize);
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (!grid.TryGetFirstValue(cell + new int2(dx, dy), out int j, out var it)) continue;
                do
                {
                    if (j == i || agents[j].team == self.team || agents[j].passesEnemies) continue;
                    // Only enemies the unit can reach before the gap must be closed matter.
                    float pair = selfRadius + AllyAvoidance.RadiusOf(agents[j], contactDistance);
                    if (math.distancesq(agents[j].position, self.position) > math.square(pair + travel)) continue;
                    if (enemies.Length < MaxEnemies) { enemies.Add(j); contacts.Add(pair); }
                } while (grid.TryGetNextValue(out j, ref it));
            }
            if (enemies.Length == 0) return;
            int blocker = -1;
            float blockerDistanceSq = float.MaxValue;
            for (int n = 0; n < enemies.Length; n++)
            {
                float2 toEnemy = agents[enemies[n]].position - self.position;
                if (!Blocks(self.preferred, velocity, toEnemy, contacts[n], closeTime)) continue;
                if (math.lengthsq(toEnemy) < blockerDistanceSq) { blockerDistanceSq = math.lengthsq(toEnemy); blocker = enemies[n]; }
            }
            blockers[i] = blocker;
            for (int pass = 0; pass < Passes; pass++)
                for (int n = 0; n < enemies.Length; n++)
                    velocity = Slide(velocity, agents[enemies[n]].position - self.position, contacts[n], closeTime);
            velocities[i] = velocity;
        }
    }
}

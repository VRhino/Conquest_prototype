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

    /// <summary>
    /// Clips the velocity of every steered agent against the enemies near it. Reuses the grid
    /// <see cref="AllyAvoidance.Run(NativeArray{AllyAvoidance.Agent}, NativeArray{float2}, NativeParallelMultiHashMap{int2, int}, in AllyAvoidance.Settings)"/>
    /// filled this frame, with its cell size; the contact distance plus one frame of travel must fit in a cell.
    /// </summary>
    public static void Run(NativeArray<AllyAvoidance.Agent> agents, NativeArray<float2> velocities,
        NativeParallelMultiHashMap<int2, int> grid, float cellSize, float contactDistance, float closeTime)
    {
        new EnemyContactJob
        {
            agents = agents, grid = grid, cellSize = cellSize, contactDistance = contactDistance,
            closeTime = closeTime, velocities = velocities,
        }.Schedule(agents.Length, 32).Complete();
    }

    [BurstCompile(CompileSynchronously = true)]
    struct EnemyContactJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<AllyAvoidance.Agent> agents;
        [ReadOnly] public NativeParallelMultiHashMap<int2, int> grid;
        public float cellSize, contactDistance, closeTime;
        public NativeArray<float2> velocities;

        public void Execute(int i)
        {
            var self = agents[i];
            // A pair collides only if both have the enemy collision on (P2).
            if (!self.steered || self.passesEnemies) return;
            float2 velocity = velocities[i];
            // Only enemies the unit can reach before the gap must be closed matter.
            float reach = contactDistance + math.length(velocity) * closeTime;
            float reachSq = reach * reach;
            var enemies = new FixedList64Bytes<int>();
            int2 cell = (int2)math.floor(self.position / cellSize);
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (!grid.TryGetFirstValue(cell + new int2(dx, dy), out int j, out var it)) continue;
                do
                {
                    if (j == i || agents[j].team == self.team || agents[j].passesEnemies) continue;
                    if (math.distancesq(agents[j].position, self.position) > reachSq) continue;
                    if (enemies.Length < MaxEnemies) enemies.Add(j);
                } while (grid.TryGetNextValue(out j, ref it));
            }
            if (enemies.Length == 0) return;
            for (int pass = 0; pass < Passes; pass++)
                for (int n = 0; n < enemies.Length; n++)
                    velocity = Slide(velocity, agents[enemies[n]].position - self.position, contactDistance, closeTime);
            velocities[i] = velocity;
        }
    }
}

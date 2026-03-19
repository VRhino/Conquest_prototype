using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Detects enemy squads within detection range and populates the three combat
/// detection buffers every frame:
///   - <see cref="DetectedEnemy"/> (per squad) — enemy squad entities in range
///   - <see cref="SquadTargetEntity"/> (per squad) — individual enemy unit entities in range
///   - <see cref="UnitDetectedEnemy"/> (per unit) — propagated from the squad's SquadTargetEntity
///
/// Runs before <see cref="SquadAISystem"/> so intent is decided with fresh data.
/// Uses an AABB-free centroid distance check — no physics queries required.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(SquadAISystem))]
public partial class EnemyDetectionSystem : SystemBase
{
    private ComponentLookup<LocalTransform> _transformLookup;
    private BufferLookup<UnitDetectedEnemy> _unitDetectedLookup;
    private BufferLookup<SquadUnitElement>  _squadUnitLookup;

    protected override void OnCreate()
    {
        _transformLookup    = GetComponentLookup<LocalTransform>(true);
        _unitDetectedLookup = GetBufferLookup<UnitDetectedEnemy>(false);
        _squadUnitLookup    = GetBufferLookup<SquadUnitElement>(true);
    }

    protected override void OnUpdate()
    {
        _transformLookup.Update(this);
        _unitDetectedLookup.Update(this);
        _squadUnitLookup.Update(this);

        // PASS 1 — squad level: detect enemy units within detectionRange
        foreach (var (dataA, teamA, unitsA, detectedEnemies, squadTargets, entityA) in
                 SystemAPI.Query<
                     RefRO<SquadDataComponent>,
                     RefRO<TeamComponent>,
                     DynamicBuffer<SquadUnitElement>,
                     DynamicBuffer<DetectedEnemy>,
                     DynamicBuffer<SquadTargetEntity>>()
                 .WithEntityAccess())
        {
            detectedEnemies.Clear();
            squadTargets.Clear();

            // Compute centroid of squad A from alive unit positions
            float3 centroidA  = float3.zero;
            int    aliveCount = 0;
            for (int i = 0; i < unitsA.Length; i++)
            {
                Entity uA = unitsA[i].Value;
                if (!SystemAPI.Exists(uA) || !_transformLookup.HasComponent(uA))
                    continue;
                centroidA += _transformLookup[uA].Position;
                aliveCount++;
            }
            if (aliveCount == 0)
                continue;

            centroidA /= aliveCount;
            float detectionRangeSq = dataA.ValueRO.detectionRange * dataA.ValueRO.detectionRange;

            // Scan all other squads for enemy units within range
            foreach (var (dataB, teamB, unitsB, entityB) in
                     SystemAPI.Query<
                         RefRO<SquadDataComponent>,
                         RefRO<TeamComponent>,
                         DynamicBuffer<SquadUnitElement>>()
                     .WithEntityAccess())
            {
                if (entityB == entityA)
                    continue;
                if (teamB.ValueRO.value == teamA.ValueRO.value)
                    continue;

                bool squadRegistered = false;
                for (int j = 0; j < unitsB.Length; j++)
                {
                    Entity uB = unitsB[j].Value;
                    if (!SystemAPI.Exists(uB) || !_transformLookup.HasComponent(uB))
                        continue;

                    float3 posB = _transformLookup[uB].Position;
                    if (math.distancesq(centroidA, posB) > detectionRangeSq)
                        continue;

                    if (!squadRegistered)
                    {
                        detectedEnemies.Add(new DetectedEnemy { Value = entityB });
                        squadRegistered = true;
                    }
                    squadTargets.Add(new SquadTargetEntity { Value = uB });
                }
            }

            // PASS 2 — propagate SquadTargetEntity to each own unit's UnitDetectedEnemy buffer
            for (int i = 0; i < unitsA.Length; i++)
            {
                Entity uA = unitsA[i].Value;
                if (!SystemAPI.Exists(uA) || !_unitDetectedLookup.HasBuffer(uA))
                    continue;

                var unitBuf = _unitDetectedLookup[uA];
                unitBuf.Clear();
                for (int j = 0; j < squadTargets.Length; j++)
                    unitBuf.Add(new UnitDetectedEnemy { Value = squadTargets[j].Value });
            }
        }
    }
}

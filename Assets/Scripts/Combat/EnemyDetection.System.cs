using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Detects enemy squads within detection range and populates the three combat
/// detection buffers every frame:
///   - <see cref="DetectedEnemy"/> (per squad) — enemy squad entities in range
///   - <see cref="SquadTargetEntity"/> (per squad) — individual enemy unit entities in range
///
/// Runs before <see cref="SquadAISystem"/> so intent is decided with fresh data.
/// Uses an AABB-free centroid distance check — no physics queries required.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(SquadAISystem))]
public partial class EnemyDetectionSystem : SystemBase
{
    private ComponentLookup<LocalTransform>  _transformLookup;
    private ComponentLookup<HeroLifeComponent> _heroLifeLookup;
    static readonly Unity.Profiling.ProfilerMarker DetectionMarker = new Unity.Profiling.ProfilerMarker("Conquest.EnemyDetection");

    protected override void OnCreate()
    {
        _transformLookup    = GetComponentLookup<LocalTransform>(true);
        _heroLifeLookup     = GetComponentLookup<HeroLifeComponent>(true);
    }

    protected override void OnUpdate()
    {
        using var detectionSample = DetectionMarker.Auto();
        _transformLookup.Update(this);
        _heroLifeLookup.Update(this);

        // PASS 1 — squad level: detect enemy units within detectionRange
        foreach (var (defA, teamA, unitsA, detectedEnemies, squadTargets, entityA) in
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
            // Compute centroid of squad A from alive unit positions
            float3 centroidA  = float3.zero;
            int    aliveCount = 0;
            for (int i = 0; i < unitsA.Length; i++)
            {
                Entity uA = unitsA[i].Value;
                if (!SystemAPI.Exists(uA) || !_transformLookup.HasComponent(uA) || SystemAPI.HasComponent<IsDeadComponent>(uA))
                    continue;
                centroidA += _transformLookup[uA].Position;
                aliveCount++;
            }
            if (aliveCount == 0)
                continue;

            centroidA /= aliveCount;
            float detectionRangeSq = defA.ValueRO.detectionRange * defA.ValueRO.detectionRange;

            // Scan all other squads for enemy units within range
            foreach (var (teamB, unitsB, entityB) in
                     SystemAPI.Query<
                         RefRO<TeamComponent>,
                         DynamicBuffer<SquadUnitElement>>()
                     .WithAll<SquadDataComponent>()
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
                    if (!SystemAPI.Exists(uB) || !_transformLookup.HasComponent(uB) || SystemAPI.HasComponent<IsDeadComponent>(uB))
                        continue;

                    float3 posB = _transformLookup[uB].Position;
                    float distSq = math.distancesq(centroidA, posB);
                    if (distSq > detectionRangeSq)
                        continue;

                    if (!squadRegistered)
                    {
                        detectedEnemies.Add(new DetectedEnemy { Value = entityB });
                        squadRegistered = true;
                    }
                    squadTargets.Add(new SquadTargetEntity { Value = uB });
                }
            }

            // PASS 2 — append detectable non-squad enemies (for example heroes).
            foreach (var (heroTeam, heroTransform, heroEntity) in
                     SystemAPI.Query<
                         RefRO<TeamComponent>,
                         RefRO<LocalTransform>>()
                     .WithAll<DetectableEntityTag>()
                     .WithEntityAccess())
            {
                if (heroTeam.ValueRO.value == teamA.ValueRO.value) continue;
                if (SystemAPI.HasComponent<IsDeadComponent>(heroEntity)) continue;
                // If the entity has a HeroLifeComponent, respect its alive state
                if (_heroLifeLookup.HasComponent(heroEntity) && !_heroLifeLookup[heroEntity].isAlive) continue;

                float distSq = math.distancesq(centroidA, heroTransform.ValueRO.Position);
                if (distSq > detectionRangeSq) continue;

                // Signal squad-level detection so SquadAISystem sets TacticalIntent.Attacking
                detectedEnemies.Add(new DetectedEnemy { Value = heroEntity });

                squadTargets.Add(new SquadTargetEntity { Value = heroEntity });
            }

        }
    }
}

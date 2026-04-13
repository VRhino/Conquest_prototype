using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Computes the squad's formation anchor (position + rotation) each frame
/// and writes it to SquadFormationAnchorComponent.
///
/// Four cases in priority order:
///   1. HoldingPosition → holdCenter + holdRotation
///   2. Retreating      → retreatTarget + default rotation
///   3. InCombat ranged → stop at range * 0.85 from enemy centroid; freeze if already in range
///   4. Default (Follow)→ heroPos + forward * followForwardOffset + hero rotation
///
/// Centralises all anchor logic so FormationSystem, GridFormationUpdateSystem,
/// and DestinationMarkerSystem can simply read the component instead of each
/// duplicating this branching logic.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitTargetingSystem))]
[UpdateBefore(typeof(FormationSystem))]
public partial class SquadAnchorSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
    }

    protected override void OnUpdate()
    {
        var targetBufferLookup = GetBufferLookup<SquadTargetEntity>(true);
        var transformLookup    = GetComponentLookup<LocalTransform>(true);

        foreach (var (state, anchor, heroWorldPos, data, squadEntity) in SystemAPI
                     .Query<RefRO<SquadStateComponent>,
                            RefRW<SquadFormationAnchorComponent>,
                            RefRO<HeroWorldPositionComponent>,
                            RefRO<SquadDataComponent>>()
                     .WithEntityAccess())
        {
            float3     position;
            quaternion rotation = default; // default == zero quaternion (no-rotation sentinel)

            if (state.ValueRO.currentState == SquadFSMState.HoldingPosition
                && SystemAPI.HasComponent<SquadHoldPositionComponent>(squadEntity))
            {
                var hold = SystemAPI.GetComponent<SquadHoldPositionComponent>(squadEntity);
                position = hold.holdCenter;
                rotation = hold.holdRotation;
            }
            else if (state.ValueRO.currentState == SquadFSMState.Retreating
                     && SystemAPI.HasComponent<RetreatComponent>(squadEntity))
            {
                position = SystemAPI.GetComponent<RetreatComponent>(squadEntity).retreatTarget;
                // rotation stays default
            }
            else if (state.ValueRO.currentState == SquadFSMState.InCombat
                     && data.ValueRO.isRangedUnit
                     && TryGetEnemyCentroid(squadEntity, targetBufferLookup, transformLookup, out float3 enemyCentroid))
            {
                float3 heroPos  = heroWorldPos.ValueRO.position;
                float3 toEnemy  = enemyCentroid - heroPos;
                float  dist     = math.length(toEnemy);
                float  stopDist = data.ValueRO.range * 0.85f;

                if (dist > stopDist)
                    // Advance until we are stopDist from the enemy centroid
                    position = enemyCentroid - math.normalizesafe(toEnemy) * stopDist;
                else
                    // Already in range: freeze the anchor where it is
                    position = anchor.ValueRO.position;

                rotation = heroWorldPos.ValueRO.rotation;
            }
            else
            {
                float followOffset =
                    SystemAPI.GetSingleton<SquadSpawnConfigComponent>().followForwardOffset;
                float3 heroPos = heroWorldPos.ValueRO.position
                                 + math.forward(heroWorldPos.ValueRO.rotation) * followOffset;
                position = heroPos;
                rotation = heroWorldPos.ValueRO.rotation;
            }

            float3 prevPosition = anchor.ValueRO.position;
            anchor.ValueRW.position = position;
            anchor.ValueRW.rotation = rotation;
            bool isMoving = math.lengthsq(position - prevPosition) > 0.01f;
            SystemAPI.SetComponentEnabled<SquadAnchorMovingTag>(squadEntity, isMoving);
        }
    }

    /// <summary>
    /// Returns the average world position of all valid enemy targets in the squad's
    /// SquadTargetEntity buffer. Returns false if no targets have a known transform.
    /// </summary>
    static bool TryGetEnemyCentroid(
        Entity squadEntity,
        BufferLookup<SquadTargetEntity> targetBufferLookup,
        ComponentLookup<LocalTransform>  transformLookup,
        out float3 centroid)
    {
        centroid = float3.zero;

        if (!targetBufferLookup.HasBuffer(squadEntity))
            return false;

        var targets = targetBufferLookup[squadEntity];
        if (targets.Length == 0)
            return false;

        float3 sum   = float3.zero;
        int    count = 0;
        foreach (var t in targets)
        {
            if (transformLookup.HasComponent(t.Value))
            {
                sum += transformLookup[t.Value].Position;
                count++;
            }
        }

        if (count == 0)
            return false;

        centroid = sum / count;
        return true;
    }
}

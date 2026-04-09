using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Computes the squad's formation anchor (position + rotation) each frame
/// and writes it to SquadFormationAnchorComponent.
///
/// Three cases in priority order:
///   1. HoldingPosition → holdCenter + holdRotation
///   2. Retreating      → retreatTarget + default rotation
///   3. Default (Follow)→ heroPos + forward * followForwardOffset + hero rotation
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
        foreach (var (state, anchor, heroWorldPos, squadEntity) in SystemAPI
                     .Query<RefRO<SquadStateComponent>,
                            RefRW<SquadFormationAnchorComponent>,
                            RefRO<HeroWorldPositionComponent>>()
                     .WithEntityAccess())
        {
            float3    position;
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
}

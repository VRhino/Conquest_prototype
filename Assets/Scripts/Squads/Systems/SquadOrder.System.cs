using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// System that interprets the <see cref="SquadInputComponent"/> and updates
/// <see cref="SquadStateComponent"/> accordingly. It only runs when a new
/// order has been issued.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SquadOrderSystem : SystemBase
{
    private EntityCommandBuffer.ParallelWriter _ecb;
    private BeginSimulationEntityCommandBufferSystem _ecbSystem;

    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
        _ecbSystem = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        var ecb = _ecbSystem.CreateCommandBuffer().AsParallelWriter();
        var transformLookup = GetComponentLookup<LocalTransform>(true);

        foreach (var (input, state, formation, owner, resolved, entity) in SystemAPI
                     .Query<RefRW<SquadInputComponent>,
                            RefRW<SquadStateComponent>,
                            RefRW<FormationComponent>,
                            RefRO<SquadOwnerComponent>,
                            RefRW<SquadResolvedOrderComponent>>()
                     .WithEntityAccess())
        {
            if (!resolved.ValueRO.hasNewOrder)
                continue;

            // Copy the winning order to the state component
            state.ValueRW.currentOrder     = resolved.ValueRO.order;
            state.ValueRW.isExecutingOrder = true;

            // Handle Hold Position order specifically
            if (resolved.ValueRO.order == SquadOrderType.HoldPosition)
            {
                // Capture hero's current facing rotation for the formation
                quaternion heroRotation = quaternion.identity;
                Entity heroEntity = owner.ValueRO.hero;
                if (transformLookup.HasComponent(heroEntity))
                {
                    heroRotation = transformLookup[heroEntity].Rotation;
                }

                // Create or update SquadHoldPositionComponent with mouse position and hero rotation
                if (SystemAPI.HasComponent<SquadHoldPositionComponent>(entity))
                {
                    var holdComponent = SystemAPI.GetComponentRW<SquadHoldPositionComponent>(entity);
                    holdComponent.ValueRW.holdCenter        = resolved.ValueRO.holdPosition;
                    holdComponent.ValueRW.holdRotation      = heroRotation;
                    holdComponent.ValueRW.originalFormation = input.ValueRO.desiredFormation;
                }
                else
                {
                    ecb.AddComponent(entity.Index, entity, new SquadHoldPositionComponent
                    {
                        holdCenter        = resolved.ValueRO.holdPosition,
                        holdRotation      = heroRotation,
                        originalFormation = input.ValueRO.desiredFormation
                    });
                }
            }
            else
            {
                // Remove SquadHoldPositionComponent when not in Hold Position
                if (SystemAPI.HasComponent<SquadHoldPositionComponent>(entity))
                {
                    ecb.RemoveComponent<SquadHoldPositionComponent>(entity.Index, entity);
                }
            }

            // Request formation change if needed (formation still sourced from SquadInputComponent)
            if (input.ValueRO.desiredFormation != formation.ValueRO.currentFormation)
            {
                formation.ValueRW.currentFormation = input.ValueRO.desiredFormation;
            }

            // Request a state transition via the FSM system
            var newState = OrderToState(resolved.ValueRO.order);
            state.ValueRW.transitionTo = newState;

            // Clear both flags so the order is not re-processed next frame
            resolved.ValueRW.hasNewOrder = false;
            input.ValueRW.hasNewOrder    = false;
        }

        _ecbSystem.AddJobHandleForProducer(Dependency);
    }

    static SquadFSMState OrderToState(SquadOrderType order)
    {
        return order switch
        {
            SquadOrderType.FollowHero => SquadFSMState.FollowingHero,
            SquadOrderType.HoldPosition => SquadFSMState.HoldingPosition,
            SquadOrderType.Attack => SquadFSMState.InCombat,
            _ => SquadFSMState.Idle,
        };
    }
}

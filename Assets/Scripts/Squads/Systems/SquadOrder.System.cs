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
        _ecbSystem = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        var ecb = _ecbSystem.CreateCommandBuffer().AsParallelWriter();
        var transformLookup = GetComponentLookup<LocalTransform>(true);

        foreach (var (input, state, formation, owner, entity) in SystemAPI
                     .Query<RefRW<SquadInputComponent>,
                            RefRW<SquadStateComponent>,
                            RefRW<FormationComponent>,
                            RefRO<SquadOwnerComponent>>()
                     .WithEntityAccess())
        {
            if (!input.ValueRO.hasNewOrder)
                continue;

            // Process the order without debug logging

            // Copy the requested order to the state component
            state.ValueRW.currentOrder = input.ValueRO.orderType;
            state.ValueRW.isExecutingOrder = true;

            // Handle Hold Position order specifically
            if (input.ValueRO.orderType == SquadOrderType.HoldPosition)
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
                    holdComponent.ValueRW.holdCenter = input.ValueRO.holdPosition;
                    holdComponent.ValueRW.holdRotation = heroRotation;
                    holdComponent.ValueRW.originalFormation = input.ValueRO.desiredFormation;
                }
                else
                {
                    ecb.AddComponent(entity.Index, entity, new SquadHoldPositionComponent
                    {
                        holdCenter = input.ValueRO.holdPosition,
                        holdRotation = heroRotation,
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

            // Request formation change if needed
            if (input.ValueRO.desiredFormation != state.ValueRO.currentFormation)
            {
                // NO cambiar currentFormation aquí - eso es responsabilidad del FormationSystem
                // Solo actualizar FormationComponent si es necesario
                if (formation.ValueRO.currentFormation != input.ValueRO.desiredFormation)
                {
                    formation.ValueRW.currentFormation = input.ValueRO.desiredFormation;
                }
            }

            // Request a state transition if using the FSM system
            var newState = OrderToState(input.ValueRO.orderType);
            state.ValueRW.transitionTo = newState;

            input.ValueRW.hasNewOrder = false;
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

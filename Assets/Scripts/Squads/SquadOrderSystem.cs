using Unity.Entities;
using Unity.Mathematics;

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
        
        foreach (var (input, state, formation, entity) in SystemAPI
                     .Query<RefRW<SquadInputComponent>,
                            RefRW<SquadStateComponent>,
                            RefRW<FormationComponent>>()
                     .WithEntityAccess())
        {
            if (!input.ValueRO.hasNewOrder)
                continue;

            // Copy the requested order to the state component
            state.ValueRW.currentOrder = input.ValueRO.orderType;
            state.ValueRW.isExecutingOrder = true;

            // Handle Hold Position order specifically
            if (input.ValueRO.orderType == SquadOrderType.HoldPosition)
            {
                // Create or update SquadHoldPositionComponent with mouse position
                if (SystemAPI.HasComponent<SquadHoldPositionComponent>(entity))
                {
                    var holdComponent = SystemAPI.GetComponentRW<SquadHoldPositionComponent>(entity);
                    holdComponent.ValueRW.holdCenter = input.ValueRO.holdPosition;
                    holdComponent.ValueRW.originalFormation = input.ValueRO.desiredFormation;
                }
                else
                {
                    ecb.AddComponent(entity.Index, entity, new SquadHoldPositionComponent
                    {
                        holdCenter = input.ValueRO.holdPosition,
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
            state.ValueRW.transitionTo = OrderToState(input.ValueRO.orderType);

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

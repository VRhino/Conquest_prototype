using Unity.Entities;

/// <summary>
/// System that interprets the <see cref="SquadInputComponent"/> and updates
/// <see cref="SquadStateComponent"/> accordingly. It only runs when a new
/// order has been issued.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SquadOrderSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach (var (input, state, entity) in SystemAPI
                     .Query<RefRW<SquadInputComponent>, RefRW<SquadStateComponent>>()
                     .WithEntityAccess())
        {
            if (!input.ValueRO.hasNewOrder)
                continue;

            // Copy the requested order to the state component
            state.ValueRW.currentOrder = input.ValueRO.orderType;
            state.ValueRW.isExecutingOrder = true;

            // Request formation change if needed
            if (input.ValueRO.desiredFormation != state.ValueRO.currentFormation)
            {
                state.ValueRW.currentFormation = input.ValueRO.desiredFormation;
            }

            // Attempt to update FSM state if the entity has such component
            if (SystemAPI.HasComponent<SquadFSMStateComponent>(entity))
            {
                var fsm = SystemAPI.GetComponentRW<SquadFSMStateComponent>(entity);
                if (fsm.ValueRO.currentState != OrderToState(input.ValueRO.orderType))
                    fsm.ValueRW.currentState = OrderToState(input.ValueRO.orderType);
            }

            input.ValueRW.hasNewOrder = false;
        }
    }

    static SquadFSMState OrderToState(SquadOrderType order)
    {
        return order switch
        {
            SquadOrderType.FollowHero => SquadFSMState.FollowingHero,
            SquadOrderType.HoldPosition => SquadFSMState.HoldingPosition,
            SquadOrderType.Attack => SquadFSMState.Attacking,
            _ => SquadFSMState.Idle,
        };
    }
}

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

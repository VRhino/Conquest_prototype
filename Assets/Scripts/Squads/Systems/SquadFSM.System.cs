using Unity.Entities;
using UnityEngine;

/// <summary>
/// Finite state machine system that decides the tactical state for each squad.
/// It only sets the logical state; other systems react to it (movement, combat,
/// retreat...).
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(SquadOrderSystem))]
public partial class SquadFSMSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;

        foreach (var (state, units, entity) in SystemAPI
                     .Query<RefRW<SquadStateComponent>, DynamicBuffer<SquadUnitElement>>()
                     .WithEntityAccess())
        {
            var s = state.ValueRW;

            // Apply pending transition
            if (s.currentState != s.transitionTo)
            {
                // Transition to the new state
                s.currentState = s.transitionTo;
                s.stateTimer = 0f;
            }
            else
            {
                s.stateTimer += dt;
            }

            // Determine KO if all units are gone
            bool allDead = units.Length == 0;
            if (!allDead)
            {
                allDead = true;
                for (int i = 0; i < units.Length; i++)
                {
                    if (SystemAPI.Exists(units[i].Value))
                    {
                        allDead = false;
                        break;
                    }
                }
            }
            if (allDead)
            {
                s.transitionTo = SquadFSMState.KO;
                state.ValueRW = s;
                continue;
            }

            // Determine next state based on conditions
            SquadFSMState desired = s.currentState;

            if (!s.lastOwnerAlive && !s.retreatTriggered)
            {
                desired = SquadFSMState.Retreating;
            }
            else if (s.isInCombat)
            {
                desired = SquadFSMState.InCombat;
            }
            else if (s.currentOrder == SquadOrderType.FollowHero)
            {
                desired = SquadFSMState.FollowingHero;
            }
            else if (s.currentOrder == SquadOrderType.HoldPosition)
            {
                desired = SquadFSMState.HoldingPosition;
            }
            else if (!s.isExecutingOrder)
            {
                desired = SquadFSMState.Idle;
            }

            // Enforce minimum time in combat
            if (s.currentState == SquadFSMState.InCombat && desired != SquadFSMState.InCombat)
            {
                if (s.stateTimer < 3f)
                    desired = SquadFSMState.InCombat;
            }

            if (desired == SquadFSMState.Retreating)
                s.retreatTriggered = true;

            s.transitionTo = desired;
            state.ValueRW = s;
        }
    }
}

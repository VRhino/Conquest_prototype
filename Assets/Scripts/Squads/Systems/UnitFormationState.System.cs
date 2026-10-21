using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(GridFormationUpdateSystem))]
public partial struct UnitFormationStateSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<MatchStateComponent>();
        state.RequireForUpdate<MovementTuningComponent>();
    }

    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        var tuning = SystemAPI.GetSingleton<MovementTuningComponent>();
        var radii = FormationArrivalRadii.From(tuning);

        foreach (var (units, squadEntity) in SystemAPI.Query<DynamicBuffer<SquadUnitElement>>().WithEntityAccess())
        {
            if (!SystemAPI.HasComponent<SquadStateComponent>(squadEntity))
                continue;

            var squadState = SystemAPI.GetComponent<SquadStateComponent>(squadEntity);

            if (!SystemAPI.HasComponent<SquadFormationAnchorComponent>(squadEntity))
                continue;

            bool heroMovingForSquad = SystemAPI.IsComponentEnabled<SquadAnchorMovingTag>(squadEntity);

            // Determinar si el escuadrón está en modo Hold Position
            bool isHoldingPosition = squadState.currentOrder == SquadOrderType.HoldPosition
                && squadState.currentState != SquadFSMState.Retreating;


            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (!SystemAPI.HasComponent<UnitGridSlotComponent>(unit) ||
                    !SystemAPI.HasComponent<LocalTransform>(unit) ||
                    !SystemAPI.HasComponent<UnitFormationStateComponent>(unit))
                    continue;

                var stateComp = SystemAPI.GetComponent<UnitFormationStateComponent>(unit);

                if (!SystemAPI.HasComponent<UnitTargetPositionComponent>(unit))
                    continue;
                float3 requestedSlotPos = SystemAPI.GetComponent<UnitTargetPositionComponent>(unit).position;
                float3 desiredSlotPos = SquadNavigationSystem.GetEffectiveFormationDestination(
                    state.EntityManager, unit, requestedSlotPos);

                float3 currentPos = SystemAPI.GetComponent<LocalTransform>(unit).Position;

                float distSq = math.lengthsq(desiredSlotPos - currentPos);

                // ── Milestone tags: reset to off each frame, then re-enable on transition ──
                bool hasMilestoneTags = SystemAPI.HasComponent<UnitStartedMovingTag>(unit);
                if (hasMilestoneTags)
                {
                    SystemAPI.SetComponentEnabled<UnitStartedMovingTag>(unit, false);
                    SystemAPI.SetComponentEnabled<UnitArrivedAtSlotTag>(unit, false);
                }

                // Arrival hysteresis (r_in / r_out / calm time). Hold Position leaves Formed above the hold radius.
                stateComp.State = FormationArrival.Step(stateComp.State, distSq, radii, isHoldingPosition,
                    heroMovingForSquad, dt, ref stateComp.CalmTimer, ref stateComp.DelayTimer,
                    stateComp.DelayDuration, out var arrivalEvent);

                if (arrivalEvent == FormationArrivalEvent.LeftSlot)
                {
                    var slot = SystemAPI.GetComponent<UnitGridSlotComponent>(unit);
                    stateComp.DelayDuration = FormationStartDelay.Compute(slot.row, slot.slotIndex,
                        tuning.startRowDelay, tuning.startJitterDelay);
                }
                if (hasMilestoneTags && arrivalEvent == FormationArrivalEvent.StartedMoving)
                    SystemAPI.SetComponentEnabled<UnitStartedMovingTag>(unit, true);
                if (hasMilestoneTags && arrivalEvent == FormationArrivalEvent.Arrived)
                    SystemAPI.SetComponentEnabled<UnitArrivedAtSlotTag>(unit, true);

                SystemAPI.SetComponent(unit, stateComp);
            }
        }
    }
}

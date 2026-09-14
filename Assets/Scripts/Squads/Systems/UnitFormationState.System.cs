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
        state.RequireForUpdate<SquadSpawnConfigComponent>();
    }

    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        var spawnConfig = SystemAPI.GetSingleton<SquadSpawnConfigComponent>();
        float slotThresholdSq = math.square(math.max(0f, spawnConfig.slotArrivalThreshold));
        float holdPositionThresholdSq = math.square(math.max(0f, spawnConfig.holdReformThreshold));

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

                // Use unified position checker
                bool inSlot = FormationPositionCalculator.IsUnitInSlot(currentPos, desiredSlotPos, slotThresholdSq);

                // ── Milestone tags: reset to off each frame, then re-enable on transition ──
                bool hasMilestoneTags = SystemAPI.HasComponent<UnitStartedMovingTag>(unit);
                if (hasMilestoneTags)
                {
                    SystemAPI.SetComponentEnabled<UnitStartedMovingTag>(unit, false);
                    SystemAPI.SetComponentEnabled<UnitArrivedAtSlotTag>(unit, false);
                }

                // State transition logic
                if (isHoldingPosition)
                {
                    // En Hold Position: transiciones de estado simplificadas
                    // Las unidades solo se mueven si están muy lejos de su posición asignada
                    // Usar un threshold más grande solo para detectar si necesita reorganizarse
                    switch (stateComp.State)
                    {
                        case UnitFormationState.Formed:
                            // Cambiar a Waiting si está fuera de la posición asignada
                            if (!FormationPositionCalculator.IsUnitInSlot(currentPos, desiredSlotPos, holdPositionThresholdSq))
                            {
                                stateComp.State = UnitFormationState.Waiting;
                                stateComp.DelayTimer = 0f;
                                stateComp.DelayDuration = UnityEngine.Random.Range(spawnConfig.unitMoveDelayMin, spawnConfig.unitMoveDelayMax);
                            }
                            break;
                        case UnitFormationState.Waiting:
                            // Esperar el delay antes de pasar a Moving
                            stateComp.DelayTimer += dt;
                            if (stateComp.DelayTimer >= stateComp.DelayDuration)
                            {
                                stateComp.State = UnitFormationState.Moving;
                                stateComp.DelayTimer = 0f;
                                if (hasMilestoneTags) SystemAPI.SetComponentEnabled<UnitStartedMovingTag>(unit, true);
                            }
                            // Si vuelve a estar en slot durante el delay, regresar a Formed
                            else if (FormationPositionCalculator.IsUnitInSlot(currentPos, desiredSlotPos, holdPositionThresholdSq))
                            {
                                stateComp.State = UnitFormationState.Formed;
                                stateComp.DelayTimer = 0f;
                            }
                            break;
                        case UnitFormationState.Moving:
                            // Moving -> Formed: Unit reaches assigned position
                            if (inSlot)
                            {
                                stateComp.State = UnitFormationState.Formed;
                                stateComp.DelayTimer = 0f;
                                if (hasMilestoneTags) SystemAPI.SetComponentEnabled<UnitArrivedAtSlotTag>(unit, true);
                            }
                            break;
                    }
                }
                else
                {
                    // Comportamiento normal de seguimiento al héroe
                    // Usar thresholds normales para formaciones precisas
                    switch (stateComp.State)
                    {
                        case UnitFormationState.Formed:
                            // Formed -> Waiting: Hero leaves grid radius OR unit is far from assigned slot (formation changed)
                            bool nearAssignedSlot = FormationPositionCalculator.IsUnitInSlot(
                                currentPos, desiredSlotPos, slotThresholdSq);
                            if (!nearAssignedSlot)
                            {
                                stateComp.State = UnitFormationState.Waiting;
                                stateComp.DelayTimer = 0f;
                                stateComp.DelayDuration = UnityEngine.Random.Range(spawnConfig.unitFollowDelayMin, spawnConfig.unitFollowDelayMax);
                            }
                            break;

                        case UnitFormationState.Waiting:
                            // Waiting -> Moving: Random delay expires
                            stateComp.DelayTimer += dt;
                            if (stateComp.DelayTimer >= stateComp.DelayDuration)
                            {
                                stateComp.State = UnitFormationState.Moving;
                                stateComp.DelayTimer = 0f;
                                if (hasMilestoneTags) SystemAPI.SetComponentEnabled<UnitStartedMovingTag>(unit, true);
                            }
                            // Waiting -> Formed: Hero returns to radius while still waiting
                            else if (inSlot)
                            {
                                stateComp.State = UnitFormationState.Formed;
                                stateComp.DelayTimer = 0f;
                            }
                            break;

                        case UnitFormationState.Moving:
                            // Moving -> Formed: Unit reaches slot AND hero is within radius
                            if (inSlot)
                            {
                                if (!heroMovingForSquad)
                                {
                                    // Si el héroe no se está moviendo, la unidad se forma
                                    stateComp.State = UnitFormationState.Formed;
                                    stateComp.DelayTimer = 0f;
                                    if (hasMilestoneTags) SystemAPI.SetComponentEnabled<UnitArrivedAtSlotTag>(unit, true);
                                }
                                else
                                {
                                    // Si el héroe se está moviendo, la unidad sigue al héroe
                                    stateComp.State = UnitFormationState.Moving;
                                }
                            }
                            // Note: If hero leaves radius while moving, unit continues moving
                            // and will transition to Waiting only after reaching slot (if hero still out)
                            // or directly to Formed if hero returns to radius when unit reaches slot
                            break;
                    }
                }

                SystemAPI.SetComponent(unit, stateComp);
            }
        }
    }
}

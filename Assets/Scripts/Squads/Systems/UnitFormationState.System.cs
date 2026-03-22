using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(GridFormationUpdateSystem))]
public partial struct UnitFormationStateSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        var spawnConfig = SystemAPI.GetSingleton<SquadSpawnConfigComponent>();
        const float formationRadiusSq = 100f; // 10m squared — supports up to 20-unit formations
        const float slotThresholdSq = 0.04f; // ~0.2m threshold for being "in slot" (reducido de 0.25f)

        foreach (var (units, squadEntity) in SystemAPI.Query<DynamicBuffer<SquadUnitElement>>().WithEntityAccess())
        {
            if (!SystemAPI.HasComponent<SquadOwnerComponent>(squadEntity))
                continue;

            if (!SystemAPI.HasComponent<SquadStateComponent>(squadEntity))
                continue;

            var squadState = SystemAPI.GetComponent<SquadStateComponent>(squadEntity);

            Entity hero = SystemAPI.GetComponent<SquadOwnerComponent>(squadEntity).hero;
            if (!SystemAPI.HasComponent<LocalTransform>(hero) || !SystemAPI.HasComponent<HeroStateComponent>(hero))
                continue;

            float3 heroPos = SystemAPI.GetComponent<LocalTransform>(hero).Position;
            var heroState = SystemAPI.GetComponent<HeroStateComponent>(hero).State;

            // Determinar si el escuadrón está en modo Hold Position
            bool isHoldingPosition = squadState.currentState == SquadFSMState.HoldingPosition;

            var localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            float farestDistSq = FormationPositionCalculator.GetFarestUnitDistanceSq(units, localTransformLookup, heroPos, out Entity farestUnit);
            bool heroWithinRadius = farestDistSq <= formationRadiusSq;
            
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
                float3 desiredSlotPos = SystemAPI.GetComponent<UnitTargetPositionComponent>(unit).position;

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
                    const float holdPositionThresholdSq = 1.0f; // 1 metro cuadrado para cambios de formación (reducido de 4.0f)
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
                            if (!heroWithinRadius || !nearAssignedSlot)
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
                            else if (heroWithinRadius && inSlot)
                            {
                                stateComp.State = UnitFormationState.Formed;
                                stateComp.DelayTimer = 0f;
                            }
                            break;

                        case UnitFormationState.Moving:
                            // Obtener el estado de movimiento del héroe
                            bool heroMovingForSquad = heroState == HeroState.Moving;
                            // Moving -> Formed: Unit reaches slot AND hero is within radius
                            if (inSlot && heroWithinRadius)
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

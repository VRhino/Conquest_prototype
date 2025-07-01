using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct UnitFormationStateSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        const float formationRadiusSq = 25f; // 5m squared
        const float slotThresholdSq = 0.25f; // ~0.5m threshold for being "in slot"

        foreach (var (units, squadEntity) in SystemAPI.Query<DynamicBuffer<SquadUnitElement>>().WithEntityAccess())
        {
            if (!SystemAPI.HasComponent<SquadOwnerComponent>(squadEntity))
                continue;

            Entity hero = SystemAPI.GetComponent<SquadOwnerComponent>(squadEntity).hero;
            if (!SystemAPI.HasComponent<LocalTransform>(hero) || !SystemAPI.HasComponent<HeroStateComponent>(hero))
                continue;

            float3 heroPos = SystemAPI.GetComponent<LocalTransform>(hero).Position;
            var heroState = SystemAPI.GetComponent<HeroStateComponent>(hero).State;

            // Calculate squad center (average position of all units)
            float3 squadCenter = float3.zero;
            int count = 0;
            foreach (var unitElem in units)
            {
                if (SystemAPI.HasComponent<LocalTransform>(unitElem.Value))
                {
                    squadCenter += SystemAPI.GetComponent<LocalTransform>(unitElem.Value).Position;
                    count++;
                }
            }
            if (count > 0) squadCenter /= count;

            float heroDistSq = math.lengthsq(heroPos - squadCenter);
            bool heroWithinRadius = heroDistSq <= formationRadiusSq;

            foreach (var unitElem in units)
            {
                Entity unit = unitElem.Value;
                if (!SystemAPI.HasComponent<UnitFormationSlotComponent>(unit) ||
                    !SystemAPI.HasComponent<LocalTransform>(unit) ||
                    !SystemAPI.HasComponent<UnitFormationStateComponent>(unit))
                    continue;

                var slot = SystemAPI.GetComponent<UnitFormationSlotComponent>(unit);
                var stateComp = SystemAPI.GetComponent<UnitFormationStateComponent>(unit);

                // Calculate desired position using same logic as UnitFollowFormationSystem
                float3 heroForward = math.forward(SystemAPI.GetComponent<LocalTransform>(hero).Rotation);
                float3 formationBase = heroPos - heroForward * 5f; // Formation 5 meters behind hero
                
                // Snap offset to grid for consistency
                float3 gridOffset = FormationGridSystem.SnapToGrid(slot.relativeOffset);
                float3 desiredSlotPos = formationBase + gridOffset;
                float3 currentPos = SystemAPI.GetComponent<LocalTransform>(unit).Position;
                float distToSlotSq = math.lengthsq(desiredSlotPos - currentPos);
                
                bool inSlot = distToSlotSq <= slotThresholdSq;

                // State transition logic
                switch (stateComp.State)
                {
                    case UnitFormationState.Formed:
                        // Formed -> Waiting: Hero leaves grid radius
                        if (!heroWithinRadius)
                        {
                            stateComp.State = UnitFormationState.Waiting;
                            stateComp.DelayTimer = 0f;
                            stateComp.DelayDuration = UnityEngine.Random.Range(0.5f, 1.5f);
                        }
                        break;

                    case UnitFormationState.Waiting:
                        // Waiting -> Moving: Random delay expires
                        stateComp.DelayTimer += dt;
                        if (stateComp.DelayTimer >= stateComp.DelayDuration)
                        {
                            stateComp.State = UnitFormationState.Moving;
                            stateComp.DelayTimer = 0f;
                        }
                        // Waiting -> Formed: Hero returns to radius while still waiting
                        else if (heroWithinRadius && inSlot)
                        {
                            stateComp.State = UnitFormationState.Formed;
                            stateComp.DelayTimer = 0f;
                        }
                        break;

                    case UnitFormationState.Moving:
                        // Moving -> Formed: Unit reaches slot AND hero is within radius
                        if (inSlot && heroWithinRadius)
                        {
                            stateComp.State = UnitFormationState.Formed;
                            stateComp.DelayTimer = 0f;
                        }
                        // Note: If hero leaves radius while moving, unit continues moving
                        // and will transition to Waiting only after reaching slot (if hero still out)
                        // or directly to Formed if hero returns to radius when unit reaches slot
                        break;
                }

                SystemAPI.SetComponent(unit, stateComp);
            }
        }
    }
}

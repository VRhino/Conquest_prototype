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
        const float slotThresholdSq = 0.25f; // ~0.5m

        foreach (var (units, squadEntity) in SystemAPI.Query<DynamicBuffer<SquadUnitElement>>().WithEntityAccess())
        {
            if (!SystemAPI.HasComponent<SquadOwnerComponent>(squadEntity))
                continue;

            Entity hero = SystemAPI.GetComponent<SquadOwnerComponent>(squadEntity).hero;
            if (!SystemAPI.HasComponent<LocalTransform>(hero) || !SystemAPI.HasComponent<HeroStateComponent>(hero))
                continue;

            float3 heroPos = SystemAPI.GetComponent<LocalTransform>(hero).Position;
            var heroState = SystemAPI.GetComponent<HeroStateComponent>(hero).State;

            // Calculate squad center
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
            bool heroOutsideRadius = heroDistSq > formationRadiusSq;

            foreach (var unitElem in units)
            {
                Entity unit = unitElem.Value;
                if (!SystemAPI.HasComponent<UnitFormationSlotComponent>(unit) ||
                    !SystemAPI.HasComponent<LocalTransform>(unit) ||
                    !SystemAPI.HasComponent<UnitFormationStateComponent>(unit))
                    continue;

                var slot = SystemAPI.GetComponent<UnitFormationSlotComponent>(unit);
                var stateComp = SystemAPI.GetComponent<UnitFormationStateComponent>(unit);

                float3 desired = heroPos + slot.relativeOffset;
                float3 current = SystemAPI.GetComponent<LocalTransform>(unit).Position;
                float distSq = math.lengthsq(desired - current);

                switch (stateComp.State)
                {
                    case UnitFormationState.Formed:
                        if (heroOutsideRadius)
                        {
                            if (!stateComp.Waiting)
                            {
                                stateComp.Delay = UnityEngine.Random.Range(0.5f, 1.5f);
                                stateComp.Timer = 0f;
                                stateComp.Waiting = true;
                            }
                            else
                            {
                                stateComp.Timer += dt;
                                if (stateComp.Timer >= stateComp.Delay)
                                {
                                    stateComp.State = UnitFormationState.Moving;
                                    stateComp.Waiting = false;
                                }
                            }
                        }
                        else
                        {
                            stateComp.Waiting = false;
                            stateComp.Timer = 0f;
                        }
                        break;
                    case UnitFormationState.Moving:
                        // Solo puede volver a Formed si está en slot Y el héroe está dentro del radio
                        if (distSq <= slotThresholdSq && !heroOutsideRadius)
                        {
                            stateComp.State = UnitFormationState.Formed;
                            stateComp.Waiting = false;
                            stateComp.Timer = 0f;
                        }
                        break;
                }
                SystemAPI.SetComponent(unit, stateComp);
            }
        }
    }
}

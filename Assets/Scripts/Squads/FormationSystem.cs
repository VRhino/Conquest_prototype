using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Assigns target positions to squad units based on the selected formation.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class FormationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (input, state, formationData, units) in SystemAPI
                    .Query<RefRO<SquadInputComponent>,
                            RefRW<SquadStateComponent>,
                            RefRO<SquadFormationDataComponent>,
                            DynamicBuffer<SquadUnitElement>>())
        {
            var s = state.ValueRW;
            s.formationChangeCooldown = math.max(0f, s.formationChangeCooldown - deltaTime);

            if (input.ValueRO.desiredFormation == s.currentFormation ||
                s.formationChangeCooldown > 0f)
            {
                state.ValueRW = s;
                continue;
            }

            if (!formationData.ValueRO.formationLibrary.IsCreated || units.Length == 0)
            {
                state.ValueRW = s;
                continue;
            }

            ref var formations = ref formationData.ValueRO.formationLibrary.Value.formations;
            ref BlobArray<float3> offsets = ref formations[0].localOffsets; // fallback inicial

            bool found = false;
            for (int i = 0; i < formations.Length; i++)
            {
                if (formations[i].formationType == input.ValueRO.desiredFormation)
                {
                    ref var formation = ref formations[i];
                    offsets = ref formation.localOffsets;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                state.ValueRW = s;
                continue;
            }

            Entity leader = units[0].Value;
            if (!SystemAPI.Exists(leader))
            {
                state.ValueRW = s;
                continue;
            }

            float3 leaderPos = SystemAPI.GetComponent<LocalTransform>(leader).Position;
            
            // Calcular punto base de la formación relativo al héroe
            float3 heroForward = math.forward(SystemAPI.GetComponent<LocalTransform>(leader).Rotation);
            float3 formationBase = leaderPos - heroForward * 5f;

            int count = math.min(units.Length, offsets.Length);
            for (int i = 0; i < count; i++)
            {
                Entity unit = units[i].Value;
                if (!SystemAPI.Exists(unit))
                    continue;

                float3 target = formationBase + offsets[i];
                
                // Actualizar UnitTargetPositionComponent
                if (SystemAPI.HasComponent<UnitTargetPositionComponent>(unit))
                {
                    var targetPos = SystemAPI.GetComponentRW<UnitTargetPositionComponent>(unit);
                    targetPos.ValueRW.position = target;
                }
                else
                {
                    EntityManager.AddComponentData(unit, new UnitTargetPositionComponent { position = target });
                }
                
                // También actualizar UnitFormationSlotComponent para consistencia
                if (SystemAPI.HasComponent<UnitFormationSlotComponent>(unit))
                {
                    var slotComp = SystemAPI.GetComponentRW<UnitFormationSlotComponent>(unit);
                    slotComp.ValueRW.relativeOffset = offsets[i];
                    slotComp.ValueRW.slotIndex = i;
                }
                
                // Marcar la unidad como Moving para que se reposicione inmediatamente
                if (SystemAPI.HasComponent<UnitFormationStateComponent>(unit))
                {
                    var formationState = SystemAPI.GetComponentRW<UnitFormationStateComponent>(unit);
                    formationState.ValueRW.State = UnitFormationState.Moving;
                    formationState.ValueRW.Timer = 0f;
                    formationState.ValueRW.Waiting = false;
                }
            }

            s.currentFormation = input.ValueRO.desiredFormation;
            s.formationChangeCooldown = 1f;
            state.ValueRW = s;
        }
    }

}

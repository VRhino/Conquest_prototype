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

            var formations = formationData.ValueRO.formationLibrary.Value.formations;
            BlobArray<float3> offsets = default;
            bool found = false;
            for (int i = 0; i < formations.Length; i++)
            {
                if (formations[i].formationType == input.ValueRO.desiredFormation)
                {
                    offsets = formations[i].localOffsets;
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

            int count = math.min(units.Length, offsets.Length);
            for (int i = 0; i < count; i++)
            {
                Entity unit = units[i].Value;
                if (!SystemAPI.Exists(unit))
                    continue;

                float3 target = leaderPos + offsets[i];
                if (SystemAPI.HasComponent<UnitTargetPositionComponent>(unit))
                {
                    var targetPos = SystemAPI.GetComponentRW<UnitTargetPositionComponent>(unit);
                    targetPos.ValueRW.position = target;
                }
                else
                {
                    EntityManager.AddComponentData(unit, new UnitTargetPositionComponent { position = target });
                }
            }

            s.currentFormation = input.ValueRO.desiredFormation;
            s.formationChangeCooldown = 1f;
            state.ValueRW = s;
        }
    }
}

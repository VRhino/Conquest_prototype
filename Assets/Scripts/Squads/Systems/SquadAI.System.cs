using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Decides the tactical AI state for each squad based on the current order,
/// detected enemies and squad cohesion. Movement or attacks are handled by
/// other systems.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SquadAISystem : SystemBase
{
    protected override void OnUpdate()
    {
        var dataLookup = GetComponentLookup<SquadDataComponent>(true);

        foreach (var (ai, state, dataRef, units, entity) in SystemAPI
                     .Query<RefRW<SquadAIComponent>,
                            RefRW<SquadStateComponent>,
                            RefRO<SquadDataReference>,
                            DynamicBuffer<SquadUnitElement>>()
                     .WithEntityAccess())
        {
            bool enemiesDetected = false;
            if (SystemAPI.HasBuffer<DetectedEnemy>(entity))
            {
                var detected = SystemAPI.GetBuffer<DetectedEnemy>(entity);
                enemiesDetected = detected.Length > 0;
            }

            // Dispersed check: compare each unit to the formation anchor, not to units[0]
            bool dispersed = false;
            if (SystemAPI.HasComponent<SquadFormationAnchorComponent>(entity))
            {
                float3 anchorPos = SystemAPI.GetComponent<SquadFormationAnchorComponent>(entity).position;
                // Scale radius with squad size: ~2× half-width of max line formation
                float cohesionRadiusSq = math.max(100f, units.Length * units.Length * 2f);
                for (int i = 0; i < units.Length; i++)
                {
                    Entity u = units[i].Value;
                    if (!SystemAPI.Exists(u) || !SystemAPI.HasComponent<LocalTransform>(u)) continue;
                    float3 pos = SystemAPI.GetComponent<LocalTransform>(u).Position;
                    if (math.distancesq(pos, anchorPos) > cohesionRadiusSq)
                    {
                        dispersed = true;
                        break;
                    }
                }
            }

            BehaviorProfile profile = BehaviorProfile.Versatile;
            if (dataLookup.TryGetComponent(dataRef.ValueRO.dataEntity, out var data))
                profile = data.behaviorProfile;

            TacticalIntent  desiredState;
            switch (profile)
            {
                case BehaviorProfile.Defensive:
                    desiredState = dispersed ? TacticalIntent .Regrouping
                                             : enemiesDetected ? TacticalIntent .Defending
                                                               : TacticalIntent .Idle;
                    break;
                case BehaviorProfile.Harassing:
                    desiredState = dispersed ? TacticalIntent .Regrouping
                                             : enemiesDetected ? TacticalIntent .Attacking
                                                               : TacticalIntent .Idle;
                    break;
                case BehaviorProfile.AntiCharge:
                    desiredState = dispersed ? TacticalIntent .Regrouping
                                             : TacticalIntent .Defending;
                    break;
                default: // Versatile
                    if (dispersed)
                        desiredState = TacticalIntent .Regrouping;
                    else if (enemiesDetected)
                        desiredState = TacticalIntent .Attacking;
                    else if (state.ValueRO.currentOrder == SquadOrderType.HoldPosition)
                        desiredState = TacticalIntent .Defending;
                    else
                        desiredState = TacticalIntent .Idle;
                    break;
            }

            ai.ValueRW.tacticalIntent = desiredState;
            ai.ValueRW.isInCombat = enemiesDetected;
            state.ValueRW.isInCombat = enemiesDetected;


        }
    }
}

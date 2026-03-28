using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Decides the tactical AI state for each squad based on the current order,
/// detected enemies and squad cohesion. Movement or attacks are handled by
/// other systems.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(SquadFSMSystem))]
public partial class SquadAISystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
    }

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

            // Dispersed check + retaliation signal sweep (single unit loop for both)
            bool dispersed = false;
            bool wasHit    = false;
            bool isBracing = SystemAPI.HasComponent<SquadCombatModeComponent>(entity)
                          && SystemAPI.GetComponent<SquadCombatModeComponent>(entity).mode == SquadCombatMode.Brace;

            if (SystemAPI.HasComponent<SquadFormationAnchorComponent>(entity))
            {
                float3 anchorPos = SystemAPI.GetComponent<SquadFormationAnchorComponent>(entity).position;
                // Scale radius with squad size: ~2× half-width of max line formation
                float cohesionRadiusSq = math.max(100f, units.Length * units.Length * 2f);
                for (int i = 0; i < units.Length; i++)
                {
                    Entity u = units[i].Value;
                    if (!SystemAPI.Exists(u)) continue;

                    if (SystemAPI.HasComponent<LocalTransform>(u))
                    {
                        float3 pos = SystemAPI.GetComponent<LocalTransform>(u).Position;
                        if (math.distancesq(pos, anchorPos) > cohesionRadiusSq)
                            dispersed = true;
                    }

                    // Consume retaliation pulse set by DamageCalculationSystem
                    if (!wasHit
                        && SystemAPI.HasComponent<IsUnderAttackTag>(u)
                        && SystemAPI.IsComponentEnabled<IsUnderAttackTag>(u))
                    {
                        wasHit = true;
                        SystemAPI.SetComponentEnabled<IsUnderAttackTag>(u, false);
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
                                             : enemiesDetected ? TacticalIntent .Attacking
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

            // Retaliation override: a unit was hit this frame — always retaliate regardless of brace mode
            if (wasHit && desiredState != TacticalIntent.Attacking)
                desiredState = TacticalIntent.Attacking;

            bool hasAnchor = SystemAPI.HasComponent<SquadFormationAnchorComponent>(entity);


            ai.ValueRW.tacticalIntent = desiredState;
            ai.ValueRW.isInCombat     = enemiesDetected || wasHit;
            state.ValueRW.isInCombat  = enemiesDetected || wasHit;


        }
    }
}

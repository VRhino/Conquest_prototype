using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Decides the tactical AI state for each squad based on the current order,
/// detected enemies and squad cohesion. Movement or attacks are handled by
/// other systems.
/// Cohesion (F12): a soldier farther than <c>stragglerDistance</c> from its resolved slot has not arrived, one whose
/// slot is <see cref="FormationSlotResolveStatus.Unresolved"/> is isolated and left out of the count, and the formation
/// is ready per <see cref="FormationLayout.IsFormationReady"/>. The AI hero waits for it before charging.
/// </summary>
[ServerAuthoritative] // Decides the match (L4-06)
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
        var readiness = SystemAPI.TryGetSingleton<SquadSpawnConfigComponent>(out var config)
            ? config.formationReadiness.OrDefault() : FormationReadinessTuning.Default;
        float stragglerDistanceSq = readiness.stragglerDistance * readiness.stragglerDistance;

        foreach (var (ai, state, dataRef, def, units, entity) in SystemAPI
                     .Query<RefRW<SquadAIComponent>,
                            RefRW<SquadStateComponent>,
                            RefRO<SquadDataReference>,
                            RefRO<SquadDefinitionComponent>,
                            DynamicBuffer<SquadUnitElement>>()
                     .WithEntityAccess())
        {
            bool enemiesDetected = false;
            if (SystemAPI.HasBuffer<DetectedEnemy>(entity))
            {
                var detected = SystemAPI.GetBuffer<DetectedEnemy>(entity);
                enemiesDetected = detected.Length > 0;
            }

            // Cohesion count + retaliation signal sweep (single unit loop for both)
            int  counted    = 0;
            int  stragglers = 0;
            int  isolated   = 0;
            bool wasHit     = false;
            bool isBracing = SystemAPI.HasComponent<SquadCombatModeComponent>(entity)
                          && SystemAPI.GetComponent<SquadCombatModeComponent>(entity).mode == SquadCombatMode.Brace;

            if (SystemAPI.HasComponent<SquadFormationAnchorComponent>(entity))
            {
                for (int i = 0; i < units.Length; i++)
                {
                    Entity u = units[i].Value;
                    if (!SystemAPI.Exists(u)) continue;

                    if (SystemAPI.HasComponent<LocalTransform>(u) && SystemAPI.HasComponent<UnitResolvedSlotComponent>(u))
                    {
                        var slot = SystemAPI.GetComponent<UnitResolvedSlotComponent>(u);
                        if (slot.status == FormationSlotResolveStatus.Unresolved)
                            isolated++;
                        else
                        {
                            counted++;
                            float3 pos = SystemAPI.GetComponent<LocalTransform>(u).Position;
                            if (math.distancesq(pos.xz, slot.position.xz) > stragglerDistanceSq)
                                stragglers++;
                        }
                    }

                    // Consume retaliation pulse set by DamageCalculationSystem
                    // Always disable the tag on ALL units (not just the first) to prevent
                    // lingering tags keeping isInCombat=true across multiple frames (BUG-008)
                    if (SystemAPI.HasComponent<IsUnderAttackTag>(u)
                        && SystemAPI.IsComponentEnabled<IsUnderAttackTag>(u))
                    {
                        wasHit = true;
                        SystemAPI.SetComponentEnabled<IsUnderAttackTag>(u, false);
                    }
                }
            }

            bool ready = FormationLayout.IsFormationReady(stragglers, counted, readiness.quorum);
            // Out of their slots in a fight is normal: only a squad with no enemy in sight regroups.
            bool dispersed = !ready && !enemiesDetected;

            BehaviorProfile profile = def.ValueRO.behaviorProfile;

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

            ai.ValueRW.tacticalIntent = desiredState;
            ai.ValueRW.isInCombat     = enemiesDetected || wasHit;
            ai.ValueRW.formationReady = ready;
            ai.ValueRW.stragglers     = stragglers;
            ai.ValueRW.isolated       = isolated;

        }
    }
}

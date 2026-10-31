using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Activates brace for a HoldPosition order when the committed formation supports it.
/// Reads the layout's copied policy rather than a scene blob, which can be unloaded.
/// Formation changes become visible on the next decision cycle; leaving hold releases brace immediately.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(SquadFSMSystem))]
[UpdateBefore(typeof(UnitTargetingSystem))]
public partial class BraceWeaponActivationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (state, entity) in
                 SystemAPI.Query<RefRO<SquadStateComponent>>().WithEntityAccess())
        {
            var newMode = new SquadCombatModeComponent();
            if (state.ValueRO.currentOrder == SquadOrderType.HoldPosition && !state.ValueRO.retreatTriggered
                && SystemAPI.HasComponent<FormationComponent>(entity)
                && SystemAPI.HasComponent<SquadHoldPositionComponent>(entity))
            {
                var formation = SystemAPI.GetComponent<FormationComponent>(entity);
                if (formation.supportsBrace)
                {
                    newMode.mode = SquadCombatMode.Brace;
                    newMode.halfAngleRadians = formation.braceHalfAngleRadians;
                    newMode.forward = math.forward(SystemAPI.GetComponent<SquadHoldPositionComponent>(entity).holdRotation);
                }
            }
            bool shouldBrace = newMode.mode == SquadCombatMode.Brace;
            bool hasModeComp = SystemAPI.HasComponent<SquadCombatModeComponent>(entity);
            var currentMode  = hasModeComp
                ? SystemAPI.GetComponent<SquadCombatModeComponent>(entity).mode
                : SquadCombatMode.Normal;

            if (shouldBrace)
            {
                if (hasModeComp)
                    ecb.SetComponent(entity, newMode);
                else
                    ecb.AddComponent(entity, newMode);
            }
            else if (!shouldBrace && currentMode == SquadCombatMode.Brace)
            {
                ecb.SetComponent(entity, new SquadCombatModeComponent { mode = SquadCombatMode.Normal });
            }
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}

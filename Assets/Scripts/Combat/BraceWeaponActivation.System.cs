using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Detects when a squad enters HoldingPosition state and activates Brace mode
/// (SquadCombatModeComponent = Brace). Reverts to Normal when leaving hold.
/// BraceWeaponSystem then applies per-row weapon overrides.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class BraceWeaponActivationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (state, entity) in
                 SystemAPI.Query<RefRO<SquadStateComponent>>().WithEntityAccess())
        {
            bool shouldBrace = false; // Brace mode no se activa automáticamente
            bool hasModeComp = SystemAPI.HasComponent<SquadCombatModeComponent>(entity);
            var currentMode  = hasModeComp
                ? SystemAPI.GetComponent<SquadCombatModeComponent>(entity).mode
                : SquadCombatMode.Normal;

            if (shouldBrace && currentMode != SquadCombatMode.Brace)
            {
                var newMode = new SquadCombatModeComponent { mode = SquadCombatMode.Brace };
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

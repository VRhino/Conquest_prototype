using Unity.Entities;

/// <summary>
/// Applies per-row BraceRowProfile overrides to UnitWeaponComponent when the squad
/// is in Brace mode (SquadCombatModeComponent.mode == Brace).
/// Each row gets different weapon shape/timing based on its BraceRowProfile entry.
/// Example: front-rank alabarda gets a high yOffset to capture enemies on stairs.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BraceWeaponActivationSystem))]
public partial class BraceWeaponSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var weaponLookup   = GetComponentLookup<UnitWeaponComponent>();
        var gridSlotLookup = GetComponentLookup<UnitGridSlotComponent>(true);

        foreach (var (unitBuffer, mode, profiles) in
                 SystemAPI.Query<DynamicBuffer<SquadUnitElement>,
                                  RefRO<SquadCombatModeComponent>,
                                  DynamicBuffer<BraceRowProfile>>())
        {
            if (mode.ValueRO.mode != SquadCombatMode.Brace) continue;

            for (int u = 0; u < unitBuffer.Length; u++)
            {
                Entity unit = unitBuffer[u].Value;
                if (!weaponLookup.HasComponent(unit))   continue;
                if (!gridSlotLookup.HasComponent(unit)) continue;

                int row = gridSlotLookup[unit].gridPosition.y;

                for (int p = 0; p < profiles.Length; p++)
                {
                    if (profiles[p].row != row) continue;

                    var prof = profiles[p];
                    var w    = weaponLookup[unit];

                    w.attackRange             = prof.attackRange;
                    w.damageZoneStart         = prof.damageZoneStart;
                    w.damageZoneHalfWidth     = prof.damageZoneHalfWidth;
                    w.damageZoneYOffset       = prof.damageZoneYOffset;
                    w.damageZoneHalfHeight    = prof.damageZoneHalfHeight;
                    w.strikeWindowStart       = prof.strikeWindowStart;
                    w.strikeWindowDuration    = prof.strikeWindowDuration;
                    w.attackAnimationDuration = prof.attackAnimationDuration;

                    weaponLookup[unit] = w;
                    break;
                }
            }
        }
    }
}

using Unity.Entities;

/// <summary>
/// Applies per-row BraceRowProfile overrides to UnitWeaponComponent when the squad
/// is in Brace mode (SquadCombatModeComponent.mode == Brace).
/// Each row gets different weapon shape/timing based on its BraceRowProfile entry.
/// Example: front-rank alabarda gets a high yOffset to capture enemies on stairs.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BraceWeaponActivationSystem))]
[UpdateBefore(typeof(UnitTargetingSystem))]
public partial class BraceWeaponSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var weaponLookup   = GetComponentLookup<UnitWeaponComponent>();
        var gridSlotLookup = GetComponentLookup<UnitGridSlotComponent>(true);

        foreach (var (unitBuffer, mode, profiles, data) in
                 SystemAPI.Query<DynamicBuffer<SquadUnitElement>,
                                  RefRO<SquadCombatModeComponent>,
                                  DynamicBuffer<BraceRowProfile>, RefRO<SquadDataComponent>>())
        {

            for (int u = 0; u < unitBuffer.Length; u++)
            {
                Entity unit = unitBuffer[u].Value;
                if (!weaponLookup.HasComponent(unit))   continue;
                if (!gridSlotLookup.HasComponent(unit)) continue;

                int row = gridSlotLookup[unit].row; // 0 = front rank, as the profiles expect
                var baseline = weaponLookup[unit];
                baseline.attackRange = data.ValueRO.attackRange;
                baseline.strikeWindowStart = data.ValueRO.strikeWindowStart;
                baseline.strikeWindowDuration = data.ValueRO.strikeWindowDuration;
                baseline.attackAnimationDuration = data.ValueRO.attackAnimationDuration;
                weaponLookup[unit] = baseline;
                if (mode.ValueRO.mode != SquadCombatMode.Brace) continue;

                for (int p = 0; p < profiles.Length; p++)
                {
                    if (profiles[p].row != row) continue;

                    var prof = profiles[p];
                    var w    = weaponLookup[unit];

                    w.attackRange             = prof.attackRange;
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

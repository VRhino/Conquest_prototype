using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Manages the shield break stun timer and regenerates block points over time.
///
/// While brokenTimer > 0:   tick down the timer, skip regen.
/// When brokenTimer hits 0: instantly restore currentBlock to maxBlock, exit stun.
/// Otherwise:               regenerate currentBlock toward maxBlock at regenRate per second.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(DamageCalculationSystem))]
public partial class BlockRegenSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;
        foreach (var shield in SystemAPI.Query<RefRW<UnitShieldComponent>>())
        {
            if (shield.ValueRO.brokenTimer > 0f)
            {
                shield.ValueRW.brokenTimer = math.max(0f, shield.ValueRO.brokenTimer - dt);
                // Restore to full block when stun expires
                if (shield.ValueRO.brokenTimer <= 0f)
                    shield.ValueRW.currentBlock = shield.ValueRO.maxBlock;
                // No regen while shield is broken
            }
            else if (shield.ValueRO.currentBlock < shield.ValueRO.maxBlock)
            {
                shield.ValueRW.currentBlock = math.min(
                    shield.ValueRO.maxBlock,
                    shield.ValueRO.currentBlock + shield.ValueRO.regenRate * dt);
            }
        }
    }
}

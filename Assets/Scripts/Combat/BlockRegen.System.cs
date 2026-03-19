using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Regenerates shield block points over time for all units with UnitShieldComponent.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class BlockRegenSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;
        foreach (var shield in SystemAPI.Query<RefRW<UnitShieldComponent>>())
        {
            if (shield.ValueRO.currentBlock < shield.ValueRO.maxBlock)
                shield.ValueRW.currentBlock = math.min(
                    shield.ValueRO.maxBlock,
                    shield.ValueRO.currentBlock + shield.ValueRO.regenRate * dt);
        }
    }
}

using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

/// <summary>
/// Samples the position of every attacker that can charge (L1-11), so the melee hit can measure how far it came towards
/// its target. Only writes <see cref="ChargeTrackComponent"/>.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(MeleeContactResolutionSystem))]
[BurstCompile]
public partial struct ChargeTrackSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        double now = SystemAPI.Time.ElapsedTime;
        float window = CombatTuning.Default.chargeWindowSeconds;
        foreach (var (track, transform) in SystemAPI.Query<RefRW<ChargeTrackComponent>, RefRO<LocalTransform>>())
            ChargeTrack.Sample(ref track.ValueRW, transform.ValueRO.Position, now, window);
    }
}

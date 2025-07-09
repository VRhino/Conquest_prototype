using Unity.Entities;

/// <summary>
/// Updates <see cref="CooldownComponent"/> timers each frame and marks abilities
/// as ready when the timer reaches zero.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class CooldownSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var cooldown in SystemAPI.Query<RefRW<CooldownComponent>>())
        {
            if (!cooldown.ValueRO.isReady)
            {
                float remaining = cooldown.ValueRO.currentCooldown - deltaTime;
                cooldown.ValueRW.currentCooldown = remaining;

                if (remaining <= 0f)
                {
                    cooldown.ValueRW.currentCooldown = 0f;
                    cooldown.ValueRW.isReady = true;
                }
            }
        }
    }
}

using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Tracks how long each hero with perks has been still and how long since it moved (<see cref="HeroMotionComponent"/>),
/// from its position, so local and AI heroes are measured the same way.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HeroMovementSystem))]
public partial class HeroMotionSystem : SystemBase
{
    /// <summary>Speed below which the hero counts as still (m/s), so standing jitter does not reset the clock.</summary>
    const float StillSpeed = 0.2f;

    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;
        if (dt <= 0f) return;
        foreach (var (motion, transform) in SystemAPI.Query<RefRW<HeroMotionComponent>, RefRO<LocalTransform>>())
        {
            ref var m = ref motion.ValueRW;
            float3 position = transform.ValueRO.Position;
            if (!m.initialised)
            {
                m = new HeroMotionComponent { lastPosition = position, initialised = true, sinceMoved = float.MaxValue };
                continue;
            }
            float speed = math.distance(position.xz, m.lastPosition.xz) / dt;
            m.lastPosition = position;
            if (speed > StillSpeed) { m.stillFor = 0f; m.sinceMoved = 0f; }
            else { m.stillFor += dt; m.sinceMoved = m.sinceMoved >= float.MaxValue ? m.sinceMoved : m.sinceMoved + dt; }
        }
    }
}

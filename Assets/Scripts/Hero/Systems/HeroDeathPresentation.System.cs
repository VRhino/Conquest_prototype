using Unity.Entities;

/// <summary>
/// Single writer of HeroDeathPresentationComponent: counts deaths for the death animation, hides the
/// visual once the configured delay has passed and shows it again after the respawn pose is published.
/// HeroVisualInstantiationSystem reads it in the same frame to remove or rebuild the visual.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HeroSpawnSystem))]
[UpdateBefore(typeof(HeroVisualInstantiationSystem))]
public partial class HeroDeathPresentationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        float hideDelay = SystemAPI.TryGetSingleton<HeroGameplayConfigComponent>(out var config)
            ? config.deathVisualHideDelay
            : HeroGameplayConfigComponent.DefaultDeathVisualHideDelay;

        foreach (var (life, spawn, presentation) in
                 SystemAPI.Query<RefRO<HeroLifeComponent>, RefRO<HeroSpawnComponent>,
                                 RefRW<HeroDeathPresentationComponent>>())
        {
            ref var state = ref presentation.ValueRW;
            if (life.ValueRO.isAlive)
            {
                // A respawned hero stays hidden until HeroSpawnSystem has placed it at its spawn pose.
                if (state.isDead && spawn.ValueRO.hasSpawned)
                {
                    state.isDead = false;
                    state.visualHidden = false;
                    state.timeSinceDeath = 0f;
                }
                continue;
            }

            if (!state.isDead)
            {
                state.isDead = true;
                state.deathRevision++;
                state.timeSinceDeath = 0f;
            }
            else
            {
                state.timeSinceDeath += deltaTime;
            }

            if (state.timeSinceDeath >= hideDelay)
                state.visualHidden = true;
        }
    }
}

using Unity.Entities;

/// <summary>
/// Marks heroes dead at zero health and respawns the local hero after its death timer. On respawn it gives the hero a
/// timed health floor on the modifier stack (L1-07), with the seconds and ratio of <see cref="HeroGameplayConfigComponent"/>.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(DamageCalculationSystem))]
[UpdateBefore(typeof(HeroSpawnSystem))]
public partial class HeroRespawnSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
    }

    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        double now = SystemAPI.Time.ElapsedTime;
        SystemAPI.TryGetSingleton<HeroGameplayConfigComponent>(out var config);

        foreach (var (life, health, spawn, entity) in
                 SystemAPI.Query<RefRW<HeroLifeComponent>,
                                 RefRW<HeroHealthComponent>,
                                 RefRW<HeroSpawnComponent>>()
                                 .WithEntityAccess())
        {
            if (life.ValueRO.isAlive)
            {
                if (health.ValueRO.currentHealth <= 0f)
                {
                    life.ValueRW.isAlive = false;
                    life.ValueRW.deathTimer = life.ValueRO.respawnCooldown;
                }
                continue;
            }

            // Remote heroes have no respawn contract; they remain dead so AI and
            // squad-owner systems stop acting on their behalf.
            if (!EntityManager.HasComponent<IsLocalPlayer>(entity))
                continue;

            life.ValueRW.deathTimer -= deltaTime;
            if (life.ValueRO.deathTimer <= 0f)
            {
                life.ValueRW.isAlive = true;
                life.ValueRW.deathTimer = life.ValueRO.respawnCooldown;
                health.ValueRW.currentHealth = health.ValueRO.maxHealth;
                Protect(entity, config, now);

                spawn.ValueRW.hasSpawned = false;
            }
        }
    }

    /// <summary>Health floor for the protection window; StatResolveSystem writes it to the hero's health.</summary>
    void Protect(Entity hero, in HeroGameplayConfigComponent config, double now)
    {
        if (config.respawnProtectionSeconds <= 0f || config.respawnProtectionHealthRatio <= 0f) return;
        if (!EntityManager.HasBuffer<StatModifier>(hero)) return;
        StatEffects.ApplyTimed(EntityManager.GetBuffer<StatModifier>(hero), StatId.MinHealthRatio, ModifierOp.Flat,
            config.respawnProtectionHealthRatio, now + config.respawnProtectionSeconds, ModifierSource.Effect,
            StatEffects.RespawnProtection);
        if (EntityManager.HasComponent<ModifiersDirty>(hero)) EntityManager.SetComponentEnabled<ModifiersDirty>(hero, true);
    }
}

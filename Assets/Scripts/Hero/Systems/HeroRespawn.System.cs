using Unity.Entities;

/// <summary>
/// Marks heroes dead at zero health and respawns the local hero after its death timer.
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

                spawn.ValueRW.hasSpawned = false;
            }
        }
    }
}

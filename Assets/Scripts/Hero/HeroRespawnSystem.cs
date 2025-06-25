using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Handles respawning the hero after a death timer expires.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class HeroRespawnSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (life, health, spawn, transform) in
                 SystemAPI.Query<RefRW<HeroLifeComponent>,
                                 RefRW<HealthComponent>,
                                 RefRO<HeroSpawnComponent>,
                                 RefRW<LocalTransform>>()
                        .WithAll<IsLocalPlayer>())
        {
            if (life.ValueRO.isAlive)
            {
                if (health.ValueRO.currentHealth <= 0f)
                {
                    life.ValueRW.isAlive = false;
                    life.ValueRW.deathTimer = life.ValueRO.respawnDelay;
                }

                continue;
            }

            life.ValueRW.deathTimer -= deltaTime;
            if (life.ValueRO.deathTimer <= 0f)
            {
                life.ValueRW.isAlive = true;
                life.ValueRW.deathTimer = life.ValueRO.respawnDelay;
                health.ValueRW.currentHealth = health.ValueRO.maxHealth;

                transform.ValueRW.Position = spawn.ValueRO.spawnPosition;
                transform.ValueRW.Rotation = spawn.ValueRO.spawnRotation;
            }
        }
    }
}

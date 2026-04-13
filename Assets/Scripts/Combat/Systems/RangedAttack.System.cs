using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// State machine for ranged unit attacks (archers, etc.).
/// Mirrors UnitAttackSystem's three-phase structure but replaces the hitbox with
/// a ProjectileSpawnRequest entity that ProjectileSpawnSystem picks up each frame.
///
/// Phase 1 — Reload: if currentAmmo == 0, wait reloadSpeed seconds, restore ammo.
/// Phase 2 — Shot cooldown: if shotTimer > 0, wait.
/// Phase 3 — Fire: target in range → roll critical → emit ProjectileSpawnRequest → tick ammo/cooldown.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitTargetingSystem))]
public partial class RangedAttackSystem : SystemBase
{
    EndSimulationEntityCommandBufferSystem _ecbSystem;

    protected override void OnCreate()
    {
        _ecbSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        float dt   = SystemAPI.Time.DeltaTime;
        uint  seed = (uint)(SystemAPI.Time.ElapsedTime * 10000.0 + 1.0);

        var ecb              = _ecbSystem.CreateCommandBuffer();
        var transformLookup  = GetComponentLookup<LocalTransform>(true);
        var healthLookup     = GetComponentLookup<HealthComponent>(true);
        var heroLifeLookup   = GetComponentLookup<HeroLifeComponent>(true);
        var teamLookup       = GetComponentLookup<TeamComponent>(true);
        var isDeadLookup     = GetComponentLookup<IsDeadComponent>(true);

        uint unitIndex = 0;

        foreach (var (combat, rangedState, rangedStats, weapon, transform, entity) in
                 SystemAPI.Query<
                     RefRW<UnitCombatComponent>,
                     RefRW<RangedAttackStateComponent>,
                     RefRO<UnitRangedStatsComponent>,
                     RefRO<UnitWeaponComponent>,
                     RefRO<LocalTransform>>()
                          .WithEntityAccess())
        {
            ref var state = ref rangedState.ValueRW;
            ref var c     = ref combat.ValueRW;

            // Clear one-frame firing signal from previous frame
            state.isFiring = false;

            // Tick timers
            state.shotTimer   = math.max(0f, state.shotTimer   - dt);
            state.reloadTimer = math.max(0f, state.reloadTimer - dt);

            // Finish reload
            if (state.isReloading)
            {
                if (state.reloadTimer <= 0f)
                {
                    state.isReloading = false;
                    state.currentAmmo = rangedStats.ValueRO.totalAmmo;
                }
                continue;
            }

            // Trigger reload when out of ammo
            if (state.currentAmmo <= 0)
            {
                state.isReloading = true;
                state.reloadTimer = rangedStats.ValueRO.reloadSpeed > 0f
                    ? rangedStats.ValueRO.reloadSpeed
                    : 2f;
                continue;
            }

            // Shot cooldown still active
            if (state.shotTimer > 0f) continue;

            // Validate target
            Entity target = c.target;
            if (target == Entity.Null || !SystemAPI.Exists(target))
            {
                c.target = Entity.Null;
                continue;
            }

            if (isDeadLookup.HasComponent(target))
            {
                c.target = Entity.Null;
                continue;
            }

            bool alive = true;
            if (healthLookup.HasComponent(target))
                alive = healthLookup[target].currentHealth > 0f;
            else if (heroLifeLookup.HasComponent(target))
                alive = heroLifeLookup[target].isAlive;
            if (!alive)
            {
                c.target = Entity.Null;
                continue;
            }

            if (!transformLookup.HasComponent(target))
            {
                continue;
            }

            float3 myPos     = transform.ValueRO.Position + new float3(0f, 1.2f, 0f);
            float3 targetPos = transformLookup[target].Position + new float3(0f, 0.9f, 0f);
            float3 toTarget  = targetPos - myPos;
            float  dist2D    = math.sqrt(toTarget.x * toTarget.x + toTarget.z * toTarget.z);

            // Add a small tolerance (0.25f) to account for floating point errors and NavMesh agent stopping 
            // infinitesimally outside the exact perfect range border due to radius/collision.
            if (dist2D > rangedStats.ValueRO.range + 0.5f)
            {
                continue;
            }

            // Roll critical (unique seed per unit per frame)
            var   rngCrit    = new Unity.Mathematics.Random(seed + unitIndex * 1234567u);
            bool  crit       = rngCrit.NextFloat() < weapon.ValueRO.criticalChance;
            float multiplier = crit ? weapon.ValueRO.criticalMultiplier : 1f;

            // Compute direction with accuracy scatter
            float3 baseDir = math.normalizesafe(toTarget);
            float  spread  = math.max(0f, 1f - rangedStats.ValueRO.accuracy) * 0.15f;
            float3 dir     = baseDir;
            if (spread > 0f)
            {
                var rngSpread = new Unity.Mathematics.Random(seed + unitIndex * 7654321u);
                dir = math.normalizesafe(baseDir + new float3(
                    rngSpread.NextFloat(-spread, spread),
                    0f,
                    rngSpread.NextFloat(-spread, spread)));
            }

            Team sourceTeam = teamLookup.HasComponent(entity)
                ? teamLookup[entity].value
                : Team.None;

            // Emit projectile spawn request
            Entity req = ecb.CreateEntity();
            ecb.AddComponent(req, new ProjectileSpawnRequest
            {
                shooter         = entity,
                target          = target,
                spawnPosition   = myPos,
                attackDirection = dir,
                damageProfile   = weapon.ValueRO.damageProfile,
                sourceTeam      = sourceTeam,
                multiplier      = multiplier,
                poolKey         = rangedStats.ValueRO.projectilePoolKey,
                trajectory      = rangedStats.ValueRO.trajectory
            });

            state.currentAmmo--;
            state.shotTimer = rangedStats.ValueRO.fireRate > 0f
                ? 1f / rangedStats.ValueRO.fireRate
                : 1f;
            state.isFiring = true;    // consumed by UnitAnimationAdapter this frame

            unitIndex++;
        }
    }
}

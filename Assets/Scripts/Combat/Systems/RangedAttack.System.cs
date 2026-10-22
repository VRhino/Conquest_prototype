using ConquestTactics.Animation;
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
[UpdateAfter(typeof(NavMeshPositionSyncSystem))]
public partial class RangedAttackSystem : SystemBase
{
    EndSimulationEntityCommandBufferSystem _ecbSystem;

    protected override void OnCreate()
    {
        _ecbSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        bool battleActive = !SystemAPI.TryGetSingleton<MatchStateComponent>(out var match)
            || match.currentState == MatchState.InBattle;
        float dt   = SystemAPI.Time.DeltaTime;
        uint  tick = CombatRandom.Tick(SystemAPI.Time.ElapsedTime);

        var ecb              = _ecbSystem.CreateCommandBuffer();
        var transformLookup  = GetComponentLookup<LocalTransform>(true);
        var healthLookup     = GetComponentLookup<HealthComponent>(true);
        var heroLifeLookup   = GetComponentLookup<HeroLifeComponent>(true);
        var teamLookup       = GetComponentLookup<TeamComponent>(true);
        var isDeadLookup     = GetComponentLookup<IsDeadComponent>(true);
        var critLookup       = GetComponentLookup<CritStatsComponent>(true);
        var movementLookup   = GetComponentLookup<UnitAnimationMovementComponent>(true);
        var statsLookup      = GetComponentLookup<UnitStatsComponent>(true);
        var combatTuning     = CombatTuning.Default;
        float criticalFloor  = CombatTuning.Default.criticalExtraFloor;


        foreach (var (combat, rangedState, rangedStats, weapon, transform, entity) in
                 SystemAPI.Query<
                     RefRW<UnitCombatComponent>,
                     RefRW<RangedAttackStateComponent>,
                     RefRO<UnitRangedStatsComponent>,
                     RefRO<UnitWeaponComponent>,
                     RefRO<LocalTransform>>()
                          .WithNone<IsDeadComponent>()
                          .WithEntityAccess())
        {
            ref var state = ref rangedState.ValueRW;
            ref var c     = ref combat.ValueRW;

            // Clear one-frame firing signal from previous frame
            state.isFiring = false;
            if (!battleActive) continue;

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

            // Seeded by (tick, shooter), not by query order (L1-05).
            // Critical stats on the stack (L1-04), against the target's anti-critical; the weapon's own otherwise.
            float critChance = weapon.ValueRO.criticalChance;
            float critMultiplier = weapon.ValueRO.criticalMultiplier;
            if (critLookup.HasComponent(entity))
            {
                var defenderCrit = critLookup.HasComponent(target) ? critLookup[target] : CritFormula.NoAntiCrit;
                critChance = CritFormula.Rate(critLookup[entity]);
                critMultiplier = CritFormula.Multiplier(critLookup[entity], defenderCrit, criticalFloor);
            }
            bool  crit       = CombatRandom.RollCritical(tick, entity, critChance);
            float multiplier = crit ? critMultiplier : 1f;

            // Compute direction with accuracy scatter, wider while the shooter moves (L2-7).
            // accuracy is authored as 1..100 (100 = perfect precision).
            float speedFraction = movementLookup.HasComponent(entity) && statsLookup.HasComponent(entity) && statsLookup[entity].speed > 0f
                ? movementLookup[entity].CurrentSpeed / statsLookup[entity].speed : 0f;
            float3 baseDir = math.normalizesafe(toTarget);
            float  spread  = LauncherScheme.Spread(rangedStats.ValueRO.accuracy, speedFraction, combatTuning);
            float3 dir     = baseDir;
            if (spread > 0f)
            {
                var rngSpread = CombatRandom.ForHit(tick, entity, CombatRandom.SpreadSalt);
                dir = math.normalizesafe(baseDir + new float3(
                    rngSpread.NextFloat(-spread, spread),
                    0f,
                    rngSpread.NextFloat(-spread, spread)));
            }

            Team sourceTeam = teamLookup.HasComponent(entity)
                ? teamLookup[entity].value
                : Team.None;

            if (!SystemAPI.HasComponent<DamageProfileComponent>(weapon.ValueRO.damageProfile))
                continue;

            // Emit one projectile spawn request per projectile of the shot, fanned out (L2-7).
            int count = math.max(1, rangedStats.ValueRO.projectilesPerShot);
            var snapshot = SystemAPI.GetComponent<DamageProfileComponent>(weapon.ValueRO.damageProfile);
            for (int p = 0; p < count; p++)
            {
                Entity req = ecb.CreateEntity();
                ecb.AddComponent(req, new ProjectileSpawnRequest
                {
                    shooter         = entity,
                    target          = target,
                    spawnPosition   = myPos,
                    attackDirection = LauncherScheme.FanDirection(dir, p, count, rangedStats.ValueRO.fanAngleDegrees),
                    damageProfile   = weapon.ValueRO.damageProfile,
                    damageProfileSnapshot = snapshot,
                    sourceTeam      = sourceTeam,
                    multiplier      = multiplier,
                    poolKey         = rangedStats.ValueRO.projectilePoolKey,
                    trajectory      = rangedStats.ValueRO.trajectory
                });
            }

            state.currentAmmo--;
            state.shotTimer = rangedStats.ValueRO.fireRate > 0f
                ? 1f / rangedStats.ValueRO.fireRate
                : 1f;
            state.isFiring = true;    // consumed by UnitAnimationAdapter this frame

        }
    }
}

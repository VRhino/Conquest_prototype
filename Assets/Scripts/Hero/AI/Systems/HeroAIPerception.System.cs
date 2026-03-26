using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Per-hero perception layer. Reads the centralized <see cref="TeamWorldState"/>
/// and computes per-hero derived values (distances, "am I inside a zone?", nearest enemy, etc.)
/// into each hero's <see cref="HeroAIBlackboard"/>.
///
/// Runs every 5 frames for performance. All raw world data comes from
/// <see cref="BattleWorldStateSystem"/> — this system never queries the world directly.
///
/// Pipeline position:
///   BattleWorldStateSystem → HeroAIPerceptionSystem → [Behaviors] → HeroAIExecutionSystem
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BattleWorldStateSystem))]
[UpdateBefore(typeof(HeroAIRusherSystem))]
public partial class HeroAIPerceptionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Tick-gate: only update every 5 frames for performance
        if (UnityEngine.Time.frameCount % 5 != 0) return;

        // Read centralized world state
        if (!SystemAPI.ManagedAPI.TryGetSingleton<TeamWorldState>(out var ws))
            return;
        if (!ws.match.isActive) return;

        // Per AI-hero: compute derived values from TeamWorldState
        foreach (var (transform, health, stamina, life, team, entity) in
                 SystemAPI.Query<RefRO<LocalTransform>,
                                 RefRO<HeroHealthComponent>,
                                 RefRO<StaminaComponent>,
                                 RefRO<HeroLifeComponent>,
                                 RefRO<TeamComponent>>()
                          .WithAll<HeroAITag>()
                          .WithEntityAccess())
        {
            var bb = EntityManager.GetComponentObject<HeroAIBlackboard>(entity);
            if (bb == null) continue;

            float3 selfPos  = transform.ValueRO.Position;
            Team   selfTeam = team.ValueRO.value;
            int    selfTeamInt = selfTeam == Team.TeamA ? 1 : 2;

            // ── Self state ─────────────────────────────────────────────
            bb.selfPosition       = selfPos;
            bb.selfHealthPercent  = health.ValueRO.maxHealth > 0f
                ? health.ValueRO.currentHealth / health.ValueRO.maxHealth : 0f;
            bb.selfStaminaPercent = stamina.ValueRO.maxStamina > 0f
                ? stamina.ValueRO.currentStamina / stamina.ValueRO.maxStamina : 0f;
            bb.selfIsAlive    = life.ValueRO.isAlive;
            bb.selfTeam       = selfTeam;
            bb.selfIsAttacker = EntityManager.HasComponent<IsAttackerRole>(entity);

            // ── Own squad (from TeamWorldState hero snapshot) ──────────
            var myView = ws.For(selfTeam);
            bb.hasSquad = false;
            foreach (var hero in myView.allyHeroes)
            {
                if (hero.entity != entity) continue;
                bb.ownSquadEntity    = hero.squadEntity;
                bb.hasSquad          = hero.hasSquad;
                bb.ownSquadType      = hero.squadType;
                bb.ownSquadIsRanged  = hero.isRangedSquad;
                bb.squadIsInCombat   = hero.squadIsInCombat;
                bb.squadCurrentOrder = hero.squadCurrentOrder;
                break;
            }

            // ── Match context ──────────────────────────────────────────
            bb.matchIsActive = ws.match.isActive;
            bb.winnerTeam    = ws.match.winnerTeam;

            // ── Spawn position (cached once) ───────────────────────────
            if (!bb.spawnPositionCached && ws.spawnsCached)
            {
                bb.spawnPosition       = ws.SpawnFor(selfTeam);
                bb.spawnPositionCached = true;
            }

            // ── Nearest enemy hero (from fog-of-war filtered list) ─────
            bb.nearestEnemyHero       = Entity.Null;
            bb.nearestEnemyPosition   = float3.zero;
            bb.nearestEnemyDistanceSq = float.MaxValue;

            foreach (var enemy in myView.visibleEnemyHeroes)
            {
                if (!enemy.isAlive) continue;
                float dSq = math.distancesq(selfPos, enemy.position);
                if (dSq < bb.nearestEnemyDistanceSq)
                {
                    bb.nearestEnemyDistanceSq = dSq;
                    bb.nearestEnemyHero       = enemy.entity;
                    bb.nearestEnemyPosition   = enemy.position;
                }
            }

            // ── Zone-derived data ──────────────────────────────────────
            bb.isInsideAnyZone  = false;
            bb.zoneImInside     = Entity.Null;
            bb.zoneImInsideInfo = default;
            bb.bestObjectiveZone     = Entity.Null;
            bb.bestObjectivePosition = float3.zero;
            bb.threatZone            = Entity.Null;
            bb.threatZonePosition    = float3.zero;

            float bestDist = float.MaxValue;

            foreach (var zi in ws.zones)
            {
                if (zi.isLocked) continue;

                float dSq = math.distancesq(selfPos, zi.position);

                // Am I inside this zone?
                float radiusSq = zi.radius * zi.radius;
                if (!bb.isInsideAnyZone && dSq <= radiusSq)
                {
                    bb.isInsideAnyZone  = true;
                    bb.zoneImInside     = zi.entity;
                    bb.zoneImInsideInfo = zi;
                }

                // Best objective: closest zone not owned by us
                if (zi.teamOwner != selfTeamInt && dSq < bestDist)
                {
                    bestDist                 = dSq;
                    bb.bestObjectiveZone     = zi.entity;
                    bb.bestObjectivePosition = zi.position;
                }

                // Threat: enemy capturing our zone
                if (zi.teamOwner == selfTeamInt && zi.isBeingCaptured && bb.threatZone == Entity.Null)
                {
                    bb.threatZone         = zi.entity;
                    bb.threatZonePosition = zi.position;
                }
            }
        }
    }
}

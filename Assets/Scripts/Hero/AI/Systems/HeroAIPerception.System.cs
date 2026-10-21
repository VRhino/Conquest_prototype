using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;


/// <summary>
/// Per-hero perception layer. Reads the centralized <see cref="TeamWorldState"/>
/// and computes per-hero derived values (distances, "am I inside a zone?", nearest enemy, etc.)
/// into each hero's <see cref="HeroAIBlackboard"/>.
///
/// Each AI hero thinks on its own clock (<see cref="AIThinkClock"/>, L4-01): every <c>heroAIThinkInterval</c>
/// seconds of simulation time, phased by its entity, at most <c>heroAIMaxThinksPerFrame</c> per frame (most overdue
/// first). All raw world data comes from <see cref="BattleWorldStateSystem"/> — this system never queries the world directly.
///
/// Pipeline position:
///   BattleWorldStateSystem → HeroAIPerceptionSystem → [Behaviors] → HeroAIExecutionSystem
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BattleWorldStateSystem))]
[UpdateBefore(typeof(HeroAIRusherSystem))]
public partial class HeroAIPerceptionSystem : SystemBase
{
    private const float UnderAttackRangeSq = 20f * 20f;
    // Fallbacks when no SquadSpawnConfig is baked (reduced test worlds).
    private const float DefaultThinkInterval = 0.1f;
    private const int DefaultMaxThinksPerFrame = 8;

    private readonly List<(double due, Entity entity)> _due = new();

    protected override void OnUpdate()
    {
        // Read centralized world state
        if (!SystemAPI.ManagedAPI.TryGetSingleton<TeamWorldState>(out var ws))
            return;
        if (!ws.match.isActive) return;

        bool hasConfig = SystemAPI.HasSingleton<SquadSpawnConfigComponent>();
        var config = hasConfig ? SystemAPI.GetSingleton<SquadSpawnConfigComponent>() : default;
        float interval = hasConfig && config.heroAIThinkInterval > 0f ? config.heroAIThinkInterval : DefaultThinkInterval;
        int maxThinks = hasConfig && config.heroAIMaxThinksPerFrame > 0 ? config.heroAIMaxThinksPerFrame : DefaultMaxThinksPerFrame;
        double now = SystemAPI.Time.ElapsedTime;

        // Who is due: each hero on its own phase; the most overdue think first, up to the per-frame cap.
        _due.Clear();
        foreach (var (_, entity) in SystemAPI.Query<RefRO<LocalTransform>>()
                     .WithAll<HeroAITag, HeroHealthComponent, StaminaComponent>()
                     .WithAll<HeroLifeComponent, TeamComponent>()
                     .WithEntityAccess())
        {
            var bb = EntityManager.GetComponentObject<HeroAIBlackboard>(entity);
            if (bb == null) continue;
            if (!bb.thinkClockStarted)
            {
                bb.thinkClockStarted = true;
                bb.nextThinkTime = now + AIThinkClock.Phase(entity, interval);
                bb.lastThinkTime = now;
            }
            if (now >= bb.nextThinkTime) _due.Add((bb.nextThinkTime, entity));
        }
        _due.Sort((a, b) => a.due != b.due ? a.due.CompareTo(b.due) : a.entity.Index.CompareTo(b.entity.Index));
        for (int i = 0; i < _due.Count && i < maxThinks; i++)
        {
            Entity entity = _due[i].entity;
            var bb = EntityManager.GetComponentObject<HeroAIBlackboard>(entity);
            bb.thinkDeltaTime = (float)(now - bb.lastThinkTime);
            bb.lastThinkTime = now;
            bb.nextThinkTime = AIThinkClock.Next(_due[i].due, now, interval);
            Think(entity, bb, ws);
        }
    }

    /// <summary>Refreshes the derived values of one AI hero from <see cref="TeamWorldState"/>.</summary>
    void Think(Entity entity, HeroAIBlackboard bb, TeamWorldState ws)
    {
        var transform = SystemAPI.GetComponentRO<LocalTransform>(entity);
        var health = SystemAPI.GetComponentRO<HeroHealthComponent>(entity);
        var stamina = SystemAPI.GetComponentRO<StaminaComponent>(entity);
        var life = SystemAPI.GetComponentRO<HeroLifeComponent>(entity);
        var team = SystemAPI.GetComponentRO<TeamComponent>(entity);
        {

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

            // ── Under attack ──────────────────────────────────────────
            bb.isUnderAttack = (bb.nearestEnemyHero != Entity.Null
                                && bb.nearestEnemyDistanceSq <= UnderAttackRangeSq)
                               || bb.squadIsInCombat;

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

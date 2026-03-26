using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Decision system for the Balanced behavior (<see cref="BalancedBehaviorActive"/>).
///
/// Philosophy: win by capturing objectives while staying alive.
/// Role-aware: attackers push and capture enemy zones, defenders guard and protect owned zones.
///
/// ATTACKER priorities:
///   1. Dead → Idle
///   2. HP &lt; 30% → Retreat
///   3. Inside enemy/neutral zone: enemy &lt; 10m → AttackTarget; else → CaptureZone
///   4. Enemy &lt; 15m + HP advantage → AttackTarget (clear blockers)
///   5. bestObjectiveZone → sprint toward it
///   6. → Idle
///
/// DEFENDER priorities:
///   1. Dead → Idle
///   2. HP &lt; 30% → Retreat
///   3. Own zone under attack (threatZone) → DefendZone (sprint)
///   4. Inside own zone + enemy &lt; 12m → AttackTarget
///   5. Enemy &lt; 15m + HP advantage → AttackTarget
///   6. Nearest owned zone → DefendZone (patrol)
///   7. → Idle
///
/// Pipeline: HeroAIPerceptionSystem → THIS → HeroAIExecutionSystem
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HeroAIPerceptionSystem))]
[UpdateBefore(typeof(HeroAIExecutionSystem))]
public partial class HeroAIBalancedSystem : SystemBase
{
    private const float LowHealthThreshold   = 0.30f;
    private const float EngageRangeSq        = 15f * 15f;   // general engage distance
    private const float DefendZoneRangeSq    = 12f * 12f;   // defender attacks enemy while in own zone
    private const float CaptureEngageRangeSq = 10f * 10f;   // attacker fights while standing on zone
    private const float AdvantageHpMargin    = 0.15f;

    protected override void OnUpdate()
    {
        TeamWorldState ws = null;
        SystemAPI.ManagedAPI.TryGetSingleton<TeamWorldState>(out ws);

        foreach (var (transform, life, entity) in
                 SystemAPI.Query<RefRO<LocalTransform>,
                                 RefRO<HeroLifeComponent>>()
                          .WithAll<HeroAITag, BalancedBehaviorActive>()
                          .WithEntityAccess())
        {
            var bb = EntityManager.GetComponentObject<HeroAIBlackboard>(entity);
            if (bb == null) continue;

            var    dec         = new HeroAIDecision();
            float3 selfPos     = transform.ValueRO.Position;
            int    selfTeamInt = bb.selfIsAttacker ? 1 : 2;

            // 1. Dead → idle
            if (!bb.selfIsAlive)
            {
                dec.action = AIActionType.Idle;
                SystemAPI.SetComponent(entity, dec);
                continue;
            }

            // 2. Low health → retreat
            if (bb.selfHealthPercent < LowHealthThreshold && bb.spawnPositionCached)
            {
                dec.action           = AIActionType.Retreat;
                dec.targetPosition   = bb.spawnPosition;
                dec.shouldSprint     = true;
                dec.squadOrder       = SquadOrderType.FollowHero;
                dec.hasNewSquadOrder = true;
                SystemAPI.SetComponent(entity, dec);
                continue;
            }

            // Role-specific logic
            if (bb.selfIsAttacker)
                RunAttackerLogic(ref dec, selfPos, selfTeamInt, bb, ws, entity);
            else
                RunDefenderLogic(ref dec, selfPos, selfTeamInt, bb, ws, entity);

            SystemAPI.SetComponent(entity, dec);
        }
    }

    // ── Attacker ─────────────────────────────────────────────────────────────

    private void RunAttackerLogic(ref HeroAIDecision dec, float3 selfPos, int selfTeamInt,
                                  HeroAIBlackboard bb, TeamWorldState ws, Entity entity)
    {
        // 3. Inside enemy/neutral zone
        if (bb.isInsideAnyZone && bb.zoneImInside != Entity.Null
            && bb.zoneImInsideInfo.teamOwner != selfTeamInt)
        {
            if (bb.nearestEnemyHero != Entity.Null
                && bb.nearestEnemyDistanceSq <= CaptureEngageRangeSq)
            {
                // Defender is nearby — fight them while staying on the zone
                dec.action           = AIActionType.AttackTarget;
                dec.targetEntity     = bb.nearestEnemyHero;
                dec.targetPosition   = bb.nearestEnemyPosition;
                dec.shouldAttack     = true;
                dec.squadOrder       = SquadOrderType.Attack;
                dec.hasNewSquadOrder = true;
            }
            else
            {
                // Zone is clear — hold it and capture
                dec.action              = AIActionType.CaptureZone;
                dec.targetEntity        = bb.zoneImInside;
                dec.targetPosition      = selfPos;
                dec.squadOrder          = SquadOrderType.HoldPosition;
                dec.squadOrderPosition  = selfPos;
                dec.hasNewSquadOrder    = true;
            }
            return;
        }

        // 4. Enemy in range + HP advantage → clear blockers
        if (bb.nearestEnemyHero != Entity.Null && bb.nearestEnemyDistanceSq <= EngageRangeSq)
        {
            float enemyHpPct = GetEnemyHpPct(ws, bb);
            bool  hasAdvantage = (bb.selfHealthPercent - enemyHpPct) >= AdvantageHpMargin
                              || bb.selfHealthPercent >= 0.7f;
            if (hasAdvantage)
            {
                dec.action           = AIActionType.AttackTarget;
                dec.targetEntity     = bb.nearestEnemyHero;
                dec.targetPosition   = bb.nearestEnemyPosition;
                dec.shouldAttack     = true;
                dec.squadOrder       = SquadOrderType.Attack;
                dec.hasNewSquadOrder = true;
                return;
            }
        }

        // 5. Sprint toward best objective zone
        if (bb.bestObjectiveZone != Entity.Null)
        {
            dec.action           = AIActionType.CaptureZone;
            dec.targetEntity     = bb.bestObjectiveZone;
            dec.targetPosition   = bb.bestObjectivePosition;
            dec.shouldSprint     = true;
            dec.squadOrder       = SquadOrderType.FollowHero;
            dec.hasNewSquadOrder = true;
            return;
        }

        // 6. Idle
        dec.action           = AIActionType.Idle;
        dec.squadOrder       = SquadOrderType.FollowHero;
        dec.hasNewSquadOrder = false;
    }

    // ── Defender ─────────────────────────────────────────────────────────────

    private void RunDefenderLogic(ref HeroAIDecision dec, float3 selfPos, int selfTeamInt,
                                  HeroAIBlackboard bb, TeamWorldState ws, Entity entity)
    {
        // 3. Own zone being captured → rush to defend
        if (bb.threatZone != Entity.Null)
        {
            dec.action           = AIActionType.DefendZone;
            dec.targetEntity     = bb.threatZone;
            dec.targetPosition   = bb.threatZonePosition;
            dec.shouldSprint     = true;
            dec.squadOrder       = SquadOrderType.FollowHero;
            dec.hasNewSquadOrder = true;
            return;
        }

        // 4. Standing inside own zone + enemy close → expel the attacker
        if (bb.isInsideAnyZone && bb.zoneImInside != Entity.Null
            && bb.zoneImInsideInfo.teamOwner == selfTeamInt
            && bb.nearestEnemyHero != Entity.Null
            && bb.nearestEnemyDistanceSq <= DefendZoneRangeSq)
        {
            dec.action           = AIActionType.AttackTarget;
            dec.targetEntity     = bb.nearestEnemyHero;
            dec.targetPosition   = bb.nearestEnemyPosition;
            dec.shouldAttack     = true;
            dec.squadOrder       = SquadOrderType.Attack;
            dec.hasNewSquadOrder = true;
            return;
        }

        // 5. Enemy in range + HP advantage → engage
        if (bb.nearestEnemyHero != Entity.Null && bb.nearestEnemyDistanceSq <= EngageRangeSq)
        {
            float enemyHpPct   = GetEnemyHpPct(ws, bb);
            bool  hasAdvantage = (bb.selfHealthPercent - enemyHpPct) >= AdvantageHpMargin
                              || bb.selfHealthPercent >= 0.7f;
            if (hasAdvantage)
            {
                dec.action           = AIActionType.AttackTarget;
                dec.targetEntity     = bb.nearestEnemyHero;
                dec.targetPosition   = bb.nearestEnemyPosition;
                dec.shouldAttack     = true;
                dec.squadOrder       = SquadOrderType.Attack;
                dec.hasNewSquadOrder = true;
                return;
            }
        }

        // 6. Patrol nearest owned zone
        if (ws != null)
        {
            Entity  bestZone    = Entity.Null;
            float3  bestPos     = float3.zero;
            float   bestDistSq  = float.MaxValue;

            foreach (var zone in ws.zones)
            {
                if (zone.teamOwner != selfTeamInt) continue;
                float distSq = math.distancesq(selfPos, zone.position);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestZone   = zone.entity;
                    bestPos    = zone.position;
                }
            }

            if (bestZone != Entity.Null)
            {
                dec.action           = AIActionType.DefendZone;
                dec.targetEntity     = bestZone;
                dec.targetPosition   = bestPos;
                dec.shouldSprint     = false;
                dec.squadOrder       = SquadOrderType.FollowHero;
                dec.hasNewSquadOrder = true;
                return;
            }
        }

        // 7. Idle
        dec.action           = AIActionType.Idle;
        dec.squadOrder       = SquadOrderType.FollowHero;
        dec.hasNewSquadOrder = false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static float GetEnemyHpPct(TeamWorldState ws, HeroAIBlackboard bb)
    {
        if (ws == null) return 1f;
        foreach (var enemy in ws.For(bb.selfTeam).visibleEnemyHeroes)
        {
            if (enemy.entity == bb.nearestEnemyHero)
                return enemy.healthPercent;
        }
        return 1f;
    }
}

using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Decision system for the Rusher behavior (<see cref="RusherBehaviorActive"/>).
///
/// Philosophy: win by capturing objectives as fast as possible.
/// Fights only when an enemy hero is blocking the path to the next objective.
/// Always sprints when moving.
///
/// Pipeline: HeroAIPerceptionSystem → THIS → HeroAIExecutionSystem
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HeroAIPerceptionSystem))]
[UpdateBefore(typeof(HeroAIExecutionSystem))]
public partial class HeroAIRusherSystem : SystemBase
{
    private const float BlockingRangeSq = 10f * 10f;   // fallback: attack enemy if < 10m (already under attack)

    protected override void OnUpdate()
    {
        foreach (var (transform, life, entity) in
                 SystemAPI.Query<RefRO<LocalTransform>,
                                 RefRO<HeroLifeComponent>>()
                          .WithAll<HeroAITag, RusherBehaviorActive>()
                          .WithEntityAccess())
        {

            var bb = EntityManager.GetComponentObject<HeroAIBlackboard>(entity);
            if (bb == null) continue;

            var dec     = new HeroAIDecision();
            float3 self = transform.ValueRO.Position;

            // 1. Dead → do nothing
            if (!bb.selfIsAlive)
            {
                dec.action = AIActionType.Idle;
                SystemAPI.SetComponent(entity, dec);
                continue;
            }

            // 2. Under attack (enemy within 20m OR squad in combat) → commit to combat, no running
            if (bb.isUnderAttack && bb.nearestEnemyHero != Entity.Null)
            {
                dec.action           = AIActionType.AttackTarget;
                dec.targetEntity     = bb.nearestEnemyHero;
                dec.targetPosition   = bb.nearestEnemyPosition;
                dec.shouldSprint     = false;
                dec.shouldAttack     = true;
                dec.squadOrder       = SquadOrderType.Attack;
                dec.hasNewSquadOrder = true;
                SystemAPI.SetComponent(entity, dec);
                continue;
            }

            int selfTeamInt = bb.selfIsAttacker ? 1 : 2;

            // 3. Inside an enemy/neutral zone → stay and hold it
            if (bb.isInsideAnyZone && bb.zoneImInside != Entity.Null
                && bb.zoneImInsideInfo.teamOwner != selfTeamInt)
            {
                dec.action              = AIActionType.CaptureZone;
                dec.targetEntity        = bb.zoneImInside;
                dec.targetPosition      = self;         // stay in place
                dec.shouldSprint        = false;
                dec.squadOrder          = SquadOrderType.HoldPosition;
                dec.squadOrderPosition  = self;
                dec.hasNewSquadOrder    = true;
                SystemAPI.SetComponent(entity, dec);
                continue;
            }

            // 4. Enemy hero close enough to be blocking → attack
            if (bb.nearestEnemyHero != Entity.Null && bb.nearestEnemyDistanceSq <= BlockingRangeSq)
            {
                dec.action           = AIActionType.AttackTarget;
                dec.targetEntity     = bb.nearestEnemyHero;
                dec.targetPosition   = bb.nearestEnemyPosition;
                dec.shouldSprint     = false;
                dec.shouldAttack     = true;
                dec.squadOrder       = SquadOrderType.Attack;
                dec.hasNewSquadOrder = true;
                SystemAPI.SetComponent(entity, dec);
                continue;
            }

            // 5. Rush to best objective zone
            if (bb.bestObjectiveZone != Entity.Null)
            {
                dec.action           = AIActionType.CaptureZone;
                dec.targetEntity     = bb.bestObjectiveZone;
                dec.targetPosition   = bb.bestObjectivePosition;
                dec.shouldSprint     = true;
                dec.squadOrder       = SquadOrderType.FollowHero;
                dec.hasNewSquadOrder = true;
                SystemAPI.SetComponent(entity, dec);
                continue;
            }

            // 6. No objectives left → idle
            dec.action = AIActionType.Idle;
            SystemAPI.SetComponent(entity, dec);
        }
    }
}

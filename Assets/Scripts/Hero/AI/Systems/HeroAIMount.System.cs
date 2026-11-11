using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// When an AI hero wants its horse (M19): to cover a long way with no enemy near, it rides; near the enemy, or close to
/// its destination, it fights on foot, where it has its skills. The only writer of its
/// <see cref="HeroMountRequestComponent"/>; <see cref="HeroMountSystem"/> calls the horse or sends it away.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HeroAIPerceptionSystem))]
public partial class HeroAIMountSystem : SystemBase
{
    EntityQuery _withoutRequest;

    protected override void OnCreate()
    {
        _withoutRequest = GetEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<HeroAITag>(), ComponentType.ReadOnly<HeroAIDecision>() },
            None = new[] { ComponentType.ReadOnly<HeroMountRequestComponent>() },
        });
    }

    protected override void OnUpdate()
    {
        if (!_withoutRequest.IsEmptyIgnoreFilter) EntityManager.AddComponent<HeroMountRequestComponent>(_withoutRequest);
        foreach (var (decision, pose, request, entity) in SystemAPI
                     .Query<RefRO<HeroAIDecision>, RefRO<LocalTransform>, RefRW<HeroMountRequestComponent>>()
                     .WithAll<HeroAITag>().WithEntityAccess())
        {
            bool riding = SystemAPI.HasComponent<HeroMountComponent>(entity) && SystemAPI.GetComponent<HeroMountComponent>(entity).Riding;
            bool underAttack = false;
            float enemyDistance = float.MaxValue;
            if (EntityManager.HasComponent<HeroAIBlackboard>(entity))
            {
                var blackboard = EntityManager.GetComponentObject<HeroAIBlackboard>(entity);
                underAttack = blackboard.isUnderAttack;
                if (blackboard.nearestEnemyHero != Entity.Null) enemyDistance = math.sqrt(blackboard.nearestEnemyDistanceSq);
            }
            var dec = decision.ValueRO;
            float distance = dec.action == AIActionType.Idle ? 0f : math.distance(pose.ValueRO.Position.xz, dec.targetPosition.xz);
            bool ride = HeroMountRules.AIWantsToRide(riding, distance, underAttack, enemyDistance);
            if (request.ValueRO.ride != ride) request.ValueRW.ride = ride;
        }
    }
}

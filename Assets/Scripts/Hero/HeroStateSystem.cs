using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct HeroStateSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>().WithEntityAccess())
        {
            if (!SystemAPI.HasComponent<HeroStateComponent>(entity))
                continue;

            var heroState = SystemAPI.GetComponent<HeroStateComponent>(entity);
            float3 prevPos = float3.zero;
            if (SystemAPI.HasComponent<UnitPrevLeaderPosComponent>(entity))
                prevPos = SystemAPI.GetComponent<UnitPrevLeaderPosComponent>(entity).value;

            float3 currPos = transform.ValueRO.Position;
            float distSq = math.lengthsq(currPos - prevPos);

            // Threshold for movement detection
            heroState.State = distSq > 0.0025f ? HeroState.Moving : HeroState.Idle;

            SystemAPI.SetComponent(entity, heroState);
            SystemAPI.SetComponent(entity, new UnitPrevLeaderPosComponent { value = currPos });
        }
    }
}

using Unity.Entities;
using Unity.Transforms;

/// <summary>
/// Caches the hero's world position and rotation onto each squad entity every frame.
/// Eliminates Squad→Hero cross-lookups in SquadAnchorSystem and FormationAdaptationSystem.
/// Must run before SquadAnchorSystem.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(SquadAnchorSystem))]
public partial struct HeroPositionCacheSystem : ISystem
{
    private ComponentLookup<LocalTransform> _transformLookup;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<MatchStateComponent>();
        _transformLookup = state.GetComponentLookup<LocalTransform>(true);
    }

    public void OnUpdate(ref SystemState state)
    {
        _transformLookup.Update(ref state);

        foreach (var (owner, heroWorldPos) in SystemAPI
                     .Query<RefRO<SquadOwnerComponent>, RefRW<HeroWorldPositionComponent>>())
        {
            if (!_transformLookup.TryGetComponent(owner.ValueRO.hero, out var heroTx))
                continue;

            heroWorldPos.ValueRW.position = heroTx.Position;
            heroWorldPos.ValueRW.rotation = heroTx.Rotation;
        }
    }
}

using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Computes the squad's formation anchor (position + rotation) each frame
/// and writes it to SquadFormationAnchorComponent.
///
/// Three cases in priority order:
///   1. HoldingPosition → holdCenter + holdRotation
///   2. Retreating      → retreatTarget + default rotation
///   3. Default (Follow)→ heroPos + forward * followForwardOffset + default rotation
///
/// Centralises all anchor logic so FormationSystem, GridFormationUpdateSystem,
/// and DestinationMarkerSystem can simply read the component instead of each
/// duplicating this branching logic.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitTargetingSystem))]
[UpdateBefore(typeof(FormationSystem))]
public partial class SquadAnchorSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
    }

    protected override void OnUpdate()
    {
        var ownerLookup    = GetComponentLookup<SquadOwnerComponent>(true);
        var transformLookup = GetComponentLookup<LocalTransform>(true);

        foreach (var (state, anchor, squadEntity) in SystemAPI
                     .Query<RefRO<SquadStateComponent>, RefRW<SquadFormationAnchorComponent>>()
                     .WithEntityAccess())
        {
            float3    position;
            quaternion rotation = default; // default == zero quaternion (no-rotation sentinel)

            if (state.ValueRO.currentState == SquadFSMState.HoldingPosition
                && SystemAPI.HasComponent<SquadHoldPositionComponent>(squadEntity))
            {
                var hold = SystemAPI.GetComponent<SquadHoldPositionComponent>(squadEntity);
                position = hold.holdCenter;
                rotation = hold.holdRotation;
            }
            else if (state.ValueRO.currentState == SquadFSMState.Retreating
                     && SystemAPI.HasComponent<RetreatComponent>(squadEntity))
            {
                position = SystemAPI.GetComponent<RetreatComponent>(squadEntity).retreatTarget;
                // rotation stays default
            }
            else
            {
                if (!HeroPositionUtility.TryGetHeroPosition(
                        squadEntity, ownerLookup, transformLookup, out float3 heroPos))
                    continue;

                if (ownerLookup.TryGetComponent(squadEntity, out var owner)
                    && transformLookup.TryGetComponent(owner.hero, out var heroTx))
                {
                    float followOffset =
                        SystemAPI.GetSingleton<SquadSpawnConfigComponent>().followForwardOffset;
                    heroPos += math.forward(heroTx.Rotation) * followOffset;
                }

                position = heroPos;
                // rotation stays default
            }

            anchor.ValueRW.position = position;
            anchor.ValueRW.rotation = rotation;
        }
    }
}

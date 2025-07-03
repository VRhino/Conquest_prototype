using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Scales <see cref="UnitStatsComponent"/> values based on squad level.
/// It runs during battle loading and whenever a <see cref="SquadLevelUpEvent"/>
/// is detected.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class UnitStatScalingSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<SquadProgressComponent>();
    }

    protected override void OnUpdate()
    {
        bool applyStats = false;

        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (_, evtEntity) in SystemAPI
                     .Query<RefRO<SquadLevelUpEvent>>()
                     .WithEntityAccess())
        {
            applyStats = true;
            ecb.DestroyEntity(evtEntity);
        }
        ecb.Playback(EntityManager);
        ecb.Dispose();

        if (!applyStats && !IsBattleLoading())
            return;

        var dataLookup = GetComponentLookup<SquadDataComponent>(true);
        var unitBufferLookup = GetBufferLookup<SquadUnitElement>();

        foreach (var (progress, dataRef, squad) in SystemAPI
                     .Query<RefRO<SquadProgressComponent>, RefRO<SquadDataReference>>()
                     .WithEntityAccess())
        {
            if (!dataLookup.TryGetComponent(dataRef.ValueRO.dataEntity, out var data))
                continue;

            UnitStatsUtility.ApplyStatsToSquad(squad, data, progress.ValueRO.level, EntityManager, unitBufferLookup);
        }
    }

    bool IsBattleLoading()
    {
        var q = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<MatchStateComponent>());
        if (q.IsEmptyIgnoreFilter)
            return false;
        var state = q.GetSingleton<MatchStateComponent>();
        return state.currentState == MatchState.LoadingMap;
    }
}

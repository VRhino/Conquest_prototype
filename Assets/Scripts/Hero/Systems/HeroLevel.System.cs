using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Aggregates XP events for the player hero and handles level progression.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class HeroLevelSystem : SystemBase
{
    LocalSaveSystem.PlayerProgressData _saveData;
    bool _initialized;

    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
        _saveData = LocalSaveSystem.LoadProgress();
    }

    protected override void OnUpdate()
    {
        if (!_initialized)
        {
            _initialized = InitializeProgressComponent();
            if (!_initialized) return;
        }

        if (!SystemAPI.TryGetSingletonEntity<HeroProgressComponent>(out var entity))
            return;

        var progress = SystemAPI.GetComponentRW<HeroProgressComponent>(entity);
        int gainedXP = 0;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (xp, evt) in SystemAPI.Query<RefRO<XPEventComponent>>().WithEntityAccess())
        {
            gainedXP += xp.ValueRO.amount;
            ecb.DestroyEntity(evt);
        }

        bool save = false;

        if (gainedXP > 0)
        {
            progress.ValueRW.currentXP += gainedXP;
            save = true;
        }

        while (progress.ValueRO.currentXP >= progress.ValueRO.xpToNextLevel)
        {
            progress.ValueRW.currentXP -= progress.ValueRO.xpToNextLevel;
            progress.ValueRW.level += 1;
            progress.ValueRW.perkPoints += 1;
            progress.ValueRW.xpToNextLevel = CalculateNext(progress.ValueRO.level);

            Entity evt = ecb.CreateEntity();
            ecb.AddComponent(evt, new LevelUpEvent { newLevel = progress.ValueRO.level });
            save = true;
        }

        // Playback destroys event entities and invalidates RefRW handles.
        var snapshot = progress.ValueRO;
        ecb.Playback(EntityManager);
        ecb.Dispose();

        if (save)
        {
            LocalSaveSystem.UpdateProgress(latest =>
            {
                latest.level = snapshot.level;
                latest.currentXP = snapshot.currentXP;
                latest.perkPoints = snapshot.perkPoints;
            });
        }
    }

    bool InitializeProgressComponent()
    {
        var q = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<HeroProgressComponent>());
        if (q.IsEmptyIgnoreFilter)
            return false;

        Entity entity = q.GetSingletonEntity();
        var progress = EntityManager.GetComponentData<HeroProgressComponent>(entity);
        progress.level = _saveData.level;
        progress.currentXP = _saveData.currentXP;
        progress.xpToNextLevel = CalculateNext(_saveData.level);
        progress.perkPoints = _saveData.perkPoints;
        EntityManager.SetComponentData(entity, progress);
        return true;
    }

    static int CalculateNext(int level)
    {
        return (int)math.floor(100 * math.pow(1.2f, level - 1));
    }

}

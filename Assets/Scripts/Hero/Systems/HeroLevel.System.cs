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
        _saveData = LocalSaveSystem.LoadProgress();
    }

    protected override void OnUpdate()
    {
        if (!_initialized)
        {
            InitializeProgressComponent();
            _initialized = true;
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

        ecb.Playback(EntityManager);
        ecb.Dispose();

        if (save || IsPostMatch())
        {
            _saveData.level = progress.ValueRO.level;
            _saveData.currentXP = progress.ValueRO.currentXP;
            _saveData.perkPoints = progress.ValueRO.perkPoints;
            LocalSaveSystem.SaveProgress(_saveData);
        }
    }

    void InitializeProgressComponent()
    {
        var q = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<HeroProgressComponent>());
        if (q.IsEmptyIgnoreFilter)
            return;

        Entity entity = q.GetSingletonEntity();
        var progress = EntityManager.GetComponentData<HeroProgressComponent>(entity);
        progress.level = _saveData.level;
        progress.currentXP = _saveData.currentXP;
        progress.xpToNextLevel = CalculateNext(_saveData.level);
        progress.perkPoints = _saveData.perkPoints;
        EntityManager.SetComponentData(entity, progress);
    }

    static int CalculateNext(int level)
    {
        return (int)math.floor(100 * math.pow(1.2f, level - 1));
    }

    bool IsPostMatch()
    {
        var q = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GameStateComponent>());
        if (q.IsEmptyIgnoreFilter)
            return false;
        var state = q.GetSingleton<GameStateComponent>();
        return state.currentPhase == GamePhase.PostPartida;
    }
}

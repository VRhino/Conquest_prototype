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
        _saveData = LocalSaveSystem.LoadGame();
        if (_saveData.heroProgress == null)
            _saveData.heroProgress = new LocalSaveSystem.HeroProgressData();
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
            _saveData.heroProgress.level = progress.ValueRO.level;
            _saveData.heroProgress.currentXP = progress.ValueRO.currentXP;
            _saveData.heroProgress.xpToNextLevel = progress.ValueRO.xpToNextLevel;
            _saveData.heroProgress.perkPoints = progress.ValueRO.perkPoints;
            LocalSaveSystem.SaveGame(_saveData);
        }
    }

    void InitializeProgressComponent()
    {
        if (!SystemAPI.TryGetSingletonEntity<HeroProgressComponent>(out var entity))
            return;

        var progress = SystemAPI.GetComponentRW<HeroProgressComponent>(entity);
        progress.ValueRW.level = _saveData.heroProgress.level;
        progress.ValueRW.currentXP = _saveData.heroProgress.currentXP;
        progress.ValueRW.xpToNextLevel = _saveData.heroProgress.xpToNextLevel;
        progress.ValueRW.perkPoints = _saveData.heroProgress.perkPoints;
    }

    static int CalculateNext(int level)
    {
        return (int)math.floor(100 * math.pow(1.2f, level - 1));
    }

    bool IsPostMatch()
    {
        if (SystemAPI.TryGetSingleton<GameStateComponent>(out var state))
            return state.currentPhase == GamePhase.PostPartida;
        return false;
    }
}

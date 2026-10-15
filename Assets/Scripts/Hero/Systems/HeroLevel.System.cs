using Unity.Collections;
using Unity.Entities;

/// <summary>Commits explicitly addressed hero XP; legacy unowned progress remains untouched.</summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BattleBootstrapSystem))]
public partial class HeroLevelSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<MatchStateComponent>();
        RequireForUpdate<XPEventComponent>();
    }

    protected override void OnUpdate()
    {
        if (!SystemAPI.TryGetSingletonEntity<HeroProgressComponent>(out var entity)) return;
        var progress = EntityManager.GetComponentData<HeroProgressComponent>(entity);
        if (progress.heroId.IsEmpty) return;
        int gainedXP = 0;
        using var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (xp, evt) in SystemAPI.Query<RefRO<XPEventComponent>>().WithEntityAccess())
        {
            // A stale event from another battle/hero is consumed without awarding it to this owner.
            if (xp.ValueRO.heroId == progress.heroId && xp.ValueRO.amount > 0)
                gainedXP = checked(gainedXP + xp.ValueRO.amount);
            ecb.DestroyEntity(evt);
        }
        if (gainedXP > 0)
        {
            int oldLevel = progress.level;
            progress.currentXP = checked(progress.currentXP + gainedXP);
            progress.xpToNextLevel = HeroExperienceCalculator.GetBattleXPForNextLevel(progress.level);
            while (progress.currentXP >= progress.xpToNextLevel && progress.xpToNextLevel > 0)
            {
                progress.currentXP -= progress.xpToNextLevel;
                progress.level++;
                progress.perkPoints++;
                progress.xpToNextLevel = HeroExperienceCalculator.GetBattleXPForNextLevel(progress.level);
            }
            // Persist first: a failed commit leaves both ECS and reward events unchanged for retry.
            if (!SaveSystem.TryUpdateHeroProgress(progress.heroId.ToString(), progress.level,
                progress.currentXP, progress.perkPoints)) return;
            EntityManager.SetComponentData(entity, progress);
            for (int level = oldLevel + 1; level <= progress.level; level++)
                ecb.AddComponent(ecb.CreateEntity(), new LevelUpEvent { newLevel = level });
        }
        ecb.Playback(EntityManager);
    }
}

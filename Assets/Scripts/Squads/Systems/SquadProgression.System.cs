using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>Consumes explicit XP deltas. Merely entering PostPartida never awards XP.</summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(UnitStatScalingSystem))]
[UpdateAfter(typeof(BattleBootstrapSystem))]
public partial class SquadProgressionSystem : SystemBase
{
    EntityQuery xpQuery, containerQuery;
    EntityArchetype levelUpArchetype;

    protected override void OnCreate()
    {
        xpQuery = GetEntityQuery(ComponentType.ReadOnly<SquadXPEvent>());
        containerQuery = GetEntityQuery(ComponentType.ReadOnly<DataContainerComponent>());
        RequireForUpdate(xpQuery);
        levelUpArchetype = EntityManager.CreateArchetype(typeof(SquadLevelUpEvent));
    }

    protected override void OnUpdate()
    {
        // A snapshot allows each successful reward to be consumed immediately. A later failed
        // commit must not leave already-persisted events in a discarded ECB for another retry.
        using var events = xpQuery.ToEntityArray(Allocator.Temp);
        foreach (var eventEntity in events)
        {
            if (!EntityManager.Exists(eventEntity)) continue;
            var reward = EntityManager.GetComponentData<SquadXPEvent>(eventEntity);
            if (reward.amount <= 0 || !EntityManager.HasComponent<SquadProgressComponent>(reward.squad))
            {
                EntityManager.DestroyEntity(eventEntity);
                continue;
            }
            Entity owner = EntityManager.HasComponent<SquadOwnerComponent>(reward.squad)
                ? EntityManager.GetComponentData<SquadOwnerComponent>(reward.squad).hero : Entity.Null;
            bool local = EntityManager.HasComponent<IsLocalSquadActive>(reward.squad)
                || (EntityManager.Exists(owner) && (EntityManager.HasComponent<IsLocalPlayer>(owner)
                    || (EntityManager.HasComponent<DataContainerComponent>(owner)
                        && EntityManager.HasComponent<HeroProgressComponent>(owner))));
            bool hasInstance = EntityManager.HasComponent<SquadInstanceComponent>(reward.squad);
            var instance = hasInstance
                ? EntityManager.GetComponentData<SquadInstanceComponent>(reward.squad) : default;
            Entity mapOwner = ResolveMapOwner(reward.squad, local);
            int mapIndex = hasInstance ? FindMapIndex(mapOwner, instance) : -1;
            if (local && (instance.persistentId.IsEmpty || mapIndex < 0))
            {
                EntityManager.DestroyEntity(eventEntity); // Stale/recycled local instance, never another roster entry.
                continue;
            }
            var progress = EntityManager.GetComponentData<SquadProgressComponent>(reward.squad);
            int oldLevel = progress.level;
            progress.level = math.clamp(progress.level, 1, SquadProgressionCurves.MaxLevel);
            progress.currentXP = math.max(0f, progress.currentXP) + reward.amount;
            progress.xpToNextLevel = CalculateNext(progress.level);
            while (progress.level < SquadProgressionCurves.MaxLevel && progress.currentXP >= progress.xpToNextLevel)
            {
                progress.currentXP -= progress.xpToNextLevel;
                progress.level++;
                progress.xpToNextLevel = CalculateNext(progress.level);
            }
            if (local)
            {
                if (!EntityManager.HasComponent<HeroProgressComponent>(mapOwner)) continue;
                var heroId = EntityManager.GetComponentData<HeroProgressComponent>(mapOwner).heroId;
                if (heroId.IsEmpty || !SaveSystem.TryUpdateSquadProgress(heroId.ToString(),
                    instance.persistentId.ToString(), progress.level, checked((int)progress.currentXP))) continue;
            }
            EntityManager.SetComponentData(reward.squad, progress);
            if (mapIndex >= 0)
            {
                var map = EntityManager.GetBuffer<SquadIdMapElement>(mapOwner);
                var entry = map[mapIndex];
                entry.level = progress.level;
                entry.currentXP = progress.currentXP;
                map[mapIndex] = entry;
            }
            EntityManager.DestroyEntity(eventEntity);
            if (oldLevel != progress.level)
            {
                var levelUp = EntityManager.CreateEntity(levelUpArchetype);
                EntityManager.SetComponentData(levelUp, new SquadLevelUpEvent { squad = reward.squad });
            }
        }
    }

    Entity ResolveMapOwner(Entity squad, bool local)
    {
        if (!EntityManager.HasComponent<SquadOwnerComponent>(squad)) return Entity.Null;
        Entity owner = EntityManager.GetComponentData<SquadOwnerComponent>(squad).hero;
        if (!EntityManager.Exists(owner)) return Entity.Null;
        if (EntityManager.HasBuffer<SquadIdMapElement>(owner)) return owner;
        if (local && EntityManager.HasComponent<IsLocalPlayer>(owner) && containerQuery.CalculateEntityCount() == 1)
            return containerQuery.GetSingletonEntity();
        return Entity.Null;
    }

    int FindMapIndex(Entity owner, SquadInstanceComponent instance)
    {
        if (!EntityManager.Exists(owner) || !EntityManager.HasBuffer<SquadIdMapElement>(owner)) return -1;
        var map = EntityManager.GetBuffer<SquadIdMapElement>(owner);
        for (int i = 0; i < map.Length; i++)
            if (map[i].squadId == instance.id && map[i].persistentId == instance.persistentId) return i;
        return -1;
    }

    public static float CalculateNext(int level) => math.floor(100f * math.pow(1.1f, level - 1));
}

/// <summary>Local ECS delivery event, not an authenticated/network BattleResult.</summary>
public struct SquadXPEvent : IComponentData
{
    public Entity squad;
    public int amount;
}


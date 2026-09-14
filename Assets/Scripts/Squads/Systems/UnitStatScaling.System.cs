using Unity.Collections;
using Unity.Entities;

/// <summary>Initializes each squad once and updates only squads named by level-up events.</summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(SquadSpawningSystem))]
[UpdateAfter(typeof(SquadProgressionSystem))]
public partial class UnitStatScalingSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Snapshot entities before utilities perform any structural changes.
        using var initial = GetEntityQuery(
            ComponentType.ReadOnly<SquadProgressComponent>(),
            ComponentType.ReadOnly<SquadDataReference>(),
            ComponentType.Exclude<SquadStatsInitialized>()).ToEntityArray(Allocator.Temp);
        using var events = GetEntityQuery(ComponentType.ReadOnly<SquadLevelUpEvent>()).ToEntityArray(Allocator.Temp);
        using var targets = new NativeHashSet<Entity>(initial.Length + events.Length + 1, Allocator.Temp);
        foreach (var squad in initial) targets.Add(squad);
        foreach (var evt in events)
        {
            targets.Add(EntityManager.GetComponentData<SquadLevelUpEvent>(evt).squad);
            EntityManager.DestroyEntity(evt);
        }
        foreach (var squad in targets)
        {
            if (!EntityManager.HasComponent<SquadProgressComponent>(squad) ||
                !EntityManager.HasComponent<SquadDataReference>(squad)) continue;
            var source = EntityManager.GetComponentData<SquadDataReference>(squad).dataEntity;
            if (!EntityManager.HasComponent<SquadDataComponent>(source) ||
                !EntityManager.HasBuffer<SquadUnitElement>(squad)) continue;
            var data = EntityManager.GetComponentData<SquadDataComponent>(source);
            int leadership = EntityManager.HasComponent<SquadDefinitionComponent>(source)
                ? EntityManager.GetComponentData<SquadDefinitionComponent>(source).leadershipCost : 0;
            int level = EntityManager.GetComponentData<SquadProgressComponent>(squad).level;
            UnitStatsUtility.ApplyStatsToSquad(squad, data, leadership, level, EntityManager, GetBufferLookup<SquadUnitElement>(true));
            UpdateUnlocks(squad, source, level);
            if (!EntityManager.HasComponent<SquadStatsInitialized>(squad))
                EntityManager.AddComponent<SquadStatsInitialized>(squad);
        }
    }

    void UpdateUnlocks(Entity squad, Entity source, int level)
    {
        if (!EntityManager.HasBuffer<AbilityByLevelElement>(source)) return;
        if (!EntityManager.HasBuffer<UnlockedAbilityElement>(squad))
            EntityManager.AddBuffer<UnlockedAbilityElement>(squad);
        var abilities = EntityManager.GetBuffer<AbilityByLevelElement>(source, true);
        var unlocked = EntityManager.GetBuffer<UnlockedAbilityElement>(squad);
        for (int i = 0; i < abilities.Length && (i + 1) * 10 <= level; i++)
        {
            var ability = abilities[i].Value;
            if (ability == Entity.Null) continue;
            bool exists = false;
            foreach (var entry in unlocked)
                if (entry.Value == ability) { exists = true; break; }
            if (!exists) unlocked.Add(new UnlockedAbilityElement { Value = ability });
        }
    }
}

public struct SquadStatsInitialized : IComponentData { }

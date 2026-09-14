using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>Consumes explicit XP deltas. Merely entering PostPartida never awards XP.</summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(UnitStatScalingSystem))]
public partial class SquadProgressionSystem : SystemBase
{
    protected override void OnCreate() => RequireForUpdate<SquadXPEvent>();

    protected override void OnUpdate()
    {
        var progressLookup = GetComponentLookup<SquadProgressComponent>();
        using var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (xp, eventEntity) in SystemAPI.Query<RefRO<SquadXPEvent>>().WithEntityAccess())
        {
            var reward = xp.ValueRO;
            ecb.DestroyEntity(eventEntity);
            if (reward.amount <= 0 || !progressLookup.HasComponent(reward.squad)) continue;
            var progress = progressLookup[reward.squad];
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
            progressLookup[reward.squad] = progress;
            if (SystemAPI.HasComponent<SquadOwnerComponent>(reward.squad) &&
                SystemAPI.HasComponent<SquadInstanceComponent>(reward.squad))
            {
                var owner = SystemAPI.GetComponent<SquadOwnerComponent>(reward.squad).hero;
                var instance = SystemAPI.GetComponent<SquadInstanceComponent>(reward.squad);
                if (!SystemAPI.HasBuffer<SquadIdMapElement>(owner) &&
                    SystemAPI.HasComponent<IsLocalSquadActive>(reward.squad) &&
                    SystemAPI.TryGetSingletonEntity<DataContainerComponent>(out var container))
                    owner = container;
                if (SystemAPI.HasBuffer<SquadIdMapElement>(owner))
                {
                    var map = SystemAPI.GetBuffer<SquadIdMapElement>(owner);
                    for (int i = 0; i < map.Length; i++)
                    {
                        var entry = map[i];
                        if (entry.squadId != instance.id) continue;
                        entry.level = progress.level;
                        entry.currentXP = progress.currentXP;
                        map[i] = entry;
                        break;
                    }
                }
            }
            if (oldLevel != progress.level)
                ecb.AddComponent(ecb.CreateEntity(), new SquadLevelUpEvent { squad = reward.squad });

            if (SystemAPI.HasComponent<IsLocalSquadActive>(reward.squad) &&
                SystemAPI.HasComponent<SquadInstanceComponent>(reward.squad))
            {
                var instance = SystemAPI.GetComponent<SquadInstanceComponent>(reward.squad);
                LocalSaveSystem.UpdateProgress(latest =>
                {
                    var record = LocalSaveSystem.FindSquad(latest, instance.id, instance.persistentId.ToString());
                    if (record == null)
                    {
                        record = new LocalSaveSystem.SquadInstanceData
                        {
                            id = instance.id, persistentId = instance.persistentId.ToString()
                        };
                        latest.squads.Add(record);
                    }
                    record.level = progress.level;
                    record.currentXP = progress.currentXP;
                });
            }
        }
        ecb.Playback(EntityManager);
    }

    public static float CalculateNext(int level) => math.floor(100f * math.pow(1.1f, level - 1));
}

/// <summary>Local ECS delivery event, not an authenticated/network BattleResult.</summary>
public struct SquadXPEvent : IComponentData
{
    public Entity squad;
    public int amount;
}


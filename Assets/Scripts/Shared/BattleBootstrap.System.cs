using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>Managed requests only; ECS writes are owned by BattleBootstrapSystem.</summary>
public static class BattleBootstrapRequests
{
    internal static BattleData Battle;
    internal static string LocalHeroName;
    internal static readonly List<(World world, Entity hero, SquadIdMapElement[] squads)> Remote = new();

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { Battle = null; LocalHeroName = null; Remote.Clear(); }

    public static void SubmitLocal(BattleData battle, string heroName)
    {
        Battle = battle;
        LocalHeroName = heroName;
    }

    public static void SubmitRemote(World world, Entity hero, List<SquadInstanceData> squads)
        => Remote.Add((world, hero, Snapshot(squads)));

    internal static SquadIdMapElement[] Snapshot(List<SquadInstanceData> squads)
    {
        var result = new SquadIdMapElement[squads?.Count ?? 0];
        for (int i = 0; i < result.Length; i++)
        {
            var squad = squads[i];
            if (squad == null) continue;
            result[i] = new SquadIdMapElement
            {
                squadId = i,
                baseSquadID = new FixedString64Bytes(squad.baseSquadID ?? string.Empty),
                persistentId = new FixedString64Bytes(squad.id ?? string.Empty),
                hasSnapshot = true,
                level = math.clamp(squad.level, 1, SquadProgressionCurves.MaxLevel),
                currentXP = math.max(0, squad.experience),
                totalUnits = math.max(0, squad.unitsInSquad),
                aliveUnits = math.clamp(squad.unitsAlive, 0, math.max(0, squad.unitsInSquad)),
                formationIndex = math.max(0, squad.selectedFormationIndex)
            };
        }
        return result;
    }
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(HeroSpawnSystem))]
[UpdateBefore(typeof(SquadSpawningSystem))]
public partial class BattleBootstrapSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var battle = BattleBootstrapRequests.Battle;
        if (battle != null && SystemAPI.TryGetSingletonEntity<DataContainerComponent>(out var container))
        {
            var hero = battle.findHeroDataByName(BattleBootstrapRequests.LocalHeroName);
            if (hero != null)
            {
                var data = EntityManager.GetComponentData<DataContainerComponent>(container);
                var maps = BattleBootstrapRequests.Snapshot(hero.squadInstances);
                data.selectedSquads.Clear();
                data.selectedSquadBaseID = default;
                data.isReady = false;
                if (maps.Length <= data.selectedSquads.Capacity)
                {
                    int.TryParse(hero.spawnPointId, out int spawn);
                    data.selectedSpawnID = spawn > 0 ? spawn : 1;
                    data.teamID = battle.playerSide(hero.heroName) == Side.Defenders ? 2 : 1;
                    data.isReady = maps.Length > 0;
                    if (maps.Length > 0) data.selectedSquadBaseID = maps[0].baseSquadID;
                    for (int i = 0; i < maps.Length; i++) data.selectedSquads.Add(i);
                    SetMap(container, maps);
                }
                else
                    UnityEngine.Debug.LogError("Battle loadout exceeds DataContainer selectedSquads capacity.");
                EntityManager.SetComponentData(container, data);
            }
            else UnityEngine.Debug.LogError("Battle bootstrap: local hero is absent from the battle ticket.");
            BattleBootstrapRequests.Battle = null;
        }
        for (int i = BattleBootstrapRequests.Remote.Count - 1; i >= 0; i--)
        {
            var request = BattleBootstrapRequests.Remote[i];
            if (request.world != World) continue;
            if (EntityManager.Exists(request.hero)) SetMap(request.hero, request.squads);
            BattleBootstrapRequests.Remote.RemoveAt(i);
        }
    }

    void SetMap(Entity target, SquadIdMapElement[] values)
    {
        if (!EntityManager.HasBuffer<SquadIdMapElement>(target)) EntityManager.AddBuffer<SquadIdMapElement>(target);
        var buffer = EntityManager.GetBuffer<SquadIdMapElement>(target);
        buffer.Clear();
        foreach (var value in values) buffer.Add(value);
    }
}

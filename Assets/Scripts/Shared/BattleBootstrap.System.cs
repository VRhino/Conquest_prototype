using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>Managed requests only; ECS writes are owned by BattleBootstrapSystem.</summary>
public static class BattleBootstrapRequests
{
    internal static BattleData Battle;
    internal static string LocalHeroName;
    internal static string LocalHeroId;
    internal static readonly List<(World world, Entity hero, SquadIdMapElement[] squads)> Remote = new();

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { Battle = null; LocalHeroName = null; LocalHeroId = null; Remote.Clear(); }

    public static void SubmitLocal(BattleData battle, string heroName, string heroId = null)
    {
        Battle = battle;
        LocalHeroName = heroName;
        LocalHeroId = heroId;
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
[UpdateBefore(typeof(MatchControllerSystem))]
public partial class BattleBootstrapSystem : SystemBase
{
    /// <summary>Starts (or restarts) the battle clock from the battle and its map (L7-1).</summary>
    void StartBattleClock(BattleData battle)
    {
        var map = battle.mapData;
        var clock = new BattleClockComponent
        {
            remainingSeconds = BattleClock.InitialSeconds(battle.BattleTimer, map != null ? map.battleDuration : 0),
            maxSeconds = map != null ? map.maxBattleDurationSeconds : BattleClock.DefaultMaxBattleSeconds,
            captureExtensionSeconds = map != null ? map.captureTimeExtensionSeconds : BattleClock.DefaultCaptureExtensionSeconds,
        };
        if (SystemAPI.TryGetSingletonEntity<BattleClockComponent>(out var entity))
            EntityManager.SetComponentData(entity, clock);
        else
            EntityManager.AddComponentData(EntityManager.CreateEntity(), clock);
    }

    protected override void OnUpdate()
    {
        var battle = BattleBootstrapRequests.Battle;
        if (battle != null && SystemAPI.TryGetSingletonEntity<DataContainerComponent>(out var container))
        {
            var hero = battle.FindHero(BattleBootstrapRequests.LocalHeroId, BattleBootstrapRequests.LocalHeroName);
            if (hero != null)
            {
                var player = PlayerSessionService.CurrentPlayer;
                if (player?.requiresIdentitySave == true)
                {
                    SaveSystem.SavePlayer(player);
                    if (player.requiresIdentitySave) return; // Retain the request until identity can be committed.
                }
                var data = EntityManager.GetComponentData<DataContainerComponent>(container);
                // The accepted ticket owns the phase in this world, including direct test-scene
                // loads where the previous world's scene transition cannot initialize it.
                if (SystemAPI.TryGetSingletonEntity<GameStateComponent>(out var gameStateEntity))
                {
                    var gameState = EntityManager.GetComponentData<GameStateComponent>(gameStateEntity);
                    gameState.currentPhase = GamePhase.Combate;
                    EntityManager.SetComponentData(gameStateEntity, gameState);
                }
                EntityManager.AddComponentObject(container,
                    HeroAppearanceComponent.Capture(hero.avatar, hero.equipment, hero.gender));
                var maps = BattleBootstrapRequests.Snapshot(hero.squadInstances);
                data.selectedSquads.Clear();
                data.selectedSquadBaseID = default;
                data.isReady = false;
                if (maps.Length <= data.selectedSquads.Capacity)
                {
                    int.TryParse(hero.spawnPointId, out int spawn);
                    data.selectedSpawnID = spawn > 0 ? spawn : 1;
                    data.teamID = battle.GetSide(hero) == Side.Defenders ? 2 : 1;
                    data.isReady = maps.Length > 0;
                    if (maps.Length > 0) data.selectedSquadBaseID = maps[0].baseSquadID;
                    for (int i = 0; i < maps.Length; i++) data.selectedSquads.Add(i);
                    SetMap(container, maps);
                    string ownerId = string.IsNullOrEmpty(hero.heroId) ? BattleBootstrapRequests.LocalHeroId : hero.heroId;
                    var owner = HeroDataService.FindById(PlayerSessionService.CurrentPlayer, ownerId);
                    var progress = new HeroProgressComponent
                    {
                        heroId = owner != null ? new FixedString64Bytes(owner.heroId) : default,
                        level = owner?.level ?? hero.level,
                        currentXP = owner?.currentXP ?? 0,
                        perkPoints = owner?.perkPoints ?? 0,
                        xpToNextLevel = HeroExperienceCalculator.GetBattleXPForNextLevel(owner?.level ?? hero.level)
                    };
                    if (EntityManager.HasComponent<HeroProgressComponent>(container))
                        EntityManager.SetComponentData(container, progress);
                    else EntityManager.AddComponentData(container, progress);
                    if (data.isReady && SystemAPI.TryGetSingletonEntity<MatchStateComponent>(out var matchEntity))
                    {
                        var match = EntityManager.GetComponentData<MatchStateComponent>(matchEntity);
                        match.currentState = MatchState.InBattle;
                        match.stateTimer = 0f;
                        match.victoryConditionMet = false;
                        match.winnerTeam = 0;
                        match.playersReady = 0;
                        EntityManager.SetComponentData(matchEntity, match);
                        var controller = World.GetExistingSystemManaged<MatchControllerSystem>();
                        if (controller != null) controller.Enabled = true;
                        StartBattleClock(battle);
                    }
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

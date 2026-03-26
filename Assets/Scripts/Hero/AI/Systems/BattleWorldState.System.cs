using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Centralized battle data collection system. Intent-agnostic — purely collects
/// and organizes world state for consumption by AI systems.
///
/// Four passes per frame:
///   1. Visibility — aggregates DetectedEnemy buffers per team → fog-of-war sets
///   2. Heroes    — snapshots all heroes, splits by team, applies fog-of-war
///   3. Squads    — snapshots all squads, splits by team, applies fog-of-war
///   4. Zones     — snapshots all active zones (shared, no fog-of-war)
///
/// Pipeline position:
///   BattleWorldStateSystem → HeroAIPerceptionSystem → [Behaviors] → HeroAIExecutionSystem
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(HeroAIPerceptionSystem))]
public partial class BattleWorldStateSystem : SystemBase
{
    private Entity _singletonEntity;

    // Cached per-frame fog-of-war sets — cleared each frame instead of allocated
    private readonly HashSet<Entity> _visibleByTeamA = new();
    private readonly HashSet<Entity> _visibleByTeamB = new();

    // Lookups
    private ComponentLookup<TeamComponent>                 _teamLookup;
    private ComponentLookup<HeroHealthComponent>           _healthLookup;
    private ComponentLookup<StaminaComponent>              _staminaLookup;
    private ComponentLookup<HeroLifeComponent>             _lifeLookup;
    private ComponentLookup<HeroSquadReference>            _squadRefLookup;
    private ComponentLookup<SquadDataComponent>            _squadDataLookup;
    private ComponentLookup<SquadAIComponent>              _squadAILookup;
    private ComponentLookup<SquadStateComponent>           _squadStateLookup;
    private ComponentLookup<SquadOwnerComponent>           _squadOwnerLookup;
    private ComponentLookup<SquadFormationAnchorComponent> _anchorLookup;
    private ComponentLookup<SupplyPointComponent>          _supplyLookup;
    private ComponentLookup<CapturePointProgressComponent> _captureLookup;
    private ComponentLookup<HeroAIDecision>                _decisionLookup;
    private ComponentLookup<HeroAITag>                     _aiTagLookup;
    private BufferLookup<DetectedEnemy>                    _detectedEnemyLookup;

    protected override void OnCreate()
    {
        _teamLookup          = GetComponentLookup<TeamComponent>(true);
        _healthLookup        = GetComponentLookup<HeroHealthComponent>(true);
        _staminaLookup       = GetComponentLookup<StaminaComponent>(true);
        _lifeLookup          = GetComponentLookup<HeroLifeComponent>(true);
        _squadRefLookup      = GetComponentLookup<HeroSquadReference>(true);
        _squadDataLookup     = GetComponentLookup<SquadDataComponent>(true);
        _squadAILookup       = GetComponentLookup<SquadAIComponent>(true);
        _squadStateLookup    = GetComponentLookup<SquadStateComponent>(true);
        _squadOwnerLookup    = GetComponentLookup<SquadOwnerComponent>(true);
        _anchorLookup        = GetComponentLookup<SquadFormationAnchorComponent>(true);
        _supplyLookup        = GetComponentLookup<SupplyPointComponent>(true);
        _captureLookup       = GetComponentLookup<CapturePointProgressComponent>(true);
        _decisionLookup      = GetComponentLookup<HeroAIDecision>(true);
        _aiTagLookup         = GetComponentLookup<HeroAITag>(true);
        _detectedEnemyLookup = GetBufferLookup<DetectedEnemy>(true);

        // Create singleton
        _singletonEntity = EntityManager.CreateEntity();
        EntityManager.AddComponentObject(_singletonEntity, new TeamWorldState());
#if UNITY_EDITOR
        EntityManager.SetName(_singletonEntity, "TeamWorldState");
#endif
    }

    protected override void OnUpdate()
    {
        // Only run during active battle
        if (!SystemAPI.TryGetSingleton<MatchStateComponent>(out var matchComp))
            return;
        if (matchComp.currentState != MatchState.InBattle)
            return;

        // Refresh lookups
        _teamLookup.Update(this);
        _healthLookup.Update(this);
        _staminaLookup.Update(this);
        _lifeLookup.Update(this);
        _squadRefLookup.Update(this);
        _squadDataLookup.Update(this);
        _squadAILookup.Update(this);
        _squadStateLookup.Update(this);
        _squadOwnerLookup.Update(this);
        _anchorLookup.Update(this);
        _supplyLookup.Update(this);
        _captureLookup.Update(this);
        _decisionLookup.Update(this);
        _aiTagLookup.Update(this);
        _detectedEnemyLookup.Update(this);

        var ws = EntityManager.GetComponentObject<TeamWorldState>(_singletonEntity);

        // ── Match context ──────────────────────────────────────────────────
        ws.match = new MatchContext
        {
            isActive   = true,
            stateTimer = matchComp.stateTimer,
            winnerTeam = matchComp.winnerTeam,
            state      = matchComp.currentState
        };

        // ── Spawn positions (cached once) ──────────────────────────────────
        if (!ws.spawnsCached)
        {
            foreach (var (sp, _) in SystemAPI.Query<RefRO<SpawnPointComponent>>().WithEntityAccess())
            {
                if (!sp.ValueRO.isActive) continue;
                if (sp.ValueRO.teamID == 1) ws.spawnPositionTeamA = sp.ValueRO.position;
                if (sp.ValueRO.teamID == 2) ws.spawnPositionTeamB = sp.ValueRO.position;
            }
            ws.spawnsCached = true;
        }

        // ── PASS 1: Visibility — fog-of-war per team ──────────────────────
        _visibleByTeamA.Clear();
        _visibleByTeamB.Clear();
        var visibleByTeamA = _visibleByTeamA;
        var visibleByTeamB = _visibleByTeamB;

        foreach (var (owner, entity) in
                 SystemAPI.Query<RefRO<SquadOwnerComponent>>()
                          .WithEntityAccess())
        {
            Entity heroEntity = owner.ValueRO.hero;
            if (!_teamLookup.HasComponent(heroEntity)) continue;

            Team heroTeam = _teamLookup[heroEntity].value;
            var targetSet = heroTeam == Team.TeamA ? visibleByTeamA : visibleByTeamB;

            if (_detectedEnemyLookup.HasBuffer(entity))
            {
                var buffer = _detectedEnemyLookup[entity];
                for (int i = 0; i < buffer.Length; i++)
                    targetSet.Add(buffer[i].Value);
            }
        }

        // ── PASS 2: Hero snapshots ────────────────────────────────────────
        ws.teamA.allyHeroes.Clear();
        ws.teamA.visibleEnemyHeroes.Clear();
        ws.teamB.allyHeroes.Clear();
        ws.teamB.visibleEnemyHeroes.Clear();

        foreach (var (xform, health, stamina, life, team, entity) in
                 SystemAPI.Query<RefRO<LocalTransform>,
                                 RefRO<HeroHealthComponent>,
                                 RefRO<StaminaComponent>,
                                 RefRO<HeroLifeComponent>,
                                 RefRO<TeamComponent>>()
                          .WithEntityAccess())
        {
            var snap = new HeroSnapshot
            {
                entity         = entity,
                position       = xform.ValueRO.Position,
                healthPercent  = health.ValueRO.maxHealth > 0f
                    ? health.ValueRO.currentHealth / health.ValueRO.maxHealth : 0f,
                staminaPercent = stamina.ValueRO.maxStamina > 0f
                    ? stamina.ValueRO.currentStamina / stamina.ValueRO.maxStamina : 0f,
                isAlive        = life.ValueRO.isAlive,
                team           = team.ValueRO.value,
                isAIControlled = _aiTagLookup.HasComponent(entity),
                currentDecision = _decisionLookup.HasComponent(entity)
                    ? _decisionLookup[entity] : default,
            };

            // Resolve squad info
            if (_squadRefLookup.HasComponent(entity))
            {
                Entity sq = _squadRefLookup[entity].squad;
                snap.squadEntity = sq;
                snap.hasSquad    = sq != Entity.Null && SystemAPI.Exists(sq);

                if (snap.hasSquad)
                {
                    if (_squadDataLookup.HasComponent(sq))
                    {
                        var sd = _squadDataLookup[sq];
                        snap.squadType      = sd.squadType;
                        snap.isRangedSquad  = sd.isRangedUnit;
                    }
                    if (_squadAILookup.HasComponent(sq))
                        snap.squadIsInCombat = _squadAILookup[sq].isInCombat;
                    if (_squadStateLookup.HasComponent(sq))
                    {
                        snap.squadCurrentOrder = _squadStateLookup[sq].currentOrder;
                        snap.squadFormation    = _squadStateLookup[sq].currentFormation;
                    }
                }
            }

            // Place in correct team lists
            Team heroTeam = team.ValueRO.value;
            if (heroTeam == Team.TeamA)
            {
                ws.teamA.allyHeroes.Add(snap);
                // TeamB sees this hero only if detected
                if (visibleByTeamB.Contains(entity))
                    ws.teamB.visibleEnemyHeroes.Add(snap);
            }
            else
            {
                ws.teamB.allyHeroes.Add(snap);
                if (visibleByTeamA.Contains(entity))
                    ws.teamA.visibleEnemyHeroes.Add(snap);
            }
        }

        // ── PASS 3: Squad snapshots ───────────────────────────────────────
        ws.teamA.allySquads.Clear();
        ws.teamA.visibleEnemySquads.Clear();
        ws.teamB.allySquads.Clear();
        ws.teamB.visibleEnemySquads.Clear();

        foreach (var (owner, anchor, entity) in
                 SystemAPI.Query<RefRO<SquadOwnerComponent>,
                                 RefRO<SquadFormationAnchorComponent>>()
                          .WithEntityAccess())
        {
            Entity heroEntity = owner.ValueRO.hero;
            if (!_teamLookup.HasComponent(heroEntity)) continue;

            Team squadTeam = _teamLookup[heroEntity].value;

            var snap = new SquadSnapshot
            {
                entity    = entity,
                ownerHero = heroEntity,
                position  = anchor.ValueRO.position,
                squadType = default,
                isRanged  = false,
            };

            if (_squadDataLookup.HasComponent(entity))
            {
                var sd = _squadDataLookup[entity];
                snap.squadType = sd.squadType;
                snap.isRanged  = sd.isRangedUnit;
            }
            if (_squadAILookup.HasComponent(entity))
            {
                var ai = _squadAILookup[entity];
                snap.isInCombat    = ai.isInCombat;
                snap.tacticalIntent = ai.tacticalIntent;
            }
            if (_squadStateLookup.HasComponent(entity))
            {
                var st = _squadStateLookup[entity];
                snap.currentOrder     = st.currentOrder;
                snap.currentFormation = st.currentFormation;
            }

            if (squadTeam == Team.TeamA)
            {
                ws.teamA.allySquads.Add(snap);
                if (visibleByTeamB.Contains(entity))
                    ws.teamB.visibleEnemySquads.Add(snap);
            }
            else
            {
                ws.teamB.allySquads.Add(snap);
                if (visibleByTeamA.Contains(entity))
                    ws.teamA.visibleEnemySquads.Add(snap);
            }
        }

        // ── Diagnostic (first InBattle frame) ────────────────────────
        // ── PASS 4: Zones (shared, no fog-of-war) ────────────────────────
        ws.zones.Clear();

        foreach (var (zone, xform, entity) in
                 SystemAPI.Query<RefRO<ZoneTriggerComponent>, RefRO<LocalTransform>>()
                          .WithEntityAccess())
        {
            if (!zone.ValueRO.isActive) continue;

            float progress  = 0f;
            bool  contested = false;
            bool  capturing = false;

            if (_supplyLookup.HasComponent(entity))
            {
                var sp   = _supplyLookup[entity];
                progress  = sp.captureProgress;
                contested = sp.isContested;
                capturing = sp.isCapturing;
            }
            else if (_captureLookup.HasComponent(entity))
            {
                var cp   = _captureLookup[entity];
                progress  = cp.captureProgress;
                contested = cp.isContested;
                capturing = cp.isBeingCaptured;
            }

            ws.zones.Add(new ZoneSnapshot
            {
                entity          = entity,
                position        = xform.ValueRO.Position,
                radius          = zone.ValueRO.radius,
                teamOwner       = zone.ValueRO.teamOwner,
                captureProgress = progress,
                isContested     = contested,
                isBeingCaptured = capturing,
                isLocked        = zone.ValueRO.isLocked,
                isFinal         = zone.ValueRO.isFinal,
                zoneType        = zone.ValueRO.zoneType,
            });
        }

    }
}

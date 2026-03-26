using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Centralized, intent-agnostic battle data available to AI heroes.
/// Managed singleton populated once per frame by <see cref="BattleWorldStateSystem"/>.
/// Contains per-team views with fog-of-war filtering: each team only sees
/// enemy heroes/squads detected by their own squads' <see cref="DetectedEnemy"/> buffers.
///
/// This component is a pure DATA SERVICE — it never interprets, prioritizes,
/// or calculates objectives. Each behavior system reads the raw data
/// and applies its own logic.
/// </summary>
public class TeamWorldState : IComponentData
{
    // =========================================================
    // Per-team views
    // =========================================================
    public TeamView teamA = new();
    public TeamView teamB = new();

    // =========================================================
    // Shared data (no fog-of-war on zones or match state)
    // =========================================================
    public List<ZoneSnapshot> zones = new();
    public MatchContext match;
    public float3 spawnPositionTeamA;
    public float3 spawnPositionTeamB;
    public bool spawnsCached;

    // ── Access helpers ──────────────────────────────────────────
    public TeamView For(Team team) => team == Team.TeamA ? teamA : teamB;
    public TeamView EnemyOf(Team team) => team == Team.TeamA ? teamB : teamA;

    public float3 SpawnFor(Team team) =>
        team == Team.TeamA ? spawnPositionTeamA : spawnPositionTeamB;
}

/// <summary>
/// One team's view of the battlefield.
/// Ally data is always complete; enemy data is filtered by fog-of-war.
/// </summary>
public class TeamView
{
    public List<HeroSnapshot>  allyHeroes          = new();
    public List<HeroSnapshot>  visibleEnemyHeroes  = new();
    public List<SquadSnapshot> allySquads           = new();
    public List<SquadSnapshot> visibleEnemySquads   = new();
}

// =========================================================
// Snapshot structs — frozen copies of ECS state this frame
// =========================================================

public struct HeroSnapshot
{
    public Entity    entity;
    public float3    position;
    public float     healthPercent;   // 0–1
    public float     staminaPercent;  // 0–1
    public bool      isAlive;
    public Team      team;

    // Squad info (resolved from HeroSquadReference)
    public Entity         squadEntity;
    public bool           hasSquad;
    public SquadType      squadType;
    public bool           isRangedSquad;
    public bool           squadIsInCombat;
    public SquadOrderType squadCurrentOrder;
    public FormationType  squadFormation;

    // AI state (Entity.Null decision for human-controlled heroes)
    public HeroAIDecision currentDecision;
    public bool           isAIControlled;
}

public struct SquadSnapshot
{
    public Entity         entity;
    public Entity         ownerHero;
    public float3         position;
    public SquadType      squadType;
    public bool           isRanged;
    public bool           isInCombat;
    public SquadOrderType currentOrder;
    public FormationType  currentFormation;
    public TacticalIntent tacticalIntent;
}

public struct ZoneSnapshot
{
    public Entity   entity;
    public float3   position;
    public float    radius;
    public int      teamOwner;       // 0=neutral, 1=TeamA, 2=TeamB
    public float    captureProgress;
    public bool     isContested;
    public bool     isBeingCaptured;
    public bool     isLocked;
    public bool     isFinal;
    public ZoneType zoneType;
}

public struct MatchContext
{
    public bool       isActive;
    public float      stateTimer;
    public int        winnerTeam;    // 0=undecided, 1=attackers, 2=defenders
    public MatchState state;
}

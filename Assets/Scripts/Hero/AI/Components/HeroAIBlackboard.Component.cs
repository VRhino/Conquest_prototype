using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Per-hero derived perception cache. Written by <see cref="HeroAIPerceptionSystem"/>
/// every N frames using data from <see cref="TeamWorldState"/>.
///
/// Contains ONLY per-hero computed values (distances, "am I inside a zone?", etc.).
/// Raw team/world data lives in <see cref="TeamWorldState"/> — behaviors read both.
/// </summary>
public class HeroAIBlackboard : IComponentData
{
    // =========================================================
    // Self state
    // =========================================================
    public float3 selfPosition;
    public float  selfHealthPercent;    // 0–1
    public float  selfStaminaPercent;   // 0–1
    public bool   selfIsAlive;
    public Team   selfTeam;
    public bool   selfIsAttacker;       // true = TeamA (attackers)

    // =========================================================
    // Own squad (resolved from HeroSquadReference)
    // =========================================================
    public Entity         ownSquadEntity;
    public bool           hasSquad;
    public SquadType      ownSquadType;
    public bool           ownSquadIsRanged;
    public bool           squadIsInCombat;
    public int            squadUnitCount;
    public SquadOrderType squadCurrentOrder;

    // =========================================================
    // Match context
    // =========================================================
    public bool matchIsActive;
    public int  winnerTeam;             // 0 = no winner yet

    // =========================================================
    // Derived data — computed by Perception from TeamWorldState
    // =========================================================

    /// <summary>Closest zone that is not already owned by this hero's team and is not locked.</summary>
    public Entity  bestObjectiveZone;
    public float3  bestObjectivePosition;

    /// <summary>Zone that currently contains a hostile capture attempt targeting this hero's team.</summary>
    public Entity  threatZone;
    public float3  threatZonePosition;

    /// <summary>Nearest alive enemy hero and its squared distance.</summary>
    public Entity nearestEnemyHero;
    public float3 nearestEnemyPosition;
    public float  nearestEnemyDistanceSq;

    /// <summary>Whether this hero is currently inside any active zone.</summary>
    public bool   isInsideAnyZone;
    /// <summary>Zone entity this hero is standing in (Entity.Null if none).</summary>
    public Entity zoneImInside;
    /// <summary>Cached zone info for the zone this hero is standing in.</summary>
    public ZoneSnapshot zoneImInsideInfo;

    /// <summary>Spawn point position for this hero's team (used for Retreat action).</summary>
    public float3 spawnPosition;
    public bool   spawnPositionCached;
}

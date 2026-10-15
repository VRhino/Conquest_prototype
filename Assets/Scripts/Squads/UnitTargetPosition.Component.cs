using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Desired world position for a squad unit. Other systems should
/// move the unit towards this position.
/// </summary>
public struct UnitTargetPositionComponent : IComponentData
{
    public float3 position;
    public Entity cachedSquadEntity;
    public uint cachedAnchorRevision;
    public FormationType cachedFormation;
    public SquadOrderType cachedOrder;
    public SquadFSMState cachedState;
    public int cachedSlotIndex;
    public int2 cachedGridPosition;
    public float3 cachedWorldOffset;
    public bool hasFormationCache;

    public bool hasTerrainHeight;
    public float3 sampledPosition;
    public float sampledHeight;
    public EntityId terrainInstanceId;
    public EntityId terrainDataId;
    public float3 terrainOrigin;
    public uint terrainRevision;
}

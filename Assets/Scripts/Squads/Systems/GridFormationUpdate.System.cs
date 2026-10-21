using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Sistema que mantiene sincronizados los componentes de grid con las posiciones de formación.
/// Simplificado para trabajar solo con grid-based formations.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(FormationSystem))]
public partial class GridFormationUpdateSystem : SystemBase
{
    private uint _terrainRevision;

    private void OnTerrainHeightChanged(Terrain terrain, RectInt region, bool synched) => _terrainRevision++;

    protected override void OnDestroy() => TerrainCallbacks.heightmapChanged -= OnTerrainHeightChanged;

    protected override void OnCreate()
    {
        base.OnCreate();
        TerrainCallbacks.heightmapChanged += OnTerrainHeightChanged;
        RequireForUpdate<MatchStateComponent>();
    }

    protected override void OnUpdate()
    {
        var terrain = Terrain.activeTerrain;
        var terrainId = terrain != null ? terrain.GetEntityId() : default;
        var terrainDataId = terrain != null && terrain.terrainData != null ? terrain.terrainData.GetEntityId() : default;
        float3 terrainOrigin = terrain != null ? (float3)terrain.transform.position : float3.zero;
        // Actualizar posiciones target cuando las unidades cambien de grid slot
        foreach (var (units, squadEntity) in SystemAPI
                    .Query<DynamicBuffer<SquadUnitElement>>()
                    .WithEntityAccess())
        {
            if (units.Length == 0) continue;
            
            // Get squad data and state to access formation library
            if (!SystemAPI.HasComponent<SquadDefinitionComponent>(squadEntity) ||
                !SystemAPI.HasComponent<SquadStateComponent>(squadEntity))
                continue;
            var squadState = SystemAPI.GetComponent<SquadStateComponent>(squadEntity);
            if (!SystemAPI.HasComponent<FormationComponent>(squadEntity)) continue;
            var formationComp = SystemAPI.GetComponent<FormationComponent>(squadEntity);

            if (!SystemAPI.HasComponent<SquadFormationAnchorComponent>(squadEntity))
                continue;
            var anchorComp = SystemAPI.GetComponent<SquadFormationAnchorComponent>(squadEntity);

            bool needsUpdate = false;
            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (!SystemAPI.HasComponent<UnitGridSlotComponent>(unit)
                    || !SystemAPI.HasComponent<UnitTargetPositionComponent>(unit))
                {
                    needsUpdate = true;
                    continue;
                }

                var slot = SystemAPI.GetComponent<UnitGridSlotComponent>(unit);
                var target = SystemAPI.GetComponent<UnitTargetPositionComponent>(unit);
                if (!target.hasFormationCache
                    || target.cachedSquadEntity != squadEntity
                    || target.cachedAnchorRevision != anchorComp.revision
                    || target.cachedFormation != formationComp.currentFormation
                    || target.cachedOrder != squadState.currentOrder
                    || target.cachedState != squadState.currentState
                    || target.cachedSlotIndex != slot.slotIndex
                    || !math.all(target.cachedLocalOffset == slot.localOffset)
                    || target.hasTerrainHeight != (terrain != null)
                    || target.terrainInstanceId != terrainId
                    || target.terrainDataId != terrainDataId
                    || !math.all(target.terrainOrigin == terrainOrigin)
                    || target.terrainRevision != _terrainRevision)
                {
                    needsUpdate = true;
                    break;
                }
            }
            if (!needsUpdate)
                continue;

            float3 heroPos = anchorComp.position;

            // Obtener el componente de hold position si existe (needed by FormationPositionCalculator)
            SquadHoldPositionComponent? holdComponent = null;
            if (SystemAPI.HasComponent<SquadHoldPositionComponent>(squadEntity))
                holdComponent = SystemAPI.GetComponent<SquadHoldPositionComponent>(squadEntity);

            // Slot offsets come from FormationSystem; here they only follow the anchor (position and rotation).
            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (!SystemAPI.HasComponent<UnitGridSlotComponent>(unit)) continue;

                var gridSlot = SystemAPI.GetComponent<UnitGridSlotComponent>(unit);
                float3 targetPos = FormationPositionCalculator.DesiredPosition(gridSlot.localOffset, squadState,
                    holdComponent, heroPos, anchorComp.rotation, false);

                // Actualizar target position si existe el componente
                if (SystemAPI.HasComponent<UnitTargetPositionComponent>(unit))
                {
                    var targetComp = SystemAPI.GetComponentRW<UnitTargetPositionComponent>(unit);
                    ref var cached = ref targetComp.ValueRW;
                    if (terrain != null)
                    {
                        if (!cached.hasTerrainHeight || cached.terrainInstanceId != terrainId
                            || cached.terrainDataId != terrainDataId || !math.all(cached.terrainOrigin == terrainOrigin)
                            || cached.terrainRevision != _terrainRevision
                            || cached.sampledPosition.x != targetPos.x
                            || cached.sampledPosition.z != targetPos.z)
                        {
                            cached.sampledHeight = terrain.SampleHeight(targetPos) + terrain.transform.position.y;
                            cached.sampledPosition = targetPos;
                            cached.hasTerrainHeight = true;
                            cached.terrainInstanceId = terrainId;
                            cached.terrainDataId = terrainDataId;
                            cached.terrainOrigin = terrainOrigin;
                            cached.terrainRevision = _terrainRevision;
                        }
                        targetPos.y = cached.sampledHeight;
                    }
                    else
                    {
                        cached.hasTerrainHeight = false;
                        cached.terrainInstanceId = terrainId;
                        cached.terrainDataId = terrainDataId;
                        cached.terrainOrigin = terrainOrigin;
                        cached.terrainRevision = _terrainRevision;
                    }
                    if (!math.all(cached.position == targetPos)) cached.position = targetPos;
                    cached.cachedSquadEntity = squadEntity;
                    cached.cachedAnchorRevision = anchorComp.revision;
                    cached.cachedFormation = formationComp.currentFormation;
                    cached.cachedOrder = squadState.currentOrder;
                    cached.cachedState = squadState.currentState;
                    cached.cachedSlotIndex = gridSlot.slotIndex;
                    cached.cachedLocalOffset = gridSlot.localOffset;
                    cached.hasFormationCache = true;
                }
            }
        }
    }
}

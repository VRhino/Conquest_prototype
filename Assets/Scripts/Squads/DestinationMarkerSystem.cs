using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using UnityEngine;

/// <summary>
/// System that creates and manages destination markers for units.
/// Shows a visual marker where each unit is supposed to move to.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitFollowFormationSystem))]
public partial class DestinationMarkerSystem : SystemBase
{
    private EntityCommandBuffer.ParallelWriter _ecb;
    private BeginSimulationEntityCommandBufferSystem _ecbSystem;
    
    protected override void OnCreate()
    {
        base.OnCreate();
        _ecbSystem = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        var ecb = _ecbSystem.CreateCommandBuffer().AsParallelWriter();
        
        // Get marker prefab from singleton
        Entity markerPrefab = Entity.Null;
        foreach (var prefabComp in SystemAPI.Query<RefRO<DestinationMarkerPrefabComponent>>())
        {
            markerPrefab = prefabComp.ValueRO.markerPrefab;
            break;
        }
        
        if (markerPrefab == Entity.Null)
        {
            // No marker prefab configured, skip
            return;
        }
        
        var transformLookup = GetComponentLookup<LocalTransform>(true);
        var ownerLookup = GetComponentLookup<SquadOwnerComponent>(true);
        var stateLookup = GetComponentLookup<SquadStateComponent>(true);
        var squadDataLookup = GetComponentLookup<SquadDataComponent>(true);
        var slotLookup = GetComponentLookup<UnitGridSlotComponent>(true);
        
        // Process each squad to update destination markers for their units
        foreach (var (units, squadEntity) in SystemAPI.Query<DynamicBuffer<SquadUnitElement>>().WithEntityAccess())
        {
            if (units.Length == 0)
                continue;
                
            if (!ownerLookup.TryGetComponent(squadEntity, out var squadOwner))
                continue;
                
            if (!stateLookup.TryGetComponent(squadEntity, out var squadState))
                continue;
                
            if (!squadDataLookup.TryGetComponent(squadEntity, out var squadData))
                continue;
                
            if (!transformLookup.TryGetComponent(squadOwner.hero, out var heroTransform))
                continue;
                
            float3 heroPosition = heroTransform.Position;
            
            // Get current formation gridPositions from squad data
            ref BlobArray<int2> gridPositions = ref squadData.formationLibrary.Value.formations[0].gridPositions;
            if (squadData.formationLibrary.IsCreated)
            {
                ref var formations = ref squadData.formationLibrary.Value.formations;
                FormationType currentFormation = squadState.currentFormation;
                
                // Find the current formation in the library
                for (int f = 0; f < formations.Length; f++)
                {
                    if (formations[f].formationType == currentFormation)
                    {
                        gridPositions = ref formations[f].gridPositions;
                        break;
                    }
                }
            }
            
            // Determine squad center based on current state
            float3 squadCenter = heroPosition;
            bool isHoldingPosition = squadState.currentState == SquadFSMState.HoldingPosition;
            
            if (isHoldingPosition && SystemAPI.HasComponent<SquadHoldPositionComponent>(squadEntity))
            {
                var holdComponent = SystemAPI.GetComponent<SquadHoldPositionComponent>(squadEntity);
                squadCenter = holdComponent.holdCenter;
            }
            
            // Process each unit in the squad
            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (!transformLookup.TryGetComponent(unit, out var unitTransform))
                    continue;
                    
                if (!slotLookup.TryGetComponent(unit, out var gridSlot))
                    continue;
                    
                // Calculate desired position for this unit
                float3 desiredPosition = float3.zero;
                
                if (gridPositions.Length > 0 && i < gridPositions.Length)
                {
                    FormationPositionCalculator.CalculateDesiredPosition(
                        unit,
                        ref gridPositions,
                        squadCenter,
                        i,
                        out int2 originalGridPos,
                        out float3 gridOffset,
                        out float3 worldPos,
                        true);
                    desiredPosition = worldPos;
                }
                else
                {
                    desiredPosition = squadCenter + gridSlot.worldOffset;
                }
                
                // Check if unit should have a marker (SOLO en Hold Position)
                bool shouldShowMarker = false;
                if (isHoldingPosition && SystemAPI.HasComponent<UnitFormationStateComponent>(unit))
                {
                    var unitState = SystemAPI.GetComponent<UnitFormationStateComponent>(unit);
                    shouldShowMarker = unitState.State == UnitFormationState.Moving;
                }
                
                // Manage marker for this unit
                if (SystemAPI.HasComponent<UnitDestinationMarkerComponent>(unit))
                {
                    var marker = SystemAPI.GetComponentRW<UnitDestinationMarkerComponent>(unit);
                    
                    if (shouldShowMarker)
                    {
                        // Update marker position
                        marker.ValueRW.targetPosition = desiredPosition;
                        marker.ValueRW.isActive = true;
                        
                        // Update marker entity position if it exists
                        if (marker.ValueRO.markerEntity != Entity.Null && 
                            transformLookup.TryGetComponent(marker.ValueRO.markerEntity, out var markerTransform))
                        {
                            var newTransform = markerTransform;
                            newTransform.Position = desiredPosition;
                            ecb.SetComponent(0, marker.ValueRO.markerEntity, newTransform);
                        }
                        else if (marker.ValueRO.markerEntity == Entity.Null)
                        {
                            // El marcador fue destruido pero el componente sigue existiendo
                            // Crear un nuevo marcador
                            Entity newMarker = ecb.Instantiate(0, markerPrefab);
                            ecb.SetComponent(0, newMarker, new LocalTransform
                            {
                                Position = desiredPosition,
                                Rotation = quaternion.identity,
                                Scale = 1f
                            });
                            
                            marker.ValueRW.markerEntity = newMarker;
                            
                            // Debug temporal
                            // Marker recreated for unit
                        }
                    }
                    else
                    {
                        // Limpiar marcadores cuando no se deben mostrar
                        // (ya sea porque no está en Hold Position o porque no está Moving)
                        marker.ValueRW.isActive = false;
                        if (marker.ValueRO.markerEntity != Entity.Null)
                        {
                            ecb.DestroyEntity(0, marker.ValueRO.markerEntity);
                            marker.ValueRW.markerEntity = Entity.Null;
                        }
                        // IMPORTANTE: Remover el componente para que pueda crearse de nuevo más tarde
                        ecb.RemoveComponent<UnitDestinationMarkerComponent>(0, unit);
                        
                        // Debug temporal
                        // Marker component removed from unit
                    }
                }
                else if (shouldShowMarker)
                {
                    // Create new marker for this unit
                    Entity newMarker = ecb.Instantiate(0, markerPrefab);
                    ecb.SetComponent(0, newMarker, new LocalTransform
                    {
                        Position = desiredPosition,
                        Rotation = quaternion.identity,
                        Scale = 1f
                    });
                    
                    ecb.AddComponent(0, unit, new UnitDestinationMarkerComponent
                    {
                        markerEntity = newMarker,
                        targetPosition = desiredPosition,
                        isActive = true,
                        ownerUnit = unit
                    });
                    
                    // Debug temporal
                    // New marker created for unit
                }
            }
            
            // Limpiar markers de unidades cuando NO estamos en Hold Position
            if (!isHoldingPosition)
            {
                for (int i = 0; i < units.Length; i++)
                {
                    Entity unit = units[i].Value;
                    if (SystemAPI.HasComponent<UnitDestinationMarkerComponent>(unit))
                    {
                        var marker = SystemAPI.GetComponentRW<UnitDestinationMarkerComponent>(unit);
                        if (marker.ValueRO.markerEntity != Entity.Null)
                        {
                            ecb.DestroyEntity(0, marker.ValueRO.markerEntity);
                        }
                        ecb.RemoveComponent<UnitDestinationMarkerComponent>(0, unit);
                    }
                }
            }
        }
        
        _ecbSystem.AddJobHandleForProducer(Dependency);
    }
}

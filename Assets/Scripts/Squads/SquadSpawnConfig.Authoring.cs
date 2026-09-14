using Unity.Entities;
using UnityEngine;

public class SquadSpawnConfigAuthoring : MonoBehaviour
{
    public float ownerDeathRetreatDuration = 5f;
    public float retreatArrivalThreshold = 0.5f;
    public float slotArrivalThreshold = 0.2f;
    public float holdReformThreshold = 1f;
    public float anchorMovingSpeedThreshold = 0.1f;
    public float bodyblockRadius = 0.8f;
    public float bodyblockRepulsionStrength = 8f;
    public float bodyblockWallStrength = 60f;
    public float bodyblockMaxPushSpeed = 18f;
    public float bodyblockEngagingRadius = 0.35f;
    public float bodyblockEngagingStrength = 3f;
    public float navMeshDestinationSampleRadius = 2f;
    public float navMeshFailureRetryDistance = 1f;
    public float heroAIArrivalDistance = 1.5f;
    public float squadSpawnOffset = 5f;
    public float unitMinDistance   = 1.5f;
    public float unitRepelForce    = 1f;
    public float unitRotationSpeed = 5f;
    public float heroSlotSpacing        = 10f;
    public float followForwardOffset    = 2f;
    public float unitLeashDistance      = 6f;
    public int   maxUnitsPerTarget      = 2;
    public float unitMoveDelayMin       = 0.5f;
    public float unitMoveDelayMax       = 1.0f;
    public float unitFollowDelayMin     = 0.5f;
    public float unitFollowDelayMax     = 1.5f;
    public float minCombatDuration      = 1f;

    class Baker : Baker<SquadSpawnConfigAuthoring>
    {
        public override void Bake(SquadSpawnConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new SquadSpawnConfigComponent
            {
                squadSpawnOffset    = authoring.squadSpawnOffset,
                unitMinDistance     = authoring.unitMinDistance,
                unitRepelForce      = authoring.unitRepelForce,
                unitRotationSpeed   = authoring.unitRotationSpeed,
                heroSlotSpacing     = authoring.heroSlotSpacing,
                followForwardOffset = authoring.followForwardOffset,
                unitLeashDistance   = authoring.unitLeashDistance,
                maxUnitsPerTarget   = authoring.maxUnitsPerTarget,
                unitMoveDelayMin    = authoring.unitMoveDelayMin,
                unitMoveDelayMax    = authoring.unitMoveDelayMax,
                unitFollowDelayMin  = authoring.unitFollowDelayMin,
                unitFollowDelayMax  = authoring.unitFollowDelayMax,
                minCombatDuration   = authoring.minCombatDuration,
                ownerDeathRetreatDuration = authoring.ownerDeathRetreatDuration,
                retreatArrivalThreshold = authoring.retreatArrivalThreshold,
                slotArrivalThreshold = authoring.slotArrivalThreshold,
                holdReformThreshold = authoring.holdReformThreshold,
                anchorMovingSpeedThreshold = authoring.anchorMovingSpeedThreshold,
                bodyblockRadius = authoring.bodyblockRadius,
                bodyblockRepulsionStrength = authoring.bodyblockRepulsionStrength,
                bodyblockWallStrength = authoring.bodyblockWallStrength,
                bodyblockMaxPushSpeed = authoring.bodyblockMaxPushSpeed,
                bodyblockEngagingRadius = authoring.bodyblockEngagingRadius,
                bodyblockEngagingStrength = authoring.bodyblockEngagingStrength,
                navMeshDestinationSampleRadius = authoring.navMeshDestinationSampleRadius,
                navMeshFailureRetryDistance = authoring.navMeshFailureRetryDistance,
                heroAIArrivalDistance = authoring.heroAIArrivalDistance
            });
        }
    }
}

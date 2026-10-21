using Unity.Entities;
using UnityEngine;

public class SquadSpawnConfigAuthoring : MonoBehaviour
{
    [Tooltip("Experimental environment sensing; enable when a navigation consumer is installed.")]
    public bool enableEnvironmentAdaptation;
    [Tooltip("Movement tuning baked as MovementTuningComponent. Missing asset: built-in defaults and a bake warning.")]
    public MovementTuningConfig movementTuning;
    public float ownerDeathRetreatDuration = 5f;
    public float retreatArrivalThreshold = 0.5f;
    public float anchorMovingSpeedThreshold = 0.1f;
    [Min(0f)] public float animationMovingSpeedThreshold = 0.05f;
    [Range(0f, 1f)] public float animationRunningSpeedRatio = 0.6f;
    public float bodyblockRadius = 0.8f;
    [Min(0f)] public float bodyblockOverlapEpsilonSq = 1e-6f;
    [Min(1)] public int bodyblockJobBatchSize = 32;
    [Min(1)] public int bodyblockParallelThreshold = 128;
    public float bodyblockRepulsionStrength = 8f;
    public float bodyblockWallStrength = 60f;
    public float bodyblockMaxPushSpeed = 18f;
    [Tooltip("Enemy contacts resolve like colliding capsules: each unit moves just enough to undo the overlap (half each when both move, all of it against a still unit or a wall), with no spring. Off: the old spring push scaled by the strengths below.")]
    public bool bodyblockResolveOverlapOnly = true;
    public float bodyblockEngagingRadius = 0.35f;
    public float bodyblockEngagingStrength = 3f;
    public float navMeshDestinationSampleRadius = 2f;
    public float navMeshDestinationChangeThreshold = 0.25f;
    public float navMeshFailureRetryDistance = 1f;
    public float navMeshFailureRetryInterval = 1f;
    public float heroAIArrivalDistance = 1.5f;
    [Tooltip("Seconds of simulation time between two thinks of one AI hero (L4-01). Each hero has its own phase.")]
    [Min(0.01f)] public float heroAIThinkInterval = 0.1f;
    [Tooltip("At most this many AI heroes think in one frame; the most overdue go first.")]
    [Min(1)] public int heroAIMaxThinksPerFrame = 8;
    [Tooltip("An AI hero counts as under attack only after an enemy hero is near (or its squad fights) for this long (L4-02).")]
    [Min(0f)] public float heroAIUnderAttackOpenTime = 0.3f;
    [Tooltip("...and stops counting as under attack only after that has been false for this long.")]
    [Min(0f)] public float heroAIUnderAttackCloseTime = 1f;
    public float squadSpawnOffset = 5f;
    public float unitRotationSpeed = 5f;
    public float heroSlotSpacing        = 10f;
    public float followForwardOffset    = 2f;
    public float unitLeashDistance      = 6f;
    public int   maxUnitsPerTarget      = 2;
    public float minCombatDuration      = 1f;

    class Baker : Baker<SquadSpawnConfigAuthoring>
    {
        public override void Bake(SquadSpawnConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            DependsOn(authoring.movementTuning);
            if (authoring.movementTuning == null)
                Debug.LogWarning($"[SquadSpawnConfigAuthoring] {authoring.name}: no MovementTuningConfig assigned; baking defaults.", authoring);
            AddComponent(entity, authoring.movementTuning != null
                ? authoring.movementTuning.ToComponent()
                : MovementTuningComponent.Default);
            AddComponent(entity, new SquadSpawnConfigComponent
            {
                enableEnvironmentAdaptation = authoring.enableEnvironmentAdaptation,
                squadSpawnOffset    = authoring.squadSpawnOffset,
                unitRotationSpeed   = authoring.unitRotationSpeed,
                heroSlotSpacing     = authoring.heroSlotSpacing,
                followForwardOffset = authoring.followForwardOffset,
                unitLeashDistance   = authoring.unitLeashDistance,
                maxUnitsPerTarget   = authoring.maxUnitsPerTarget,
                minCombatDuration   = authoring.minCombatDuration,
                ownerDeathRetreatDuration = authoring.ownerDeathRetreatDuration,
                retreatArrivalThreshold = authoring.retreatArrivalThreshold,
                anchorMovingSpeedThreshold = authoring.anchorMovingSpeedThreshold,
                animationMovingSpeedThreshold = authoring.animationMovingSpeedThreshold,
                animationRunningSpeedRatio = authoring.animationRunningSpeedRatio,
                bodyblockRadius = authoring.bodyblockRadius,
                bodyblockOverlapEpsilonSq = authoring.bodyblockOverlapEpsilonSq,
                bodyblockJobBatchSize = authoring.bodyblockJobBatchSize,
                bodyblockParallelThreshold = authoring.bodyblockParallelThreshold,
                bodyblockRepulsionStrength = authoring.bodyblockRepulsionStrength,
                bodyblockWallStrength = authoring.bodyblockWallStrength,
                bodyblockMaxPushSpeed = authoring.bodyblockMaxPushSpeed,
                bodyblockResolveOverlapOnly = authoring.bodyblockResolveOverlapOnly,
                bodyblockEngagingRadius = authoring.bodyblockEngagingRadius,
                bodyblockEngagingStrength = authoring.bodyblockEngagingStrength,
                navMeshDestinationSampleRadius = authoring.navMeshDestinationSampleRadius,
                navMeshDestinationChangeThreshold = authoring.navMeshDestinationChangeThreshold,
                navMeshFailureRetryDistance = authoring.navMeshFailureRetryDistance,
                navMeshFailureRetryInterval = authoring.navMeshFailureRetryInterval,
                heroAIArrivalDistance = authoring.heroAIArrivalDistance,
                heroAIThinkInterval = authoring.heroAIThinkInterval,
                heroAIMaxThinksPerFrame = authoring.heroAIMaxThinksPerFrame,
                heroAIUnderAttackOpenTime = authoring.heroAIUnderAttackOpenTime,
                heroAIUnderAttackCloseTime = authoring.heroAIUnderAttackCloseTime
            });
        }
    }
}

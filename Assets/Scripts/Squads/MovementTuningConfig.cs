using UnityEngine;

/// <summary>
/// Designer-facing movement tuning. Baked into <see cref="MovementTuningComponent"/> by
/// <see cref="SquadSpawnConfigAuthoring"/>. Defaults match MovementTuningComponent.Default.
/// </summary>
[CreateAssetMenu(fileName = "MovementTuning", menuName = "Conquest/Movement Tuning")]
public class MovementTuningConfig : ScriptableObject
{
    [Header("Arrival")]
    [Tooltip("A unit becomes Formed below this distance to its slot (m).")]
    [Min(0.01f)] public float arrivalRadiusIn = 0.2f;
    [Tooltip("A Formed unit leaves its slot above this distance (m). Must stay above arrivalRadiusIn.")]
    [Min(0.01f)] public float arrivalRadiusOut = 0.5f;
    [Tooltip("Seconds the anchor must be still before a unit counts as Formed.")]
    [Min(0f)] public float arrivalCalmTime = 0.15f;
    [Tooltip("Hold Position: distance to the held slot before a unit reforms (m).")]
    [Min(0.01f)] public float holdReformRadius = 1f;

    [Header("Start delays")]
    [Tooltip("Extra delay per row behind the front row (s). The Dispersed formation will use a larger jitter.")]
    [Min(0f)] public float startRowDelay = 0.08f;
    [Tooltip("Deterministic per-unit jitter added to the row delay (s).")]
    [Min(0f)] public float startJitterDelay = 0.1f;

    [Header("Formation destination")]
    [Min(0.01f)] public float destinationChangeMin = 0.25f;
    [Tooltip("Seconds of slot travel before the formation command is re-sent; keep below destinationLeadTime.")]
    [Min(0f)] public float destinationChangeSpeedFactor = 0.25f;
    [Min(0f)] public float destinationLeadTime = 0.4f;
    [Min(0f)] public float destinationLeadMaxDistance = 1f;
    [Min(0.01f)] public float slotVelocitySmoothing = 0.15f;

    [Header("Movement constants (moved from code, unchanged values)")]
    [Min(0f)] public float combatFacingRange = 3.5f;
    [Min(0f)] public float formedRotationSpeed = 5f;
    [Min(1f), UnityEngine.Serialization.FormerlySerializedAs("hurrySpeedMultiplier")] public float runSpeedMultiplier = 2f;
    [Min(0f)] public float formationChangeCooldown = 1f;

    [Header("Arrival braking (M12/M13)")]
    [Tooltip("Units heading for a still destination (stopped slot, melee stop point) cap their speed at sqrt(2 a d), with a = this fraction of acceleration x brakingFactor, so they arrive slow instead of stopping in one frame. 0 = off.")]
    [Min(0f)] public float arrivalDecelerationFraction = 0.6f;

    [Header("Acceleration limits (paso 5)")]
    [Tooltip("Units may slow down this many times faster than they speed up (a person stops faster than they start).")]
    [Min(1f)] public float brakingFactor = 2.5f;

    [Header("Ally avoidance (C14, ORCA)")]
    public bool avoidanceEnabled = true;
    [Tooltip("Allies closer than this are avoided (m).")]
    [Min(0.5f)] public float avoidanceNeighborRadius = 4f;
    [Tooltip("Nearest allies considered per unit.")]
    [Min(0f)] public float avoidanceMaxNeighbors = 8f;
    [Tooltip("Collisions further away in time are ignored (s).")]
    [Min(0.1f)] public float avoidanceTimeHorizon = 2f;
    [Tooltip("Time horizon within one spacing of the slot (s).")]
    [Min(0.1f)] public float avoidanceTimeHorizonNear = 1f;
    [Tooltip("Time to separate overlapping allies (s).")]
    [Min(0.02f)] public float avoidanceCollisionResolveTime = 0.25f;

    [Header("Slot following (C9)")]
    [Tooltip("Switch to Run beyond this distance to the slot (m).")]
    [Min(0f)] public float runEnterDistance = 0.9f;
    [Tooltip("Back to Walk below this distance to the slot (m).")]
    [Min(0f)] public float runExitDistance = 0.45f;
    [Tooltip("Seconds to close the gap to the slot.")]
    [Min(0.05f)] public float followRecoveryTime = 0.5f;
    [Tooltip("Minimum following speed as a fraction of the base speed.")]
    [Min(0f)] public float followMinSpeedRatio = 0.3f;
    [Tooltip("Body radius used for contact distances and ally avoidance (m).")]
    [Min(0.1f)] public float bodyRadius = 0.5f;
    [Tooltip("Melee units stop at bodies + this fraction of the weapon reach beyond their own body.")]
    [Min(0f)] public float meleeStopReachFraction = 0.6f;
    [Tooltip("Ranged units stop at this fraction of their range and approach again beyond it.")]
    [Min(0f)] public float rangedStopFraction = 0.92f;

    [Header("Combat contact")]
    [Tooltip("Maximum turn speed (deg/s) when a unit turns to face its combat target.")]
    [Min(0f)] public float combatTurnRate = 450f;

    public MovementTuningComponent ToComponent() => new MovementTuningComponent
    {
        arrivalRadiusIn = arrivalRadiusIn,
        arrivalRadiusOut = arrivalRadiusOut,
        arrivalCalmTime = arrivalCalmTime,
        holdReformRadius = holdReformRadius,
        startRowDelay = startRowDelay,
        startJitterDelay = startJitterDelay,
        destinationChangeMin = destinationChangeMin,
        destinationChangeSpeedFactor = destinationChangeSpeedFactor,
        destinationLeadTime = destinationLeadTime,
        destinationLeadMaxDistance = destinationLeadMaxDistance,
        slotVelocitySmoothing = slotVelocitySmoothing,
        combatFacingRange = combatFacingRange,
        formedRotationSpeed = formedRotationSpeed,
        runSpeedMultiplier = runSpeedMultiplier,
        formationChangeCooldown = formationChangeCooldown,
        arrivalDecelerationFraction = arrivalDecelerationFraction,
        brakingFactor = brakingFactor,
        avoidanceEnabled = avoidanceEnabled,
        avoidanceNeighborRadius = avoidanceNeighborRadius,
        avoidanceMaxNeighbors = avoidanceMaxNeighbors,
        avoidanceTimeHorizon = avoidanceTimeHorizon,
        avoidanceTimeHorizonNear = avoidanceTimeHorizonNear,
        avoidanceCollisionResolveTime = avoidanceCollisionResolveTime,
        runEnterDistance = runEnterDistance,
        runExitDistance = runExitDistance,
        followRecoveryTime = followRecoveryTime,
        followMinSpeedRatio = followMinSpeedRatio,
        bodyRadius = bodyRadius,
        meleeStopReachFraction = meleeStopReachFraction,
        rangedStopFraction = rangedStopFraction,
        combatTurnRate = combatTurnRate,
    };

    void OnValidate()
    {
        var issue = MovementTuningComponent.Validate(ToComponent(), MovementTuningComponent.ReferenceSpacing);
        if (issue != null) Debug.LogWarning($"[MovementTuningConfig] {name}: {issue}", this);
    }
}

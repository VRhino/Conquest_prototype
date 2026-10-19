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
    [Min(0f)] public float destinationChangeSpeedFactor = 0.05f;
    [Min(0f)] public float destinationLeadTime = 0.4f;
    [Min(0f)] public float destinationLeadMaxDistance = 1f;
    [Min(0.01f)] public float slotVelocitySmoothing = 0.15f;

    [Header("Movement constants (moved from code, unchanged values)")]
    [Min(0f)] public float meleeStopDistanceFactor = 0.75f;
    [Min(0f)] public float combatFacingRange = 3.5f;
    [Min(0f)] public float formedRotationSpeed = 5f;
    [Min(1f)] public float hurrySpeedMultiplier = 2f;
    [Min(0f)] public float formationChangeCooldown = 1f;

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
        meleeStopDistanceFactor = meleeStopDistanceFactor,
        combatFacingRange = combatFacingRange,
        formedRotationSpeed = formedRotationSpeed,
        hurrySpeedMultiplier = hurrySpeedMultiplier,
        formationChangeCooldown = formationChangeCooldown,
    };

    void OnValidate()
    {
        var issue = MovementTuningComponent.Validate(ToComponent(), FormationGridSystem.CELL_WIDTH);
        if (issue != null) Debug.LogWarning($"[MovementTuningConfig] {name}: {issue}", this);
    }
}

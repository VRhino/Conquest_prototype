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

    [Header("Hit knock-back")]
    [Tooltip("A hit that reaches a unit's body pushes it this far away from the attacker (the reference's hit_curve pattern). 0 = no knock-back.")]
    [Min(0f)] public float hitKnockbackDistance = 0.3f;
    [Tooltip("Seconds the push takes. It starts and ends at rest; peak acceleration is 6 x distance / duration^2 (0.3 m in 0.35 s gives about 15 m/s2).")]
    [Min(0f)] public float hitKnockbackDuration = 0.35f;

    [Header("Walk catch-up (M1)")]
    [Tooltip("Behind a moving slot in Walk, a unit may go up to this multiple of its Walk speed to recover the start-up delay without switching to Run. 1 = never above the Walk speed (decision D4 before M1).")]
    [Min(1f)] public float walkCatchUpRatio = 1.05f;

    [Header("Anchor arrival (C10)")]
    [Tooltip("The squad anchor follows its target point (hero + offset, hold point, retreat point) with its own velocity: it speeds up at most at this rate and brakes in advance so it stops on the target. While marching it trails by v^2/(2a). 0 = the anchor copies the hero exactly (behaviour before C10).")]
    [Min(0f)] public float anchorAcceleration = 8f;
    [Tooltip("Beyond this distance to its target the anchor jumps instead of travelling (spawn, respawn, teleport).")]
    [Min(0f)] public float anchorSnapDistance = 10f;
    [Tooltip("An attacker only reserves a ring slot within this angle of the side it comes from; slots round the back of the target lie among its own comrades. Without a slot it waits outside.")]
    [Min(0f)] public float contactRingMaxArcDegrees = 60f;

    [Header("Contact ring (C11)")]
    [Tooltip("Melee attackers reserve a slot on their target's ring. Off: the old fan of ±45° around the line to the target.")]
    public bool contactRingEnabled = true;
    [Tooltip("Gap between neighbouring attackers on a target's contact ring. Slots = floor(2 pi d_stop / (2 bodyRadius + margin)); attackers without a slot wait at d_resume + (2 bodyRadius + margin).")]
    [Min(0f)] public float contactRingMargin = 0.2f;

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
    [Min(0.1f)] public float avoidanceTimeHorizon = 1f;
    [Tooltip("Time horizon within one spacing of the slot (s).")]
    [Min(0.1f)] public float avoidanceTimeHorizonNear = 1f;
    [Tooltip("Time to separate overlapping allies (s).")]
    [Min(0.02f)] public float avoidanceCollisionResolveTime = 0.25f;

    [Header("Enemy contact (M2)")]
    [Tooltip("Enemy bodies touch at 2 x bodyRadius whether or not they fight (the bodyblock undoes overlaps below it), and a unit that moves slides along the enemies in front of it instead of walking into them: the part of its velocity that would close the gap faster than the gap allows is removed, the sideways part kept. A still unit is never pushed. Runs in UnitAvoidanceSystem, so it is off with avoidanceEnabled. Off = the bodyblock radii of SquadSpawnConfig, as before.")]
    public bool enemyContactEnabled = true;
    [Tooltip("A moving unit closes its gap to an enemy in no less than this time, so it slows down just before contact instead of stopping in one frame (s).")]
    [Min(0f)] public float enemyContactLookaheadTime = 0.05f;

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

    [Header("Formation slots on the NavMesh (C13)")]
    [Tooltip("Each squad checks its slots against the NavMesh and moves blocked or taken ones to the nearest free cell. Off = each unit projects its own slot, as before C13.")]
    public bool slotResolveEnabled = true;
    [Tooltip("A squad checks its slots again once a template slot has moved this far since the last check (m).")]
    [Min(0.01f)] public float slotResolveRevalidateDistance = 0.5f;
    [Tooltip("Furthest a blocked slot looks for a free cell, in cells (four-neighbour distance).")]
    [Min(1)] public int slotResolveSearchCells = 6;
    [Tooltip("Largest radius used to put a slot on the NavMesh, capped at the formation spacing (m, L3-04).")]
    [Min(0.01f)] public float slotSnapRadiusMax = 0.65f;

    [Header("Combat contact")]
    [Tooltip("Maximum turn speed (deg/s) when a unit turns to face its combat target.")]
    [Min(0f)] public float combatTurnRate = 450f;

    [Header("Melee target score (L4-04, D6)")]
    [Tooltip("Proximity term of the score: 1 at contact, 0.5 at this distance (m).")]
    [Min(0.01f)] public float targetScoreDistanceScale = 4f;
    [Tooltip("Added to the score of an enemy in the squad's focus: the target of its Attack order, or the enemy squad most of its units fight.")]
    public float targetScoreFocusBonus = 0.1f;
    [Tooltip("Added to the score of an enemy hero. Negative keeps soldiers on soldiers.")]
    public float targetScoreHeroBias = 0f;
    [Tooltip("Soft saturation (D6): the score is multiplied by max(floor, 1 - n·k), n = melee attackers already on that enemy from every squad.")]
    [Min(0f)] public float targetSaturationPerAttacker = 0.35f;
    [Tooltip("A crowded enemy keeps at least this share of its score, so it stays eligible (no hard cap).")]
    [Range(0.01f, 1f)] public float targetSaturationFloor = 0.1f;
    [Tooltip("Hysteresis: added to the score of the current target, so a unit only switches to a clearly better one.")]
    [Min(0f)] public float targetKeepBonus = 0.1f;
    [Tooltip("Nearest candidates scored per unit.")]
    [Min(1)] public int targetCandidateCount = 6;
    [Tooltip("A melee unit not yet within reach looks at its target again every this many seconds, each with its own phase. 0 = every frame.")]
    [Min(0f)] public float targetReevaluateInterval = 0.3f;
    [Tooltip("A melee unit blocked this long by an enemy that is not its target scores its targets again with that enemy in (P4). 0 = off.")]
    [Min(0f)] public float blockedRetargetTime = 0.5f;
    [Tooltip("Added to the blocking enemy's score before the soft saturation (P4): it wins unless already crowded, so lines form instead of piles.")]
    [Min(0f)] public float targetBlockerBonus = 0.5f;

    [Header("Squad enemy search (L4-09)")]
    [Tooltip("New targets only within the squad's combat radius and on its level. Off = every detected enemy, as before L4-09.")]
    public bool enemySearchEnabled = true;
    [Tooltip("Combat radius beyond the squad's extent while still or holding a position (m). Matches the leash (unitLeashDistance), so a still squad takes on the enemies it may chase.")]
    [Min(0f)] public float enemySearchRadiusMin = 6f;
    [Tooltip("Combat radius beyond the squad's extent under an Attack order, or following at full speed (m).")]
    [Min(0f)] public float enemySearchRadiusMax = 8f;
    [Tooltip("Speed at which a squad following its hero reaches the largest combat radius (m/s).")]
    [Min(0f)] public float enemySearchFullRadiusSpeed = 4f;
    [Tooltip("Enemies further above or below the squad are on another level (m).")]
    [Min(0f)] public float enemySearchMaxHeightDifference = 3f;
    [Tooltip("Height differences up to this ignore the slope limit (m).")]
    [Min(0f)] public float enemySearchStepHeight = 0.5f;
    [Tooltip("Steepest slope from the squad to a new enemy (degrees): an enemy on a wall beside the squad is left out.")]
    [Range(0f, 89f)] public float enemySearchMaxSlopeDegrees = 45f;

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
        hitKnockbackDistance = hitKnockbackDistance,
        hitKnockbackDuration = hitKnockbackDuration,
        walkCatchUpRatio = walkCatchUpRatio,
        anchorAcceleration = anchorAcceleration,
        anchorSnapDistance = anchorSnapDistance,
        contactRingMaxArcDegrees = contactRingMaxArcDegrees,
        contactRingEnabled = contactRingEnabled,
        contactRingMargin = contactRingMargin,
        arrivalDecelerationFraction = arrivalDecelerationFraction,
        brakingFactor = brakingFactor,
        avoidanceEnabled = avoidanceEnabled,
        avoidanceNeighborRadius = avoidanceNeighborRadius,
        avoidanceMaxNeighbors = avoidanceMaxNeighbors,
        avoidanceTimeHorizon = avoidanceTimeHorizon,
        avoidanceTimeHorizonNear = avoidanceTimeHorizonNear,
        avoidanceCollisionResolveTime = avoidanceCollisionResolveTime,
        enemyContactEnabled = enemyContactEnabled,
        enemyContactLookaheadTime = enemyContactLookaheadTime,
        runEnterDistance = runEnterDistance,
        runExitDistance = runExitDistance,
        followRecoveryTime = followRecoveryTime,
        followMinSpeedRatio = followMinSpeedRatio,
        bodyRadius = bodyRadius,
        meleeStopReachFraction = meleeStopReachFraction,
        rangedStopFraction = rangedStopFraction,
        combatTurnRate = combatTurnRate,
        slotResolveEnabled = slotResolveEnabled,
        slotResolveRevalidateDistance = slotResolveRevalidateDistance,
        slotResolveSearchCells = slotResolveSearchCells,
        slotSnapRadiusMax = slotSnapRadiusMax,
        targetScoreDistanceScale = targetScoreDistanceScale,
        targetScoreFocusBonus = targetScoreFocusBonus,
        targetScoreHeroBias = targetScoreHeroBias,
        targetSaturationPerAttacker = targetSaturationPerAttacker,
        targetSaturationFloor = targetSaturationFloor,
        targetKeepBonus = targetKeepBonus,
        targetCandidateCount = targetCandidateCount,
        targetReevaluateInterval = targetReevaluateInterval,
        blockedRetargetTime = blockedRetargetTime,
        targetBlockerBonus = targetBlockerBonus,
        enemySearchEnabled = enemySearchEnabled,
        enemySearchRadiusMin = enemySearchRadiusMin,
        enemySearchRadiusMax = enemySearchRadiusMax,
        enemySearchFullRadiusSpeed = enemySearchFullRadiusSpeed,
        enemySearchMaxHeightDifference = enemySearchMaxHeightDifference,
        enemySearchStepHeight = enemySearchStepHeight,
        enemySearchMaxSlopeDegrees = enemySearchMaxSlopeDegrees,
    };

    void OnValidate()
    {
        var issue = MovementTuningComponent.Validate(ToComponent(), MovementTuningComponent.ReferenceSpacing);
        if (issue != null) Debug.LogWarning($"[MovementTuningConfig] {name}: {issue}", this);
    }
}

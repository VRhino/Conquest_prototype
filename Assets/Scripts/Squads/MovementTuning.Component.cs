using Unity.Entities;

/// <summary>
/// Singleton with the unit movement tuning baked from <see cref="MovementTuningConfig"/>.
/// One source of truth for arrival radii, start delays, destination updates and the movement constants
/// that used to live in system code (Docs/Design/Movimiento_Formaciones.md §3).
/// </summary>
public struct MovementTuningComponent : IComponentData
{
    // Arrival (UnitFormationStateSystem, UnitNavMeshSystem)
    public float arrivalRadiusIn;
    public float arrivalRadiusOut;
    public float arrivalCalmTime;
    public float holdReformRadius;

    // Start delays: front row first, then startRowDelay per row plus a deterministic jitter.
    public float startRowDelay;
    public float startJitterDelay;

    // Formation destination updates (UnitNavMeshSystem)
    public float destinationChangeMin;
    public float destinationChangeSpeedFactor;
    public float destinationLeadTime;
    public float destinationLeadMaxDistance;
    public float slotVelocitySmoothing;

    // Constants moved out of system code with their current values
    public float combatFacingRange;
    public float formedRotationSpeed;
    public float runSpeedMultiplier;
    public float formationChangeCooldown;
    public float formationCompactDelay; // Seconds after a casualty before the gaps of the formation are closed (L3-02)
    public float staminaRunSpeedFactor; // A soldier spends squad stamina only above this × its walking speed (L5-7)
    public float hitKnockbackDistance; // How far a body hit pushes a unit away from the attacker, m; 0 = off
    public float hitKnockbackDuration; // Seconds the knock-back takes (smoothstep: peak acceleration 6 D / T^2)
    public float walkCatchUpRatio; // Walk cap while recovering behind a moving slot, as a multiple of the Walk speed (M1)
    public float anchorAcceleration; // Anchor arrival acceleration, m/s2; 0 = anchor copies the hero (C10)
    public float anchorSnapDistance; // Beyond this distance to its target the anchor jumps (spawn, respawn), m
    public float contactRingMaxArcDegrees; // Attackers only reserve ring slots within this angle of the side they come from, degrees (C11)
    public bool contactRingEnabled; // Melee attackers reserve ring slots (C11); off = the old ±45° fan.
    public float advanceSpeedRatio; // Testudo Advance: walking speed as a fraction of the soldiers' base speed
    public float advanceContactDistance; // Testudo Advance stops when a soldier is this close to an enemy, m
    public float advanceObstacleLookahead; // Testudo Advance stops when the NavMesh ends this far ahead of the front rank, m
    public float contactRingMargin; // Gap between neighbouring attackers on a contact ring, m (C11: s_arco = 2 rc + m)
    public float arrivalDecelerationFraction; // Planned arrival braking as a fraction of acceleration x brakingFactor
    public float brakingFactor; // Deceleration allowed as a multiple of the agent acceleration.
    public bool avoidanceEnabled; // Allies dodge each other by velocity (C14).
    public float avoidanceNeighborRadius; // Allies closer than this are avoided (m, C14).
    public float avoidanceMaxNeighbors; // Nearest allies considered per unit.
    public float avoidanceTimeHorizon; // Collisions further away in time are ignored (s); 1 s, the reference after its first state change (M8).
    public float avoidanceTimeHorizonNear; // Time horizon within one spacing of the slot (s).
    public float avoidanceCollisionResolveTime; // Time to separate overlapping allies (s).
    public bool enemyContactEnabled; // Enemies touch at 2·bodyRadius and movers slide along them (M2, P0/P1).
    public float enemyContactLookaheadTime; // A mover closes its gap to an enemy in no less than this (s, P1).
    public bool attackMovementLock; // While a melee unit swings it only brakes, it does not re-approach (P5).
    public float runEnterDistance; // Switch to Run beyond this distance to the slot (m).
    public float runExitDistance; // Back to Walk below this distance to the slot (m).
    public float followRecoveryTime; // Seconds to close the gap to the slot.
    public float followMinSpeedRatio; // Minimum following speed as a fraction of the base speed.
    public float bodyRadius; // Body radius for contact and avoidance, m (D10).
    public float meleeStopReachFraction; // Melee stop: fraction of the weapon reach past the body (C5).
    public float rangedStopFraction; // Ranged stop as a fraction of range (C5).
    public float combatTurnRate; // Max combat turn speed, degrees per second (C7).
    public bool slotResolveEnabled; // Squads check their slots against the NavMesh (C13); off = each unit projects its own slot.
    public float slotResolveRevalidateDistance; // Check again once a template slot has moved this far, m (C13).
    public int slotResolveSearchCells; // Furthest a blocked slot looks for a free cell, in cells (C13).
    public float slotSnapRadiusMax; // Largest radius to put a slot on the NavMesh, capped at the spacing, m (L3-04).
    public bool slotOccupancyEnabled; // Cells of an earlier allied squad count as taken (F17); off = each squad on its own.
    public float slotResolveAllyRevalidateDistance; // slotResolveRevalidateDistance for two allied squads walking head on, m; twice this widens the cells they keep off. Allies marching together keep the plain distance.

    // Melee target score (L4-04, D6) and squad enemy search (L4-09); UnitTargetingSystem.
    public float targetScoreDistanceScale; // Distance at which the proximity term halves, m.
    public float targetScoreFocusBonus; // Added for an enemy of the squad's focus.
    public float targetScoreHeroBias; // Added for an enemy hero; negative keeps soldiers off heroes.
    public float targetSaturationPerAttacker; // k of D6: score × max(floor, 1 − n·k) with n attackers already on the enemy.
    public float targetSaturationFloor; // A crowded enemy keeps at least this share of its score (stays eligible).
    public float targetKeepBonus; // Hysteresis: added for the current target.
    public int targetCandidateCount; // K nearest candidates scored per unit.
    public float targetReevaluateInterval; // A melee unit looks at its target again every this many seconds, s.
    public float blockedRetargetTime; // Blocked this long by an enemy that is not its target, a melee unit re-scores with it (P4), s; 0 = off.
    public float targetBlockerBonus; // Added to the blocking enemy's score before saturation (P4): wins unless crowded.
    public bool enemySearchEnabled; // New targets only within the squad's combat radius and level (L4-09); off = all detected.
    public float enemySearchRadiusMin; // Combat radius beyond the squad's extent when still or holding, m.
    public float enemySearchRadiusMax; // ... when attacking or at full speed, m.
    public float enemySearchFullRadiusSpeed; // Squad speed at which a following squad reaches the largest radius, m/s.
    public float enemySearchMaxHeightDifference; // Enemies further above or below are on another level, m.
    public float enemySearchStepHeight; // Height differences up to this ignore the slope limit, m.
    public float enemySearchMaxSlopeDegrees; // Steepest slope from the squad to a new enemy, degrees.

    /// <summary>Values used when no tuning asset is assigned (and by tests).</summary>
    public static MovementTuningComponent Default => new MovementTuningComponent
    {
        arrivalRadiusIn = 0.2f,
        arrivalRadiusOut = 0.5f,
        arrivalCalmTime = 0.15f,
        holdReformRadius = 1f,
        startRowDelay = 0.08f,
        startJitterDelay = 0.1f,
        destinationChangeMin = 0.25f,
        destinationChangeSpeedFactor = 0.25f,
        destinationLeadTime = 0.4f,
        destinationLeadMaxDistance = 1f,
        slotVelocitySmoothing = 0.15f,
        combatFacingRange = 3.5f,
        formedRotationSpeed = 5f,
        runSpeedMultiplier = 2f,
        formationChangeCooldown = 1f,
        formationCompactDelay = 1.5f,
        staminaRunSpeedFactor = 1.1f,
        hitKnockbackDistance = 0.3f,
        hitKnockbackDuration = 0.35f,
        walkCatchUpRatio = 1.05f,
        anchorAcceleration = 8f,
        anchorSnapDistance = 10f,
        contactRingMaxArcDegrees = 60f,
        contactRingEnabled = true,
        advanceSpeedRatio = 0.4f,
        advanceContactDistance = 1.5f,
        advanceObstacleLookahead = 1f,
        contactRingMargin = 0.2f,
        arrivalDecelerationFraction = 0.6f,
        brakingFactor = 2.5f,
        avoidanceEnabled = true,
        avoidanceNeighborRadius = 4f,
        avoidanceMaxNeighbors = 8f,
        avoidanceTimeHorizon = 1f,
        avoidanceTimeHorizonNear = 1f,
        avoidanceCollisionResolveTime = 0.25f,
        enemyContactEnabled = true,
        enemyContactLookaheadTime = 0.05f,
        attackMovementLock = true,
        runEnterDistance = 0.9f,
        runExitDistance = 0.45f,
        followRecoveryTime = 0.5f,
        followMinSpeedRatio = 0.3f,
        bodyRadius = 0.5f,
        meleeStopReachFraction = 0.6f,
        rangedStopFraction = 0.92f,
        combatTurnRate = 450f,
        slotResolveEnabled = true,
        slotResolveRevalidateDistance = 0.5f,
        slotResolveSearchCells = 6,
        slotSnapRadiusMax = 0.65f,
        slotOccupancyEnabled = true,
        slotResolveAllyRevalidateDistance = 0.25f,
        targetScoreDistanceScale = 4f,
        targetScoreFocusBonus = 0.1f,
        targetScoreHeroBias = 0f,
        targetSaturationPerAttacker = 0.35f,
        targetSaturationFloor = 0.1f,
        targetKeepBonus = 0.1f,
        targetCandidateCount = 6,
        targetReevaluateInterval = 0.3f,
        blockedRetargetTime = 0.5f,
        targetBlockerBonus = 0.5f,
        enemySearchEnabled = true,
        enemySearchRadiusMin = 6f,
        enemySearchRadiusMax = 8f,
        enemySearchFullRadiusSpeed = 4f,
        enemySearchMaxHeightDifference = 3f,
        enemySearchStepHeight = 0.5f,
        enemySearchMaxSlopeDegrees = 45f,
    };

    /// <summary>
    /// Coherence rules of Movimiento_Formaciones.md §3 that apply to these fields.
    /// <paramref name="cellSize"/> is the formation spacing (usually <see cref="ReferenceSpacing"/>). Returns null when coherent.
    /// </summary>
    /// <summary>Spacing of a regular formation (m); compact ones such as Testudo tighten it on purpose.</summary>
    public const float ReferenceSpacing = 1f;

    public static string Validate(in MovementTuningComponent t, float cellSize)
    {
        if (t.arrivalRadiusIn <= 0f) return "arrivalRadiusIn must be positive.";
        if (t.arrivalRadiusIn >= t.arrivalRadiusOut) return "arrivalRadiusIn must be smaller than arrivalRadiusOut.";
        if (t.arrivalRadiusOut > cellSize * 0.5f) return $"arrivalRadiusOut must not exceed half the formation spacing ({cellSize * 0.5f}).";
        if (t.arrivalRadiusIn >= cellSize * 0.25f) return $"arrivalRadiusIn must be smaller than a quarter of the formation spacing ({cellSize * 0.25f}).";
        if (t.holdReformRadius < t.arrivalRadiusIn) return "holdReformRadius must not be smaller than arrivalRadiusIn.";
        if (t.startRowDelay < 0f || t.startJitterDelay < 0f) return "Start delays must not be negative.";
        if (t.destinationLeadTime < 0f || t.destinationLeadMaxDistance < 0f) return "Destination lead must not be negative.";
        if (t.meleeStopReachFraction <= 0f || t.meleeStopReachFraction >= 1f) return "meleeStopReachFraction must be between 0 and 1.";
        if (t.rangedStopFraction <= 0f || t.rangedStopFraction >= 1f) return "rangedStopFraction must be between 0 and 1.";
        if (t.bodyRadius <= 0f) return "bodyRadius must be positive.";
        if (t.runExitDistance >= t.runEnterDistance) return "runExitDistance must be smaller than runEnterDistance.";
        if (t.followRecoveryTime <= 0f) return "followRecoveryTime must be positive.";
        if (t.avoidanceTimeHorizonNear > t.avoidanceTimeHorizon) return "avoidanceTimeHorizonNear must not exceed avoidanceTimeHorizon.";
        if (t.avoidanceMaxNeighbors > 28f) return "avoidanceMaxNeighbors must not exceed 28.";
        if (t.enemyContactLookaheadTime < 0f) return "enemyContactLookaheadTime must not be negative.";
        if (t.blockedRetargetTime < 0f) return "blockedRetargetTime must not be negative.";
        if (2f * t.bodyRadius + 0.5f > t.avoidanceNeighborRadius) return "avoidanceNeighborRadius must exceed the enemy contact distance (2·bodyRadius) by 0.5 m.";
        if (t.runExitDistance <= t.arrivalRadiusIn) return "runExitDistance must exceed arrivalRadiusIn.";
        if (t.followMinSpeedRatio < 0f || t.followMinSpeedRatio > 1f) return "followMinSpeedRatio must be between 0 and 1.";
        if (t.destinationChangeMin <= 0f) return "destinationChangeMin must be positive.";
        if (t.slotResolveRevalidateDistance <= 0f) return "slotResolveRevalidateDistance must be positive.";
        if (t.slotResolveAllyRevalidateDistance <= 0f) return "slotResolveAllyRevalidateDistance must be positive.";
        if (t.slotResolveSearchCells < 1) return "slotResolveSearchCells must be at least 1.";
        if (t.slotSnapRadiusMax <= 0f) return "slotSnapRadiusMax must be positive.";
        if (t.targetSaturationFloor <= 0f || t.targetSaturationFloor > 1f)
            return "targetSaturationFloor must be above 0 (a crowded enemy stays eligible) and at most 1.";
        if (t.targetCandidateCount < 1) return "targetCandidateCount must be at least 1.";
        if (t.enemySearchRadiusMin < 0f || t.enemySearchRadiusMax < t.enemySearchRadiusMin)
            return "enemySearchRadiusMax must not be smaller than enemySearchRadiusMin, and both not negative.";
        if (t.enemySearchStepHeight > t.enemySearchMaxHeightDifference)
            return "enemySearchStepHeight must not exceed enemySearchMaxHeightDifference.";
        // The command must be re-sent before the agent reaches it, or the lead stops paying off.
        if (t.destinationLeadTime > 0f && t.destinationChangeSpeedFactor >= t.destinationLeadTime)
            return "destinationChangeSpeedFactor must be smaller than destinationLeadTime.";
        return null;
    }
}

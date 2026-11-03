using Unity.Mathematics;

/// <summary>Weights of the melee target score (L4-04), baked from <see cref="MovementTuningComponent"/>.</summary>
public struct TargetScoreWeights
{
    public float distanceScale;         // Distance at which proximity halves, m.
    public float focusBonus;            // Added for an enemy of the squad's focus.
    public float heroBias;              // Added for an enemy hero (negative keeps soldiers off heroes).
    public float saturationPerAttacker; // k in p' = p·(1 − n·k) (D6).
    public float saturationFloor;       // Lowest saturation factor: a crowded enemy stays eligible.
    public float keepBonus;             // Added for the current target (hysteresis).
    public float blockerBonus;          // Added for the enemy that blocks the unit, before saturation (P4).

    public static TargetScoreWeights From(in MovementTuningComponent t) => new TargetScoreWeights
    {
        distanceScale = t.targetScoreDistanceScale,
        focusBonus = t.targetScoreFocusBonus,
        heroBias = t.targetScoreHeroBias,
        saturationPerAttacker = t.targetSaturationPerAttacker,
        saturationFloor = t.targetSaturationFloor,
        keepBonus = t.targetKeepBonus,
        blockerBonus = t.targetBlockerBonus,
    };
}

/// <summary>
/// Melee target score (L4-04, D6 of Movimiento_Formaciones.md): proximity plus squad focus plus bias, scaled down by
/// the attackers the enemy already has (soft saturation, never to zero) plus a bonus for the current target, so a
/// unit only switches to a clearly better enemy. Pattern of the reference; code and values are our own.
/// </summary>
public static class TargetScoring
{
    /// <summary>Consecutive entity indices times this fraction spread evenly over [0, 1) (re-evaluation phases).</summary>
    const double GoldenRatioFraction = 0.618034;

    /// <summary>1 at contact, 0.5 at <paramref name="scale"/>, never 0.</summary>
    public static float Proximity(float distance, float scale) =>
        scale > 0f ? 1f / (1f + math.max(0f, distance) / scale) : 1f;

    /// <summary>max(floor, 1 − n·k): each attacker already on the enemy makes it less attractive.</summary>
    public static float Saturation(int otherAttackers, float perAttacker, float floor) =>
        math.max(math.saturate(floor), 1f - math.max(0, otherAttackers) * math.max(0f, perAttacker));

    /// <param name="otherAttackers">Melee attackers already on the enemy, the scoring unit not included.</param>
    /// <param name="isBlocker">The enemy stops the unit (P4): its bonus is scaled by the saturation like the rest, so a
    /// crowded blocker loses to a free enemy and the melee keeps a line instead of piling up.</param>
    /// <param name="aggro">The squad's aggro bias towards this enemy (L4-05, taunt): added after the saturation, so a
    /// taunting enemy keeps its pull however many already attack it.</param>
    public static float Score(float distance, bool inFocus, bool isHero, int otherAttackers, bool isCurrent,
        in TargetScoreWeights w, bool isBlocker = false, float aggro = 0f)
    {
        float baseScore = Proximity(distance, w.distanceScale)
            + (inFocus ? w.focusBonus : 0f)
            + (isHero ? w.heroBias : 0f)
            + (isBlocker ? w.blockerBonus : 0f);
        return baseScore * Saturation(otherAttackers, w.saturationPerAttacker, w.saturationFloor)
            + (isCurrent ? w.keepBonus : 0f) + aggro;
    }

    /// <summary>
    /// Keeps the <paramref name="capacity"/> nearest candidates seen so far, sorted by distance: index arrays sized
    /// to the capacity, <paramref name="count"/> filled in. Returns false when the candidate is not among them.
    /// </summary>
    public static bool InsertNearest(int[] indices, float[] distancesSq, ref int count, int capacity,
        int candidate, float distanceSq)
    {
        capacity = math.min(capacity, indices.Length);
        if (capacity <= 0 || (count >= capacity && distanceSq >= distancesSq[count - 1])) return false;
        int i = math.min(count, capacity - 1);
        while (i > 0 && distancesSq[i - 1] > distanceSq)
        {
            indices[i] = indices[i - 1];
            distancesSq[i] = distancesSq[i - 1];
            i--;
        }
        indices[i] = candidate;
        distancesSq[i] = distanceSq;
        if (count < capacity) count++;
        return true;
    }

    /// <summary>
    /// P4: a melee unit scores its targets again with the enemy that blocks it once the block has lasted
    /// <paramref name="threshold"/>, if that enemy is attackable and the unit is not already within reach of its own
    /// target. Threshold ≤ 0 = off.
    /// </summary>
    public static bool BlockerDue(bool blockerAttackable, double blockedFor, float threshold, bool targetInReach)
        => threshold > 0f && blockerAttackable && !targetInReach && blockedFor >= threshold;

    /// <summary>
    /// A unit looks at its target again every <paramref name="interval"/> seconds, with a phase from its entity so
    /// the squad does not re-think on one frame (L4-01 pattern); independent of the frame rate. Interval ≤ 0 = every frame.
    /// </summary>
    public static bool ReevaluationDue(int entityIndex, double now, float deltaTime, float interval)
    {
        if (interval <= 0f || deltaTime >= interval) return true;
        double phase = math.frac(entityIndex * GoldenRatioFraction) * interval;
        return math.floor((now + phase) / interval) != math.floor((now - deltaTime + phase) / interval);
    }
}

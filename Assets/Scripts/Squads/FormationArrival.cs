/// <summary>What happened to a unit's formation state in one step.</summary>
public enum FormationArrivalEvent : byte
{
    None,
    LeftSlot,       // Formed -> Waiting: the caller assigns a new start delay.
    StartedMoving,  // Waiting -> Moving
    Arrived,        // Moving -> Formed
    Resettled       // Waiting -> Formed before the delay expired
}

/// <summary>Squared radii and calm time used by <see cref="FormationArrival.Step"/>.</summary>
public struct FormationArrivalRadii
{
    public float inSq;      // Enter Formed below this squared distance.
    public float outSq;     // Leave Formed above this squared distance (follow).
    public float holdSq;    // Leave Formed above this squared distance (Hold Position).
    public float calmTime;  // Seconds within inSq with a still anchor before becoming Formed.

    public static FormationArrivalRadii From(in MovementTuningComponent tuning) => new FormationArrivalRadii
    {
        inSq = tuning.arrivalRadiusIn * tuning.arrivalRadiusIn,
        outSq = tuning.arrivalRadiusOut * tuning.arrivalRadiusOut,
        holdSq = tuning.holdReformRadius * tuning.holdReformRadius,
        calmTime = tuning.arrivalCalmTime,
    };
}

/// <summary>
/// Pure Formed/Waiting/Moving transition with hysteresis (Movimiento_Formaciones.md L-4).
/// A unit enters Formed below r_in once the anchor has been still for the calm time, and leaves only
/// above r_out (or the hold radius), so noise around one threshold no longer toggles the state.
/// </summary>
public static class FormationArrival
{
    public static UnitFormationState Step(UnitFormationState state, float distSq, in FormationArrivalRadii radii,
        bool holding, bool anchorMoving, float dt, ref float calmTimer, ref float delayTimer, float delayDuration,
        out FormationArrivalEvent evt)
    {
        evt = FormationArrivalEvent.None;
        bool insideEntry = distSq <= radii.inSq;
        calmTimer = insideEntry && !anchorMoving ? calmTimer + dt : 0f;
        bool canSettle = insideEntry && calmTimer >= radii.calmTime;
        float exitSq = holding ? radii.holdSq : radii.outSq;

        switch (state)
        {
            case UnitFormationState.Formed:
                if (distSq > exitSq)
                {
                    delayTimer = 0f;
                    evt = FormationArrivalEvent.LeftSlot;
                    return UnitFormationState.Waiting;
                }
                return UnitFormationState.Formed;

            case UnitFormationState.Waiting:
                delayTimer += dt;
                if (delayTimer >= delayDuration)
                {
                    delayTimer = 0f;
                    evt = FormationArrivalEvent.StartedMoving;
                    return UnitFormationState.Moving;
                }
                // Back inside before the delay expired: settle without moving. Hold keeps its wider band.
                if (holding ? distSq <= radii.holdSq : canSettle)
                {
                    delayTimer = 0f;
                    evt = FormationArrivalEvent.Resettled;
                    return UnitFormationState.Formed;
                }
                return UnitFormationState.Waiting;

            default: // Moving
                if (canSettle)
                {
                    delayTimer = 0f;
                    evt = FormationArrivalEvent.Arrived;
                    return UnitFormationState.Formed;
                }
                return UnitFormationState.Moving;
        }
    }
}

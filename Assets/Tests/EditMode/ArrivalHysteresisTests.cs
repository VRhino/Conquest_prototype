using NUnit.Framework;

public class ArrivalHysteresisTests
{
    const float Dt = 1f / 60f;
    static readonly FormationArrivalRadii Radii = FormationArrivalRadii.From(MovementTuningComponent.Default);

    static UnitFormationState Run(UnitFormationState state, float distance, bool anchorMoving, int frames,
        ref float calm, ref float delay, float delayDuration, out int transitions, bool holding = false)
    {
        transitions = 0;
        for (int i = 0; i < frames; i++)
        {
            var next = FormationArrival.Step(state, distance * distance, Radii, holding, anchorMoving, Dt,
                ref calm, ref delay, delayDuration, out _);
            if (next != state) transitions++;
            state = next;
        }
        return state;
    }

    [Test]
    public void NoiseAroundTheEntryRadiusDoesNotToggleAFormedUnit()
    {
        var state = UnitFormationState.Formed;
        float calm = 1f, delay = 0f;
        int transitions = 0;
        float rIn = MovementTuningComponent.Default.arrivalRadiusIn;
        for (int i = 0; i < 600; i++) // 10 s alternating just inside and just outside r_in.
        {
            float distance = i % 2 == 0 ? rIn * 0.9f : rIn * 1.4f;
            var next = FormationArrival.Step(state, distance * distance, Radii, false, false, Dt,
                ref calm, ref delay, 1f, out _);
            if (next != state) transitions++;
            state = next;
        }
        Assert.That(transitions, Is.EqualTo(0));
        Assert.That(state, Is.EqualTo(UnitFormationState.Formed));
    }

    [Test]
    public void FormedUnitLeavesOnlyAboveTheExitRadius()
    {
        float rOut = MovementTuningComponent.Default.arrivalRadiusOut;
        float calm = 1f, delay = 0f;
        var inside = Run(UnitFormationState.Formed, rOut * 0.95f, false, 30, ref calm, ref delay, 1f, out _);
        Assert.That(inside, Is.EqualTo(UnitFormationState.Formed));
        var outside = FormationArrival.Step(UnitFormationState.Formed, rOut * 1.05f * rOut * 1.05f, Radii,
            false, false, Dt, ref calm, ref delay, 1f, out var evt);
        Assert.That(outside, Is.EqualTo(UnitFormationState.Waiting));
        Assert.That(evt, Is.EqualTo(FormationArrivalEvent.LeftSlot));
    }

    [Test]
    public void MovingUnitBecomesFormedOnlyAfterTheAnchorIsStillForTheCalmTime()
    {
        float rIn = MovementTuningComponent.Default.arrivalRadiusIn;
        float calmTime = MovementTuningComponent.Default.arrivalCalmTime;
        float calm = 0f, delay = 0f;

        var marching = Run(UnitFormationState.Moving, rIn * 0.5f, true, 120, ref calm, ref delay, 1f, out _);
        Assert.That(marching, Is.EqualTo(UnitFormationState.Moving), "A moving anchor must keep the unit Moving.");

        int framesBeforeCalm = (int)(calmTime / Dt) - 1;
        var early = Run(UnitFormationState.Moving, rIn * 0.5f, false, framesBeforeCalm, ref calm, ref delay, 1f, out _);
        Assert.That(early, Is.EqualTo(UnitFormationState.Moving));

        var settled = Run(early, rIn * 0.5f, false, 3, ref calm, ref delay, 1f, out int transitions);
        Assert.That(settled, Is.EqualTo(UnitFormationState.Formed));
        Assert.That(transitions, Is.EqualTo(1));
    }

    [Test]
    public void WaitingUnitStartsMovingWhenItsDelayExpires()
    {
        float calm = 0f, delay = 0f;
        var state = Run(UnitFormationState.Waiting, 2f, true, 58, ref calm, ref delay, 1f, out _);
        Assert.That(state, Is.EqualTo(UnitFormationState.Waiting));
        state = Run(state, 2f, true, 4, ref calm, ref delay, 1f, out int transitions); // Crosses 1 s.
        Assert.That(state, Is.EqualTo(UnitFormationState.Moving));
        Assert.That(transitions, Is.EqualTo(1));
    }

    [Test]
    public void HoldPositionUsesTheWiderHoldRadiusToLeaveFormed()
    {
        float hold = MovementTuningComponent.Default.holdReformRadius;
        float calm = 1f, delay = 0f;
        var state = Run(UnitFormationState.Formed, hold * 0.9f, false, 30, ref calm, ref delay, 1f, out _, holding: true);
        Assert.That(state, Is.EqualTo(UnitFormationState.Formed));
    }
}

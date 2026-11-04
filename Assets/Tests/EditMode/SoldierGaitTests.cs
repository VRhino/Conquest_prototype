using ConquestTactics.Animation;
using NUnit.Framework;

/// <summary>
/// A soldier's locomotion animation follows its movement gear (Docs/Por_Abordar/Movimiento.md M4). Measured against the
/// base speed, a soldier walking at its base speed looked like running and a running one like sprinting.
/// </summary>
public class SoldierGaitTests
{
    const float Base = 3f; // A squire.
    static readonly LocomotionAnimationSettings Settings = new LocomotionAnimationSettings();

    [Test]
    public void TheGaitIsTheGearAndNeverASprint()
    {
        Assert.That(Settings.GetGait(MovementGear.Walk, Base * 1.05f), Is.EqualTo(LocomotionGait.Walk), "Catching up its slot.");
        Assert.That(Settings.GetGait(MovementGear.Walk, Base * 0.3f), Is.EqualTo(LocomotionGait.Walk));
        Assert.That(Settings.GetGait(MovementGear.Run, Base * 2f), Is.EqualTo(LocomotionGait.Run));
        Assert.That(Settings.GetGait(MovementGear.Run, Base * 0.5f), Is.EqualTo(LocomotionGait.Run));
        Assert.That(Settings.GetGait(MovementGear.Run, 0f), Is.EqualTo(LocomotionGait.Idle));
    }

    [Test]
    public void TheBlendReachesTheGearsClipAndNeverTheSprint()
    {
        Assert.That(Settings.GetBlendSpeed(MovementGear.Walk, Base, Base), Is.EqualTo(Settings.walkBlendSpeed));
        Assert.That(Settings.GetBlendSpeed(MovementGear.Walk, Base * Settings.walkToRunRatio, Base), Is.EqualTo(Settings.walkBlendSpeed).Within(1e-4f));
        Assert.That(Settings.GetBlendSpeed(MovementGear.Run, Base * 2f, Base), Is.EqualTo(Settings.runBlendSpeed), "Full Run speed is the run clip, not the sprint.");
        Assert.That(Settings.GetBlendSpeed(MovementGear.Run, Base, Base), Is.EqualTo(Settings.runBlendSpeed));
        Assert.That(Settings.GetBlendSpeed(MovementGear.Run, Base * 0.5f, Base), Is.EqualTo(Settings.runBlendSpeed * 0.5f).Within(1e-4f), "Setting off blends from idle.");
        Assert.That(Settings.GetBlendSpeed(MovementGear.Walk, 0f, Base), Is.EqualTo(0f));
    }
}

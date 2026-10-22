using NUnit.Framework;
using Unity.Mathematics;

/// <summary>Launcher scheme: fan of projectiles and scatter that grows while moving (Aprendizajes_Referencia.md L2-7).</summary>
public class LauncherSchemeTests
{
    static readonly float3 Forward = new float3(0f, 0f, 1f);

    static float YawDegrees(float3 d) => math.degrees(math.atan2(d.x, d.z));

    [Test]
    public void AFanSpreadsTheProjectilesEvenlyAndASingleShotIsUntouched()
    {
        Assert.That(LauncherScheme.FanDirection(Forward, 0, 1, 30f), Is.EqualTo(Forward));
        Assert.That(LauncherScheme.FanDirection(Forward, 1, 3, 0f), Is.EqualTo(Forward), "No angle: all along the aim.");
        Assert.That(YawDegrees(LauncherScheme.FanDirection(Forward, 0, 3, 30f)), Is.EqualTo(-15f).Within(1e-3f));
        Assert.That(YawDegrees(LauncherScheme.FanDirection(Forward, 1, 3, 30f)), Is.EqualTo(0f).Within(1e-3f));
        Assert.That(YawDegrees(LauncherScheme.FanDirection(Forward, 2, 3, 30f)), Is.EqualTo(15f).Within(1e-3f));
        Assert.That(math.length(LauncherScheme.FanDirection(Forward, 2, 3, 30f)), Is.EqualTo(1f).Within(1e-5f));
    }

    [Test]
    public void StandingScatterIsTodaysAndItWidensAtFullSpeed()
    {
        var tuning = CombatTuning.Default;
        Assert.That(LauncherScheme.Spread(60f, 0f, tuning), Is.EqualTo((1f - 0.6f) * 0.15f).Within(1e-6f), "Same as before when still.");
        Assert.That(LauncherScheme.Spread(100f, 1f, tuning), Is.EqualTo(0f), "Perfect accuracy never scatters.");
        Assert.That(LauncherScheme.Spread(60f, 1f, tuning),
            Is.EqualTo(LauncherScheme.Spread(60f, 0f, tuning) * (1f + tuning.movingSpreadFactor)).Within(1e-6f));
        Assert.That(LauncherScheme.Spread(60f, 3f, tuning), Is.EqualTo(LauncherScheme.Spread(60f, 1f, tuning)), "Capped at full speed.");
    }
}

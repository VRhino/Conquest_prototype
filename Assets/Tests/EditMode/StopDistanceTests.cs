using NUnit.Framework;
using UnityEditor;

/// <summary>Melee stop/resume distances derived from body radius and weapon reach (C5).</summary>
public class StopDistanceTests
{
    const float BodyRadius = 0.5f;
    const float ReachFraction = 0.6f;

    [Test]
    public void StopKeepsBodiesApartAndTargetInsideTheHit()
    {
        foreach (float attackRange in new[] { 0.6f, 1.2f, 1.5f, 2f, 3f })
        {
            float stop = MeleeReach.StopDistance(attackRange, BodyRadius, BodyRadius, ReachFraction);
            float resume = MeleeReach.ResumeDistance(attackRange, BodyRadius);
            Assert.That(stop, Is.GreaterThanOrEqualTo(2f * BodyRadius + MeleeReach.BodyClearance - 1e-5f), $"range {attackRange}");
            Assert.That(stop, Is.LessThanOrEqualTo(resume), $"range {attackRange}");
            Assert.That(MeleeReach.InAttackRange(stop, attackRange, BodyRadius), Is.True, $"range {attackRange}");
        }
    }

    [Test]
    public void ResumeIsBeyondStopSoUnitsDoNotShuffle()
    {
        float stop = MeleeReach.StopDistance(2f, BodyRadius, BodyRadius, ReachFraction);
        float resume = MeleeReach.ResumeDistance(2f, BodyRadius);
        Assert.That(resume - stop, Is.GreaterThan(0.3f));
        Assert.That(MeleeReach.InAttackRange(resume + 0.01f, 2f, BodyRadius), Is.False);
    }

    [Test]
    public void RangedStopsJustInsideRange()
    {
        Assert.That(MeleeReach.RangedStopDistance(30f, 0.92f), Is.EqualTo(27.6f).Within(1e-4f));
        Assert.That(MeleeReach.RangedStopDistance(30f, 2f), Is.EqualTo(30f));
    }

    [Test]
    public void EverySquadDefinitionStopsWithReachLeft()
    {
        var tuning = MovementTuningComponent.Default;
        string[] guids = AssetDatabase.FindAssets("t:SquadData");
        Assert.That(guids, Is.Not.Empty);
        foreach (string guid in guids)
        {
            var squad = AssetDatabase.LoadAssetAtPath<SquadData>(AssetDatabase.GUIDToAssetPath(guid));
            if (squad == null || squad.meleeData == null) continue;
            float range = squad.meleeData.attackRange;
            float stop = MeleeReach.StopDistance(range, tuning.bodyRadius, tuning.bodyRadius, tuning.meleeStopReachFraction);
            Assert.That(stop, Is.GreaterThanOrEqualTo(2f * tuning.bodyRadius), squad.name);
            Assert.That(stop, Is.LessThan(range + tuning.bodyRadius), squad.name);
            Assert.That(MeleeReach.InAttackRange(stop, range, tuning.bodyRadius), Is.True, squad.name);
        }
    }

    [Test]
    public void MovingUnitInReachRestsWhereBrakingEnds()
    {
        var position = new Unity.Mathematics.float2(0f, 0f);
        var velocity = new Unity.Mathematics.float2(0f, 4f);
        // 4² / (2·12) = 0.667 m ahead along the velocity.
        var rest = MeleeReach.BrakingStopPoint(position, velocity, 12f, 5f);
        Assert.That(rest.y, Is.EqualTo(16f / 24f).Within(1e-4f));
        Assert.That(rest.x, Is.EqualTo(0f).Within(1e-6f));
        // Never further than allowed (the target's body), and a still unit stays put.
        Assert.That(MeleeReach.BrakingStopPoint(position, velocity, 12f, 0.2f).y, Is.EqualTo(0.2f).Within(1e-5f));
        Assert.That(MeleeReach.BrakingStopPoint(position, Unity.Mathematics.float2.zero, 12f, 5f), Is.EqualTo(position));
    }
}

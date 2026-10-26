using NUnit.Framework;
using Unity.Mathematics;

/// <summary>Melee target score with soft saturation and hysteresis (L4-04, D6) and the squad enemy search (L4-09).</summary>
public class TargetScoringTests
{
    static TargetScoreWeights Weights => TargetScoreWeights.From(MovementTuningComponent.Default);
    static EnemySearchSettings Search => EnemySearchSettings.From(MovementTuningComponent.Default);

    [Test]
    public void SaturationFollowsD6AndNeverReachesZero()
    {
        Assert.That(TargetScoring.Saturation(0, 0.3f, 0.1f), Is.EqualTo(1f));
        Assert.That(TargetScoring.Saturation(2, 0.3f, 0.1f), Is.EqualTo(0.4f).Within(1e-5f));
        Assert.That(TargetScoring.Saturation(10, 0.3f, 0.1f), Is.EqualTo(0.1f), "A crowded enemy stays eligible.");
    }

    [Test]
    public void FreeEnemyAFewMetresFurtherBeatsADoubledOne()
    {
        var w = Weights;
        float doubled = TargetScoring.Score(1f, false, false, 1, false, w);
        float free = TargetScoring.Score(2.5f, false, false, 0, false, w);
        float farFree = TargetScoring.Score(12f, false, false, 0, false, w);
        Assert.That(free, Is.GreaterThan(doubled));
        Assert.That(farFree, Is.LessThan(doubled), "Saturation spreads attackers, it does not send them across the field.");
    }

    [Test]
    public void CurrentTargetKeepsItsBonusAgainstASlightlyBetterOne()
    {
        var w = Weights;
        float current = TargetScoring.Score(2f, false, false, 0, true, w);
        float slightlyCloser = TargetScoring.Score(1.8f, false, false, 0, false, w);
        float muchCloser = TargetScoring.Score(0.3f, false, false, 0, false, w);
        Assert.That(current, Is.GreaterThan(slightlyCloser));
        Assert.That(muchCloser, Is.GreaterThan(current));
    }

    [Test]
    public void FocusAndHeroBiasAreAdded()
    {
        var w = Weights;
        w.heroBias = -0.2f;
        Assert.That(TargetScoring.Score(2f, true, false, 0, false, w),
            Is.EqualTo(TargetScoring.Score(2f, false, false, 0, false, w) + w.focusBonus).Within(1e-5f));
        Assert.That(TargetScoring.Score(2f, false, true, 0, false, w),
            Is.LessThan(TargetScoring.Score(2f, false, false, 0, false, w)));
    }

    [Test]
    public void InsertNearestKeepsTheKClosestSorted()
    {
        var indices = new int[3];
        var distances = new float[3];
        int count = 0;
        float[] input = { 9f, 1f, 5f, 7f, 0.5f, 6f };
        for (int i = 0; i < input.Length; i++)
            TargetScoring.InsertNearest(indices, distances, ref count, 3, i, input[i]);
        Assert.That(count, Is.EqualTo(3));
        Assert.That(indices, Is.EqualTo(new[] { 4, 1, 2 }));
        Assert.That(TargetScoring.InsertNearest(indices, distances, ref count, 3, 9, 8f), Is.False);
    }

    [Test]
    public void ReevaluationIsSpreadOverTheIntervalAndFrameRateIndependent()
    {
        const float interval = 0.3f;
        for (int entity = 0; entity < 20; entity++)
        {
            int at60 = 0, at20 = 0;
            for (int f = 1; f <= 180; f++) if (TargetScoring.ReevaluationDue(entity, f / 60.0, 1f / 60f, interval)) at60++;
            for (int f = 1; f <= 60; f++) if (TargetScoring.ReevaluationDue(entity, f / 20.0, 1f / 20f, interval)) at20++;
            Assert.That(at60, Is.InRange(9, 11), "About once per interval over 3 s at 60 fps.");
            Assert.That(at20, Is.InRange(9, 11), "And the same at 20 fps.");
        }
        int sameFrame = 0;
        for (int entity = 0; entity < 60; entity++)
            if (TargetScoring.ReevaluationDue(entity, 1.0, 1f / 60f, interval)) sameFrame++;
        Assert.That(sameFrame, Is.LessThan(15), "Phases by entity: a squad does not re-think on one frame.");
        Assert.That(TargetScoring.ReevaluationDue(3, 0.0, 0f, 0f), Is.True, "Interval 0 = every frame.");
    }

    [Test]
    public void StrategyFollowsTheOrderAndItsSource()
    {
        Assert.That(EnemySearch.StrategyFor(SquadOrderType.HoldPosition, OrderSource.Player), Is.EqualTo(SquadSearchStrategy.Zone));
        Assert.That(EnemySearch.StrategyFor(SquadOrderType.Attack, OrderSource.AI), Is.EqualTo(SquadSearchStrategy.Advance));
        Assert.That(EnemySearch.StrategyFor(SquadOrderType.Attack, OrderSource.CombatReaction), Is.EqualTo(SquadSearchStrategy.Follow));
        Assert.That(EnemySearch.StrategyFor(SquadOrderType.FollowHero, OrderSource.Player), Is.EqualTo(SquadSearchStrategy.Follow));
    }

    [Test]
    public void CombatRadiusGrowsWithSpeedBetweenMinAndMax()
    {
        var s = Search;
        Assert.That(EnemySearch.CombatRadius(SquadSearchStrategy.Follow, 0f, s), Is.EqualTo(s.radiusMin));
        Assert.That(EnemySearch.CombatRadius(SquadSearchStrategy.Follow, s.fullRadiusSpeed * 0.5f, s),
            Is.EqualTo(math.lerp(s.radiusMin, s.radiusMax, 0.5f)).Within(1e-4f));
        Assert.That(EnemySearch.CombatRadius(SquadSearchStrategy.Follow, s.fullRadiusSpeed * 3f, s), Is.EqualTo(s.radiusMax));
        Assert.That(EnemySearch.CombatRadius(SquadSearchStrategy.Zone, 10f, s), Is.EqualTo(s.radiusMin));
        Assert.That(EnemySearch.CombatRadius(SquadSearchStrategy.Advance, 0f, s), Is.EqualTo(s.radiusMax));
    }

    [Test]
    public void RadiusIsMeasuredBeyondTheSquadExtent()
    {
        // A squad 4 m wide around its anchor: an enemy 6 m from the anchor is 2 m beyond its flank.
        Assert.That(EnemySearch.WithinRadius(float2.zero, 4f, new float2(6f, 0f), 2.5f), Is.True);
        Assert.That(EnemySearch.WithinRadius(float2.zero, 4f, new float2(7f, 0f), 2.5f), Is.False);
    }

    [Test]
    public void EnemiesOnAnotherLevelAreLeftOut()
    {
        var s = Search;
        Assert.That(EnemySearch.SameLevel(0f, new float3(0f, s.stepHeight * 0.5f, 0f), 0.5f, s), Is.True, "A step.");
        Assert.That(EnemySearch.SameLevel(0f, new float3(0f, 2f, 0f), 1f, s), Is.False, "On a wall beside the squad.");
        Assert.That(EnemySearch.SameLevel(0f, new float3(0f, 2f, 0f), 10f, s), Is.True, "Up a gentle slope.");
        Assert.That(EnemySearch.SameLevel(0f, new float3(0f, s.maxHeightDifference + 1f, 0f), 100f, s), Is.False,
            "Too high whatever the slope.");
    }
}

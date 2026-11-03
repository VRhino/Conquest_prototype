using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>Aggro bias with expiry, added to the melee target score (Aprendizajes_Referencia.md L4-05).</summary>
public class AggroBiasTests
{
    static readonly TargetScoreWeights W = new TargetScoreWeights
    {
        distanceScale = 4f, saturationPerAttacker = 0.25f, saturationFloor = 0.2f, keepBonus = 0.2f,
    };

    [Test]
    public void TheBiasPullsAfterSaturationSoACrowdedTaunterStillWins()
    {
        float free = TargetScoring.Score(1f, false, false, 0, false, W);
        float crowdedTaunter = TargetScoring.Score(1f, false, true, 6, false, W, false, 1f);
        Assert.That(crowdedTaunter, Is.GreaterThan(free));
        Assert.That(TargetScoring.Score(1f, false, false, 0, false, W, false, 0f), Is.EqualTo(free), "No bias, same score.");
    }

    [Test]
    public void ItMatchesTheEnemyOrItsSquadExpiresAndARecastRefreshes()
    {
        using var world = new World("Aggro");
        var em = world.EntityManager;
        var squad = em.CreateEntity();
        var hero = em.CreateEntity();
        var enemySquad = em.CreateEntity();
        var soldier = em.CreateEntity();
        var biases = em.AddBuffer<AggroBiasElement>(squad);
        AggroBias.Apply(biases, hero, 1f, 7, 10.0, 0.0);
        AggroBias.Apply(biases, enemySquad, 0.5f, 8, 4.0, 0.0);
        Assert.That(AggroBias.For(biases, hero, hero, 1.0), Is.EqualTo(1f));
        Assert.That(AggroBias.For(biases, soldier, enemySquad, 1.0), Is.EqualTo(0.5f), "Through the soldier's squad.");
        Assert.That(AggroBias.For(biases, soldier, enemySquad, 4.0), Is.EqualTo(0f), "Expired.");

        AggroBias.Apply(biases, hero, 2f, 7, 20.0, 5.0);
        Assert.That(biases.Length, Is.EqualTo(1), "The recast replaced its own and the expired one is gone.");
        Assert.That(AggroBias.For(biases, hero, hero, 5.0), Is.EqualTo(2f));
    }

    [Test]
    public void ATauntPullsTheSquadsOfTheEnemiesHitTowardsTheCaster()
    {
        using var world = new World("Taunt");
        var em = world.EntityManager;
        var squad = em.CreateEntity();
        em.AddBuffer<SquadUnitElement>(squad);
        for (int i = 0; i < 2; i++)
        {
            var unit = em.CreateEntity(typeof(LocalTransform), typeof(TeamComponent), typeof(HealthComponent));
            em.SetComponentData(unit, LocalTransform.FromPosition(new float3(i, 0, 2)));
            em.SetComponentData(unit, new TeamComponent { value = Team.TeamB });
            em.AddComponentData(unit, new UnitOwnerComponent { squad = squad });
            em.GetBuffer<SquadUnitElement>(squad).Add(new SquadUnitElement { Value = unit });
        }
        var caster = em.CreateEntity();
        var request = em.CreateEntity();
        em.AddComponentData(request, new SkillEffectRequestComponent
        {
            caster = caster, skillId = 9, team = Team.TeamA, forward = new float3(0, 0, 1),
            effect = new SkillEvent { type = SkillEventType.Taunt, target = SkillTarget.EnemiesInRadius, radius = 10f, value = 1.5f, duration = 4f },
        });
        world.GetOrCreateSystemManaged<SkillEffectSystem>().Update();

        var biases = em.GetBuffer<AggroBiasElement>(squad);
        Assert.That(biases.Length, Is.EqualTo(1), "Two soldiers hit, one bias on their squad.");
        Assert.That(biases[0].toward, Is.EqualTo(caster));
        Assert.That(biases[0].bias, Is.EqualTo(1.5f));
        Assert.That(biases[0].expiresAt, Is.EqualTo(4.0).Within(1e-6));
    }
}

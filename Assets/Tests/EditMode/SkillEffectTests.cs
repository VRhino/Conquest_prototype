using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>Generic skill effects (Fase 2 step 6): area damage, timed modifiers, absorb shield and dash.</summary>
public class SkillEffectTests
{
    static readonly float3 Forward = new float3(0f, 0f, 1f);

    [Test]
    public void AreaShapesAreCirclesOrConesAroundTheFacing()
    {
        Assert.That(SkillArea.Contains(float3.zero, Forward, 3f, 0f, new float3(0f, 0f, -2.9f)), Is.True, "Circle: behind counts.");
        Assert.That(SkillArea.Contains(float3.zero, Forward, 3f, 0f, new float3(0f, 0f, 3.1f)), Is.False);
        Assert.That(SkillArea.Contains(float3.zero, Forward, 3f, 90f, new float3(1f, 0f, 2f)), Is.True, "Inside a 90° cone.");
        Assert.That(SkillArea.Contains(float3.zero, Forward, 3f, 90f, new float3(2f, 0f, 1f)), Is.False, "Outside its half-angle.");
        Assert.That(SkillArea.Contains(float3.zero, Forward, 3f, 90f, new float3(0f, 0f, -1f)), Is.False, "Behind a cone.");
        Assert.That(SkillArea.Centre(float3.zero, Forward, 15f).z, Is.EqualTo(15f));
    }

    World _world;
    EntityManager _em;

    [SetUp] public void SetUp() { _world = new World("Skill effects"); _em = _world.EntityManager; }
    [TearDown] public void TearDown() => _world.Dispose();

    Entity Body(float3 position, Team team, bool hero = false)
    {
        var e = _em.CreateEntity(typeof(LocalTransform), typeof(TeamComponent));
        _em.SetComponentData(e, LocalTransform.FromPosition(position));
        _em.SetComponentData(e, new TeamComponent { value = team });
        if (hero) _em.AddComponentData(e, new HeroHealthComponent { maxHealth = 100f, currentHealth = 100f });
        else _em.AddComponentData(e, new HealthComponent { maxHealth = 100f, currentHealth = 100f });
        StatStack.Ensure(_em, e);
        return e;
    }

    void Request(Entity caster, SkillEvent effect)
        => _em.SetComponentData(_em.CreateEntity(typeof(SkillEffectRequestComponent)), new SkillEffectRequestComponent
        {
            caster = caster, skillId = 0x12345, effect = effect, origin = float3.zero, forward = Forward, team = Team.TeamA,
        });

    [Test]
    public void AreaDamageHitsOnlyEnemiesInTheConeWithTheCastersProfile()
    {
        var caster = Body(float3.zero, Team.TeamA, hero: true);
        _em.AddComponentData(caster, new DamageProfileComponent { slashingDamage = 40f });
        var front = Body(new float3(0f, 0f, 2f), Team.TeamB);
        Body(new float3(0f, 0f, -2f), Team.TeamB); // behind
        Body(new float3(0.5f, 0f, 1.5f), Team.TeamA); // ally in the cone
        Request(caster, new SkillEvent { type = SkillEventType.AreaDamage, radius = 3f, angle = 90f, value = 1.5f });
        _world.GetOrCreateSystemManaged<SkillEffectSystem>().Update();

        using var hits = _em.CreateEntityQuery(typeof(PendingDamageEvent)).ToComponentDataArray<PendingDamageEvent>(Allocator.Temp);
        Assert.That(hits.Length, Is.EqualTo(1));
        Assert.That(hits[0].target, Is.EqualTo(front));
        Assert.That(hits[0].multiplier, Is.EqualTo(1.5f));
        Assert.That(hits[0].inlineDamageProfile.slashingDamage, Is.EqualTo(40f));
        Assert.That(hits[0].category, Is.EqualTo(DamageCategory.Ability));
        Assert.That(_em.CreateEntityQuery(typeof(SkillEffectRequestComponent)).CalculateEntityCount(), Is.EqualTo(0), "Requests are consumed.");
    }

    [Test]
    public void ModifiersReachAlliesInRadiusAndRefreshPerStatInsteadOfStacking()
    {
        _world.SetTime(new Unity.Core.TimeData(5.0, 0.02f));
        var caster = Body(float3.zero, Team.TeamA, hero: true);
        var near = Body(new float3(3f, 0f, 0f), Team.TeamA);
        var far = Body(new float3(9f, 0f, 0f), Team.TeamA);
        var enemy = Body(new float3(1f, 0f, 0f), Team.TeamB);
        var effects = _world.GetOrCreateSystemManaged<SkillEffectSystem>();
        var defense = new SkillEvent { type = SkillEventType.ApplyModifier, target = SkillTarget.AlliesInRadius, radius = 5f,
            stat = StatId.SlashDefense, op = ModifierOp.Ratio, value = 0.3f, duration = 8f };
        Request(caster, defense);
        Request(caster, new SkillEvent { type = SkillEventType.ApplyModifier, target = SkillTarget.AlliesInRadius, radius = 5f,
            stat = StatId.BluntDefense, op = ModifierOp.Ratio, value = 0.3f, duration = 8f });
        effects.Update();
        Request(caster, defense); // Recast: refreshes, does not stack.
        effects.Update();

        Assert.That(_em.GetBuffer<StatModifier>(caster).Length, Is.EqualTo(2), "Caster included; one per stat.");
        Assert.That(_em.GetBuffer<StatModifier>(near).Length, Is.EqualTo(2));
        Assert.That(_em.GetBuffer<StatModifier>(far).Length, Is.EqualTo(0));
        Assert.That(_em.GetBuffer<StatModifier>(enemy).Length, Is.EqualTo(0));
        Assert.That(_em.GetBuffer<StatModifier>(near)[0].expiresAt, Is.EqualTo(13.0).Within(1e-6));
        Assert.That(_em.IsComponentEnabled<ModifiersDirty>(near), Is.True);
    }

    [Test]
    public void ShieldAndDashGoOnTheTargetAndTheCaster()
    {
        _world.SetTime(new Unity.Core.TimeData(2.0, 0.02f));
        var caster = Body(float3.zero, Team.TeamA, hero: true);
        var effects = _world.GetOrCreateSystemManaged<SkillEffectSystem>();
        Request(caster, new SkillEvent { type = SkillEventType.AbsorbShield, target = SkillTarget.Self, value = 60f, duration = 5f });
        Request(caster, new SkillEvent { type = SkillEventType.Dash, distance = 6f, duration = 0.4f });
        effects.Update();

        var shield = _em.GetComponentData<AbsorbShieldComponent>(caster);
        Assert.That(shield.value, Is.EqualTo(60f));
        Assert.That(shield.expiresAt, Is.EqualTo(7.0).Within(1e-6));
        var dash = _em.GetComponentData<HeroDashComponent>(caster);
        Assert.That(dash.speed, Is.EqualTo(15f).Within(1e-4f));
        Assert.That(dash.Active(2.3), Is.True);
        Assert.That(dash.Active(2.5), Is.False);
    }
}

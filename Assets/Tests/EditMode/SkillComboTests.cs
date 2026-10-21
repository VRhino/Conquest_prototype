using NUnit.Framework;
using Unity.Entities;

/// <summary>Basic attack chain of 3 with a chain window and input buffering (Aprendizajes_Referencia.md L2-3).</summary>
public class SkillComboTests
{
    const float ChainWindow = 0.4f;
    const float InputBuffer = 0.2f;

    World _world;
    EntityManager _em;
    Entity _hero;
    SkillExecutionSystem _system;
    BlobAssetReference<SkillBlob>[] _hits;

    [SetUp]
    public void SetUp()
    {
        _t = 0;
        _world = new World("Combo");
        _em = _world.EntityManager;
        _em.SetComponentData(_em.CreateEntity(typeof(MatchStateComponent)), new MatchStateComponent { currentState = MatchState.InBattle });
        _em.CreateSingleton(new HeroGameplayConfigComponent { skillInputBuffer = InputBuffer });
        // Windup 0.2, strike 0.2, recovery 0.4.
        var combat = new HeroCombatComponent { strikeWindowStart = 0.2f, strikeWindowDuration = 0.2f, attackAnimationDuration = 0.8f };
        _hits = new BlobAssetReference<SkillBlob>[3];
        _hero = _em.CreateEntity(typeof(HeroLifeComponent), typeof(HeroCombatComponent), typeof(StaminaComponent),
            typeof(SkillInstanceComponent), typeof(SkillRequestComponent));
        _em.SetComponentData(_hero, new HeroLifeComponent { isAlive = true });
        _em.SetComponentData(_hero, combat);
        _em.SetComponentData(_hero, new StaminaComponent { currentStamina = 100f, maxStamina = 100f });
        _em.AddBuffer<CooldownGroupElement>(_hero);
        for (int i = 0; i < 3; i++)
            _hits[i] = SkillBlobBuilder.BuildBasicAttack(combat, 5f, 0.3f, i, 3, ChainWindow);
        var skills = _em.AddBuffer<HeroSkillElement>(_hero);
        foreach (var hit in _hits) skills.Add(new HeroSkillElement { slot = SkillSlot.Basic, skillId = hit.Value.id, skill = hit });
        _system = _world.GetOrCreateSystemManaged<SkillExecutionSystem>();
    }

    [TearDown]
    public void TearDown()
    {
        _world.Dispose();
        foreach (var hit in _hits) hit.Dispose();
    }

    double _t;

    void Tick(bool press, double requestedAt = -1)
    {
        _world.SetTime(new Unity.Core.TimeData(_t, 0.05f));
        if (press)
        {
            _em.SetComponentData(_hero, new SkillRequestComponent { slot = SkillSlot.Basic, requestedAt = requestedAt >= 0 ? requestedAt : _t });
            _em.SetComponentEnabled<SkillRequestComponent>(_hero, true);
        }
        _system.Update();
        _t += 0.05;
    }

    int CurrentHit => _em.GetComponentData<SkillInstanceComponent>(_hero).skillIndex;
    string State { get { var i = _em.GetComponentData<SkillInstanceComponent>(_hero); return $"t={_t:F2} hit={i.skillIndex} phase={i.phase} time={i.phaseTime:F3} active={i.active} launches={i.instanceId}"; } }
    bool Active => _em.GetComponentData<SkillInstanceComponent>(_hero).active;

    void TickUntil(double t) { while (_t < t - 1e-9) Tick(false); }

    [Test]
    public void PressingInTheRecoveryChainsThreeHitsThenStartsOver()
    {
        Tick(true);
        Assert.That(CurrentHit, Is.EqualTo(0));
        TickUntil(0.45); // Hit 1 in its recovery.
        Tick(true);
        Assert.That(CurrentHit, Is.EqualTo(1), "Second hit of the chain. " + State);
        TickUntil(0.95);
        Tick(true);
        Assert.That(CurrentHit, Is.EqualTo(2), "Third hit.");
        TickUntil(1.45);
        Tick(true);
        Assert.That(CurrentHit, Is.EqualTo(2), "The third closes the chain: nothing follows it during its recovery.");
        TickUntil(2.5);
        Tick(true);
        Assert.That(CurrentHit, Is.EqualTo(0), "Afterwards the chain starts over.");
    }

    [Test]
    public void TheNextHitIsAcceptedWithinTheChainWindowButNotAfterIt()
    {
        Tick(true);
        TickUntil(0.85); // Hit 1 ended at 0.8.
        Assert.That(Active, Is.False, State);
        Tick(true);
        Assert.That(CurrentHit, Is.EqualTo(1), "Within the window.");

        TickUntil(3.0);
        Tick(true);
        Assert.That(CurrentHit, Is.EqualTo(0), "Too late: back to the first hit.");
    }

    [Test]
    public void APressDuringTheStrikeIsRememberedForAMoment()
    {
        Tick(true);
        TickUntil(0.3); // In the strike (not interruptible).
        Tick(true);
        Assert.That(CurrentHit, Is.EqualTo(0));
        Assert.That(_em.IsComponentEnabled<SkillRequestComponent>(_hero), Is.True, "Buffered.");
        TickUntil(0.45); // The strike ended at 0.4: the buffered press chains hit 2 without a new press.
        Assert.That(CurrentHit, Is.EqualTo(1));
    }
}

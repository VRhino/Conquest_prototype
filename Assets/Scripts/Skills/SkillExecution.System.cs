using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Runs every hero's skills (Aprendizajes_Referencia.md L2-1, L2-2), the player's and the AI's alike: advances the
/// current instance through its phases and applies its events, then evaluates the pending request with
/// <see cref="SkillGate"/> and launches it (stamina and cooldown are charged at the start, as in the reference). A new
/// skill replaces the current one when the current phase is interruptible.
/// Keeps <see cref="HeroCombatComponent.isAttacking"/>, <see cref="HeroCombatComponent.hitboxFired"/>, the attack
/// animation trigger and <see cref="WeaponHitboxActiveTag"/> up to date for stamina, visuals and melee contacts.
/// Replaces HeroAttackSystem and its duplicated player/AI paths.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HeroSkillRequestSystem))]
[UpdateAfter(typeof(HeroRespawnSystem))]
public partial class SkillExecutionSystem : SystemBase
{
    NativeList<SkillEvent> _fired;

    protected override void OnCreate()
    {
        RequireForUpdate<MatchStateComponent>();
        _fired = new NativeList<SkillEvent>(16, Allocator.Persistent);
    }

    protected override void OnDestroy() => _fired.Dispose();

    protected override void OnUpdate()
    {
        bool battle = SystemAPI.GetSingleton<MatchStateComponent>().currentState == MatchState.InBattle;
        float dt = SystemAPI.Time.DeltaTime;
        double now = SystemAPI.Time.ElapsedTime;

        foreach (var (skills, instance, combat, stamina, life, groups, entity) in SystemAPI
                     .Query<DynamicBuffer<HeroSkillElement>, RefRW<SkillInstanceComponent>, RefRW<HeroCombatComponent>,
                         RefRW<StaminaComponent>, RefRO<HeroLifeComponent>, DynamicBuffer<CooldownGroupElement>>()
                     .WithAll<SkillRequestComponent>()
                     .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                     .WithEntityAccess())
        {
            ref var inst = ref instance.ValueRW;
            ref var c = ref combat.ValueRW;
            bool requested = SystemAPI.IsComponentEnabled<SkillRequestComponent>(entity);
            if (requested) SystemAPI.SetComponentEnabled<SkillRequestComponent>(entity, false);

            if (!battle || !life.ValueRO.isAlive)
            {
                if (inst.active) End(ref inst, ref c, skills, now);
                PublishState(entity, inst, ref c);
                continue;
            }

            if (inst.active && Valid(skills, inst.skillIndex))
            {
                _fired.Clear();
                bool ended = SkillTimeline.Advance(ref skills[inst.skillIndex].skill.Value, ref inst, dt, _fired);
                Apply(entity, ref inst, ref c);
                if (ended) End(ref inst, ref c, skills, now);
            }
            else if (inst.active)
            {
                inst.active = false; // Its skill is gone (the hero was re-initialised).
            }

            if (requested)
                TryLaunch(entity, SystemAPI.GetComponent<SkillRequestComponent>(entity).slot, skills, ref inst, ref c,
                    ref stamina.ValueRW, groups, now);

            PublishState(entity, inst, ref c);
        }
    }

    void TryLaunch(Entity entity, SkillSlot slot, DynamicBuffer<HeroSkillElement> skills, ref SkillInstanceComponent inst,
        ref HeroCombatComponent combat, ref StaminaComponent stamina, DynamicBuffer<CooldownGroupElement> groups, double now)
    {
        int index = Find(skills, slot);
        bool busy = inst.active && Valid(skills, inst.skillIndex);
        var input = new SkillGateInput
        {
            inBattle = true,
            alive = true,
            immobilised = SystemAPI.HasComponent<MoveSpeedComponent>(entity) && SystemAPI.GetComponent<MoveSpeedComponent>(entity).value <= 0f,
            hasSkill = index >= 0,
            busy = busy,
            currentInterruptible = !busy || SkillTimeline.Interruptible(ref skills[inst.skillIndex].skill.Value, inst),
            exhausted = stamina.isExhausted,
            stamina = stamina.currentStamina,
        };
        if (index >= 0)
        {
            ref var candidate = ref skills[index].skill.Value;
            input.cooldownReady = CooldownUtility.IsReady(groups, candidate.cooldownGroup, now);
            input.staminaCost = candidate.staminaCost;
        }
        if (SkillGate.Evaluate(input) != SkillRejection.None) return;

        if (busy) End(ref inst, ref combat, skills, now); // Interrupt (L2-4 adds yield).
        ref var skill = ref skills[index].skill.Value;
        stamina.currentStamina -= skill.staminaCost;
        float scale = SystemAPI.HasComponent<CooldownScaleComponent>(entity) ? SystemAPI.GetComponent<CooldownScaleComponent>(entity).value : 1f;
        CooldownUtility.Start(groups, skill.cooldownGroup, skill.cooldown, scale, now);
        _fired.Clear();
        SkillTimeline.Start(ref skill, ref inst, index, _fired);
        Apply(entity, ref inst, ref combat);
    }

    /// <summary>Events of the core lifecycle; the effect events are applied from step 6 on.</summary>
    void Apply(Entity entity, ref SkillInstanceComponent inst, ref HeroCombatComponent combat)
    {
        foreach (var e in _fired)
        {
            switch (e.type)
            {
                case SkillEventType.Animation:
                    if (SystemAPI.HasComponent<HeroAnimationComponent>(entity))
                        SystemAPI.GetComponentRW<HeroAnimationComponent>(entity).ValueRW.triggerAttack = true;
                    break;
                case SkillEventType.HitboxOpen:
                    inst.hitboxOpen = true;
                    combat.hitboxFired = false;
                    break;
                case SkillEventType.HitboxClose:
                    inst.hitboxOpen = false;
                    break;
            }
        }
    }

    static void End(ref SkillInstanceComponent inst, ref HeroCombatComponent combat, DynamicBuffer<HeroSkillElement> skills, double now)
    {
        if (Valid(skills, inst.skillIndex)) inst.lastSkillId = skills[inst.skillIndex].skillId;
        inst.lastEndedAt = now;
        inst.active = false;
        inst.hitboxOpen = false;
        inst.phase = 0;
        inst.phaseTime = 0f;
        combat.hitboxFired = false;
    }

    void PublishState(Entity entity, in SkillInstanceComponent inst, ref HeroCombatComponent combat)
    {
        combat.isAttacking = inst.active;
        if (SystemAPI.HasComponent<WeaponHitboxActiveTag>(entity))
            SystemAPI.SetComponentEnabled<WeaponHitboxActiveTag>(entity, inst.active && inst.hitboxOpen && !combat.hitboxFired);
    }

    static int Find(DynamicBuffer<HeroSkillElement> skills, SkillSlot slot)
    {
        for (int i = 0; i < skills.Length; i++)
            if (skills[i].slot == slot && skills[i].skill.IsCreated) return i;
        return -1;
    }

    static bool Valid(DynamicBuffer<HeroSkillElement> skills, int index)
        => index >= 0 && index < skills.Length && skills[index].skill.IsCreated;
}

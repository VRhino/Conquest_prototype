using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Runs every hero's skills (Aprendizajes_Referencia.md L2-1, L2-2), the player's and the AI's alike: advances the
/// current instance through its phases and applies its events, then evaluates the pending request with
/// <see cref="SkillGate"/> and launches it (stamina and cooldown are charged at the start, as in the reference). A new
/// skill replaces the current one when the current phase is interruptible (<see cref="SkillRefresh.Interrupt"/>), or
/// parks it and restores it when it ends (<see cref="SkillRefresh.Yield"/>, L2-4). Death, the end of the battle, control
/// (a stun) and an <see cref="EndSkillsRequestComponent"/> end the current and the parked skill.
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
    /// <summary>Events of a parked skill: its clock runs, but nothing it would do happens while it is set aside.</summary>
    NativeList<SkillEvent> _discarded;
    /// <summary>Effect events of this frame, created as requests for <see cref="SkillEffectSystem"/> after the loop.</summary>
    NativeList<SkillEffectRequestComponent> _effects;

    protected override void OnCreate()
    {
        RequireForUpdate<MatchStateComponent>();
        _fired = new NativeList<SkillEvent>(16, Allocator.Persistent);
        _discarded = new NativeList<SkillEvent>(16, Allocator.Persistent);
        _effects = new NativeList<SkillEffectRequestComponent>(16, Allocator.Persistent);
    }

    protected override void OnDestroy()
    {
        _fired.Dispose();
        _discarded.Dispose();
        _effects.Dispose();
    }

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

            bool endRequested = SystemAPI.HasComponent<EndSkillsRequestComponent>(entity)
                && SystemAPI.IsComponentEnabled<EndSkillsRequestComponent>(entity);
            if (endRequested) SystemAPI.SetComponentEnabled<EndSkillsRequestComponent>(entity, false);
            if (!battle || !life.ValueRO.isAlive || endRequested || Immobilised(entity))
            {
                if (inst.active) End(ref inst, ref c, skills, now);
                ClearParked(entity);
                PublishState(entity, inst, ref c);
                continue;
            }

            AdvanceParked(entity, skills, dt);
            if (inst.active && Valid(skills, inst.skillIndex))
            {
                _fired.Clear();
                bool ended = SkillTimeline.Advance(ref skills[inst.skillIndex].skill.Value, ref inst, dt, _fired);
                Apply(entity, ref inst, ref c, skills[inst.skillIndex].skillId);
                if (ended)
                {
                    End(ref inst, ref c, skills, now);
                    RestoreParked(entity, ref inst);
                }
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

        foreach (var request in _effects)
            EntityManager.SetComponentData(EntityManager.CreateEntity(typeof(SkillEffectRequestComponent)), request);
        _effects.Clear();
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
            immobilised = Immobilised(entity),
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

        ref var skill = ref skills[index].skill.Value;
        if (busy)
        {
            ref var current = ref skills[inst.skillIndex].skill.Value;
            bool inFinal = current.phases[inst.phase].final;
            if (skill.refresh == SkillRefresh.Yield && !inFinal && TryPark(entity, inst)) inst.hitboxOpen = false;
            else End(ref inst, ref combat, skills, now);
        }
        stamina.currentStamina -= skill.staminaCost;
        float scale = SystemAPI.HasComponent<CooldownScaleComponent>(entity) ? SystemAPI.GetComponent<CooldownScaleComponent>(entity).value : 1f;
        CooldownUtility.Start(groups, skill.cooldownGroup, skill.cooldown, scale, now);
        _fired.Clear();
        SkillTimeline.Start(ref skill, ref inst, index, _fired);
        Apply(entity, ref inst, ref combat, skills[index].skillId);
    }

    /// <summary>Applies the lifecycle events and queues the effect ones with the caster's pose and team.</summary>
    void Apply(Entity entity, ref SkillInstanceComponent inst, ref HeroCombatComponent combat, int skillId)
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
                case SkillEventType.AreaDamage:
                case SkillEventType.ApplyModifier:
                case SkillEventType.AbsorbShield:
                case SkillEventType.Dash:
                    _effects.Add(Effect(entity, skillId, e));
                    break;
            }
        }
    }

    SkillEffectRequestComponent Effect(Entity caster, int skillId, in SkillEvent e)
    {
        var pose = SystemAPI.HasComponent<LocalTransform>(caster) ? SystemAPI.GetComponent<LocalTransform>(caster) : LocalTransform.Identity;
        float3 forward = math.mul(pose.Rotation, new float3(0f, 0f, 1f));
        forward.y = 0f;
        return new SkillEffectRequestComponent
        {
            caster = caster,
            skillId = skillId,
            effect = e,
            origin = pose.Position,
            forward = math.normalizesafe(forward, new float3(0f, 0f, 1f)),
            team = SystemAPI.HasComponent<TeamComponent>(caster) ? SystemAPI.GetComponent<TeamComponent>(caster).value : Team.None,
        };
    }

    bool Immobilised(Entity entity)
        => SystemAPI.HasComponent<MoveSpeedComponent>(entity) && SystemAPI.GetComponent<MoveSpeedComponent>(entity).value <= 0f;

    /// <summary>Sets the current skill aside, if the hero has no parked one yet (only one, as in the reference).</summary>
    bool TryPark(Entity entity, in SkillInstanceComponent inst)
    {
        if (!SystemAPI.HasComponent<ParkedSkillComponent>(entity)) return false;
        var parked = SystemAPI.GetComponentRW<ParkedSkillComponent>(entity);
        if (parked.ValueRO.active) return false;
        var copy = inst;
        copy.hitboxOpen = false;
        parked.ValueRW = new ParkedSkillComponent { active = true, instance = copy };
        return true;
    }

    void AdvanceParked(Entity entity, DynamicBuffer<HeroSkillElement> skills, float dt)
    {
        if (!SystemAPI.HasComponent<ParkedSkillComponent>(entity)) return;
        var parked = SystemAPI.GetComponentRW<ParkedSkillComponent>(entity);
        if (!parked.ValueRO.active) return;
        if (!Valid(skills, parked.ValueRO.instance.skillIndex)) { parked.ValueRW.active = false; return; }
        _discarded.Clear();
        if (SkillTimeline.Advance(ref skills[parked.ValueRO.instance.skillIndex].skill.Value, ref parked.ValueRW.instance, dt, _discarded))
            parked.ValueRW.active = false; // It ended while set aside.
    }

    void RestoreParked(Entity entity, ref SkillInstanceComponent inst)
    {
        if (!SystemAPI.HasComponent<ParkedSkillComponent>(entity)) return;
        var parked = SystemAPI.GetComponentRW<ParkedSkillComponent>(entity);
        if (!parked.ValueRO.active) return;
        var restored = parked.ValueRO.instance;
        restored.lastSkillId = inst.lastSkillId;
        restored.lastEndedAt = inst.lastEndedAt;
        restored.instanceId = inst.instanceId;
        inst = restored;
        parked.ValueRW.active = false;
    }

    void ClearParked(Entity entity)
    {
        if (SystemAPI.HasComponent<ParkedSkillComponent>(entity))
            SystemAPI.GetComponentRW<ParkedSkillComponent>(entity).ValueRW.active = false;
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

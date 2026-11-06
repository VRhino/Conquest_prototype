using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Runs every hero's skills (Aprendizajes_Referencia.md L2-1, L2-2), the player's and the AI's alike: advances the
/// current instance through its phases and applies its events, then evaluates the pending request with
/// <see cref="SkillGate"/> and launches it (stamina, cooldown, combo and kills (L2-16) are charged at the start, as in the reference). A new
/// skill replaces the current one when the current phase is interruptible (<see cref="SkillRefresh.Interrupt"/>), or
/// parks it and restores it when it ends (<see cref="SkillRefresh.Yield"/>, L2-4). Death, the end of the battle, control
/// (a stun) and an <see cref="EndSkillsRequestComponent"/> end the current and the parked skill.
/// Keeps <see cref="HeroCombatComponent.isAttacking"/>, <see cref="HeroCombatComponent.hitboxFired"/>, the attack
/// animation trigger and <see cref="WeaponHitboxActiveTag"/> up to date for stamina, visuals and melee contacts.
/// Replaces HeroAttackSystem and its duplicated player/AI paths. Squads run their abilities here too, with the same gate
/// and timeline and cooldown groups of their own (L2-9).
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
        float inputBuffer = SystemAPI.TryGetSingleton<HeroGameplayConfigComponent>(out var config) ? config.skillInputBuffer : 0f;

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
                ref var running = ref skills[inst.skillIndex].skill.Value;
                bool ended = SkillTimeline.Advance(ref running, ref inst, dt, _fired, AttackSpeed(entity));
                // Charge (L2-6): letting go of the key moves a waiting skill to its release phase.
                ushort held = SystemAPI.GetComponent<SkillRequestComponent>(entity).heldSlots;
                if (!ended && (held & SkillRequestComponent.Bit(skills[inst.skillIndex].slot)) == 0)
                    ended = SkillTimeline.Release(ref running, ref inst, _fired);
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
            {
                var request = SystemAPI.GetComponent<SkillRequestComponent>(entity);
                var rejection = TryLaunch(entity, request.slot, skills, ref inst, ref c, ref stamina.ValueRW, groups, now);
                // Input buffering (L2-3): a press the current phase turned down is kept for a moment.
                if (rejection == SkillRejection.CurrentNotInterruptible && now - request.requestedAt <= inputBuffer)
                    SystemAPI.SetComponentEnabled<SkillRequestComponent>(entity, true);
            }

            PublishState(entity, inst, ref c);
        }

        UpdateSquads(battle, dt, now);

        foreach (var request in _effects)
            EntityManager.SetComponentData(EntityManager.CreateEntity(typeof(SkillEffectRequestComponent)), request);
        _effects.Clear();
    }

    /// <summary>
    /// Squads launch their abilities through the same gate and timeline (L2-9; ref.: same lifecycle as the hero, cooldown
    /// per squad). A squad is alive while one of its units is; it has no stamina, no weapon hitbox and nothing to park.
    /// Its effects come from the centre of its living units, facing where its anchor faces.
    /// </summary>
    void UpdateSquads(bool battle, float dt, double now)
    {
        foreach (var (skills, instance, groups, units, entity) in SystemAPI
                     .Query<DynamicBuffer<HeroSkillElement>, RefRW<SkillInstanceComponent>, DynamicBuffer<CooldownGroupElement>,
                         DynamicBuffer<SquadUnitElement>>()
                     .WithAll<SkillRequestComponent>()
                     .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                     .WithEntityAccess())
        {
            ref var inst = ref instance.ValueRW;
            bool requested = SystemAPI.IsComponentEnabled<SkillRequestComponent>(entity);
            if (requested) SystemAPI.SetComponentEnabled<SkillRequestComponent>(entity, false);
            bool alive = SquadCentre(units, out float3 centre);
            if (!battle || !alive)
            {
                if (inst.active) EndInstance(ref inst, skills, now);
                continue;
            }
            var origin = new SquadOrigin { position = centre, forward = SquadForward(entity), team = TeamOf(entity) };

            if (inst.active && Valid(skills, inst.skillIndex))
            {
                _fired.Clear();
                ref var running = ref skills[inst.skillIndex].skill.Value;
                bool ended = SkillTimeline.Advance(ref running, ref inst, dt, _fired, SquadAttackSpeed(units));
                ushort held = SystemAPI.GetComponent<SkillRequestComponent>(entity).heldSlots;
                if (!ended && (held & SkillRequestComponent.Bit(skills[inst.skillIndex].slot)) == 0)
                    ended = SkillTimeline.Release(ref running, ref inst, _fired);
                QueueEffects(entity, origin, skills[inst.skillIndex].skillId, inst.instanceId);
                if (ended) EndInstance(ref inst, skills, now);
            }
            else if (inst.active)
            {
                inst.active = false;
            }

            if (!requested) continue;
            var slot = SystemAPI.GetComponent<SkillRequestComponent>(entity).slot;
            int index = Find(skills, slot);
            bool busy = inst.active && Valid(skills, inst.skillIndex);
            var input = new SkillGateInput
            {
                inBattle = true,
                alive = true,
                hasSkill = index >= 0,
                busy = busy,
                currentInterruptible = !busy || SkillTimeline.Interruptible(ref skills[inst.skillIndex].skill.Value, inst),
                cooldownReady = index >= 0 && CooldownUtility.IsReady(groups, skills[index].skill.Value.cooldownGroup, now),
            };
            if (SkillGate.Evaluate(input) != SkillRejection.None) continue;

            ref var skill = ref skills[index].skill.Value;
            if (busy) EndInstance(ref inst, skills, now);
            float scale = SystemAPI.HasComponent<CooldownScaleComponent>(entity) ? SystemAPI.GetComponent<CooldownScaleComponent>(entity).value : 1f;
            CooldownUtility.Start(groups, skill.cooldownGroup, skill.cooldown, scale, now);
            _fired.Clear();
            SkillTimeline.Start(ref skill, ref inst, index, _fired);
            QueueEffects(entity, origin, skills[index].skillId, inst.instanceId);
        }
    }

    struct SquadOrigin
    {
        public float3 position;
        public float3 forward;
        public Team team;
    }

    /// <summary>Centre of the squad's living units; false when none is left.</summary>
    bool SquadCentre(DynamicBuffer<SquadUnitElement> units, out float3 centre)
    {
        centre = float3.zero;
        int alive = 0;
        foreach (var unit in units)
        {
            if (!SystemAPI.Exists(unit.Value) || SystemAPI.HasComponent<IsDeadComponent>(unit.Value)
                || !SystemAPI.HasComponent<LocalTransform>(unit.Value)) continue;
            centre += SystemAPI.GetComponent<LocalTransform>(unit.Value).Position;
            alive++;
        }
        if (alive == 0) return false;
        centre /= alive;
        return true;
    }

    float3 SquadForward(Entity squad)
    {
        quaternion rotation = SystemAPI.HasComponent<SquadFormationAnchorComponent>(squad)
            ? SystemAPI.GetComponent<SquadFormationAnchorComponent>(squad).rotation
            : SystemAPI.HasComponent<LocalTransform>(squad) ? SystemAPI.GetComponent<LocalTransform>(squad).Rotation : quaternion.identity;
        float3 forward = math.mul(rotation, new float3(0f, 0f, 1f));
        forward.y = 0f;
        return math.normalizesafe(forward, new float3(0f, 0f, 1f));
    }

    Team TeamOf(Entity entity) => SystemAPI.HasComponent<TeamComponent>(entity) ? SystemAPI.GetComponent<TeamComponent>(entity).value : Team.None;

    /// <summary>A squad only has effect events: animation and hitbox events belong to a hero's weapon.</summary>
    void QueueEffects(Entity squad, in SquadOrigin origin, int skillId, uint instanceId)
    {
        foreach (var e in _fired)
            if (SkillEvents.IsEffect(e.type, true))
                _effects.Add(new SkillEffectRequestComponent
                {
                    caster = squad, skillId = skillId, instanceId = instanceId, effect = e, origin = origin.position,
                    forward = origin.forward, team = origin.team,
                });
    }

    SkillRejection TryLaunch(Entity entity, SkillSlot slot, DynamicBuffer<HeroSkillElement> skills, ref SkillInstanceComponent inst,
        ref HeroCombatComponent combat, ref StaminaComponent stamina, DynamicBuffer<CooldownGroupElement> groups, double now)
    {
        int index = slot == SkillSlot.Basic ? FindChainHit(skills, inst, now) : Find(skills, slot);
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
            input.comboCost = candidate.comboCost;
            input.killCost = candidate.killCost;
        }
        bool hasResources = SystemAPI.HasComponent<HeroResourcesComponent>(entity);
        var resources = hasResources ? SystemAPI.GetComponent<HeroResourcesComponent>(entity) : default;
        var comboTuning = ComboTuningNow();
        input.combo = HeroResources.Combo(resources, now, comboTuning);
        input.kills = resources.kills;
        var rejection = SkillGate.Evaluate(input);
        if (rejection != SkillRejection.None) return rejection;

        ref var skill = ref skills[index].skill.Value;
        if (busy)
        {
            ref var current = ref skills[inst.skillIndex].skill.Value;
            bool inFinal = current.phases[inst.phase].final;
            if (skill.refresh == SkillRefresh.Yield && !inFinal && TryPark(entity, inst)) inst.hitboxOpen = false;
            else End(ref inst, ref combat, skills, now);
        }
        stamina.currentStamina -= skill.staminaCost;
        if (hasResources && (skill.comboCost > 0f || skill.killCost > 0))
        {
            HeroResources.Pay(ref resources, now, comboTuning, skill.comboCost, skill.killCost);
            SystemAPI.SetComponent(entity, resources);
        }
        float scale = SystemAPI.HasComponent<CooldownScaleComponent>(entity) ? SystemAPI.GetComponent<CooldownScaleComponent>(entity).value : 1f;
        CooldownUtility.Start(groups, skill.cooldownGroup, skill.cooldown, scale, now);
        _fired.Clear();
        SkillTimeline.Start(ref skill, ref inst, index, _fired);
        Apply(entity, ref inst, ref combat, skills[index].skillId);
        return SkillRejection.None;
    }

    ComboTuning ComboTuningNow() => SystemAPI.TryGetSingleton<HeroGameplayConfigComponent>(out var config)
        ? config.combo.OrDefault() : ComboTuning.Default;

    /// <summary>
    /// Which hit of the basic attack chain a press launches (L2-3; ref.: the next of a chain is accepted if the previous
    /// one is its prerequisite or ended a moment ago): the next hit when the current one is in its final (recovery)
    /// phase or ended within its chain window, otherwise the first; none while the last hit of the chain is running.
    /// </summary>
    static int FindChainHit(DynamicBuffer<HeroSkillElement> skills, in SkillInstanceComponent inst, double now)
    {
        int first = Find(skills, SkillSlot.Basic);
        if (first < 0) return first;
        int nextId = 0;
        if (inst.active && Valid(skills, inst.skillIndex))
        {
            ref var current = ref skills[inst.skillIndex].skill.Value;
            // The last hit closes the chain: no basic attack until it ends (abilities can still interrupt it).
            if (skills[inst.skillIndex].slot == SkillSlot.Basic && current.nextSkillId == 0) return -1;
            if (current.phases[inst.phase].final) nextId = current.nextSkillId;
        }
        else
        {
            int last = FindById(skills, inst.lastSkillId);
            if (last >= 0 && now - inst.lastEndedAt <= skills[last].skill.Value.chainWindow) nextId = skills[last].skill.Value.nextSkillId;
        }
        int next = nextId != 0 ? FindById(skills, nextId) : -1;
        return next >= 0 ? next : first;
    }

    static int FindById(DynamicBuffer<HeroSkillElement> skills, int skillId)
    {
        if (skillId == 0) return -1;
        for (int i = 0; i < skills.Length; i++)
            if (skills[i].skillId == skillId && skills[i].skill.IsCreated) return i;
        return -1;
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
                default:
                    if (SkillEvents.IsEffect(e.type, false)) _effects.Add(Effect(entity, skillId, inst.instanceId, e));
                    break;
            }
        }
    }

    SkillEffectRequestComponent Effect(Entity caster, int skillId, uint instanceId, in SkillEvent e)
    {
        var pose = SystemAPI.HasComponent<LocalTransform>(caster) ? SystemAPI.GetComponent<LocalTransform>(caster) : LocalTransform.Identity;
        float3 forward = math.mul(pose.Rotation, new float3(0f, 0f, 1f));
        forward.y = 0f;
        return new SkillEffectRequestComponent
        {
            caster = caster,
            skillId = skillId,
            instanceId = instanceId,
            effect = e,
            origin = pose.Position,
            forward = math.normalizesafe(forward, new float3(0f, 0f, 1f)),
            team = SystemAPI.HasComponent<TeamComponent>(caster) ? SystemAPI.GetComponent<TeamComponent>(caster).value : Team.None,
        };
    }

    /// <summary>
    /// A squad's attack speed (L2-12): it has no stats of its own for it, so its skills follow the mean attack speed of its
    /// living units, the ones whose swings an effect on the squad speeds up.
    /// </summary>
    float SquadAttackSpeed(DynamicBuffer<SquadUnitElement> units)
    {
        float sum = 0f;
        int count = 0;
        foreach (var unit in units)
        {
            if (!SystemAPI.Exists(unit.Value) || SystemAPI.HasComponent<IsDeadComponent>(unit.Value)) continue;
            bool has = SystemAPI.HasComponent<CombatStatsComponent>(unit.Value);
            sum += CombatStatsComponent.AttackSpeedOf(has, has ? SystemAPI.GetComponent<CombatStatsComponent>(unit.Value) : default);
            count++;
        }
        return count > 0 ? sum / count : 1f;
    }

    /// <summary>The hero's attack speed (L2-12), also published for the animation.</summary>
    float AttackSpeed(Entity entity)
    {
        bool has = SystemAPI.HasComponent<CombatStatsComponent>(entity);
        float speed = CombatStatsComponent.AttackSpeedOf(has, has ? SystemAPI.GetComponent<CombatStatsComponent>(entity) : default);
        if (SystemAPI.HasComponent<HeroAnimationComponent>(entity))
            SystemAPI.GetComponentRW<HeroAnimationComponent>(entity).ValueRW.attackSpeed = speed;
        return speed;
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
        if (SkillTimeline.Advance(ref skills[parked.ValueRO.instance.skillIndex].skill.Value, ref parked.ValueRW.instance, dt, _discarded,
                AttackSpeed(entity)))
            parked.ValueRW.active = false; // It ended while set aside.
    }

    void RestoreParked(Entity entity, ref SkillInstanceComponent inst)
    {
        if (!SystemAPI.HasComponent<ParkedSkillComponent>(entity)) return;
        var parked = SystemAPI.GetComponentRW<ParkedSkillComponent>(entity);
        if (!parked.ValueRO.active) return;
        inst = SkillTimeline.Restore(parked.ValueRO.instance, inst);
        parked.ValueRW.active = false;
    }

    void ClearParked(Entity entity)
    {
        if (SystemAPI.HasComponent<ParkedSkillComponent>(entity))
            SystemAPI.GetComponentRW<ParkedSkillComponent>(entity).ValueRW.active = false;
    }

    static void End(ref SkillInstanceComponent inst, ref HeroCombatComponent combat, DynamicBuffer<HeroSkillElement> skills, double now)
    {
        EndInstance(ref inst, skills, now);
        combat.hitboxFired = false;
    }

    static void EndInstance(ref SkillInstanceComponent inst, DynamicBuffer<HeroSkillElement> skills, double now)
    {
        if (Valid(skills, inst.skillIndex)) inst.lastSkillId = skills[inst.skillIndex].skillId;
        inst.lastEndedAt = now;
        inst.active = false;
        inst.hitboxOpen = false;
        inst.phase = 0;
        inst.phaseTime = 0f;
    }

    void PublishState(Entity entity, in SkillInstanceComponent inst, ref HeroCombatComponent combat)
    {
        AttackSpeed(entity); // Published every frame, so a swing starts already at its speed (L2-12).
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

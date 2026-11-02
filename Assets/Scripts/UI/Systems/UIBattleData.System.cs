using Unity.Entities;

/// <summary>
/// Reads gameplay ECS components every frame and writes the results to the
/// UIHeroBattleDataComponent singleton so that UI MonoBehaviours (HUDController, etc.)
/// never need to create their own EntityQueries or modify ECS state.
///
/// This is the only system allowed to consume SquadChangeEvent entities.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class UIBattleDataSystem : SystemBase
{
    private Entity _singletonEntity;

    protected override void OnCreate()
    {
        RequireForUpdate<MatchStateComponent>();
        _singletonEntity = EntityManager.CreateEntity(typeof(UIHeroBattleDataComponent));
    }

    protected override void OnUpdate()
    {
        var data = new UIHeroBattleDataComponent();

        // ── Hero vitals ───────────────────────────────────────────────────────
        foreach (var (health, stamina) in
                 SystemAPI.Query<RefRO<HeroHealthComponent>, RefRO<StaminaComponent>>()
                          .WithAll<IsLocalPlayer>())
        {
            data.currentHealth  = health.ValueRO.currentHealth;
            data.maxHealth      = health.ValueRO.maxHealth;
            data.currentStamina = stamina.ValueRO.currentStamina;
            data.maxStamina     = stamina.ValueRO.maxStamina;
            break; // only one local player
        }
        foreach (var life in SystemAPI.Query<RefRO<HeroLifeComponent>>().WithAll<IsLocalPlayer>())
        {
            data.heroAlive = life.ValueRO.isAlive;
            break;
        }

        // ── Skill bar: the hero's slots, then its active squad's ─────────────────
        double now = SystemAPI.Time.ElapsedTime;
        data.skills.Length = UIHeroBattleDataComponent.SkillSlotCount;
        foreach (var (skills, groups) in
                 SystemAPI.Query<DynamicBuffer<HeroSkillElement>, DynamicBuffer<CooldownGroupElement>>()
                          .WithAll<IsLocalPlayer>())
        {
            WriteSkills(ref data.skills, skills, groups, now, SkillSlot.Q, SkillSlot.ActivePerk2);
            break;
        }
        foreach (var (skills, groups) in
                 SystemAPI.Query<DynamicBuffer<HeroSkillElement>, DynamicBuffer<CooldownGroupElement>>()
                          .WithAll<IsLocalSquadActive>())
        {
            WriteSkills(ref data.skills, skills, groups, now, SkillSlot.Squad1, SkillSlot.Squad3);
            break;
        }

        // ── Capture zone ──────────────────────────────────────────────────────
        if (SystemAPI.TryGetSingleton<LocalHeroCaptureStateComponent>(out var captureState))
        {
            data.isInCaptureZone = captureState.isInZone;
            data.captureProgress = captureState.captureProgress;
        }

        // ── Squad status ──────────────────────────────────────────────────────
        foreach (var status in
                 SystemAPI.Query<RefRO<SquadStatusComponent>>()
                          .WithAll<IsLocalSquadActive>())
        {
            data.aliveUnits = status.ValueRO.aliveUnits;
            data.totalUnits = status.ValueRO.totalUnits;
            break;
        }
        foreach (var formation in SystemAPI.Query<RefRO<FormationComponent>>().WithAll<IsLocalSquadActive>())
        {
            data.currentFormation = formation.ValueRO.currentFormation;
            break;
        }

        // ── Squad change event (consume here — never from UI) ─────────────────
        foreach (var (evt, entity) in
                 SystemAPI.Query<RefRO<SquadChangeEvent>>().WithEntityAccess())
        {
            data.squadChangedThisFrame = true;
            data.newSquadId            = evt.ValueRO.newSquadId;
            EntityManager.DestroyEntity(entity);
            break;
        }

        SystemAPI.SetComponent(_singletonEntity, data);
    }

    /// <summary>Writes the skills in slots <paramref name="first"/>..<paramref name="last"/> with their cooldown.</summary>
    static void WriteSkills(ref Unity.Collections.FixedList128Bytes<UISkillSlotData> slots, DynamicBuffer<HeroSkillElement> skills,
        DynamicBuffer<CooldownGroupElement> groups, double now, SkillSlot first, SkillSlot last)
    {
        foreach (var skill in skills)
        {
            if (skill.slot < first || skill.slot > last || !skill.skill.IsCreated) continue;
            int group = skill.skill.Value.cooldownGroup;
            slots[skill.slot - SkillSlot.Q] = new UISkillSlotData
            {
                skillId = skill.skillId,
                cooldown = CooldownUtility.Fraction(groups, group, now),
                remaining = CooldownUtility.Remaining(groups, group, now)
            };
        }
    }
}

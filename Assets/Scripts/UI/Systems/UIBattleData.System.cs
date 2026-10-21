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

        // ── Skill cooldowns ───────────────────────────────────────────────────
        double now = SystemAPI.Time.ElapsedTime;
        foreach (var (skills, groups) in
                 SystemAPI.Query<DynamicBuffer<HeroSkillElement>, DynamicBuffer<CooldownGroupElement>>()
                          .WithAll<IsLocalPlayer>())
        {
            foreach (var skill in skills)
            {
                int index = (int)skill.slot - (int)SkillSlot.Q;
                if (index < 0 || index > 3 || !skill.skill.IsCreated) continue;
                data.skillCooldown[index] = CooldownUtility.Fraction(groups, skill.skill.Value.cooldownGroup, now);
            }
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
}

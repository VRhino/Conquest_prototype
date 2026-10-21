using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Single writer of the derived stats (Aprendizajes_Referencia.md T1): recomposes them from <see cref="StatBaseElement"/>
/// and <see cref="StatModifier"/> with <see cref="StatMath.Resolve"/> when <see cref="ModifiersDirty"/> is enabled or a
/// modifier expires, and writes every target component the entity has:
/// <list type="bullet">
/// <item><see cref="HealthComponent"/>, <see cref="HeroHealthComponent"/> and <see cref="StaminaComponent"/> maxima (the current
/// value keeps its fraction). A hero with <see cref="HeroStatFormulaComponent"/> derives them from its attributes.</item>
/// <item><see cref="DefenseComponent"/>, <see cref="UnitStatsComponent"/> (health, speed, defense, damage; its weapon
/// penetration and identity fields stay as the spawner wrote them), <see cref="MoveSpeedComponent"/> and
/// <see cref="CooldownScaleComponent"/> of heroes, and
/// <see cref="CritStatsComponent"/>.</item>
/// <item><see cref="DamageProfileComponent"/> (damage and penetration; a squad's units share their squad's profile
/// entity, so it carries its own stats). <see cref="PenetrationComponent"/> only on entities without a profile, so
/// penetration is never counted twice.</item>
/// </list>
/// Expired modifiers are removed here.
/// Runs after <see cref="UnitStatScalingSystem"/>, so a unit spawned or levelled this frame has its stats before anything
/// reads them. It cannot also be before <see cref="DamageCalculationSystem"/>: damage already runs before respawn and
/// spawning, and Entities hangs on an ordering cycle instead of reporting it (SystemOrderTests guards this). A modifier
/// added during the frame is seen by damage on the next one.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitStatScalingSystem))]
public partial class StatResolveSystem : SystemBase
{
    protected override void OnUpdate()
    {
        double now = SystemAPI.Time.ElapsedTime;
        foreach (var (bases, modifiers, expiry, entity) in SystemAPI
                     .Query<DynamicBuffer<StatBaseElement>, DynamicBuffer<StatModifier>, RefRW<StatModifierExpiry>>()
                     .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                     .WithEntityAccess())
        {
            bool dirty = SystemAPI.HasComponent<ModifiersDirty>(entity) && SystemAPI.IsComponentEnabled<ModifiersDirty>(entity);
            bool expired = expiry.ValueRO.next > 0 && expiry.ValueRO.next <= now;
            if (!dirty && !expired) continue;

            for (int i = modifiers.Length - 1; i >= 0; i--)
                if (StatMath.IsExpired(modifiers[i], now)) modifiers.RemoveAt(i);
            var mods = modifiers.AsNativeArray();
            Write(entity, bases, mods, now);
            expiry.ValueRW.next = StatMath.NextExpiry(mods, now);
            if (SystemAPI.HasComponent<ModifiersDirty>(entity))
                SystemAPI.SetComponentEnabled<ModifiersDirty>(entity, false);
        }
    }

    void Write(Entity entity, DynamicBuffer<StatBaseElement> b, NativeArray<StatModifier> m, double now)
    {
        var stats = new Resolved { bases = b, modifiers = m, now = now };
        if (SystemAPI.HasComponent<HeroStatFormulaComponent>(entity))
            stats.UseHeroFormula(SystemAPI.GetComponent<HeroStatFormulaComponent>(entity));

        if (SystemAPI.HasComponent<HealthComponent>(entity))
        {
            var health = SystemAPI.GetComponentRW<HealthComponent>(entity);
            float max = stats.Get(StatId.MaxHealth);
            float fraction = health.ValueRO.maxHealth > 0f ? math.saturate(health.ValueRO.currentHealth / health.ValueRO.maxHealth) : 1f;
            health.ValueRW.maxHealth = max;
            health.ValueRW.currentHealth = max * fraction;
        }
        if (SystemAPI.HasComponent<HeroHealthComponent>(entity))
        {
            var health = SystemAPI.GetComponentRW<HeroHealthComponent>(entity);
            float max = stats.Get(StatId.MaxHealth);
            float fraction = health.ValueRO.maxHealth > 0f ? math.saturate(health.ValueRO.currentHealth / health.ValueRO.maxHealth) : 1f;
            health.ValueRW.maxHealth = max;
            health.ValueRW.currentHealth = max * fraction;
        }
        if (SystemAPI.HasComponent<StaminaComponent>(entity))
        {
            var stamina = SystemAPI.GetComponentRW<StaminaComponent>(entity);
            float max = stats.Get(StatId.MaxStamina);
            float fraction = stamina.ValueRO.maxStamina > 0f ? math.saturate(stamina.ValueRO.currentStamina / stamina.ValueRO.maxStamina) : 1f;
            stamina.ValueRW.maxStamina = max;
            stamina.ValueRW.currentStamina = max * fraction;
        }
        if (SystemAPI.HasComponent<DefenseComponent>(entity))
            SystemAPI.SetComponent(entity, new DefenseComponent
            {
                bluntDefense = stats.Get(StatId.BluntDefense),
                slashDefense = stats.Get(StatId.SlashDefense),
                pierceDefense = stats.Get(StatId.PierceDefense),
            });
        bool hasProfile = SystemAPI.HasComponent<DamageProfileComponent>(entity);
        if (hasProfile)
            SystemAPI.SetComponent(entity, new DamageProfileComponent
            {
                bluntDamage = stats.Get(StatId.BluntDamage),
                slashingDamage = stats.Get(StatId.SlashDamage),
                piercingDamage = stats.Get(StatId.PierceDamage),
                bluntPenetration = stats.Get(StatId.BluntPenetration),
                slashingPenetration = stats.Get(StatId.SlashPenetration),
                piercingPenetration = stats.Get(StatId.PiercePenetration),
            });
        else if (SystemAPI.HasComponent<PenetrationComponent>(entity))
            SystemAPI.SetComponent(entity, new PenetrationComponent
            {
                bluntPenetration = stats.Get(StatId.BluntPenetration),
                slashPenetration = stats.Get(StatId.SlashPenetration),
                piercePenetration = stats.Get(StatId.PiercePenetration),
            });
        if (SystemAPI.HasComponent<CritStatsComponent>(entity))
            SystemAPI.SetComponent(entity, new CritStatsComponent
            {
                critValue = stats.Get(StatId.CritValue),
                critParameter = stats.Get(StatId.CritParameter),
                critDamageValue = stats.Get(StatId.CritDamageValue),
                critDamageParameter = stats.Get(StatId.CritDamageParameter),
                critDamageRatio = stats.Get(StatId.CritDamageRatio),
                antiCritValue = stats.Get(StatId.AntiCritValue),
                antiCritParameter = stats.Get(StatId.AntiCritParameter),
            });
        if (SystemAPI.HasComponent<MoveSpeedComponent>(entity))
            SystemAPI.SetComponent(entity, new MoveSpeedComponent { value = stats.Get(StatId.MoveSpeed) });
        if (SystemAPI.HasComponent<CooldownScaleComponent>(entity))
            SystemAPI.SetComponent(entity, new CooldownScaleComponent { value = stats.Get(StatId.CooldownScale) });
        if (SystemAPI.HasComponent<UnitStatsComponent>(entity))
        {
            var s = SystemAPI.GetComponentRW<UnitStatsComponent>(entity);
            s.ValueRW.health = stats.Get(StatId.MaxHealth);
            s.ValueRW.speed = stats.Get(StatId.MoveSpeed);
            s.ValueRW.bluntDefense = stats.Get(StatId.BluntDefense);
            s.ValueRW.slashingDefense = stats.Get(StatId.SlashDefense);
            s.ValueRW.piercingDefense = stats.Get(StatId.PierceDefense);
            s.ValueRW.bluntDamage = stats.Get(StatId.BluntDamage);
            s.ValueRW.slashingDamage = stats.Get(StatId.SlashDamage);
            s.ValueRW.piercingDamage = stats.Get(StatId.PierceDamage);
        }
    }

    /// <summary>
    /// The stats of one entity: base and modifiers through <see cref="StatMath.Resolve"/>. On a hero the class formula
    /// adds to each base what its resolved attributes give, so equipment and perks on an attribute reach every stat it
    /// feeds.
    /// </summary>
    struct Resolved
    {
        public DynamicBuffer<StatBaseElement> bases;
        public NativeArray<StatModifier> modifiers;
        public double now;
        bool hero;
        HeroStatFormulaComponent formula;
        float strength, dexterity, armor, vitality;

        public void UseHeroFormula(in HeroStatFormulaComponent heroFormula)
        {
            strength = Get(StatId.Strength);
            dexterity = Get(StatId.Dexterity);
            armor = Get(StatId.Armor);
            vitality = Get(StatId.Vitality);
            formula = heroFormula;
            hero = true;
        }

        public float Get(StatId stat)
        {
            float baseValue = StatMath.Base(bases, stat);
            if (hero) baseValue += HeroStatFormula.Derived(formula, stat, strength, dexterity, armor, vitality);
            return math.max(0f, StatMath.Resolve(baseValue, modifiers, stat, now));
        }
    }
}

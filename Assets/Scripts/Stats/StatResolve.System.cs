using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Single writer of the derived stats (Aprendizajes_Referencia.md T1): recomposes them from <see cref="StatBaseElement"/>
/// and <see cref="StatModifier"/> with <see cref="StatMath.Resolve"/> when <see cref="ModifiersDirty"/> is enabled or a
/// modifier expires, and writes every target component the entity has:
/// <list type="bullet">
/// <item><see cref="HealthComponent"/> and <see cref="StaminaComponent"/> maxima (the current value keeps its fraction).</item>
/// <item><see cref="DefenseComponent"/>, <see cref="UnitStatsComponent"/> (health, speed, defense, damage; its weapon
/// penetration and identity fields stay as the spawner wrote them).</item>
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

    float Stat(DynamicBuffer<StatBaseElement> bases, NativeArray<StatModifier> mods, StatId stat, double now)
        => math.max(0f, StatMath.Resolve(StatMath.Base(bases, stat), mods, stat, now));

    void Write(Entity entity, DynamicBuffer<StatBaseElement> b, NativeArray<StatModifier> m, double now)
    {
        if (SystemAPI.HasComponent<HealthComponent>(entity))
        {
            var health = SystemAPI.GetComponentRW<HealthComponent>(entity);
            float max = Stat(b, m, StatId.MaxHealth, now);
            float fraction = health.ValueRO.maxHealth > 0f ? math.saturate(health.ValueRO.currentHealth / health.ValueRO.maxHealth) : 1f;
            health.ValueRW.maxHealth = max;
            health.ValueRW.currentHealth = max * fraction;
        }
        if (SystemAPI.HasComponent<StaminaComponent>(entity))
        {
            var stamina = SystemAPI.GetComponentRW<StaminaComponent>(entity);
            float max = Stat(b, m, StatId.MaxStamina, now);
            float fraction = stamina.ValueRO.maxStamina > 0f ? math.saturate(stamina.ValueRO.currentStamina / stamina.ValueRO.maxStamina) : 1f;
            stamina.ValueRW.maxStamina = max;
            stamina.ValueRW.currentStamina = max * fraction;
        }
        if (SystemAPI.HasComponent<DefenseComponent>(entity))
            SystemAPI.SetComponent(entity, new DefenseComponent
            {
                bluntDefense = Stat(b, m, StatId.BluntDefense, now),
                slashDefense = Stat(b, m, StatId.SlashDefense, now),
                pierceDefense = Stat(b, m, StatId.PierceDefense, now),
            });
        bool hasProfile = SystemAPI.HasComponent<DamageProfileComponent>(entity);
        if (hasProfile)
            SystemAPI.SetComponent(entity, new DamageProfileComponent
            {
                bluntDamage = Stat(b, m, StatId.BluntDamage, now),
                slashingDamage = Stat(b, m, StatId.SlashDamage, now),
                piercingDamage = Stat(b, m, StatId.PierceDamage, now),
                bluntPenetration = Stat(b, m, StatId.BluntPenetration, now),
                slashingPenetration = Stat(b, m, StatId.SlashPenetration, now),
                piercingPenetration = Stat(b, m, StatId.PiercePenetration, now),
            });
        else if (SystemAPI.HasComponent<PenetrationComponent>(entity))
            SystemAPI.SetComponent(entity, new PenetrationComponent
            {
                bluntPenetration = Stat(b, m, StatId.BluntPenetration, now),
                slashPenetration = Stat(b, m, StatId.SlashPenetration, now),
                piercePenetration = Stat(b, m, StatId.PiercePenetration, now),
            });
        if (SystemAPI.HasComponent<UnitStatsComponent>(entity))
        {
            var s = SystemAPI.GetComponentRW<UnitStatsComponent>(entity);
            s.ValueRW.health = Stat(b, m, StatId.MaxHealth, now);
            s.ValueRW.speed = Stat(b, m, StatId.MoveSpeed, now);
            s.ValueRW.bluntDefense = Stat(b, m, StatId.BluntDefense, now);
            s.ValueRW.slashingDefense = Stat(b, m, StatId.SlashDefense, now);
            s.ValueRW.piercingDefense = Stat(b, m, StatId.PierceDefense, now);
            s.ValueRW.bluntDamage = Stat(b, m, StatId.BluntDamage, now);
            s.ValueRW.slashingDamage = Stat(b, m, StatId.SlashDamage, now);
            s.ValueRW.piercingDamage = Stat(b, m, StatId.PierceDamage, now);
        }
    }
}

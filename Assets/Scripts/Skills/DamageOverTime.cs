using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Damage over time on one target (Aprendizajes_Referencia.md L2-11; ref.: an effect with its own period and duration):
/// every <see cref="period"/> seconds until <see cref="endsAt"/>, a hit of the caster's profile × <see cref="multiplier"/>
/// × <see cref="stacks"/>. Written by <see cref="SkillEffectSystem"/> and <see cref="DamageOverTimeSystem"/>.
/// </summary>
public struct DamageOverTimeElement : IBufferElementData
{
    public Entity caster;
    public int skillId;
    public Team team;
    /// <summary>The caster's damage profile when the effect was applied, so the burn outlives the caster.</summary>
    public DamageProfileComponent profile;
    public float multiplier;
    public float period;
    public double nextTick;
    public double endsAt;
    public byte stacks;
}

/// <summary>Pure rules of damage over time (L2-11).</summary>
public static class DamageOverTime
{
    /// <summary>
    /// Applies a burn from (<paramref name="caster"/>, <paramref name="skillId"/>): a new one ticks first one period from
    /// now; a live one follows <paramref name="reapply"/> (L2-10), keeping its tick rhythm.
    /// </summary>
    public static void Apply(DynamicBuffer<DamageOverTimeElement> buffer, in DamageOverTimeElement added, int maxStacks,
        EffectReapply reapply, double now)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            var d = buffer[i];
            if (d.caster != added.caster || d.skillId != added.skillId || d.endsAt < now) continue;
            if (reapply == EffectReapply.Ignore) return;
            d.stacks = reapply == EffectReapply.Stack
                ? (byte)Unity.Mathematics.math.min(Unity.Mathematics.math.max(1, (int)d.stacks) + 1, Unity.Mathematics.math.max(1, maxStacks))
                : (byte)1;
            d.multiplier = added.multiplier;
            d.profile = added.profile;
            d.endsAt = added.endsAt;
            buffer[i] = d;
            return;
        }
        var fresh = added;
        fresh.stacks = 1;
        fresh.nextTick = now + added.period;
        buffer.Add(fresh);
    }

    /// <summary>
    /// Ticks due by <paramref name="now"/> (at most one per period, none after the end); advances the next tick. Returns
    /// how many hits to deal.
    /// </summary>
    public static int DueTicks(ref DamageOverTimeElement d, double now)
    {
        if (d.period <= 0f) return 0;
        int ticks = 0;
        while (d.nextTick <= now && d.nextTick <= d.endsAt)
        {
            ticks++;
            d.nextTick += d.period;
        }
        return ticks;
    }
}

/// <summary>
/// Deals the due ticks of every damage over time as <see cref="PendingDamageEvent"/>s (form Ability, the skill's id), so
/// they go through the same formula, shields and floor as any hit, and drops the ones that ended (L2-11).
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(DamageCalculationSystem))]
public partial class DamageOverTimeSystem : SystemBase
{
    protected override void OnUpdate()
    {
        double now = SystemAPI.Time.ElapsedTime;
        uint tick = CombatRandom.Tick(now);
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (_, target) in SystemAPI.Query<DynamicBuffer<DamageOverTimeElement>>().WithNone<IsDeadComponent>()
                     .WithEntityAccess())
        {
            var burns = SystemAPI.GetBuffer<DamageOverTimeElement>(target);
            for (int i = burns.Length - 1; i >= 0; i--)
            {
                var d = burns[i];
                int ticks = DamageOverTime.DueTicks(ref d, now);
                for (int t = 0; t < ticks; t++)
                {
                    Entity hit = ecb.CreateEntity();
                    ecb.AddComponent(hit, new PendingDamageEvent
                    {
                        destroyEntityAfterProcessing = true,
                        target = target,
                        damageSource = d.caster,
                        damageProfile = d.caster,
                        hasInlineDamageProfile = true,
                        inlineDamageProfile = d.profile,
                        sourceTeam = d.team,
                        category = DamageCategory.Ability,
                        multiplier = d.multiplier * (d.stacks > 1 ? d.stacks : 1),
                        hitType = HitType.Body,
                        hitForm = HitForm.Ability,
                        skillId = d.skillId,
                        varianceOffset = CombatRandom.RollVariance(tick, d.caster, target, d.profile.traits.damageVariance),
                    });
                }
                if (d.nextTick > d.endsAt) burns.RemoveAt(i);
                else burns[i] = d;
            }
        }
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}

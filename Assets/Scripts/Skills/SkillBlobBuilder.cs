using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Builds <see cref="SkillBlob"/>s (L2-2): from a <see cref="HeroAbility"/> asset, or for the basic attack from the
/// hero's weapon timings. An ability without phases gets windup, active and recovery; its area and damage fields become
/// an area hit at the start of the active phase, so the existing assets keep their meaning.
/// </summary>
public static class SkillBlobBuilder
{
    /// <summary>Phases of an ability that declares none, in seconds.</summary>
    public const float DefaultWindup = 0.25f;
    public const float DefaultActive = 0.35f;
    public const float DefaultRecovery = 0.4f;

    /// <summary>Ids of the basic attack chain; ability ids are name hashes, so they never collide with these.</summary>
    public const int BasicAttackId = 1;

    /// <summary>Cooldown group of a slot when the skill declares none: one per key.</summary>
    public static int DefaultGroup(SkillSlot slot) => (int)slot + 1;

    public static SkillSlot SlotOf(HeroAbilityCategory category) => category switch
    {
        HeroAbilityCategory.Q => SkillSlot.Q,
        HeroAbilityCategory.E => SkillSlot.E,
        HeroAbilityCategory.R => SkillSlot.R,
        _ => SkillSlot.Ultimate,
    };

    /// <summary>Stable id of an ability (FNV-1a of its name), so saves and the network can name it.</summary>
    public static int IdOf(HeroAbility ability)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in ability.abilityName ?? ability.name)
                hash = (hash ^ c) * 16777619;
            return (int)(hash | 0x10000u); // Keeps it away from the small basic-attack ids.
        }
    }

    public static BlobAssetReference<SkillBlob> Build(HeroAbility ability)
    {
        var slot = SlotOf(ability.category);
        var phases = ability.phases != null && ability.phases.Length > 0 ? ability.phases : DefaultPhases();
        var events = ability.phases != null && ability.phases.Length > 0 && ability.events != null
            ? ability.events
            : DefaultEvents(ability);
        return Create(IdOf(ability), ability.staminaCost, ability.cooldown,
            ability.cooldownGroup != 0 ? ability.cooldownGroup : DefaultGroup(slot), ability.refresh, phases, events);
    }

    /// <summary>
    /// The basic attack: windup until the strike opens, the strike (not interruptible) with the hitbox open, and the
    /// recovery until the animation ends. Hit <paramref name="hit"/> of a chain of <paramref name="hits"/> (L2-3) points
    /// to the next one; only the first and the last start the cooldown, so the chain is not blocked by it.
    /// </summary>
    public static BlobAssetReference<SkillBlob> BuildBasicAttack(in HeroCombatComponent combat, float staminaCost, float cooldown,
        int hit = 0, int hits = 1, float chainWindow = 0f)
    {
        float strikeEnd = combat.strikeWindowStart + combat.strikeWindowDuration;
        var phases = new[]
        {
            new SkillPhase { duration = combat.strikeWindowStart, interruptible = true },
            new SkillPhase { duration = combat.strikeWindowDuration },
            new SkillPhase { duration = combat.attackAnimationDuration - strikeEnd, interruptible = true, final = true },
        };
        var events = new[]
        {
            new SkillEvent { phase = 0, type = SkillEventType.Animation },
            new SkillEvent { phase = 1, type = SkillEventType.HitboxOpen },
            new SkillEvent { phase = 2, type = SkillEventType.HitboxClose },
        };
        bool last = hit >= hits - 1;
        bool cooled = hit == 0 || last;
        var blob = Create(BasicAttackId + hit, staminaCost, cooled ? cooldown : 0f, cooled ? DefaultGroup(SkillSlot.Basic) : 0,
            SkillRefresh.Interrupt, phases, events, last ? 0 : BasicAttackId + hit + 1, chainWindow);
        return blob;
    }

    /// <summary>Length of the single phase of an active perk: its effects happen at the start.</summary>
    public const float PerkCastDuration = 0.1f;

    /// <summary>
    /// An active perk (L7-4) as a skill: one short phase that applies each effect as a timed modifier, with the perk's
    /// cooldown. It is <see cref="SkillRefresh.Yield"/>: it parks the current skill instead of cutting it.
    /// </summary>
    public static BlobAssetReference<SkillBlob> BuildPerk(HeroPerk perk, SkillSlot slot)
    {
        var phases = new[] { new SkillPhase { duration = PerkCastDuration, interruptible = true, final = true } };
        var events = new System.Collections.Generic.List<SkillEvent>();
        if (perk.effects != null)
            foreach (var e in perk.effects)
            {
                if (!TryTarget(e.target, out var target)) continue;
                events.Add(new SkillEvent
                {
                    type = SkillEventType.ApplyModifier, target = target, radius = e.radius,
                    stat = e.stat, op = e.op, value = e.value, duration = e.duration > 0f ? e.duration : perk.duration,
                });
            }
        return Create(PerkId(perk), 0f, perk.cooldown, DefaultGroup(slot), SkillRefresh.Yield, phases, events.ToArray());
    }

    /// <summary>Id of a perk as a skill: its perk id in a range of its own.</summary>
    public static int PerkId(HeroPerk perk) => 0x08000000 | perk.perkID;

    static bool TryTarget(PerkTarget target, out SkillTarget skillTarget)
    {
        switch (target)
        {
            case PerkTarget.Self: skillTarget = SkillTarget.Self; return true;
            case PerkTarget.OwnSquad: skillTarget = SkillTarget.OwnSquad; return true;
            case PerkTarget.AlliesInRadius: skillTarget = SkillTarget.AlliesInRadius; return true;
            case PerkTarget.EnemiesInRadius: skillTarget = SkillTarget.EnemiesInRadius; return true;
            default: skillTarget = default; return false; // A hit target needs a hit.
        }
    }

    static SkillPhase[] DefaultPhases() => new[]
    {
        new SkillPhase { duration = DefaultWindup, interruptible = true },
        new SkillPhase { duration = DefaultActive },
        new SkillPhase { duration = DefaultRecovery, interruptible = true, final = true },
    };

    static SkillEvent[] DefaultEvents(HeroAbility ability)
    {
        var animation = new SkillEvent { phase = 0, type = SkillEventType.Animation };
        if (ability.areaRadius <= 0f || ability.damageMultiplier <= 0f) return new[] { animation };
        return new[]
        {
            animation,
            new SkillEvent { phase = 1, type = SkillEventType.AreaDamage, radius = ability.areaRadius, value = ability.damageMultiplier },
        };
    }

    static BlobAssetReference<SkillBlob> Create(int id, float staminaCost, float cooldown, int group, SkillRefresh refresh,
        SkillPhase[] phases, SkillEvent[] events, int nextSkillId = 0, float chainWindow = 0f)
    {
        using var builder = new BlobBuilder(Allocator.Temp);
        ref var root = ref builder.ConstructRoot<SkillBlob>();
        root.id = id;
        root.staminaCost = staminaCost;
        root.cooldown = cooldown;
        root.cooldownGroup = group;
        root.refresh = refresh;
        root.nextSkillId = nextSkillId;
        root.chainWindow = chainWindow;
        var p = builder.Allocate(ref root.phases, phases.Length);
        for (int i = 0; i < phases.Length; i++) p[i] = phases[i];
        var e = builder.Allocate(ref root.events, events.Length);
        for (int i = 0; i < events.Length; i++) e[i] = events[i];
        return builder.CreateBlobAssetReference<SkillBlob>(Allocator.Persistent);
    }
}

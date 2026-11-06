/// <summary>Why the gate turned a skill down (for the HUD, the AI and, later, the server).</summary>
public enum SkillRejection : byte
{
    None,
    NotInBattle,
    Dead,
    /// <summary>Under control (a shield-break stun): the reference forbids skills by default while controlled.</summary>
    Immobilised,
    NoSkill,
    /// <summary>The current skill is in a phase that is not interruptible (skill-lifecycle.md §3.1, code 1).</summary>
    CurrentNotInterruptible,
    /// <summary>A chained skill outside its chain window (§3.1, code 30).</summary>
    NotChained,
    OnCooldown,
    Exhausted,
    NotEnoughStamina,
    /// <summary>Not enough combo (L2-16; ref. not_enough_combo_point).</summary>
    NotEnoughCombo,
    /// <summary>Not enough soldier kills (L2-16; ref. not_enough_kill_soldier_count).</summary>
    NotEnoughKills,
}

/// <summary>Everything the gate looks at, gathered by the caller (no entity lookups inside).</summary>
public struct SkillGateInput
{
    public bool inBattle;
    public bool alive;
    public bool immobilised;
    public bool hasSkill;
    public bool busy;
    public bool currentInterruptible;
    public bool requiresChain;
    public bool chainSatisfied;
    public bool cooldownReady;
    public bool exhausted;
    public float stamina;
    public float staminaCost;
    /// <summary>The caster's combo now and the skill's cost (L2-16).</summary>
    public float combo;
    public float comboCost;
    public int kills;
    public int killCost;
}

/// <summary>
/// The single launch gate (Aprendizajes_Referencia.md L2-1): every skill, from the player or the AI, passes the same
/// checks in a fixed order, as in the reference: life and battle, control, the current skill's phase, chain, cooldown,
/// stamina, combo and kills (L2-16).
/// </summary>
public static class SkillGate
{
    public static SkillRejection Evaluate(in SkillGateInput i)
    {
        if (!i.inBattle) return SkillRejection.NotInBattle;
        if (!i.alive) return SkillRejection.Dead;
        if (i.immobilised) return SkillRejection.Immobilised;
        if (!i.hasSkill) return SkillRejection.NoSkill;
        if (i.busy && !i.currentInterruptible) return SkillRejection.CurrentNotInterruptible;
        if (i.requiresChain && !i.chainSatisfied) return SkillRejection.NotChained;
        if (!i.cooldownReady) return SkillRejection.OnCooldown;
        if (i.exhausted) return SkillRejection.Exhausted;
        if (i.stamina < i.staminaCost) return SkillRejection.NotEnoughStamina;
        if (i.combo < i.comboCost) return SkillRejection.NotEnoughCombo;
        if (i.kills < i.killCost) return SkillRejection.NotEnoughKills;
        return SkillRejection.None;
    }
}

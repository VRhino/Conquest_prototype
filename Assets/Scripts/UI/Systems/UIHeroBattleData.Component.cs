using Unity.Collections;
using Unity.Entities;

/// <summary>What the skill bar shows for one slot.</summary>
public struct UISkillSlotData
{
    /// <summary>Skill in the slot (<see cref="SkillBlob.id"/>); 0 = empty or not unlocked yet.</summary>
    public int skillId;
    /// <summary>Fraction still to wait, 1 → 0; 0 = ready.</summary>
    public float cooldown;
    /// <summary>Seconds still to wait.</summary>
    public float remaining;

    /// <summary>There is a skill and it has finished cooling down.</summary>
    public readonly bool Ready => skillId != 0 && cooldown <= 0f;
}

/// <summary>
/// Singleton component written every frame by UIBattleDataSystem.
/// HUDController and other UI MonoBehaviours read from this component
/// instead of querying gameplay ECS components directly.
/// </summary>
public struct UIHeroBattleDataComponent : IComponentData
{
    // ── Hero vitals ──────────────────────────────────────────────────────────
    public float currentHealth;
    public float maxHealth;
    public float currentStamina;
    public float maxStamina;
    public bool  heroAlive;
    // ── Skill bar (L2-5, H6, H10) ────────────────────────────────────────────
    /// <summary>
    /// One entry per launchable slot, from <see cref="SkillSlot.Q"/> to <see cref="SkillSlot.Squad3"/> (index = slot − Q):
    /// the hero's Q, E, R, ultimate and active perks, then the abilities of the local active squad.
    /// </summary>
    public FixedList128Bytes<UISkillSlotData> skills;

    public const int SkillSlotCount = SkillSlot.Squad3 - SkillSlot.Q + 1;

    public readonly UISkillSlotData Skill(SkillSlot slot)
    {
        int index = slot - SkillSlot.Q;
        return index >= 0 && index < skills.Length ? skills[index] : default;
    }

    // ── Combo (L2-16, H16) ───────────────────────────────────────────────────
    /// <summary>The local hero's combo now, with the decay since its last hit applied.</summary>
    public float combo;
    /// <summary>Combo the ultimate asks for; 0 = it costs none and the bar shows no counter.</summary>
    public float ultimateComboCost;
    /// <summary>Hero slots whose skill costs more combo or kills than the hero has (bit = slot − Q).</summary>
    public ushort unaffordableSlots;

    public readonly bool CanAfford(SkillSlot slot) => (unaffordableSlots & SlotBit(slot)) == 0;

    public static ushort SlotBit(SkillSlot slot) => (ushort)(1 << (slot - SkillSlot.Q));

    // ── Capture zone ─────────────────────────────────────────────────────────
    public bool  isInCaptureZone;
    public float captureProgress;   // 0–100

    // ── Squad change (one-frame pulse) ────────────────────────────────────────
    /// <summary>True only on the frame when a SquadChangeEvent was consumed.</summary>
    public bool squadChangedThisFrame;
    public int  newSquadId;

    // ── Squad status ─────────────────────────────────────────────────────────
    public int aliveUnits;
    public int totalUnits;
    public FormationType currentFormation;
}

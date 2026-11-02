/// <summary>
/// What the battle UI asks of the input systems (H10). While <see cref="PointerMode"/> is on the cursor is free for the UI.
/// A request lasts until <see cref="HeroInputSystem"/> or <see cref="SquadControlSystem"/> takes it, and they treat it as
/// the key it stands for, so the UI never touches ECS and <see cref="SkillGate"/> still decides.
/// </summary>
public static class BattleCommandInput
{
    const int NoFormation = -1;

    static SkillSlot? _squadSkill;
    static int _formation = NoFormation;

    /// <summary>True while the player holds the cursor for the UI: no camera turn and no attack.</summary>
    public static bool PointerMode { get; private set; }

    public static void SetPointerMode(bool enabled) => PointerMode = enabled;

    public static bool IsSquadSlot(SkillSlot slot) => slot >= SkillSlot.Squad1 && slot <= SkillSlot.Squad3;

    /// <summary>Asks for a squad ability. An empty or cooling slot asks for nothing.</summary>
    public static bool TryRequestSquadSkill(SkillSlot slot, in UISkillSlotData data)
    {
        if (!PointerMode || !IsSquadSlot(slot) || !data.Ready) return false;
        _squadSkill = slot;
        return true;
    }

    /// <summary>Asks for formation <paramref name="index"/> of the squad, the same index as F1-F4.</summary>
    public static bool TryRequestFormation(int index)
    {
        if (!PointerMode || index < 0) return false;
        _formation = index;
        return true;
    }

    public static SkillSlot? ConsumeSquadSkill()
    {
        var slot = _squadSkill;
        _squadSkill = null;
        return slot;
    }

    /// <summary>The requested formation index, or a negative value when there is none.</summary>
    public static int ConsumeFormation()
    {
        int index = _formation;
        _formation = NoFormation;
        return index;
    }

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset()
    {
        PointerMode = false;
        _squadSkill = null;
        _formation = NoFormation;
    }
}

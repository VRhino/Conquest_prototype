using Unity.Collections;

/// <summary>
/// Advances a skill instance through its phases (L2-2) and collects the events due in the step. A phase with a negative
/// duration never expires (skill-lifecycle.md §1); leftover time carries into the next phase, so a long frame does not
/// skip events. Pure: the caller applies the events.
/// </summary>
public static class SkillTimeline
{
    /// <summary>Enters phase 0 and collects its events at time 0.</summary>
    public static void Start(ref SkillBlob skill, ref SkillInstanceComponent instance, int skillIndex, NativeList<SkillEvent> fired)
    {
        instance.active = true;
        instance.skillIndex = skillIndex;
        instance.phase = 0;
        instance.phaseTime = 0f;
        instance.instanceId++;
        instance.hitboxOpen = false;
        Collect(ref skill, 0, 0f, 0f, true, fired);
    }

    /// <summary>Moves <paramref name="dt"/> forward; returns true when the skill ended in this step.</summary>
    public static bool Advance(ref SkillBlob skill, ref SkillInstanceComponent instance, float dt, NativeList<SkillEvent> fired)
    {
        if (!instance.active) return false;
        float from = instance.phaseTime;
        float to = from + dt;
        while (true)
        {
            float duration = skill.phases[instance.phase].duration;
            bool endless = duration < 0f;
            if (endless || to < duration)
            {
                Collect(ref skill, instance.phase, from, to, false, fired);
                instance.phaseTime = to;
                return false;
            }
            Collect(ref skill, instance.phase, from, duration, false, fired);
            to -= duration;
            instance.phase++;
            if (instance.phase >= skill.phases.Length)
            {
                instance.active = false;
                instance.phaseTime = 0f;
                return true;
            }
            from = 0f;
            Collect(ref skill, instance.phase, 0f, 0f, true, fired);
        }
    }

    /// <summary>
    /// Releases a charge (L2-6, skill-lifecycle.md §4): from a phase without end, the skill moves to the next phase (the
    /// release); a wait that is the last phase just ends. Returns true when the skill ended.
    /// </summary>
    public static bool Release(ref SkillBlob skill, ref SkillInstanceComponent instance, NativeList<SkillEvent> fired)
    {
        if (!instance.active || skill.phases[instance.phase].duration >= 0f) return false;
        instance.phase++;
        instance.phaseTime = 0f;
        if (instance.phase >= skill.phases.Length)
        {
            instance.active = false;
            return true;
        }
        Collect(ref skill, instance.phase, 0f, 0f, true, fired);
        return false;
    }

    /// <summary>Whether the current phase lets another skill in (§3.1); an idle hero is always free.</summary>
    public static bool Interruptible(ref SkillBlob skill, in SkillInstanceComponent instance)
        => !instance.active || (instance.phase < skill.phases.Length && skill.phases[instance.phase].interruptible);

    /// <summary>Events of <paramref name="phase"/> with time in (from, to], or [from, to] when entering.</summary>
    static void Collect(ref SkillBlob skill, int phase, float from, float to, bool includeFrom, NativeList<SkillEvent> fired)
    {
        for (int i = 0; i < skill.events.Length; i++)
        {
            var e = skill.events[i];
            if (e.phase != phase) continue;
            bool after = includeFrom ? e.time >= from : e.time > from;
            if (after && e.time <= to) fired.Add(e);
        }
    }
}

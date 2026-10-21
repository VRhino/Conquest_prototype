using Unity.Entities;

/// <summary>
/// Turns the local player's input and the AI's decision into one <see cref="SkillRequestComponent"/> per hero (L2-1):
/// both go through the same gate afterwards. Attack button → basic attack; Q, E, R → class abilities; F → ultimate. A
/// held key keeps requesting, and the gate turns it down while the hero is busy or the skill is cooling down.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HeroInputSystem))]
[UpdateAfter(typeof(HeroAIExecutionSystem))]
public partial class HeroSkillRequestSystem : SystemBase
{
    protected override void OnUpdate()
    {
        double now = SystemAPI.Time.ElapsedTime;
        foreach (var (input, request, wants) in SystemAPI
                     .Query<RefRO<HeroInputComponent>, RefRW<SkillRequestComponent>, EnabledRefRW<SkillRequestComponent>>()
                     .WithAll<IsLocalPlayer>()
                     .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState))
        {
            request.ValueRW.heldSlots = Held(input.ValueRO);
            if (TryPick(input.ValueRO, out var slot)) Request(request, wants, slot, now);
        }

        foreach (var (decision, request, wants) in SystemAPI
                     .Query<RefRO<HeroAIDecision>, RefRW<SkillRequestComponent>, EnabledRefRW<SkillRequestComponent>>()
                     .WithAll<HeroAITag>()
                     .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState))
        {
            request.ValueRW.heldSlots = decision.ValueRO.shouldAttack ? SkillRequestComponent.Bit(SkillSlot.Basic) : (byte)0;
            if (decision.ValueRO.shouldAttack) Request(request, wants, SkillSlot.Basic, now);
        }
    }

    /// <summary>The ultimate first, then R, E, Q, and the basic attack last, when several keys are held.</summary>
    static bool TryPick(in HeroInputComponent input, out SkillSlot slot)
    {
        if (input.UseUltimate) { slot = SkillSlot.Ultimate; return true; }
        if (input.UseSkill3) { slot = SkillSlot.R; return true; }
        if (input.UseSkill2) { slot = SkillSlot.E; return true; }
        if (input.UseSkill1) { slot = SkillSlot.Q; return true; }
        if (input.IsAttackPressed) { slot = SkillSlot.Basic; return true; }
        slot = default;
        return false;
    }

    static byte Held(in HeroInputComponent input)
    {
        byte held = 0;
        if (input.IsAttackPressed) held |= SkillRequestComponent.Bit(SkillSlot.Basic);
        if (input.UseSkill1) held |= SkillRequestComponent.Bit(SkillSlot.Q);
        if (input.UseSkill2) held |= SkillRequestComponent.Bit(SkillSlot.E);
        if (input.UseSkill3) held |= SkillRequestComponent.Bit(SkillSlot.R);
        if (input.UseUltimate) held |= SkillRequestComponent.Bit(SkillSlot.Ultimate);
        return held;
    }

    static void Request(RefRW<SkillRequestComponent> request, EnabledRefRW<SkillRequestComponent> wants, SkillSlot slot, double now)
    {
        request.ValueRW.slot = slot;
        request.ValueRW.requestedAt = now;
        wants.ValueRW = true;
    }
}

using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Turns the local player's input and the AI's decision into one <see cref="SkillRequestComponent"/> per hero (L2-1):
/// both go through the same gate afterwards. Attack button → basic attack; Q, E, R → class abilities; F → ultimate;
/// 1, 2 → active perks. A held key keeps requesting, and the gate turns it down while the hero is busy or the skill is
/// cooling down. 3, 4, 5 → the abilities of the hero's active squad (L2-9): the request goes to the squad, which launches
/// them itself.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HeroInputSystem))]
[UpdateAfter(typeof(HeroAIExecutionSystem))]
public partial class HeroSkillRequestSystem : SystemBase
{
    struct SquadRequest
    {
        public Entity squad;
        public ushort held;
        public bool wants;
        public SkillSlot slot;
    }

    protected override void OnUpdate()
    {
        double now = SystemAPI.Time.ElapsedTime;
        var squadRequests = new NativeList<SquadRequest>(4, Allocator.Temp);
        foreach (var (input, request, wants, entity) in SystemAPI
                     .Query<RefRO<HeroInputComponent>, RefRW<SkillRequestComponent>, EnabledRefRW<SkillRequestComponent>>()
                     .WithAll<IsLocalPlayer>()
                     .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                     .WithEntityAccess())
        {
            request.ValueRW.heldSlots = Held(input.ValueRO);
            if (TryPick(input.ValueRO, out var slot)) Request(request, wants, slot, now);
            if (SystemAPI.HasComponent<HeroSquadReference>(entity))
                squadRequests.Add(new SquadRequest
                {
                    squad = SystemAPI.GetComponent<HeroSquadReference>(entity).squad,
                    held = SquadHeld(input.ValueRO),
                    wants = TryPickSquad(input.ValueRO, out var squadSlot),
                    slot = squadSlot,
                });
        }

        foreach (var (decision, request, wants) in SystemAPI
                     .Query<RefRO<HeroAIDecision>, RefRW<SkillRequestComponent>, EnabledRefRW<SkillRequestComponent>>()
                     .WithAll<HeroAITag>()
                     .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState))
        {
            request.ValueRW.heldSlots = decision.ValueRO.shouldAttack ? SkillRequestComponent.Bit(SkillSlot.Basic) : (ushort)0;
            if (decision.ValueRO.shouldAttack) Request(request, wants, SkillSlot.Basic, now);
        }

        // Squads are written after the hero loops: both are SkillRequestComponent.
        foreach (var r in squadRequests)
        {
            if (!SystemAPI.Exists(r.squad) || !SystemAPI.HasComponent<SkillRequestComponent>(r.squad)) continue;
            var request = SystemAPI.GetComponent<SkillRequestComponent>(r.squad);
            request.heldSlots = r.held;
            if (r.wants)
            {
                request.slot = r.slot;
                request.requestedAt = now;
                SystemAPI.SetComponentEnabled<SkillRequestComponent>(r.squad, true);
            }
            SystemAPI.SetComponent(r.squad, request);
        }
        squadRequests.Dispose();
    }

    /// <summary>The ultimate first, then R, E, Q, and the basic attack last, when several keys are held.</summary>
    static bool TryPick(in HeroInputComponent input, out SkillSlot slot)
    {
        if (input.UseUltimate) { slot = SkillSlot.Ultimate; return true; }
        if (input.UseSkill3) { slot = SkillSlot.R; return true; }
        if (input.UseSkill2) { slot = SkillSlot.E; return true; }
        if (input.UseSkill1) { slot = SkillSlot.Q; return true; }
        if (input.UsePerk1) { slot = SkillSlot.ActivePerk1; return true; }
        if (input.UsePerk2) { slot = SkillSlot.ActivePerk2; return true; }
        if (input.IsAttackPressed) { slot = SkillSlot.Basic; return true; }
        slot = default;
        return false;
    }

    /// <summary>The latest unlock first, when several squad keys are held.</summary>
    static bool TryPickSquad(in HeroInputComponent input, out SkillSlot slot)
    {
        if (input.UseSquadAbility3) { slot = SkillSlot.Squad3; return true; }
        if (input.UseSquadAbility2) { slot = SkillSlot.Squad2; return true; }
        if (input.UseSquadAbility1) { slot = SkillSlot.Squad1; return true; }
        slot = default;
        return false;
    }

    static ushort Held(in HeroInputComponent input)
    {
        ushort held = 0;
        if (input.IsAttackPressed) held |= SkillRequestComponent.Bit(SkillSlot.Basic);
        if (input.UseSkill1) held |= SkillRequestComponent.Bit(SkillSlot.Q);
        if (input.UseSkill2) held |= SkillRequestComponent.Bit(SkillSlot.E);
        if (input.UseSkill3) held |= SkillRequestComponent.Bit(SkillSlot.R);
        if (input.UseUltimate) held |= SkillRequestComponent.Bit(SkillSlot.Ultimate);
        if (input.UsePerk1) held |= SkillRequestComponent.Bit(SkillSlot.ActivePerk1);
        if (input.UsePerk2) held |= SkillRequestComponent.Bit(SkillSlot.ActivePerk2);
        return held;
    }

    static ushort SquadHeld(in HeroInputComponent input)
    {
        ushort held = 0;
        if (input.UseSquadAbility1) held |= SkillRequestComponent.Bit(SkillSlot.Squad1);
        if (input.UseSquadAbility2) held |= SkillRequestComponent.Bit(SkillSlot.Squad2);
        if (input.UseSquadAbility3) held |= SkillRequestComponent.Bit(SkillSlot.Squad3);
        return held;
    }

    static void Request(RefRW<SkillRequestComponent> request, EnabledRefRW<SkillRequestComponent> wants, SkillSlot slot, double now)
    {
        request.ValueRW.slot = slot;
        request.ValueRW.requestedAt = now;
        wants.ValueRW = true;
    }
}

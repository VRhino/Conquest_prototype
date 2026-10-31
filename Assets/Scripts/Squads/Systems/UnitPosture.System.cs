using Unity.Entities;

/// <summary>
/// Single writer of <see cref="UnitFormationStanceComponent"/>: each unit takes the posture its formation gives to its
/// slot role (shield in front, to the side, over the head, braced), formed or while moving
/// (game-re/notes/formation-postures.md: the slot gives the role, the posture belongs to the soldier).
/// Formation settings decide whether free combat releases the rank pose. Hold Position preserves it.
/// Propagates posture, row and role to the animation component.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitFormationStateSystem))]
public partial struct UnitPostureSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<MatchStateComponent>();
    }

    public void OnUpdate(ref SystemState state)
    {
        foreach (var (units, formationComp, definition, squad) in
            SystemAPI.Query<DynamicBuffer<SquadUnitElement>, RefRO<FormationComponent>, RefRO<SquadDefinitionComponent>>().WithEntityAccess())
        {
            if (!definition.ValueRO.formationLibrary.IsCreated) continue;
            ref var library = ref definition.ValueRO.formationLibrary.Value;
            int formationIndex = FormationLayout.Find(ref library, formationComp.ValueRO.currentFormation);
            if (formationIndex < 0) continue;
            ref var formation = ref library.formations[formationIndex];
            var order = SystemAPI.HasComponent<SquadStateComponent>(squad)
                ? SystemAPI.GetComponent<SquadStateComponent>(squad).currentOrder : SquadOrderType.FollowHero;

            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (!SystemAPI.HasComponent<UnitFormationStanceComponent>(unit)
                    || !SystemAPI.HasComponent<UnitGridSlotComponent>(unit)
                    || !SystemAPI.HasComponent<UnitFormationStateComponent>(unit))
                    continue;

                var slot = SystemAPI.GetComponent<UnitGridSlotComponent>(unit);
                bool engaging = SystemAPI.HasComponent<IsEngagingTag>(unit) && SystemAPI.IsComponentEnabled<IsEngagingTag>(unit);
                bool moving = !engaging && SystemAPI.GetComponent<UnitFormationStateComponent>(unit).State != UnitFormationState.Formed;
                // Engagement alone does not mean the soldier is still fighting from its slot.
                // Attack releases the pose immediately, including pauses and approach before acquiring a target.
                bool freeCombat = order == SquadOrderType.Attack || (engaging && order != SquadOrderType.HoldPosition);
                UnitStance stance = formation.useNormalPostureInFreeCombat && freeCombat
                    ? UnitStance.Normal : FormationLayout.Posture(ref formation, slot.role, moving);

                var stanceComp = SystemAPI.GetComponentRW<UnitFormationStanceComponent>(unit);
                if (stanceComp.ValueRO.stance != stance) stanceComp.ValueRW.stance = stance;

                if (!SystemAPI.HasComponent<ConquestTactics.Animation.UnitAnimationMovementComponent>(unit)) continue;
                var anim = SystemAPI.GetComponentRW<ConquestTactics.Animation.UnitAnimationMovementComponent>(unit);
                if (anim.ValueRO.CurrentStance != stance || anim.ValueRO.SlotRow != slot.row || anim.ValueRO.SlotRole != (int)slot.role)
                {
                    anim.ValueRW.CurrentStance = stance;
                    anim.ValueRW.SlotRow = slot.row;
                    anim.ValueRW.SlotRole = (int)slot.role;
                }
            }
        }
    }
}

using Unity.Entities;

/// <summary>
/// Reacts to formation lifecycle milestone tags and updates each unit's tactical stance.
///
/// Data flow:
///   UnitStartedMovingTag enabled  → stance = Normal   (exit BracedShields on move order)
///   UnitArrivedAtSlotTag enabled  → stance depends on formation:
///       ShieldWall → BracedShields
///       any other  → Normal
///
/// After updating stance, the component values are propagated to
/// UnitAnimationMovementComponent so that UnitAnimationAdapter can expose
/// IsBraced and SlotRow to the Animator Controller.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitFormationStateSystem))]
public partial struct FormationStanceSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (units, squadState) in
            SystemAPI.Query<DynamicBuffer<SquadUnitElement>, RefRO<SquadStateComponent>>())
        {
            var formation = squadState.ValueRO.currentFormation;

            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;

                if (!SystemAPI.HasComponent<UnitFormationStanceComponent>(unit))
                    continue;

                bool startedMoving = SystemAPI.IsComponentEnabled<UnitStartedMovingTag>(unit);
                bool arrived       = SystemAPI.IsComponentEnabled<UnitArrivedAtSlotTag>(unit);

                // Only act on milestone pulses
                if (!startedMoving && !arrived)
                    continue;

                var stanceComp = SystemAPI.GetComponent<UnitFormationStanceComponent>(unit);

                if (startedMoving)
                {
                    stanceComp.stance = UnitStance.Normal;
                }
                else // arrived
                {
                    stanceComp.stance = formation == FormationType.ShieldWall
                        ? UnitStance.BracedShields
                        : UnitStance.Normal;
                }

                SystemAPI.SetComponent(unit, stanceComp);

                // Propagate stance and slot row to the animation component
                if (!SystemAPI.HasComponent<ConquestTactics.Animation.UnitAnimationMovementComponent>(unit))
                    continue;

                var animComp = SystemAPI.GetComponent<ConquestTactics.Animation.UnitAnimationMovementComponent>(unit);
                animComp.CurrentStance = stanceComp.stance;

                if (SystemAPI.HasComponent<UnitGridSlotComponent>(unit))
                    animComp.SlotRow = SystemAPI.GetComponent<UnitGridSlotComponent>(unit).gridPosition.y;

                SystemAPI.SetComponent(unit, animComp);
            }
        }
    }
}

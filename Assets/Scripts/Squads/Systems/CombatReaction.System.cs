using Unity.Entities;

/// <summary>
/// Reads SquadCombatStateComponent (isInCombat) and SquadAIComponent (targetEntity)
/// and writes SquadCombatReactionIntentComponent each frame.
/// Runs after SquadAISystem so combat state is up to date,
/// and before OrderResolutionSystem so the intent is ready to be resolved.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(SquadAISystem))]
[UpdateBefore(typeof(OrderResolutionSystem))]
public partial class CombatReactionSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
    }

    protected override void OnUpdate()
    {
        foreach (var (combatState, ai, combatReaction) in SystemAPI
            .Query<RefRO<SquadCombatStateComponent>,
                   RefRO<SquadAIComponent>,
                   RefRW<SquadCombatReactionIntentComponent>>())
        {
            combatReaction.ValueRW.reactToEnemy = combatState.ValueRO.isInCombat;
            combatReaction.ValueRW.reactTarget  = ai.ValueRO.targetEntity;
        }
    }
}

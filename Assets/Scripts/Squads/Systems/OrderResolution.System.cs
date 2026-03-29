using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Reads the three intent components (Player, AI, CombatReaction) and writes
/// SquadResolvedOrderComponent with the winning order each frame.
///
/// Priority rules:
///   Local squad:
///     1. heroOrdenCooldown active  → Player wins (movement override)
///     2. combatReaction.reactToEnemy → CombatReaction wins (blocks movement)
///     3. Otherwise                 → Player wins (normal order)
///   Remote squad:
///     1. combatReaction.reactToEnemy → CombatReaction wins (blocks movement)
///     2. Otherwise                   → AI wins
///
/// hasNewOrder is only set to true when the winning order or source changes,
/// except for local squads which forward input.hasNewOrder directly.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(CombatReactionSystem))]
[UpdateBefore(typeof(SquadOrderSystem))]
public partial class OrderResolutionSystem : SystemBase
{
    private ComponentLookup<IsLocalPlayer>      _localPlayerLookup;
    private ComponentLookup<SquadOwnerComponent> _ownerLookup;

    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
        _localPlayerLookup = GetComponentLookup<IsLocalPlayer>(true);
        _ownerLookup       = GetComponentLookup<SquadOwnerComponent>(true);
    }

    protected override void OnUpdate()
    {
        _localPlayerLookup.Update(this);
        _ownerLookup.Update(this);

        foreach (var (playerIntent, aiIntent, combatReaction, input, resolved, entity) in SystemAPI
            .Query<RefRO<SquadPlayerOrderIntentComponent>,
                   RefRO<SquadAIOrderIntentComponent>,
                   RefRO<SquadCombatReactionIntentComponent>,
                   RefRO<SquadInputComponent>,
                   RefRW<SquadResolvedOrderComponent>>()
            .WithEntityAccess())
        {
            bool isLocal = _ownerLookup.HasComponent(entity)
                && _localPlayerLookup.HasComponent(_ownerLookup[entity].hero);

            if (isLocal)
            {
                // Local squad: forward player order, apply combat blocking unless heroOrdenCooldown active
                if (!input.ValueRO.hasNewOrder)
                {
                    resolved.ValueRW.hasNewOrder = false;
                    continue;
                }

                if (playerIntent.ValueRO.heroOrdenCooldownActive)
                {
                    // Player insisted — override combat reaction
                    resolved.ValueRW.order       = playerIntent.ValueRO.orderType;
                    resolved.ValueRW.holdPosition = playerIntent.ValueRO.holdPosition;
                    resolved.ValueRW.targetEntity = Entity.Null;
                    resolved.ValueRW.source       = OrderSource.Player;
                }
                else if (combatReaction.ValueRO.reactToEnemy)
                {
                    // Combat reaction blocks the movement order
                    resolved.ValueRW.order       = SquadOrderType.Attack;
                    resolved.ValueRW.holdPosition = default;
                    resolved.ValueRW.targetEntity = combatReaction.ValueRO.reactTarget;
                    resolved.ValueRW.source       = OrderSource.CombatReaction;
                }
                else
                {
                    // Normal player order
                    resolved.ValueRW.order       = playerIntent.ValueRO.orderType;
                    resolved.ValueRW.holdPosition = playerIntent.ValueRO.holdPosition;
                    resolved.ValueRW.targetEntity = Entity.Null;
                    resolved.ValueRW.source       = OrderSource.Player;
                }

                resolved.ValueRW.formation   = playerIntent.ValueRO.desiredFormation;
                resolved.ValueRW.hasNewOrder = true;
            }
            else
            {
                // Remote squad: combat reaction blocks AI movement orders
                SquadOrderType winningOrder;
                Entity         winningTarget;
                OrderSource    winningSource;

                if (combatReaction.ValueRO.reactToEnemy)
                {
                    winningOrder  = SquadOrderType.Attack;
                    winningTarget = combatReaction.ValueRO.reactTarget;
                    winningSource = OrderSource.CombatReaction;
                }
                else
                {
                    winningOrder  = aiIntent.ValueRO.suggestedOrder;
                    winningTarget = aiIntent.ValueRO.targetEntity;
                    winningSource = OrderSource.AI;
                }

                // Only issue a new order when something actually changed
                bool orderChanged = winningOrder  != resolved.ValueRO.order
                                 || winningSource != resolved.ValueRO.source;

                resolved.ValueRW.order       = winningOrder;
                resolved.ValueRW.holdPosition = default;
                resolved.ValueRW.targetEntity = winningTarget;
                resolved.ValueRW.source       = winningSource;
                resolved.ValueRW.formation    = input.ValueRO.desiredFormation;
                resolved.ValueRW.hasNewOrder  = orderChanged;
            }
        }
    }
}

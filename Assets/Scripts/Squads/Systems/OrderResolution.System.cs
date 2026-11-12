using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Reads the three intent components (Player, AI, CombatReaction) and writes
/// SquadResolvedOrderComponent with the winning order each frame.
///
/// Priority rules:
///   Local squad:
///     1. insisted override not lapsed (now &lt; overrideEndsAt) → Player wins, and the resolved order carries that
///        end as expiresAt (L3-05)
///     2. combatReaction.reactToEnemy → CombatReaction wins (blocks movement)
///     3. Otherwise                 → Player wins (normal order)
///   Remote squad:
///     1. combatReaction.reactToEnemy → CombatReaction wins (blocks movement)
///     2. Otherwise                   → AI wins
///
/// Orders are reissued when their winning payload changes or a local player
/// explicitly repeats a command. HoldPosition is never replaced by a combat reaction.
/// </summary>
[ServerAuthoritative] // Decides the match (L4-06)
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(CombatReactionSystem))]
[UpdateBefore(typeof(SquadOrderSystem))]
public partial class OrderResolutionSystem : SystemBase
{
    private ComponentLookup<IsLocalPlayer>      _localPlayerLookup;
    private ComponentLookup<SquadOwnerComponent> _ownerLookup;
    // A hero driven by a player over the network (battle server): its squad obeys that player like a local one.
    private ComponentLookup<HeroNetworkMotion>   _networkPlayerLookup;

    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
        _localPlayerLookup = GetComponentLookup<IsLocalPlayer>(true);
        _ownerLookup       = GetComponentLookup<SquadOwnerComponent>(true);
        _networkPlayerLookup = GetComponentLookup<HeroNetworkMotion>(true);
    }

    protected override void OnUpdate()
    {
        _localPlayerLookup.Update(this);
        _ownerLookup.Update(this);
        _networkPlayerLookup.Update(this);
        double now = SystemAPI.Time.ElapsedTime;

        foreach (var (playerIntent, aiIntent, combatReaction, input, formation, resolved, entity) in SystemAPI
            .Query<RefRO<SquadPlayerOrderIntentComponent>,
                   RefRO<SquadAIOrderIntentComponent>,
                   RefRO<SquadCombatReactionIntentComponent>,
                   RefRO<SquadInputComponent>,
                   RefRO<FormationComponent>,
                   RefRW<SquadResolvedOrderComponent>>()
            .WithEntityAccess())
        {
            bool isLocal = _ownerLookup.HasComponent(entity)
                && (_localPlayerLookup.HasComponent(_ownerLookup[entity].hero)
                    || _networkPlayerLookup.HasComponent(_ownerLookup[entity].hero));

            if (isLocal)
            {
                // Local squad: forward player order, apply combat blocking unless the insisted override holds
                SquadOrderType winningOrder;
                float3 winningPosition = default;
                Entity winningTarget = Entity.Null;
                OrderSource winningSource;
                bool insisted = now < playerIntent.ValueRO.overrideEndsAt;
                if (insisted)
                {
                    // Player insisted — override combat reaction
                    winningOrder = playerIntent.ValueRO.orderType;
                    winningPosition = winningOrder == SquadOrderType.HoldPosition
                        ? playerIntent.ValueRO.holdPosition : default;
                    winningSource = OrderSource.Player;
                }
                else if (combatReaction.ValueRO.reactToEnemy
                         && playerIntent.ValueRO.orderType != SquadOrderType.HoldPosition)
                {
                    // Combat reaction blocks the movement order; an ordered Attack stays ordered and free of the leash
                    winningOrder = SquadOrderType.Attack;
                    winningTarget = combatReaction.ValueRO.reactTarget;
                    winningSource = playerIntent.ValueRO.orderType == SquadOrderType.Attack
                        ? OrderSource.Player : OrderSource.CombatReaction;
                }
                else
                {
                    // Normal player order
                    winningOrder = playerIntent.ValueRO.orderType;
                    winningPosition = winningOrder == SquadOrderType.HoldPosition
                        ? playerIntent.ValueRO.holdPosition : default;
                    winningSource = OrderSource.Player;
                }

                bool orderChanged = winningOrder != resolved.ValueRO.order
                    || winningSource != resolved.ValueRO.source
                    || winningTarget != resolved.ValueRO.targetEntity
                    || math.any(winningPosition != resolved.ValueRO.holdPosition);
                resolved.ValueRW.order = winningOrder;
                resolved.ValueRW.holdPosition = winningPosition;
                resolved.ValueRW.targetEntity = winningTarget;
                resolved.ValueRW.source = winningSource;
                resolved.ValueRW.formation   = playerIntent.ValueRO.desiredFormation;
                resolved.ValueRW.hasNewOrder = input.ValueRO.hasNewOrder || orderChanged;
                resolved.ValueRW.expiresAt   = insisted ? playerIntent.ValueRO.overrideEndsAt : 0;
                // A formation asked for and not yet committed keeps the squad on FormationSystem's list (L3-06).
                if (playerIntent.ValueRO.desiredFormation != formation.ValueRO.currentFormation
                    && SystemAPI.HasComponent<FormationChangePending>(entity))
                    SystemAPI.SetComponentEnabled<FormationChangePending>(entity, true);
            }
            else
            {
                // Remote squad: combat reaction blocks AI movement orders
                SquadOrderType winningOrder;
                Entity         winningTarget;
                OrderSource    winningSource;
                float3         winningPosition = default;

                if (combatReaction.ValueRO.reactToEnemy
                    && aiIntent.ValueRO.suggestedOrder != SquadOrderType.HoldPosition)
                {
                    winningOrder  = SquadOrderType.Attack;
                    winningTarget = combatReaction.ValueRO.reactTarget;
                    winningSource = aiIntent.ValueRO.suggestedOrder == SquadOrderType.Attack
                        ? OrderSource.AI : OrderSource.CombatReaction;
                }
                else
                {
                    winningOrder  = aiIntent.ValueRO.suggestedOrder;
                    winningTarget = aiIntent.ValueRO.targetEntity;
                    winningSource = OrderSource.AI;
                    if (winningOrder == SquadOrderType.HoldPosition)
                        winningPosition = aiIntent.ValueRO.holdPosition;
                }

                // Only issue a new order when something actually changed
                bool orderChanged = winningOrder  != resolved.ValueRO.order
                                 || winningSource != resolved.ValueRO.source
                                 || winningTarget != resolved.ValueRO.targetEntity
                                 || math.any(winningPosition != resolved.ValueRO.holdPosition);

                resolved.ValueRW.order       = winningOrder;
                resolved.ValueRW.holdPosition = winningPosition;
                resolved.ValueRW.targetEntity = winningTarget;
                resolved.ValueRW.source       = winningSource;
                // AI orders do not request formation changes.
                resolved.ValueRW.formation    = formation.ValueRO.currentFormation;
                resolved.ValueRW.hasNewOrder  = orderChanged;
                resolved.ValueRW.expiresAt    = 0;
            }
        }
    }
}

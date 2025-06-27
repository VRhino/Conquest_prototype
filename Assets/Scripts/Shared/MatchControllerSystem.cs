using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Server-side system that controls the flow of a match.
/// It advances <see cref="MatchStateComponent"/> automatically and
/// broadcasts <see cref="GameStateChangeEvent"/> on transitions.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class MatchControllerSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();

        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var q = em.CreateEntityQuery(ComponentType.ReadOnly<MatchStateComponent>());
        if (q.IsEmptyIgnoreFilter)
        {
            Entity entity = em.CreateEntity(typeof(MatchStateComponent));
            em.SetComponentData(entity, new MatchStateComponent
            {
                currentState = MatchState.WaitingForPlayers,
                stateTimer = 0f,
                playersReady = 0,
                maxPlayers = 2,
                victoryConditionMet = false
            });
        }
    }

    protected override void OnUpdate()
    {
        if (!SystemAPI.TryGetSingletonEntity<MatchStateComponent>(out var entity))
            return;

        var state = SystemAPI.GetComponentRW<MatchStateComponent>(entity);
        float dt = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        state.ValueRW.stateTimer -= dt;

        switch (state.ValueRO.currentState)
        {
            case MatchState.WaitingForPlayers:
                if (state.ValueRO.playersReady >= state.ValueRO.maxPlayers)
                    Transition(ref state, MatchState.PreparationPhase, ref ecb);
                break;

            case MatchState.PreparationPhase:
                if (state.ValueRO.stateTimer <= 0f)
                    Transition(ref state, MatchState.LoadingMap, ref ecb);
                break;

            case MatchState.LoadingMap:
                if (state.ValueRO.playersReady >= state.ValueRO.maxPlayers)
                    Transition(ref state, MatchState.InBattle, ref ecb);
                break;

            case MatchState.InBattle:
                if (state.ValueRO.victoryConditionMet)
                    Transition(ref state, MatchState.EndMatch, ref ecb);
                break;
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    void Transition(ref RefRW<MatchStateComponent> state, MatchState newState,
                     ref EntityCommandBuffer ecb)
    {
        state.ValueRW.currentState = newState;
        state.ValueRW.stateTimer = 0f;

        Entity evt = ecb.CreateEntity();
        ecb.AddComponent(evt, new GameStateChangeEvent { newState = newState });
    }
}

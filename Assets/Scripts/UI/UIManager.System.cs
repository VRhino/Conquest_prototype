using Unity.Entities;

/// <summary>
/// Listens for changes to <see cref="GameStateComponent"/> and updates
/// the <see cref="UIManager"/> accordingly.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class UIManagerSystem : SystemBase
{
    GamePhase _lastPhase;

    protected override void OnCreate()
    {
        base.OnCreate();
        var q = World.DefaultGameObjectInjectionWorld.EntityManager
            .CreateEntityQuery(ComponentType.ReadOnly<GameStateComponent>());
        if (!q.IsEmptyIgnoreFilter)
        {
            var state = q.GetSingleton<GameStateComponent>();
            _lastPhase = state.currentPhase;
        }
    }

    protected override void OnUpdate()
    {
        if (!SystemAPI.TryGetSingleton<GameStateComponent>(out var state))
            return;

        if (state.currentPhase != _lastPhase)
        {
            _lastPhase = state.currentPhase;
            if (UIManager.Instance != null)
                UIManager.Instance.ShowScreen(state.currentPhase);
        }
    }
}

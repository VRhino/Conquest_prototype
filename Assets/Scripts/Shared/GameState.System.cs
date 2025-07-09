using Unity.Entities;

/// <summary>
/// Ensures a singleton <see cref="GameStateComponent"/> exists and persists
/// between gameplay scenes.
/// </summary>
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class GameStateSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();

        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var q = em.CreateEntityQuery(ComponentType.ReadOnly<GameStateComponent>());
        if (q.IsEmptyIgnoreFilter)
        {
            Entity entity = em.CreateEntity(typeof(GameStateComponent));
            em.SetComponentData(entity, new GameStateComponent
            {
                currentPhase = GamePhase.Login
            });
        }
    }

    protected override void OnUpdate()
    {
        // No per-frame logic required.
    }
}

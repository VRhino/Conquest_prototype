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

        if (!SystemAPI.TryGetSingletonEntity<GameStateComponent>(out _))
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
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

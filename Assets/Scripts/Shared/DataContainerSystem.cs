using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Ensures a singleton <see cref="DataContainerComponent"/> exists and persists
/// between gameplay scenes.
/// </summary>
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class DataContainerSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();

        if (!SystemAPI.TryGetSingletonEntity<DataContainerComponent>(out _))
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            Entity entity = em.CreateEntity(typeof(DataContainerComponent));
            em.SetComponentData(entity, new DataContainerComponent
            {
                playerName = default,
                selectedSquad = -1,
                selectedPerks = default,
                playerTeam = 0,
                isReady = false
            });
        }
    }

    protected override void OnUpdate()
    {
        // This system only holds persistent data; no per-frame logic required.
    }
}

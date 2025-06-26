using Unity.Entities;

/// <summary>
/// System responsible for gathering every <see cref="ZoneTriggerComponent"/>
/// present in the loaded scene and registering them in a singleton buffer
/// for fast access by other systems.
/// </summary>
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class MapSceneManager : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<ZoneTriggerComponent>();
    }

    protected override void OnUpdate()
    {
        Entity managerEntity;
        var em = EntityManager;
        if (!SystemAPI.TryGetSingletonEntity<ZoneManagerSingleton>(out managerEntity))
        {
            managerEntity = em.CreateEntity(typeof(ZoneManagerSingleton));
            em.AddBuffer<ZoneReferenceBuffer>(managerEntity);
        }

        DynamicBuffer<ZoneReferenceBuffer> buffer = em.GetBuffer<ZoneReferenceBuffer>(managerEntity);
        buffer.Clear();

        foreach (var (_, entity) in SystemAPI.Query<RefRO<ZoneTriggerComponent>>().WithEntityAccess())
            buffer.Add(new ZoneReferenceBuffer { Value = entity });

        Enabled = false;
    }
}

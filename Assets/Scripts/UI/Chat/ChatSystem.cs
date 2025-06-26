using Unity.Collections;
using Unity.Entities;

/// <summary>
/// System responsible for storing chat messages in a history buffer.
/// It converts <see cref="ChatMessageComponent"/> entities into
/// <see cref="ChatHistoryElement"/> entries and removes the original entity.
/// The buffer can then be read by the UI layer.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class ChatSystem : SystemBase
{
    const int DefaultHistoryLimit = 30;

    protected override void OnCreate()
    {
        base.OnCreate();

        // Ensure a singleton entity with a dynamic buffer exists
        if (!SystemAPI.TryGetSingletonEntity<ChatHistoryState>(out _))
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            Entity entity = em.CreateEntity(typeof(ChatHistoryState));
            em.AddBuffer<ChatHistoryElement>(entity);
            em.SetComponentData(entity, new ChatHistoryState
            {
                historyLimit = DefaultHistoryLimit
            });
        }
    }

    protected override void OnUpdate()
    {
        if (!SystemAPI.TryGetSingletonEntity<ChatHistoryState>(out var historyEntity))
            return;

        DynamicBuffer<ChatHistoryElement> history = SystemAPI.GetBuffer<ChatHistoryElement>(historyEntity);
        int limit = SystemAPI.GetComponent<ChatHistoryState>(historyEntity).historyLimit;

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (msg, ent) in SystemAPI.Query<ChatMessageComponent>().WithEntityAccess())
        {
            history.Add(new ChatHistoryElement
            {
                senderName = msg.senderName,
                message = msg.message
            });

            if (history.Length > limit)
                history.RemoveAt(0);

            ecb.DestroyEntity(ent);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}

/// <summary>
/// Buffer element storing a formatted chat message.
/// </summary>
public struct ChatHistoryElement : IBufferElementData
{
    public FixedString64Bytes senderName;
    public FixedString128Bytes message;
}

/// <summary>
/// Singleton component holding configuration for the chat history buffer.
/// </summary>
public struct ChatHistoryState : IComponentData
{
    public int historyLimit;
}

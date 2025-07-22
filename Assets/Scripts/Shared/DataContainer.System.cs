using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

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

        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var q = em.CreateEntityQuery(ComponentType.ReadOnly<DataContainerComponent>());
        if (q.IsEmptyIgnoreFilter)
        {
            Entity entity = em.CreateEntity(typeof(DataContainerComponent), typeof(HeroProgressComponent));

            var save = LocalSaveSystem.LoadProgress();

            em.SetComponentData(entity, new DataContainerComponent
            {
                playerID = 1,
                playerName = default,
                teamID = 1,
                selectedLoadoutID = -1,
                selectedSquads = default,
                selectedPerks = default,
                totalLeadershipUsed = 0,
                selectedSpawnID = 1,
                isReady = false
            });

            em.SetComponentData(entity, new HeroProgressComponent
            {
                level = save.level,
                currentXP = save.currentXP,
                xpToNextLevel = CalculateNext(save.level),
                perkPoints = save.perkPoints
            });
        }
    }

    protected override void OnUpdate()
    {
        // This system only holds persistent data; no per-frame logic required.
    }

    static int CalculateNext(int level)
    {
        return (int)Unity.Mathematics.math.floor(100 * Unity.Mathematics.math.pow(1.2f, level - 1));
    }
}

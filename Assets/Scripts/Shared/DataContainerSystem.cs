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
            Entity entity = em.CreateEntity(typeof(DataContainerComponent), typeof(HeroProgressComponent));

            var save = LocalSaveSystem.LoadGame();
            if (save.heroProgress == null)
                save.heroProgress = new LocalSaveSystem.HeroProgressData();

            em.SetComponentData(entity, new DataContainerComponent
            {
                playerID = 0,
                playerName = default,
                teamID = 0,
                selectedLoadoutID = -1,
                selectedSquads = default,
                selectedPerks = default,
                totalLeadershipUsed = 0,
                selectedSpawnID = -1,
                isReady = false
            });

            em.SetComponentData(entity, new HeroProgressComponent
            {
                level = save.heroProgress.level,
                currentXP = save.heroProgress.currentXP,
                xpToNextLevel = save.heroProgress.xpToNextLevel,
                perkPoints = save.heroProgress.perkPoints
            });
        }
    }

    protected override void OnUpdate()
    {
        // This system only holds persistent data; no per-frame logic required.
    }
}

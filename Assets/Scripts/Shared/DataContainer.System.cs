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
            em.AddBuffer<SquadIdMapElement>(entity);

            // Hero progression is captured from the addressed profile by battle bootstrap.

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
                level = 1,
                currentXP = 0,
                xpToNextLevel = HeroExperienceCalculator.GetBattleXPForNextLevel(1),
                perkPoints = 0
            });
        }
    }

    protected override void OnUpdate()
    {
        // This system only holds persistent data; no per-frame logic required.
    }

}

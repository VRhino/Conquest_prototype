using Unity.Entities;
using UnityEngine;

/// <summary>
/// MonoBehaviour used to create the persistent <see cref="DataContainerComponent"/>
/// at the start of the application.
/// </summary>
public class DataContainerAuthoring : MonoBehaviour
{
    public string playerName = "Player";
    public int playerID = 0;
    public int teamID = 0;

    class DataContainerBaker : Unity.Entities.Baker<DataContainerAuthoring>
    {
        public override void Bake(DataContainerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new DataContainerComponent
            {
                playerID = authoring.playerID,
                playerName = authoring.playerName,
                teamID = authoring.teamID,
                selectedLoadoutID = -1,
                selectedSquads = default,
                selectedPerks = default,
                totalLeadershipUsed = 0,
                selectedSpawnID = -1,
                isReady = false
            });
        }
    }
}

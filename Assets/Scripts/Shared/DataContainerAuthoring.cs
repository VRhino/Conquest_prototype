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
    public int selectedSpawnID = -1;
}

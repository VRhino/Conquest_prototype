using UnityEngine;
using Unity.Mathematics;

public class HeroSpawnAuthoring : MonoBehaviour
{
    public int spawnId = -1;
    public Vector3 spawnPosition = Vector3.zero;
    public Quaternion spawnRotation = Quaternion.identity;
    public bool hasSpawned = false;
}

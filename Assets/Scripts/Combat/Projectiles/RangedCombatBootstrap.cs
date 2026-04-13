using UnityEngine;

/// <summary>
/// Scene MonoBehaviour that pre-warms projectile pools for all ranged squad types.
/// Place in the battle scene and assign every distance SquadData asset in the Inspector.
/// Each SquadData must have projectilePoolKey and projectilePrefab set.
/// </summary>
public class RangedCombatBootstrap : MonoBehaviour
{
    [SerializeField] SquadData[] rangedSquads;
    [SerializeField] int poolSizePerType = 40;

    void Start()
    {
        if (ObjectPoolSystem.Instance == null || rangedSquads == null)
            return;

        foreach (var squadData in rangedSquads)
        {
            if (squadData == null || !squadData.IsRanged)
                continue;

            var r = squadData.rangedData;
            if (string.IsNullOrEmpty(r.projectilePoolKey) || r.projectilePrefab == null)
            {
                Debug.LogWarning($"[RangedCombatBootstrap] {squadData.squadName} tiene rangedData pero le falta projectilePoolKey o projectilePrefab.");
                continue;
            }

            ObjectPoolSystem.Instance.WarmUpPool(
                r.projectilePoolKey,
                r.projectilePrefab,
                poolSizePerType);
        }
    }
}

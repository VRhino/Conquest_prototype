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

/// <summary>
/// La reserva de proyectiles (ObjectPoolSystem) y los proyectiles de cada tropa de tiro, si la escena no los trae. Solo BattleTestScene
/// los tiene (con RangedCombatBootstrap): en BattleScene, sin reserva, ProjectileSpawnSystem no dispararía nada. La crea
/// ProjectileSpawnSystem con el primer disparo (en un jugador) y la batalla en red al montarse (en el servidor y en el cliente).
/// </summary>
public static class ReservaDeProyectiles
{
    const int PorTropa = 20;

    public static void Asegurar()
    {
        if (ObjectPoolSystem.Instance == null)
            new GameObject("ObjectPoolSystem").AddComponent<ObjectPoolSystem>();
        int tropas = 0;
        foreach (var tropa in SquadDataService.GetAllSquads())
        {
            if (tropa == null || !tropa.IsRanged)
                continue;
            var tiro = tropa.rangedData;
            if (string.IsNullOrEmpty(tiro.projectilePoolKey) || tiro.projectilePrefab == null)
                continue;
            ObjectPoolSystem.Instance.WarmUpPool(tiro.projectilePoolKey, tiro.projectilePrefab, PorTropa);
            tropas++;
        }
        Debug.Log($"[Proyectiles] Reserva de proyectiles lista para {tropas} tropas de tiro");
    }
}

// Los arqueros en una escena sin reserva de proyectiles (suelto 12 de la batalla): BattleScene no trae ObjectPoolSystem ni RangedCombatBootstrap
// (solo BattleTestScene), y ProjectileSpawnSystem no disparaba nada. Ahora crea la reserva con el primer disparo.
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

public class ProyectilesSinReservaTests
{
    [UnityTest]
    public IEnumerator SinReservaEnLaEscenaElPrimerDisparoLaCreaYSaleElProyectil()
    {
        if (ObjectPoolSystem.Instance != null) Object.DestroyImmediate(ObjectPoolSystem.Instance.gameObject);
        yield return null;
        Assert.IsNull(ObjectPoolSystem.Instance, "sin reserva en la escena, como BattleScene");
        var tropa = SquadDataService.GetAllSquads().FirstOrDefault(t => t != null && t.IsRanged && !string.IsNullOrEmpty(t.rangedData.projectilePoolKey) && t.rangedData.projectilePrefab != null);
        Assert.IsNotNull(tropa, "una tropa de tiro con su proyectil");

        using var world = new World("Proyectiles sin reserva");
        var em = world.EntityManager;
        var peticion = em.CreateEntity(typeof(ProjectileSpawnRequest));
        em.SetComponentData(peticion, new ProjectileSpawnRequest
        {
            spawnPosition = new float3(0, 1.5f, 0), attackDirection = math.forward(), multiplier = 1f,
            poolKey = new FixedString32Bytes(tropa.rangedData.projectilePoolKey), trajectory = ProjectileTrajectory.Arc,
        });
        var disparo = world.GetOrCreateSystemManaged<ProjectileSpawnSystem>();
        var ecb = world.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        disparo.Update();
        ecb.Update();

        Assert.IsNotNull(ObjectPoolSystem.Instance, "la reserva, creada con el primer disparo");
        Assert.IsFalse(em.Exists(peticion), "la petición, consumida");
        var enVuelo = Object.FindObjectsByType<ProjectileController>(FindObjectsSortMode.None).Where(p => p.gameObject.activeInHierarchy).ToList();
        Assert.IsNotEmpty(enVuelo, "el proyectil, en vuelo");
        Object.DestroyImmediate(ObjectPoolSystem.Instance.gameObject);
    }
}

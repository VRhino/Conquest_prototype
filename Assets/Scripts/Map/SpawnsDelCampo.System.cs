// Los puntos de spawn del campo del lugar (Campo_De_Batalla_Del_Lugar.md §6.8): cuando BattleScene monta un campo (`MontajeDelCampo`), los
// seis puntos de la subescena (1 a 3 del atacante, 4 a 6 del defensor) pasan a sus placas. Los que la subescena no trae se crean. Corre una
// vez por campo, antes de que nadie los lea.
using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class SpawnsDelCampoSystem : SystemBase
{
    Unity.Mathematics.float3[] _aplicados;

    protected override void OnUpdate()
    {
        var spawns = MontajeDelCampo.Spawns;
        if (spawns == null || spawns == _aplicados) return;
        var consulta = SystemAPI.QueryBuilder().WithAllRW<SpawnPointComponent>().Build();
        if (consulta.IsEmpty) return; // la subescena aún no ha cargado
        var hechos = new bool[spawns.Length];
        foreach (var punto in SystemAPI.Query<RefRW<SpawnPointComponent>>())
        {
            int i = punto.ValueRO.spawnID - 1;
            if (i < 0 || i >= spawns.Length) continue;
            punto.ValueRW.position = spawns[i];
            punto.ValueRW.teamID = i < 3 ? 1 : 2;
            punto.ValueRW.isActive = true;
            hechos[i] = true;
        }
        for (int i = 0; i < spawns.Length; i++)
            if (!hechos[i])
                EntityManager.AddComponentData(EntityManager.CreateEntity(), new SpawnPointComponent { spawnID = i + 1, teamID = i < 3 ? 1 : 2, position = spawns[i], isActive = true });
        _aplicados = spawns;
        UnityEngine.Debug.Log($"[Campo] spawns del campo puestos en la subescena: atacante {spawns[1]}, defensor {spawns[4]}");
    }
}

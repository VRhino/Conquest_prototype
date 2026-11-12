using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;

namespace ConquestTactics.Net
{
    /// <summary>Servidor: el fantasma que lleva por la red a este soldado o héroe de la batalla de verdad.</summary>
    public struct Espejo : ICleanupComponentData
    {
        public Entity Fantasma;
    }

    /// <summary>
    /// Servidor de la batalla de verdad (<c>-batallaReal</c>): cada soldado y cada héroe de Game.ECS tiene un fantasma
    /// espejo que copia su posición cada tick, y eso es lo único que viaja a los clientes. Así los 99 sistemas del juego
    /// no saben nada de Netcode. Cuando el de verdad muere, su espejo se borra.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(FinTickServidorSystem))]
    public partial struct EspejoSystem : ISystem
    {
        EntityQuery _conEspejo;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PrefabsRed>();
            _conEspejo = SystemAPI.QueryBuilder().WithAll<Espejo, LocalTransform>().Build();
            state.Enabled = OpcionesRed.BatallaReal > 0;
        }

        public void OnUpdate(ref SystemState state)
        {
            var prefab = SystemAPI.GetSingleton<PrefabsRed>().Soldado;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (pose, bando, entidad) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<TeamComponent>>()
                         .WithAll<UnitCombatComponent>().WithNone<Espejo>().WithEntityAccess())
                Crear(ref ecb, prefab, entidad, pose.ValueRO, bando.ValueRO.value, SoldadoRed.Soldado);
            foreach (var (pose, bando, entidad) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<TeamComponent>>()
                         .WithAll<HeroSpawnComponent>().WithNone<Espejo>().WithEntityAccess())
                Crear(ref ecb, prefab, entidad, pose.ValueRO, bando.ValueRO.value, SoldadoRed.Heroe);
            // Al destruirse el de verdad solo le queda este componente: se va su fantasma y luego él.
            foreach (var (espejo, entidad) in SystemAPI.Query<RefRO<Espejo>>().WithNone<LocalTransform>().WithEntityAccess())
            {
                ecb.DestroyEntity(espejo.ValueRO.Fantasma);
                ecb.RemoveComponent<Espejo>(entidad);
            }
            ecb.Playback(state.EntityManager);

            using var espejos = _conEspejo.ToComponentDataArray<Espejo>(Allocator.Temp);
            using var poses = _conEspejo.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            for (int i = 0; i < espejos.Length; i++)
                state.EntityManager.SetComponentData(espejos[i].Fantasma, poses[i]);
        }

        static void Crear(ref EntityCommandBuffer ecb, Entity prefab, Entity deVerdad, in LocalTransform pose, Team bando, byte tipo)
        {
            var fantasma = ecb.Instantiate(prefab);
            ecb.SetComponent(fantasma, pose);
            ecb.SetComponent(fantasma, new SoldadoRed { Bando = (byte)(bando == Team.TeamB ? 1 : 0), Tipo = tipo });
            ecb.AddComponent(deVerdad, new Espejo { Fantasma = fantasma });
        }
    }
}

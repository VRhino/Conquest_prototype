using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace ConquestTactics.Net
{
    /// <summary>Cliente: en cuanto tiene id de red, pide entrar en la batalla.</summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct EntrarEnBatallaClienteSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PrefabsRed>();
            state.RequireForUpdate(state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                .WithAll<NetworkId>().WithNone<NetworkStreamInGame>()));
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (_, conexion) in SystemAPI.Query<RefRO<NetworkId>>().WithNone<NetworkStreamInGame>().WithEntityAccess())
            {
                ecb.AddComponent<NetworkStreamInGame>(conexion);
                var peticion = ecb.CreateEntity();
                // Sin token (la batalla de prueba), una identidad de este proceso: si se cae y vuelve, recupera su héroe.
                OpcionesRed.Token ??= System.Guid.NewGuid().ToString("N");
                ecb.AddComponent(peticion, new EntrarEnBatalla { Token = OpcionesRed.Token });
                ecb.AddComponent(peticion, new SendRpcCommandRequest { TargetConnection = conexion });
            }
            ecb.Playback(state.EntityManager);
        }
    }

    /// <summary>
    /// Servidor: al recibir la petición crea el héroe del jugador (predicho por su dueño) y sus soldados. La primera
    /// vez crea también los héroes sin jugador de la medición. Lo del jugador cuelga de su conexión y se borra si se va.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct EntrarEnBatallaServidorSystem : ISystem
    {
        bool _extrasCreados;

        public void OnCreate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<ClientServerTickRate>())
            {
                var tasa = new ClientServerTickRate();
                tasa.ResolveDefaults();
                tasa.NetworkTickRate = OpcionesRed.TasaRed;
                tasa.TargetFrameRateMode = ClientServerTickRate.FrameRateMode.Sleep;
                state.EntityManager.CreateSingleton(tasa);
            }
            state.RequireForUpdate<PrefabsRed>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var prefabs = SystemAPI.GetSingleton<PrefabsRed>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            if (OpcionesRed.Paquete > 0 && SystemAPI.TryGetSingletonRW<GhostSendSystemData>(out var envio))
                envio.ValueRW.DefaultSnapshotPacketSize = OpcionesRed.Paquete;

            // Con ticket (de un fichero o del lanzador, aunque aún no haya llegado) los héroes salen del ticket.
            bool deTicket = !string.IsNullOrEmpty(OpcionesRed.Ticket) || !string.IsNullOrEmpty(OpcionesRed.Lanzador);
            if (!_extrasCreados && !deTicket)
            {
                _extrasCreados = true;
                for (int i = 0; i < OpcionesRed.HeroesExtra; i++)
                {
                    var heroe = CrearHeroe(ref ecb, prefabs, Entity.Null, 0, 100 + i);
                    ecb.AddComponent(heroe, new HeroeSinJugador { Fase = i * 0.7f });
                }
            }

            foreach (var (origen, entrar, peticion) in SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<EntrarEnBatalla>>()
                         .WithEntityAccess())
            {
                var conexion = origen.ValueRO.SourceConnection;
                int id = SystemAPI.GetComponent<NetworkId>(conexion).Value;
                ecb.AddComponent<NetworkStreamInGame>(conexion);
                ecb.DestroyEntity(peticion);
                // En la batalla de verdad el que entra recibe su mando; MandoSystem le da uno de los héroes de la batalla.
                if (OpcionesRed.BatallaReal > 0)
                {
                    var mando = ecb.Instantiate(prefabs.Mando);
                    ecb.SetComponent(mando, new GhostOwner { NetworkId = id });
                    ecb.AddComponent(mando, new IdentidadDeMando { Token = entrar.ValueRO.Token });
                    ecb.AppendToBuffer(conexion, new LinkedEntityGroup { Value = mando });
                    // Con ticket, qué héroe lleva lo dice su token (BatallaDeTicketSystem); MandoSystem le da ese.
                    if (deTicket)
                        BatallaDeTicket.ConexionesSinHeroe.Enqueue((id, conexion, entrar.ValueRO.Token.ToString()));
                    continue;
                }
                if (deTicket)
                {
                    // Con ticket, los héroes y sus escuadras salen del ticket (BatallaDeTicketSystem).
                    BatallaDeTicket.ConexionesSinHeroe.Enqueue((id, conexion, entrar.ValueRO.Token.ToString()));
                    continue;
                }
                CrearHeroe(ref ecb, prefabs, conexion, id, id);
                Debug.Log($"[Red] Jugador {id} entra con {OpcionesRed.SoldadosPorHeroe} soldados");
            }
            ecb.Playback(state.EntityManager);
        }

        /// <summary>
        /// El héroe aparece en un anillo según su número. Héroe y soldados cuelgan de la conexión, no del héroe:
        /// un hijo en el LinkedEntityGroup de un fantasma sería un fantasma hijo y viajaría con él.
        /// </summary>
        static Entity CrearHeroe(ref EntityCommandBuffer ecb, PrefabsRed prefabs, Entity conexion, int dueno, int numero)
        {
            float angulo = numero * 2.39996f;
            float3 posicion = new float3(math.cos(angulo), 0f, math.sin(angulo)) * (10f + numero * 2f);
            byte bando = (byte)(numero % 2);

            var heroe = ecb.Instantiate(prefabs.Heroe);
            ecb.SetComponent(heroe, LocalTransform.FromPosition(posicion));
            ecb.SetComponent(heroe, new GhostOwner { NetworkId = dueno });
            if (conexion != Entity.Null)
                ecb.AppendToBuffer(conexion, new LinkedEntityGroup { Value = heroe });

            for (int hueco = 0; hueco < OpcionesRed.SoldadosPorHeroe; hueco++)
            {
                var soldado = ecb.Instantiate(prefabs.Soldado);
                ecb.SetComponent(soldado, LocalTransform.FromPosition(posicion + FormacionBasica.Hueco(hueco, quaternion.identity)));
                ecb.SetComponent(soldado, new SoldadoRed { Bando = bando });
                ecb.AddComponent(soldado, new SoldadoEnServidor { Heroe = heroe, Desfase = FormacionBasica.Hueco(hueco, quaternion.identity) });
                if (conexion != Entity.Null)
                    ecb.AppendToBuffer(conexion, new LinkedEntityGroup { Value = soldado });
            }
            return heroe;
        }
    }
}

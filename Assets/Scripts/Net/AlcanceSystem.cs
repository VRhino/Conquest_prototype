using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace ConquestTactics.Net
{
    /// <summary>
    /// Servidor de la batalla de verdad, con <c>-alcance M</c>: a cada jugador que lleva un héroe solo le llegan los
    /// soldados y héroes que están a menos de M metros del suyo (el juego deja de pintar las unidades a 120 m). Usa la
    /// relevancia de Netcode: lo que queda fuera deja de existir en ese cliente y vuelve a aparecer al acercarse. Para
    /// que no parpadee en el borde, sale a M + 15 y entra a M. Quien solo mira (sin héroe) lo recibe todo.
    /// ponytail: repasa todos los pares jugador-fantasma 4 veces por segundo; con 30 jugadores y 2.000 fantasmas habrá
    /// que repartirlo por celdas (Netcode trae GhostDistancePartitioning para eso).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct AlcanceSystem : ISystem
    {
        const float Histeresis = 15f;
        double _proximo;
        EntityQuery _fantasmas;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GhostRelevancy>();
            _fantasmas = SystemAPI.QueryBuilder().WithAll<SoldadoRed, GhostInstance, LocalTransform>().Build();
            state.Enabled = OpcionesRed.BatallaReal > 0 && OpcionesRed.Alcance > 0f;
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.Time.ElapsedTime < _proximo)
                return;
            _proximo = SystemAPI.Time.ElapsedTime + 0.25d;

            ref var relevancia = ref SystemAPI.GetSingletonRW<GhostRelevancy>().ValueRW;
            relevancia.GhostRelevancyMode = GhostRelevancyMode.SetIsIrrelevant;
            var fuera = relevancia.GhostRelevancySet;
            float entra = OpcionesRed.Alcance, sale = OpcionesRed.Alcance + Histeresis;

            using var ids = _fantasmas.ToComponentDataArray<GhostInstance>(Allocator.Temp);
            using var poses = _fantasmas.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            foreach (var (jugador, heroe) in SystemAPI.Query<RefRO<HeroeDeJugador>, RefRO<LocalTransform>>())
            {
                float2 centro = heroe.ValueRO.Position.xz;
                for (int i = 0; i < ids.Length; i++)
                {
                    if (ids[i].ghostId == 0)
                        continue;
                    var par = new RelevantGhostForConnection(jugador.ValueRO.Jugador, ids[i].ghostId);
                    float distancia = math.distance(centro, poses[i].Position.xz);
                    if (fuera.ContainsKey(par))
                    {
                        if (distancia < entra) fuera.Remove(par);
                    }
                    else if (distancia > sale)
                        fuera.TryAdd(par, 1);
                }
            }
        }
    }
}

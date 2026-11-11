using System.Diagnostics;
using System.IO;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ConquestTactics.Net
{
    /// <summary>Cronometra cada tick de simulación del servidor (de la primera a la última tarea del grupo).</summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial class InicioTickServidorSystem : SystemBase
    {
        internal static readonly Stopwatch Reloj = new Stopwatch();
        protected override void OnUpdate() => Reloj.Restart();
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial class FinTickServidorSystem : SystemBase
    {
        internal static double MsAcumulados;
        internal static double MsPeor;
        internal static int Ticks;

        protected override void OnUpdate()
        {
            double ms = InicioTickServidorSystem.Reloj.Elapsed.TotalMilliseconds;
            MsAcumulados += ms;
            if (ms > MsPeor) MsPeor = ms;
            Ticks++;
        }
    }

    /// <summary>
    /// Mide cada segundo lo que cuesta la batalla: bytes por la red (de las estadísticas de Unity Transport), coste
    /// del tick en el servidor, CPU del proceso, fantasmas y, en el cliente, el RTT. Tras <c>-medir</c> segundos
    /// escribe la media en <c>-informe</c> y cierra el proceso. Los 5 primeros segundos no cuentan (conexión).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class MedicionRedSystem : SystemBase
    {
        const float Calentamiento = 5f;

        readonly Stopwatch _reloj = Stopwatch.StartNew();
        double _ultimoSegundo;
        ulong _rxAntes, _txAntes;
        double _cpuAntes;
        int _framesSegundo;

        // Acumulado del periodo medido.
        int _muestras;
        double _sumaRxKbps, _sumaTxKbps, _sumaCpu, _sumaRtt, _sumaFps;
        double _picoTxKbps;
        int _fantasmas, _conexiones; // máximos del periodo
        bool _terminado;

        bool EsServidor => World.IsServer();

        protected override void OnCreate() => RequireForUpdate<NetworkStreamDriver>();

        protected override void OnUpdate()
        {
            _framesSegundo++;
            double ahora = _reloj.Elapsed.TotalSeconds;
            if (ahora - _ultimoSegundo < 1.0)
                return;
            double intervalo = ahora - _ultimoSegundo;
            _ultimoSegundo = ahora;

            LeerBytes(out ulong rx, out ulong tx);
            double rxKbps = (rx - _rxAntes) * 8 / 1000.0 / intervalo;
            double txKbps = (tx - _txAntes) * 8 / 1000.0 / intervalo;
            _rxAntes = rx;
            _txAntes = tx;

            double cpu = Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds;
            double cpuPorciento = (cpu - _cpuAntes) / intervalo * 100.0;
            _cpuAntes = cpu;
            double fps = _framesSegundo / intervalo;
            _framesSegundo = 0;

            int fantasmas = SystemAPI.QueryBuilder().WithAll<GhostInstance>().Build().CalculateEntityCount();
            int conexiones = SystemAPI.QueryBuilder().WithAll<NetworkStreamInGame>().Build().CalculateEntityCount();
            if (fantasmas > _fantasmas) _fantasmas = fantasmas;
            if (conexiones > _conexiones) _conexiones = conexiones;
            string heroe = "";
            foreach (var (pose, dueno, entrada) in SystemAPI.Query<RefRO<Unity.Transforms.LocalTransform>, RefRO<GhostOwner>, RefRO<EntradaHeroe>>()
                         .WithAll<HeroeRed>())
                if (dueno.ValueRO.NetworkId == 1)
                    heroe = $" heroe1=({pose.ValueRO.Position.x:F1},{pose.ValueRO.Position.z:F1}) entrada={entrada.ValueRO.Mover}";
            float rtt = SystemAPI.TryGetSingleton<NetworkSnapshotAck>(out var ack) ? ack.EstimatedRTT : 0f;

            double hzSoldado = 0;
            if (!EsServidor && FrescuraSoldadosSystem.Soldados.Data > 0)
            {
                hzSoldado = FrescuraSoldadosSystem.Actualizaciones.Data / intervalo / FrescuraSoldadosSystem.Soldados.Data;
                FrescuraSoldadosSystem.Actualizaciones.Data = 0;
            }

            double msTick = 0, msPeor = 0;
            if (EsServidor && FinTickServidorSystem.Ticks > 0)
            {
                msTick = FinTickServidorSystem.MsAcumulados / FinTickServidorSystem.Ticks;
                msPeor = FinTickServidorSystem.MsPeor;
                FinTickServidorSystem.MsAcumulados = 0;
                FinTickServidorSystem.MsPeor = 0;
                FinTickServidorSystem.Ticks = 0;
            }

            if (((int)ahora) % 5 == 0)
                Debug.Log($"[Red] {World.Name} t={ahora:F0}s conexiones={conexiones} fantasmas={fantasmas}{heroe} " +
                          $"rx={rxKbps:F0}kbit/s tx={txKbps:F0}kbit/s cpu={cpuPorciento:F0}% fps={fps:F0}" +
                          (EsServidor ? $" tick={msTick:F2}ms peor={msPeor:F2}ms" : $" rtt={rtt:F0}ms soldado={hzSoldado:F1}Hz"));

            if (ahora < Calentamiento)
                return;
            _muestras++;
            _sumaRxKbps += rxKbps;
            _sumaTxKbps += txKbps;
            _sumaCpu += cpuPorciento;
            _sumaRtt += rtt;
            _sumaFps += fps;
            _sumaHz += hzSoldado;
            if (txKbps > _picoTxKbps) _picoTxKbps = txKbps;
            _sumaTick += msTick;
            if (msPeor > _peorTick) _peorTick = msPeor;

            if (!_terminado && OpcionesRed.MedirSegundos > 0 && ahora >= Calentamiento + OpcionesRed.MedirSegundos)
                Terminar();
        }

        double _sumaTick, _peorTick, _sumaHz;

        void LeerBytes(out ulong rx, out ulong tx)
        {
            rx = tx = 0;
            ref var almacen = ref SystemAPI.GetSingletonRW<NetworkStreamDriver>().ValueRW.DriverStore;
            for (int id = almacen.FirstDriver; id < almacen.LastDriver; id++)
            {
                var estadisticas = almacen.GetDriverRO(id).GetStatistics();
                rx += estadisticas.RxTotalBytes;
                tx += estadisticas.TxTotalBytes;
            }
        }

        void Terminar()
        {
            _terminado = true;
            int n = _muestras;
            string linea = EsServidor
                ? $"servidor soldados/heroe={OpcionesRed.SoldadosPorHeroe} heroesExtra={OpcionesRed.HeroesExtra} " +
                  $"tasaRed={OpcionesRed.TasaRed} conexiones={_conexiones} fantasmas={_fantasmas} " +
                  $"tx={_sumaTxKbps / n:F0}kbit/s (pico {_picoTxKbps:F0}) rx={_sumaRxKbps / n:F0}kbit/s " +
                  $"tick={_sumaTick / n:F2}ms (peor {_peorTick:F2}) cpu={_sumaCpu / n:F0}% segundos={n}"
                : $"cliente fantasmas={_fantasmas} rx={_sumaRxKbps / n:F0}kbit/s tx={_sumaTxKbps / n:F0}kbit/s " +
                  $"rtt={_sumaRtt / n:F0}ms actualizaciones/soldado={_sumaHz / n:F1}Hz cpu={_sumaCpu / n:F0}% fps={_sumaFps / n:F0} segundos={n}";
            Debug.Log("[Red] INFORME " + linea);
            if (!string.IsNullOrEmpty(OpcionesRed.Informe))
                File.AppendAllText(OpcionesRed.Informe, linea + System.Environment.NewLine);
#if !UNITY_EDITOR
            Application.Quit();
#endif
        }
    }
}

namespace ConquestTactics.Net
{
    /// <summary>
    /// Cliente: cuántas veces por segundo llega de verdad cada soldado. Con muchos fantasmas el snapshot (un MTU por
    /// envío) no cabe entero y Netcode reparte los envíos por importancia: este número dice cuánto se queda atrás.
    /// Lee el tick del último snapshot recibido de cada fantasma (los 4 primeros bytes de su historial).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [Unity.Burst.BurstCompile]
    public partial struct FrescuraSoldadosSystem : ISystem
    {
        internal static readonly Unity.Burst.SharedStatic<long> Actualizaciones = Unity.Burst.SharedStatic<long>.GetOrCreate<FrescuraSoldadosSystem, ActualizacionesClave>();
        internal static readonly Unity.Burst.SharedStatic<int> Soldados = Unity.Burst.SharedStatic<int>.GetOrCreate<FrescuraSoldadosSystem, SoldadosClave>();
        struct ActualizacionesClave { }
        struct SoldadosClave { }

        Unity.Collections.NativeHashMap<Entity, uint> _visto;

        public void OnCreate(ref SystemState state) => _visto = new Unity.Collections.NativeHashMap<Entity, uint>(4096, Unity.Collections.Allocator.Persistent);
        public void OnDestroy(ref SystemState state) => _visto.Dispose();

        [Unity.Burst.BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            int soldados = 0;
            long nuevas = 0;
            foreach (var (datos, historial, entidad) in SystemAPI.Query<RefRO<SnapshotData>, DynamicBuffer<SnapshotDataBuffer>>()
                         .WithAll<SoldadoRed>().WithEntityAccess())
            {
                soldados++;
                if (historial.Length == 0) continue;
                int i = datos.ValueRO.LatestIndex * datos.ValueRO.SnapshotSize;
                uint tick = historial[i].Value | (uint)historial[i + 1].Value << 8 | (uint)historial[i + 2].Value << 16 | (uint)historial[i + 3].Value << 24;
                if (!_visto.TryGetValue(entidad, out var antes) || antes != tick)
                {
                    _visto[entidad] = tick;
                    nuevas++;
                }
            }
            Actualizaciones.Data += nuevas;
            Soldados.Data = soldados;
        }
    }
}

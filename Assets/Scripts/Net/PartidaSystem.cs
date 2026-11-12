using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace ConquestTactics.Net
{
    /// <summary>
    /// Servidor de la batalla de verdad: lleva a los clientes la partida entera en un solo fantasma. El reloj, el estado y
    /// el ganador (<see cref="PartidaRed"/>), y cada zona de captura o de suministro con su dueño y su progreso
    /// (<see cref="ZonaRed"/>). Son los mismos datos que el juego guarda en BattleClockComponent, MatchStateComponent y
    /// las entidades de zona; el cliente los vuelca en los suyos y su interfaz los pinta sin saber de dónde vienen.
    /// Al acabar la partida lo apunta en el registro y, medio minuto después, cierra el proceso.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(FinTickServidorSystem))]
    public partial class PartidaSystem : SystemBase
    {
        const double EsperaAlAcabar = 30d;

        Entity _partida;
        double _fin = -1d;
        NativeList<ZonaRed> _zonas;
        NativeList<byte> _duenos;
        double _proximoResumen;

        protected override void OnCreate()
        {
            RequireForUpdate<PrefabsRed>();
            RequireForUpdate<MatchStateComponent>();
            _zonas = new NativeList<ZonaRed>(16, Allocator.Persistent);
            _duenos = new NativeList<byte>(16, Allocator.Persistent);
            Enabled = OpcionesRed.BatallaReal > 0;
        }

        protected override void OnDestroy()
        {
            _zonas.Dispose();
            _duenos.Dispose();
        }

        protected override void OnUpdate()
        {
            // Sin zonas todavía es que la escena de batalla no ha terminado de cargar.
            if (!SystemAPI.TryGetSingleton<MatchStateComponent>(out var estado))
                return;
            if (_partida == Entity.Null)
                _partida = EntityManager.Instantiate(SystemAPI.GetSingleton<PrefabsRed>().Partida);

            var red = new PartidaRed { Estado = (byte)estado.currentState, Ganador = (byte)estado.winnerTeam };
            if (SystemAPI.TryGetSingleton<BattleClockComponent>(out var reloj))
            {
                red.Reloj = reloj.remainingSeconds;
                red.Agotado = (byte)(reloj.expired ? 1 : 0);
            }
            SystemAPI.SetComponent(_partida, red);

            _zonas.Clear();
            foreach (var (zona, pose, entidad) in SystemAPI.Query<RefRO<ZoneTriggerComponent>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                var z = new ZonaRed { Posicion = pose.ValueRO.Position, Dueno = (byte)zona.ValueRO.teamOwner };
                if (zona.ValueRO.isActive) z.Banderas |= ZonaRed.Activa;
                if (zona.ValueRO.isLocked) z.Banderas |= ZonaRed.Bloqueada;
                if (SystemAPI.HasComponent<CapturePointProgressComponent>(entidad))
                {
                    var captura = SystemAPI.GetComponent<CapturePointProgressComponent>(entidad);
                    z.Progreso = captura.captureProgress;
                    z.Equipo = (byte)captura.capturingTeam;
                    if (captura.isContested) z.Banderas |= ZonaRed.Disputada;
                    if (captura.isBeingCaptured) z.Banderas |= ZonaRed.Capturandose;
                }
                else if (SystemAPI.HasComponent<SupplyPointComponent>(entidad))
                {
                    var suministro = SystemAPI.GetComponent<SupplyPointComponent>(entidad);
                    z.Progreso = suministro.captureProgress;
                    z.Equipo = (byte)suministro.currentTeam;
                    if (suministro.isContested) z.Banderas |= ZonaRed.Disputada;
                    if (suministro.isCapturing) z.Banderas |= ZonaRed.Capturandose;
                }
                _zonas.Add(z);
            }
            var enviadas = SystemAPI.GetBuffer<ZonaRed>(_partida);
            if (enviadas.Length != _zonas.Length)
                enviadas.ResizeUninitialized(_zonas.Length);
            for (int i = 0; i < _zonas.Length; i++)
            {
                // Al registro, cada vez que una zona cambia de dueño.
                if (i < _duenos.Length && _duenos[i] != _zonas[i].Dueno)
                    Debug.Log(System.FormattableString.Invariant(
                        $"[BatallaReal] La zona de ({_zonas[i].Posicion.x:F0}, {_zonas[i].Posicion.z:F0}) pasa del equipo {_duenos[i]} al {_zonas[i].Dueno}"));
                enviadas[i] = _zonas[i];
            }
            _duenos.Clear();
            foreach (var zona in _zonas)
                _duenos.Add(zona.Dueno);
            if (SystemAPI.Time.ElapsedTime >= _proximoResumen)
            {
                _proximoResumen = SystemAPI.Time.ElapsedTime + 10d;
                Debug.Log(System.FormattableString.Invariant($"[BatallaReal] Zonas (reloj {red.Reloj:F0} s): ") + ZonaRed.Resumen(_zonas.AsArray()));
            }

            if (estado.currentState != MatchState.EndMatch)
                return;
            double ahora = SystemAPI.Time.ElapsedTime;
            if (_fin < 0d)
            {
                _fin = ahora;
                string ganador = estado.winnerTeam == 1 ? "el atacante" : estado.winnerTeam == 2 ? "el defensor" : "nadie";
                Debug.Log(System.FormattableString.Invariant(
                    $"[BatallaReal] Fin de la batalla a los {ahora:F0} s: gana {ganador} (reloj {red.Reloj:F0} s, agotado {red.Agotado})"));
            }
#if !UNITY_EDITOR
            // Con ticket, el proceso sale cuando el motor acusa su resultado (BatallaDeTicketSystem), no aquí.
            else if (ahora - _fin > EsperaAlAcabar && BatallaDeTicket.Actual == null)
                Application.Quit();
#endif
        }
    }
}

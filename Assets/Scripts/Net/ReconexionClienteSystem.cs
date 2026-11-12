using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace ConquestTactics.Net
{
    /// <summary>
    /// Cliente: vigila su conexión. Si el servidor lleva un rato callado, lo avisa; si la conexión se cae (un corte de
    /// red, el servidor que no contesta), la vuelve a pedir cada pocos segundos y el servidor le devuelve su héroe (por
    /// su token; en la batalla de prueba, por la identidad de este proceso). No insiste si fue el servidor quien cerró
    /// (token no válido, versión distinta) ni si la partida ya había acabado.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class ReconexionClienteSystem : SystemBase
    {
        /// <summary>Segundos entre dos peticiones de conexión, y segundos sin snapshots para avisar.</summary>
        const double Reintento = 2d;
        const double Callado = 2d;
        /// <summary>Segundos sin snapshots para dejar de adelantar al héroe propio (lo mismo que espera el servidor para pararlo).</summary>
        const double Mudo = 0.5d;

        /// <summary>Lo que hay que decirle al jugador sobre su conexión; vacío = todo va bien.</summary>
        public static string Aviso { get; private set; } = "";
        public static int Reconexiones { get; private set; }
        /// <summary>
        /// Hace un momento que no llega nada del servidor: el héroe propio deja de adelantarse (<see cref="MovimientoMandoSystem"/>).
        /// Si siguiera andando solo, al volver la señal daría un salto atrás de metros (20 m tras un corte de 7 s).
        /// </summary>
        public static bool SinNoticias { get; private set; }

        double _proximoIntento, _ultimaNoticia;
        NetworkTick _ultimoSnapshot;
        bool _estuvoDentro, _dentro, _noInsistir, _acabada, _apagado;

        protected override void OnCreate() => RequireForUpdate<NetworkStreamDriver>();

        protected override void OnUpdate()
        {
            double ahora = UnityEngine.Time.realtimeSinceStartupAsDouble;
            string antes = Aviso;
            ref var driver = ref SystemAPI.GetSingletonRW<NetworkStreamDriver>().ValueRW;
            Apagon(ref driver, ahora);

            foreach (var evento in driver.ConnectionEventsForTick)
            {
                if (evento.State != ConnectionState.State.Disconnected)
                    continue;
                var razon = evento.DisconnectReason;
                // Se insiste si se cayó sola (sin respuesta) o la cerró este lado; no si la cerró el servidor.
                _noInsistir = _acabada || !(razon is NetworkStreamDisconnectReason.Timeout
                    or NetworkStreamDisconnectReason.MaxConnectionAttempts or NetworkStreamDisconnectReason.ConnectionClose);
                _dentro = false;
                Debug.Log($"[Red] Conexión perdida ({razon})" + (_noInsistir ? "" : ": se vuelve a pedir"));
            }
            foreach (var partida in SystemAPI.Query<RefRO<PartidaRed>>())
                _acabada |= partida.ValueRO.Estado == (byte)MatchState.EndMatch;

            if (SystemAPI.TryGetSingleton<NetworkSnapshotAck>(out var ack) && ack.LastReceivedSnapshotByLocal != _ultimoSnapshot)
            {
                _ultimoSnapshot = ack.LastReceivedSnapshotByLocal;
                _ultimaNoticia = ahora;
            }
            SinNoticias = _estuvoDentro && ahora - _ultimaNoticia > Mudo;
            bool hayConexion = !SystemAPI.QueryBuilder().WithAll<NetworkStreamConnection>().Build().IsEmpty;
            bool enJuego = !SystemAPI.QueryBuilder().WithAll<NetworkStreamInGame>().Build().IsEmpty;
            string esperando = _estuvoDentro ? "Conexión perdida. Reconectando…" : "Conectando con el servidor…";

            if (enJuego && ahora - _ultimaNoticia < Callado)
            {
                if (!_dentro && _estuvoDentro)
                    Debug.Log($"[Red] De vuelta en la batalla (reconexión {++Reconexiones})");
                _dentro = _estuvoDentro = true;
                Aviso = "";
            }
            else if (hayConexion)
                Aviso = _dentro ? "Sin noticias del servidor…" : esperando;
            else if (_noInsistir)
                Aviso = _acabada ? "" : "El servidor cerró la conexión.";
            else
            {
                Aviso = esperando;
                if (ahora >= _proximoIntento)
                {
                    _proximoIntento = ahora + Reintento;
                    driver.Connect(EntityManager, ClientServerBootstrap.DefaultConnectAddress);
                }
            }

            if (Aviso != antes)
            {
                Debug.Log($"[Red] Aviso al jugador: «{Aviso}»");
                if (Aviso.Length > 0 && !Application.isBatchMode && AvisoDeRed.Instancia == null)
                    new GameObject("AvisoDeRed").AddComponent<AvisoDeRed>();
            }
        }

        /// <summary>
        /// Prueba (<c>-apagon inicio,duracion</c>, solo con el simulador de red de Netcode): durante ese tramo no pasa ni un
        /// paquete en ningún sentido, como un corte de red de verdad.
        /// </summary>
        void Apagon(ref NetworkStreamDriver driver, double ahora)
        {
#if UNITY_EDITOR || NETCODE_DEBUG
            if (OpcionesRed.ApagonDura <= 0f || !NetworkSimulatorSettings.Enabled)
                return;
            bool apagado = ahora >= OpcionesRed.ApagonA && ahora < OpcionesRed.ApagonA + OpcionesRed.ApagonDura;
            if (apagado == _apagado)
                return;
            _apagado = apagado;
            var parametros = NetworkSimulatorSettings.ClientSimulatorParameters;
            if (apagado) parametros.PacketDropPercentage = 100;
            NetworkSimulatorSettings.RefreshSimulationPipelineParametersLive(in parametros, ref driver.DriverStore);
            Debug.Log(apagado ? $"[Red] APAGÓN de {OpcionesRed.ApagonDura:F0} s: no pasa ni un paquete" : "[Red] Fin del apagón");
#endif
        }
    }

    /// <summary>
    /// El aviso de conexión en pantalla.
    /// ponytail: una caja de OnGUI arriba y en el centro; el aviso de verdad llega con la interfaz del juego.
    /// </summary>
    public class AvisoDeRed : MonoBehaviour
    {
        public static AvisoDeRed Instancia { get; private set; }
        GUIStyle _estilo;

        void Awake()
        {
            Instancia = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnGUI()
        {
            string aviso = ReconexionClienteSystem.Aviso;
            if (string.IsNullOrEmpty(aviso))
                return;
            _estilo ??= new GUIStyle(GUI.skin.box) { fontSize = 22, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            GUI.Box(new Rect(Screen.width / 2f - 260f, 24f, 520f, 48f), aviso, _estilo);
        }
    }
}

// La sesión del contrato 05 (puerto de `BronzeAgeClient/src/contrato/sesion.ts`): un WebSocket con tramas binarias (una trama = un
// `MensajeDeServidor` / `MensajeDeCliente`), zstd negociado, secuencia sin huecos, órdenes con respuesta causal, crónica, `CambiarVista`, reloj
// del mundo sincronizado y reconexión con reanudación por versión con lo que guarda el `Almacen`. Es solo transporte: no sabe nada de la interfaz.
//
// Hilos: el WebSocket recibe en un hilo de fondo y solo encola las tramas. Todo lo demás (descomprimir, aplicar al almacén, resolver órdenes,
// reconectar y avisar a los oyentes) pasa en `Bombear()`, que llama el hilo principal (en Unity, `ConexionDeMundo.Update`). Así el almacén y
// los oyentes solo se tocan desde un hilo y nada de Unity se toca fuera del principal.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using BronzeAge.Contract.V1;
using Google.Protobuf;

namespace ConquestTactics.Mundo
{
    public enum EstadoSesion { Desconectada, Conectando, Abierta, Reconectando, Cerrada }

    /// <summary>Una sesión que acaba con un `ErrorSesion` fatal (token caducado, rol no permitido, sesión reemplazada...).</summary>
    public sealed class ErrorDeSesion : Exception
    {
        public readonly string Codigo;
        public readonly bool Fatal;
        public ErrorDeSesion(string codigo, string mensaje, bool fatal) : base(mensaje) { Codigo = codigo; Fatal = fatal; }
    }

    public sealed class Sesion
    {
        /// <summary>`ws://host:puerto/` del servidor.</summary>
        public readonly string Url;
        public readonly string Token;
        public readonly string VersionCliente;
        /// <summary>El hash del artefacto de mundo que ya se tiene en disco (no hace falta bajarlo otra vez).</summary>
        public string MundoHash;
        /// <summary>Reconectar solo al caerse la conexión (por defecto sí).</summary>
        public bool Reconectar = true;

        public readonly Almacen Almacen;
        public EstadoSesion Estado { get; private set; } = EstadoSesion.Desconectada;
        public Bienvenida Bienvenida { get; private set; }
        /// <summary>El último error que cerró la sesión (null si no lo hubo).</summary>
        public ErrorSesion UltimoError { get; private set; }
        /// <summary>Tramas recibidas y cuántas venían en zstd (para la pantalla de diagnóstico).</summary>
        public long TramasRecibidas { get; private set; }
        public long TramasZstd { get; private set; }

        /// <summary>El ámbito que cambió (foto, delta o revocado), ya aplicado al almacén.</summary>
        public event Action<string> AlCambiar;
        /// <summary>Cada hecho de una delta, con el ámbito por el que llegó.</summary>
        public event Action<string, EventoDominio> AlEvento;
        /// <summary>Un `Aviso` del servidor, o un `ErrorSesion` no fatal convertido en aviso.</summary>
        public event Action<Aviso> AlAviso;
        public event Action<EstadoSesion> AlEstado;
        public event Action<Bienvenida> AlBienvenida;

        const int PingMs = 10_000;
        static readonly byte[] Zstd = { 0x28, 0xb5, 0x2f, 0xfd };

        // Lo que deja el hilo de red para el principal: una trama de la conexión `n`, o su cierre (trama null).
        readonly ConcurrentQueue<(int conexion, byte[] trama)> entrantes = new ConcurrentQueue<(int, byte[])>();
        readonly Stopwatch relojLocal = Stopwatch.StartNew();
        readonly Dictionary<string, TaskCompletionSource<ResultadoOrden>> ordenes = new Dictionary<string, TaskCompletionSource<ResultadoOrden>>();
        readonly Dictionary<string, TaskCompletionSource<Cronica>> cronicas = new Dictionary<string, TaskCompletionSource<Cronica>>();
        readonly List<(string peticionId, MensajeDeCliente mensaje)> enEspera = new List<(string, MensajeDeCliente)>();
        readonly ZstdSharp.Decompressor descompresor = new ZstdSharp.Decompressor();

        ClientWebSocket ws;
        CancellationTokenSource cancelar;
        readonly SemaphoreSlim enviando = new SemaphoreSlim(1, 1);
        int conexion;
        ulong secuencia;
        Rectangulo vista;
        int intentos;
        double proximoIntentoMs = double.NaN;
        double proximoPingMs = double.NaN;
        bool cerradaAPeticion;
        TaskCompletionSource<Bienvenida> abriendo;

        // El reloj de mundo: `mundoMs` en el instante local `localMs`, con su factor; `rtt` de la mejor muestra.
        double relojMundoMs, relojLocalMs, relojFactor = 1, relojRtt = double.PositiveInfinity;
        bool relojPausado = true;

        /// <summary>Qué cliente se presenta en el `Hola`: Unity; las pruebas abren también sesiones de administración (`ClienteAdminWeb`, solo
        /// con una cuenta admin del servidor de prueba) para preparar el mundo.</summary>
        public TipoCliente Tipo = TipoCliente.ClienteUnity;

        public Sesion(string url, string token, string versionCliente, Almacen almacen = null)
        {
            Url = url;
            Token = token;
            VersionCliente = versionCliente;
            Almacen = almacen ?? new Almacen();
        }

        double AhoraLocal => relojLocal.Elapsed.TotalMilliseconds;

        /// <summary>Abre la sesión; la tarea acaba con la `Bienvenida` (las fotos de los ámbitos llegan justo después y avisan por `AlCambiar`).
        /// Hay que ir llamando a `Bombear()` para que avance.</summary>
        public Task<Bienvenida> Abrir()
        {
            cerradaAPeticion = false;
            abriendo = new TaskCompletionSource<Bienvenida>();
            Conectar(EstadoSesion.Conectando);
            return abriendo.Task;
        }

        public void Cerrar()
        {
            cerradaAPeticion = true;
            proximoPingMs = proximoIntentoMs = double.NaN;
            SoltarConexion();
            FijarEstado(EstadoSesion.Cerrada);
            FallarPendientes(new ErrorDeSesion("cerrada", "sesión cerrada", false));
        }

        /// <summary>Da una orden (con el cuerpo puesto; la sesión pone `peticion_id`). Acaba con su `ResultadoOrden`: cuando llega, el almacén ya
        /// tiene lo que la orden cambió (respuesta causal). Una orden en vuelo al caerse la conexión se vuelve a mandar con el mismo id.</summary>
        public Task<ResultadoOrden> Ordenar(Orden orden, ulong? versionEsperada = null)
        {
            orden.PeticionId = Guid.NewGuid().ToString();
            if (versionEsperada.HasValue) orden.VersionEsperada = versionEsperada.Value;
            var t = new TaskCompletionSource<ResultadoOrden>();
            ordenes[orden.PeticionId] = t;
            Encolar(orden.PeticionId, new MensajeDeCliente { Orden = orden });
            return t.Task;
        }

        /// <summary>Una página de la crónica pública (hacia atrás con `hasta`, hacia delante con `desde`).</summary>
        public Task<Cronica> PedirCronica(ulong? desde = null, ulong? hasta = null, uint limite = 0)
        {
            var p = new PedirCronica { PeticionId = Guid.NewGuid().ToString(), Limite = limite };
            if (desde.HasValue) p.DesdeVersion = desde.Value;
            if (hasta.HasValue) p.HastaVersion = hasta.Value;
            var t = new TaskCompletionSource<Cronica>();
            cronicas[p.PeticionId] = t;
            Encolar(p.PeticionId, new MensajeDeCliente { PedirCronica = p });
            return t.Task;
        }

        /// <summary>Si ya se pidió la `VISTA`.</summary>
        public bool TieneVista => vista != null;

        /// <summary>Fija el rectángulo de la `VISTA` (milésimas de unidad). Al reconectar se vuelve a pedir solo.</summary>
        public void CambiarVista(Rectangulo r)
        {
            vista = r;
            if (Estado == EstadoSesion.Abierta) Enviar(MensajeDeClienteVista(r));
        }

        /// <summary>El instante de mundo ahora, en ms del contrato (desde 2026-01-01), con el reloj sincronizado.</summary>
        public double AhoraMundo => relojPausado ? relojMundoMs : relojMundoMs + (AhoraLocal - relojLocalMs) * relojFactor;
        public bool RelojPausado => relojPausado;
        public double FactorReloj => relojFactor;
        /// <summary>La mejor ida y vuelta medida con el ping, en ms (infinito sin muestra).</summary>
        public double Rtt => relojRtt;

        /// <summary>Procesa lo que haya llegado y lo que toque por tiempo (ping, reintento). Desde el hilo principal, cada fotograma.</summary>
        public void Bombear()
        {
            while (entrantes.TryDequeue(out var x))
            {
                if (x.conexion != conexion) continue; // de una conexión vieja: su secuencia no es la de la nueva
                if (x.trama == null) { AlCerrarse(); continue; }
                Recibir(x.trama);
            }
            var ahora = AhoraLocal;
            if (ahora >= proximoPingMs && Estado == EstadoSesion.Abierta) Ping();
            if (ahora >= proximoIntentoMs && !cerradaAPeticion)
            {
                proximoIntentoMs = double.NaN;
                Conectar(EstadoSesion.Reconectando);
            }
        }

        // ─────────────────────────── por dentro ───────────────────────────

        void Conectar(EstadoSesion estado)
        {
            FijarEstado(estado);
            SoltarConexion();
            var n = ++conexion;
            secuencia = 0;
            var socket = new ClientWebSocket();
            var c = new CancellationTokenSource();
            ws = socket;
            cancelar = c;
            var hola = Almacen.Hola(Token, Tipo, VersionCliente, MundoHash, (long)AhoraLocal);
            _ = Task.Run(() => Escuchar(socket, n, new MensajeDeCliente { Hola = hola }.ToByteArray(), c.Token));
        }

        /// <summary>El hilo de red: abre, manda el `Hola` y encola cada trama entera. Acaba encolando el cierre.</summary>
        async Task Escuchar(ClientWebSocket socket, int n, byte[] hola, CancellationToken ct)
        {
            try
            {
                await socket.ConnectAsync(new Uri(Url), ct).ConfigureAwait(false);
                await EnviarCrudo(socket, hola).ConfigureAwait(false);
                var bufer = new byte[64 * 1024];
                var trama = new System.IO.MemoryStream();
                while (!ct.IsCancellationRequested)
                {
                    var r = await socket.ReceiveAsync(new ArraySegment<byte>(bufer), ct).ConfigureAwait(false);
                    if (r.MessageType == WebSocketMessageType.Close) break;
                    trama.Write(bufer, 0, r.Count);
                    if (!r.EndOfMessage) continue;
                    entrantes.Enqueue((n, trama.ToArray()));
                    trama.SetLength(0);
                }
            }
            catch (Exception) { /* caída, cierre o cancelación: lo decide `AlCerrarse` */ }
            entrantes.Enqueue((n, null));
        }

        void Recibir(byte[] trama)
        {
            TramasRecibidas++;
            if (trama.Length >= 4 && trama[0] == Zstd[0] && trama[1] == Zstd[1] && trama[2] == Zstd[2] && trama[3] == Zstd[3])
            {
                TramasZstd++;
                trama = descompresor.Unwrap(trama).ToArray();
            }
            var m = MensajeDeServidor.Parser.ParseFrom(trama);
            if (m.Secuencia != secuencia + 1)
            {
                // Un hueco es un error del servidor: se reanuda con lo que hay.
                ReiniciarConexion();
                return;
            }
            secuencia = m.Secuencia;
            foreach (var kv in m.NombresHeroes) Almacen.NombresHeroes[kv.Key] = kv.Value;
            switch (m.CuerpoCase)
            {
                case MensajeDeServidor.CuerpoOneofCase.Bienvenida: AlLlegarBienvenida(m.Bienvenida); break;
                case MensajeDeServidor.CuerpoOneofCase.Foto:
                case MensajeDeServidor.CuerpoOneofCase.Delta:
                case MensajeDeServidor.CuerpoOneofCase.Revocado:
                    string ambito;
                    try { ambito = Almacen.Aplicar(m); }
                    catch (Incoherencia e)
                    {
                        // Lo que no encaja se tira y se pide de nuevo (llega como foto). Solo ese ámbito, si se sabe cuál.
                        if (e.Ambito != null) Almacen.Ambitos.Remove(e.Ambito);
                        else Almacen.Vaciar();
                        ReiniciarConexion();
                        return;
                    }
                    if (m.CuerpoCase == MensajeDeServidor.CuerpoOneofCase.Delta)
                        foreach (var ev in m.Delta.Eventos) AlEvento?.Invoke(ambito, ev);
                    if (ambito != null) AlCambiar?.Invoke(ambito);
                    break;
                case MensajeDeServidor.CuerpoOneofCase.Resultado:
                    Resolver(ordenes, m.Resultado.PeticionId, m.Resultado);
                    break;
                case MensajeDeServidor.CuerpoOneofCase.Cronica:
                    Resolver(cronicas, m.Cronica.PeticionId, m.Cronica);
                    break;
                case MensajeDeServidor.CuerpoOneofCase.Pong:
                    var rtt = AhoraLocal - m.Pong.ClienteEnvioMs;
                    if (m.Pong.Reloj != null) AjustarReloj(m.Pong.Reloj, rtt, false);
                    break;
                case MensajeDeServidor.CuerpoOneofCase.Aviso:
                    if (m.Aviso.Reloj != null) AjustarReloj(m.Aviso.Reloj, double.PositiveInfinity, true);
                    AlAviso?.Invoke(m.Aviso);
                    // Otra sesión de la misma cuenta la ha sustituido y el servidor cierra esta. Reconectar sola la echaría a su vez, y las dos se
                    // echarían una a la otra sin parar: se queda cerrada hasta que el jugador lo pida.
                    if (m.Aviso.Codigo == "sesion.reemplazada")
                        CerrarPorError(new ErrorSesion { Codigo = "sesion_reemplazada", Mensaje = string.IsNullOrEmpty(m.Aviso.Mensaje) ? "la partida se abrió en otro sitio con esta cuenta" : m.Aviso.Mensaje, Fatal = true });
                    break;
                case MensajeDeServidor.CuerpoOneofCase.Error:
                    if (m.Error.Fatal) CerrarPorError(m.Error);
                    else AlAviso?.Invoke(new Aviso { Codigo = m.Error.Codigo, Mensaje = m.Error.Mensaje });
                    break;
            }
        }

        void AlLlegarBienvenida(Bienvenida b)
        {
            Bienvenida = b;
            if (b.Reloj != null) AjustarReloj(b.Reloj, double.PositiveInfinity, true);
            // Lo que reanuda con foto o es nuevo llega en su mensaje; lo que ya no está suscrito se descarta. La VISTA no se reanuda: se vuelve a
            // pedir abajo y llega como foto; hasta entonces se queda la de antes, para que el mapa no se vacíe un momento.
            var suscritos = new HashSet<string>();
            foreach (var a in b.Ambitos) if (a.Ambito != null) suscritos.Add(Almacen.ClaveDeAmbito(a.Ambito));
            foreach (var k in new List<string>(Almacen.Ambitos.Keys))
                if (!suscritos.Contains(k) && !(k.StartsWith("VISTA:", StringComparison.Ordinal) && vista != null)) Almacen.Ambitos.Remove(k);
            intentos = 0;
            FijarEstado(EstadoSesion.Abierta);
            Ping();
            if (vista != null) Enviar(MensajeDeClienteVista(vista));
            foreach (var p in enEspera) Enviar(p.mensaje);
            AlBienvenida?.Invoke(b);
            var t = abriendo;
            abriendo = null;
            t?.TrySetResult(b);
        }

        void CerrarPorError(ErrorSesion e)
        {
            cerradaAPeticion = true;
            UltimoError = e;
            proximoPingMs = double.NaN;
            var error = new ErrorDeSesion(e.Codigo, e.Mensaje, true);
            abriendo?.TrySetException(error);
            abriendo = null;
            FallarPendientes(error);
            SoltarConexion();
            FijarEstado(EstadoSesion.Cerrada);
        }

        /// <summary>Con una muestra de reloj nueva: se queda con la de menor ida y vuelta, salvo que el reloj haya cambiado de ritmo.</summary>
        void AjustarReloj(Reloj r, double rtt, bool forzar)
        {
            var factor = r.FactorMilesimas / 1000.0;
            bool cambio = forzar || factor != relojFactor || r.Pausado != relojPausado;
            bool conRtt = !double.IsInfinity(rtt);
            if (!cambio && !double.IsInfinity(relojRtt) && rtt > relojRtt * 1.5) return;
            var ida = conRtt ? rtt / 2 : 0;
            relojMundoMs = r.MundoMs + (r.Pausado ? 0 : ida * factor);
            relojLocalMs = AhoraLocal;
            relojFactor = factor;
            relojPausado = r.Pausado;
            if (conRtt) relojRtt = cambio ? rtt : Math.Min(rtt, relojRtt);
        }

        void Encolar(string peticionId, MensajeDeCliente m)
        {
            enEspera.Add((peticionId, m));
            if (Estado == EstadoSesion.Abierta) Enviar(m);
        }

        void Resolver<T>(Dictionary<string, TaskCompletionSource<T>> pendientes, string peticionId, T valor)
        {
            enEspera.RemoveAll(p => p.peticionId == peticionId);
            if (!pendientes.TryGetValue(peticionId, out var t)) return;
            pendientes.Remove(peticionId);
            t.TrySetResult(valor);
        }

        void Ping()
        {
            proximoPingMs = AhoraLocal + PingMs;
            Enviar(new MensajeDeCliente { Ping = new Ping { ClienteEnvioMs = (long)AhoraLocal } });
        }

        void Enviar(MensajeDeCliente m)
        {
            var socket = ws;
            if (socket == null || socket.State != WebSocketState.Open) return;
            _ = EnviarCrudo(socket, m.ToByteArray());
        }

        /// <summary>ClientWebSocket admite un solo envío a la vez: se ponen en fila.</summary>
        async Task EnviarCrudo(ClientWebSocket socket, byte[] datos)
        {
            await enviando.WaitAsync().ConfigureAwait(false);
            try { await socket.SendAsync(new ArraySegment<byte>(datos), WebSocketMessageType.Binary, true, CancellationToken.None).ConfigureAwait(false); }
            catch (Exception) { /* la caída la ve el receptor */ }
            finally { enviando.Release(); }
        }

        void SoltarConexion()
        {
            var socket = ws;
            ws = null;
            cancelar?.Cancel();
            cancelar = null;
            if (socket == null) return;
            if (socket.State == WebSocketState.Open)
                _ = socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).ContinueWith(_ => socket.Dispose());
            else socket.Dispose();
        }

        void ReiniciarConexion()
        {
            SoltarConexion();
            conexion++; // lo que aún entregue la vieja se ignora
            AlCerrarse();
        }

        void AlCerrarse()
        {
            proximoPingMs = double.NaN;
            if (cerradaAPeticion || !Reconectar)
            {
                if (Estado != EstadoSesion.Cerrada) FijarEstado(EstadoSesion.Cerrada);
                var error = new ErrorDeSesion("conexion_cerrada", "la conexión se cerró", false);
                abriendo?.TrySetException(error);
                abriendo = null;
                FallarPendientes(error);
                return;
            }
            // Las órdenes en vuelo se vuelven a mandar con su mismo `peticion_id`: si ya se aplicaron, el servidor contesta `ORDEN_DUPLICADA`.
            var espera = Math.Min(30_000, 500 * (1 << Math.Min(intentos++, 6)));
            FijarEstado(EstadoSesion.Reconectando);
            proximoIntentoMs = AhoraLocal + espera;
        }

        void FallarPendientes(Exception e)
        {
            foreach (var t in ordenes.Values) t.TrySetException(e);
            foreach (var t in cronicas.Values) t.TrySetException(e);
            ordenes.Clear();
            cronicas.Clear();
            enEspera.Clear();
        }

        void FijarEstado(EstadoSesion e)
        {
            Estado = e;
            AlEstado?.Invoke(e);
        }

        static MensajeDeCliente MensajeDeClienteVista(Rectangulo r) => new MensajeDeCliente { Vista = new CambiarVista { Rectangulo = r } };
    }
}

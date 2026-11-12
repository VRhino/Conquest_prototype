using System;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;

namespace ConquestTactics.Net
{
    /// <summary>
    /// Opciones de la prueba de red, leídas de la línea de órdenes:
    /// <c>-red servidor|cliente|ambos -ip 127.0.0.1 -puerto 7979 -bot -heroesExtra 0 -soldados 20
    /// -tasaRed 60 -medir 30 -informe ruta.txt</c>; para jugar un ticket del motor,
    /// <c>-ticket ticket.bin|.json -resultado resultado.bin -intento a1 -duracion 120 -espera 10</c>. Lanzado por el lanzador del
    /// motor (Batalla_Unity_Ciclo §8.8): <c>--batalla &lt;uuid&gt; --lanzador 127.0.0.1:&lt;p&gt; --puerto &lt;n&gt;</c>. Un cliente
    /// que entra en una batalla del motor se presenta con <c>-token &lt;t&gt;</c>.
    /// </summary>
    public static class OpcionesRed
    {
        public enum Modo { Ninguno, Servidor, Cliente, Ambos }

        public static Modo ModoPedido = Modo.Ninguno;
        public static string Ip = "127.0.0.1";
        public static ushort Puerto = 7979;
        /// <summary>El cliente se mueve solo (para medir sin nadie al teclado).</summary>
        public static bool Bot;
        /// <summary>Héroes sin jugador que el servidor mueve, para llenar la batalla.</summary>
        public static int HeroesExtra;
        public static int SoldadosPorHeroe = 20;
        /// <summary>Snapshots por segundo; la simulación va siempre a 60.</summary>
        public static int TasaRed = 60;
        /// <summary>Bytes por snapshot y conexión; 0 = un MTU (lo de Netcode por defecto, ~1.400 bytes).</summary>
        public static int Paquete;
        /// <summary>Segundos que dura la medición antes de escribir el informe y salir; 0 = no sale.</summary>
        public static float MedirSegundos;
        public static string Informe;
        /// <summary>Ticket del motor (MensajeDeServidor con <c>batalla.ticket</c>, en .bin o en el .json de los dorados).</summary>
        public static string Ticket;
        /// <summary>Dónde escribir el resultado (MensajeDeCliente con <c>batalla.resultado</c>; al lado, el mismo en .json).</summary>
        public static string Resultado;
        public static string Intento = "local";
        /// <summary>Tope de la batalla en segundos, por debajo del del ticket (para probar sin esperar media hora); 0 = el del ticket.</summary>
        public static float Duracion;
        /// <summary>Segundos que se espera a los jugadores humanos antes de empezar con la IA en su lugar.</summary>
        public static float EsperaJugadores;
        /// <summary>El socket del lanzador (<c>host:puerto</c>); con él, el ticket y la asignación llegan por ahí y no de un fichero.</summary>
        public static string Lanzador;
        public static string Batalla;
        /// <summary>Cliente: el token de su héroe para esta batalla (<c>Batalla.asignacion_propia</c> del motor).</summary>
        public static string Token;
        /// <summary>Héroes por bando de la batalla de verdad (los sistemas de Game.ECS en el mundo servidor, camino A); 0 = la prueba de cápsulas.</summary>
        public static int BatallaReal;
        /// <summary>Cliente con pantalla: guarda una captura en esta ruta a los <see cref="CapturaA"/> segundos.</summary>
        public static string Captura;
        public static float CapturaA = 20f;

        public static void Leer()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string siguiente = i + 1 < args.Length ? args[i + 1] : null;
                switch (args[i])
                {
                    case "-red":
                        ModoPedido = siguiente switch
                        {
                            "servidor" => Modo.Servidor,
                            "cliente" => Modo.Cliente,
                            "ambos" => Modo.Ambos,
                            _ => Modo.Ninguno,
                        };
                        break;
                    case "-ip": Ip = siguiente; break;
                    case "-puerto": Puerto = ushort.Parse(siguiente); break;
                    case "-bot": Bot = true; break;
                    case "-heroesExtra": HeroesExtra = int.Parse(siguiente); break;
                    case "-soldados": SoldadosPorHeroe = int.Parse(siguiente); break;
                    case "-tasaRed": TasaRed = int.Parse(siguiente); break;
                    case "-paquete": Paquete = int.Parse(siguiente); break;
                    case "-medir": MedirSegundos = float.Parse(siguiente, System.Globalization.CultureInfo.InvariantCulture); break;
                    case "-informe": Informe = siguiente; break;
                    case "-ticket": Ticket = siguiente; break;
                    case "-resultado": Resultado = siguiente; break;
                    case "-intento": Intento = siguiente; break;
                    case "-duracion": Duracion = float.Parse(siguiente, System.Globalization.CultureInfo.InvariantCulture); break;
                    case "-espera": EsperaJugadores = float.Parse(siguiente, System.Globalization.CultureInfo.InvariantCulture); break;
                    case "-token": Token = siguiente; break;
                    case "-batallaReal": BatallaReal = int.Parse(siguiente); break;
                    case "-captura": Captura = siguiente; break;
                    case "-capturaA": CapturaA = float.Parse(siguiente, System.Globalization.CultureInfo.InvariantCulture); break;
                    case "--lanzador": Lanzador = siguiente; ModoPedido = Modo.Servidor; break;
                    case "--batalla": Batalla = siguiente; break;
                    case "--puerto": Puerto = ushort.Parse(siguiente); break;
                }
            }
        }
    }

    /// <summary>
    /// Arranque de mundos. Sin <c>-red</c> y fuera de la escena de prueba deja que Entities cree su mundo por defecto, así
    /// que el juego actual no cambia. Con red, los mundos de servidor y cliente llevan solo los sistemas de Unity y los de
    /// este ensamblado: los de Game.ECS todavía no saben separar servidor y cliente. Con <c>-batallaReal</c> el servidor
    /// lleva también los de Game.ECS y juega la batalla de verdad (<see cref="BatallaRealSystem"/>).
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public class ArranqueRed : ClientServerBootstrap
    {
        public override bool Initialize(string defaultWorldName)
        {
            OpcionesRed.Leer();
            var modo = OpcionesRed.ModoPedido;
            if (modo == OpcionesRed.Modo.Ninguno)
            {
                var escena = DiscoverAutomaticNetcodeBootstrap();
                bool escenaDeRed = escena != null
                    && escena.ForceAutomaticBootstrapInScene == NetCodeConfig.AutomaticBootstrapSetting.EnableAutomaticBootstrap;
                // false: Entities crea su mundo por defecto, exactamente como sin este arranque.
                if (!escenaDeRed)
                    return false;
                modo = OpcionesRed.Modo.Ambos;
                OpcionesRed.ModoPedido = modo;
            }

            // Cifras con punto en registros e informes, sea cual sea el idioma del sistema.
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            // Un cliente sin pantalla iría a miles de fotogramas por segundo; uno de verdad va a la tasa del monitor.
            if (modo == OpcionesRed.Modo.Cliente && Application.isBatchMode)
                Application.targetFrameRate = 60;
            AutoConnectPort = OpcionesRed.Puerto;
            DefaultConnectAddress = NetworkEndpoint.Parse(OpcionesRed.Ip, OpcionesRed.Puerto);
            Debug.Log($"[Red] Modo {modo}, {OpcionesRed.Ip}:{OpcionesRed.Puerto}, soldados {OpcionesRed.SoldadosPorHeroe}, " +
                      $"héroes extra {OpcionesRed.HeroesExtra}, tasa de red {OpcionesRed.TasaRed}, paquete {OpcionesRed.Paquete}");

            if (modo != OpcionesRed.Modo.Cliente)
                CreateServerWorld("ServerWorld", Sistemas(WorldSystemFilterFlags.ServerSimulation, OpcionesRed.BatallaReal > 0));
            if (modo != OpcionesRed.Modo.Servidor)
                CreateClientWorld("ClientWorld", Sistemas(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Presentation));
            return true;
        }

        static NativeList<SystemTypeIndex> Sistemas(WorldSystemFilterFlags flags, bool conJuego = false)
        {
            var propio = typeof(ArranqueRed).Assembly;
            var todos = DefaultWorldInitialization.GetAllSystems(flags);
            var elegidos = new NativeList<SystemTypeIndex>(todos.Count, Allocator.Temp);
            foreach (var tipo in todos)
            {
                string ensamblado = tipo.Assembly.GetName().Name;
                if (tipo.Assembly == propio || ensamblado.StartsWith("Unity.", StringComparison.Ordinal) || (conJuego && ensamblado == "Game.ECS"))
                    elegidos.Add(TypeManager.GetSystemTypeIndex(tipo));
            }
            return elegidos;
        }
    }
}

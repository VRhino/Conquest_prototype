using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace ConquestTactics.Net
{
    /// <summary>
    /// La preparación de la batalla en el cliente, entre la pantalla que la enseña (la del mundo, <c>PantallasDeMundo</c>) y el servidor de
    /// batalla (<see cref="BatallaDeTicketSystem"/>). La pantalla pone aquí lo que elige el jugador (el orden de sus escuadras, su formación y
    /// «Listo») y lee lo que dice el servidor (lo que queda, quién está listo, su punto de aparición y si ya empezó). Con
    /// <see cref="ConPantalla"/> apagado (un cliente sin mundo, como PruebaRed con <c>-red cliente</c>) el cliente se da por listo al entrar y
    /// carga BattleScene enseguida, como antes.
    /// </summary>
    public static class PreparacionRed
    {
        /// <summary>El cliente lo arrancó el mundo, que enseña la preparación: no se da por listo solo ni carga BattleScene hasta que empieza.</summary>
        public static bool ConPantalla;

        // Lo que elige el jugador.
        public static readonly List<string> Orden = new List<string>();
        public static readonly Dictionary<string, string> Formaciones = new Dictionary<string, string>();
        public static bool Listo;

        // Lo que dice el servidor.
        public static bool Conectado { get; internal set; }
        public static float Queda { get; internal set; } = -1f;
        public static bool Empezada { get; internal set; }
        public static readonly List<(string heroe, int estado, int punto)> Heroes = new List<(string, int, int)>();

        public const int SinConectar = 0, ConectadoSinListo = 1, ConListo = 2;

        /// <summary>Todo como al arrancar el proceso, para la batalla siguiente.</summary>
        public static void Reiniciar()
        {
            ConPantalla = false;
            Orden.Clear();
            Formaciones.Clear();
            Listo = false;
            Conectado = false;
            Queda = -1f;
            Empezada = false;
            Heroes.Clear();
        }

        internal static PreparacionDelJugador Mensaje()
        {
            var formaciones = new List<string>();
            foreach (var f in Formaciones) formaciones.Add(f.Key + "=" + f.Value);
            return new PreparacionDelJugador
            {
                Orden = string.Join(",", Orden),
                Formaciones = string.Join(",", formaciones),
                Listo = (byte)(Listo || !ConPantalla ? 1 : 0),
            };
        }

        internal static void Leer(in EstadoDePreparacion e)
        {
            Conectado = true;
            Queda = e.Queda;
            Empezada |= e.Empezada != 0;
            Heroes.Clear();
            foreach (var h in e.Heroes.ToString().Split(','))
            {
                var p = h.Split(':');
                if (p.Length == 3 && int.TryParse(p[1], out int estado) && int.TryParse(p[2], out int punto))
                    Heroes.Add((p[0], estado, punto));
            }
        }
    }

    /// <summary>
    /// Cliente: manda cada segundo lo que eligió el jugador en la preparación (<see cref="PreparacionDelJugador"/>) hasta que la batalla
    /// empieza, y recoge el <see cref="EstadoDePreparacion"/> del servidor.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class PreparacionClienteSystem : SystemBase
    {
        const double CadaCuanto = 1d;
        double _ultimo = -1d;

        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (estado, peticion) in SystemAPI.Query<RefRO<EstadoDePreparacion>>().WithAll<ReceiveRpcCommandRequest>().WithEntityAccess())
            {
                PreparacionRed.Leer(estado.ValueRO);
                ecb.DestroyEntity(peticion);
            }
            double ahora = SystemAPI.Time.ElapsedTime;
            if (!PreparacionRed.Empezada && ahora - _ultimo >= CadaCuanto
                && SystemAPI.TryGetSingletonEntity<NetworkStreamInGame>(out var conexion))
            {
                _ultimo = ahora;
                var mensaje = ecb.CreateEntity();
                ecb.AddComponent(mensaje, PreparacionRed.Mensaje());
                ecb.AddComponent(mensaje, new SendRpcCommandRequest { TargetConnection = conexion });
            }
            ecb.Playback(EntityManager);
        }
    }
}

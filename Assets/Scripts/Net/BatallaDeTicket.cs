using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BronzeAge.Contract.V1;
using Google.Protobuf;

namespace ConquestTactics.Net
{
    public sealed class EscuadraEnJuego
    {
        public string Id;
        public string TropaId;
        public byte Bando;
        public int Autorizados;
        /// <summary>Los que salen al campo: los autorizados, o menos si en la tropa de Conquest no caben todos.</summary>
        public int Desplegados;
        public int Muertos;
        /// <summary>Índice en <see cref="BatallaDeTicket.Heroes"/>; -1 = escuadra sin héroe (bandidos, carreteros).</summary>
        public int Heroe = -1;
    }

    public sealed class HeroeEnJuego
    {
        public string Id;
        public byte Bando;
        public bool EsBot;
        /// <summary>NetworkId del jugador que lo lleva; 0 = lo lleva la IA.</summary>
        public int Dueno;
        public bool Caido;
        /// <summary>Falso solo para quien se unió y no llegó a salir al campo (una tropa que Conquest no tiene).</summary>
        public bool Participa = true;
        /// <summary>Ya salió de la batalla (su salida se mandó al motor): el resultado no lo nombra, ni a sus escuadras.</summary>
        public bool Salio;
        /// <summary>Servidor: desde cuándo está sin jugador el héroe de un humano (segundos de mundo); negativo si lo tiene.</summary>
        public double SinJugadorDesde = -1d;
    }

    /// <summary>
    /// Una batalla jugada desde un ticket del motor: lee el ticket, lleva la cuenta de cada escuadra y cada héroe y
    /// escribe el <see cref="BattleResult"/> que el motor valida (Batalla_Unity_Ciclo §8.5 y §9). No sabe nada de ECS.
    /// </summary>
    public sealed class BatallaDeTicket
    {
        public const byte Atacante = 0;
        public const byte Defensor = 1;
        public const string VersionServidor = "conquest-prueba-red-0.1";

        /// <summary>ponytail: una batalla por proceso, que es lo que hará el lanzador (un servidor Unity por batalla).</summary>
        public static BatallaDeTicket Actual;

        public readonly BattleTicket Ticket;
        public readonly List<EscuadraEnJuego> Escuadras = new List<EscuadraEnJuego>();
        public readonly List<HeroeEnJuego> Heroes = new List<HeroeEnJuego>();
        /// <summary>Clientes que han entrado y aún no tienen héroe: su id de red, su conexión y el token que presentan.</summary>
        public static readonly Queue<(int id, Unity.Entities.Entity conexion, string token)> ConexionesSinHeroe =
            new Queue<(int, Unity.Entities.Entity, string)>();
        /// <summary>La asignación del lanzador: el intento y el token de cada héroe humano. Sin ella (ticket en fichero), los héroes se dan por orden de llegada.</summary>
        public BattleServerAssignment Asignacion;
        /// <summary>Los que se unieron después del ticket (incorporaciones del motor), por orden de llegada.</summary>
        public readonly List<IncorporacionBatalla> Incorporaciones = new List<IncorporacionBatalla>();
        public readonly float DuracionSegundos;
        public long InicioMs;
        public bool Empezada;
        public bool Terminada;

        public BatallaDeTicket(BattleTicket ticket, float topeSegundos = 0f)
        {
            Ticket = ticket;
            DuracionSegundos = topeSegundos > 0f
                ? Math.Min(topeSegundos, ticket.Reglas.DuracionMaximaSegundos)
                : ticket.Reglas.DuracionMaximaSegundos;
            Anadir(ticket.Atacante, Atacante);
            Anadir(ticket.Defensor, Defensor);
        }

        /// <summary>
        /// Quien se une con el ticket ya cerrado: su héroe y sus escuadras entran en la cuenta de la batalla, porque el
        /// motor los espera en el resultado. Falso si ya estaba (el motor la repite) o viene vacía.
        /// </summary>
        public bool Incorporar(IncorporacionBatalla incorporacion)
        {
            var p = incorporacion.Participante;
            if (p == null || Heroes.Exists(h => h.Id == p.HeroeId))
                return false;
            Incorporaciones.Add(incorporacion);
            byte bando = incorporacion.Lado == LadoId.Defensor ? Defensor : Atacante;
            Heroes.Add(new HeroeEnJuego { Id = p.HeroeId, Bando = bando, EsBot = p.EsBot });
            foreach (var escuadra in p.Escuadras)
                Escuadras.Add(Escuadra(escuadra, bando, Heroes.Count - 1));
            return true;
        }

        /// <summary>
        /// La salida de un héroe (§8.4 del ciclo del motor), con todas sus escuadras: las bajas hasta ahora si sale a media
        /// batalla, o intactas si no llegó a conectarse. Quien sale a media batalla queda herido (Doc 5.16.4); quien no llegó
        /// a conectarse, no. Desde aquí el resultado ya no lo nombra.
        /// </summary>
        public SalidaBatalla Salida(string heroeId, MotivoSalida motivo, string intento)
        {
            int indice = Heroes.FindIndex(h => h.Id == heroeId);
            bool aMedias = motivo != MotivoSalida.SalidaNoConectado;
            var salida = new SalidaBatalla
            {
                SchemaVersion = 1, BattleId = Ticket.BattleId, TicketRevision = Ticket.TicketRevision,
                IntentoAsignacionId = intento, HeroeId = heroeId, Motivo = motivo, Derrotado = aMedias,
            };
            if (indice < 0)
                return salida;
            Heroes[indice].Salio = true;
            foreach (var e in Escuadras)
                if (e.Heroe == indice)
                {
                    int muertos = aMedias ? e.Muertos : 0;
                    salida.Escuadras.Add(new ResultadoEscuadraSalida { SquadId = e.Id, Supervivientes = (uint)(e.Autorizados - muertos), Muertos = (uint)muertos });
                }
            return salida;
        }

        /// <summary>Los ids de los héroes que ya salieron.</summary>
        public List<string> Salidos() => Heroes.Where(h => h.Salio).Select(h => h.Id).ToList();

        /// <summary>Ese héroe no llega a salir al campo: no participa y sus escuadras vuelven enteras, sin desplegar.</summary>
        public void DejarFuera(string heroeId)
        {
            int indice = Heroes.FindIndex(h => h.Id == heroeId);
            if (indice < 0)
                return;
            Heroes[indice].Participa = false;
            foreach (var escuadra in Escuadras)
                if (escuadra.Heroe == indice)
                    escuadra.Desplegados = 0;
        }

        void Anadir(BattleSide lado, byte bando)
        {
            if (lado == null)
                return;
            foreach (var participante in lado.Participantes)
            {
                Heroes.Add(new HeroeEnJuego { Id = participante.HeroeId, Bando = bando, EsBot = participante.EsBot });
                foreach (var escuadra in participante.Escuadras)
                    Escuadras.Add(Escuadra(escuadra, bando, Heroes.Count - 1));
            }
            foreach (var escuadra in lado.EscuadrasSinHeroe)
                Escuadras.Add(Escuadra(escuadra, bando, -1));
        }

        static EscuadraEnJuego Escuadra(BronzeAge.Contract.V1.SquadSnapshot s, byte bando, int heroe) => new EscuadraEnJuego
        {
            Id = s.SquadId, TropaId = s.TropaId, Bando = bando, Autorizados = (int)s.EfectivosAutorizados,
            Desplegados = (int)s.EfectivosAutorizados, Heroe = heroe,
        };

        /// <summary>Lee un ticket: el <c>.bin</c> o el <c>.json</c> de un <see cref="MensajeDeServidor"/> (como los dorados) o un <see cref="BattleTicket"/> suelto.</summary>
        public static BattleTicket Leer(string ruta)
        {
            if (ruta.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                string json = File.ReadAllText(ruta);
                var mensaje = JsonParser.Default.Parse<MensajeDeServidor>(json);
                return mensaje.Batalla?.Ticket ?? JsonParser.Default.Parse<BattleTicket>(json);
            }
            var bytes = File.ReadAllBytes(ruta);
            var envuelto = MensajeDeServidor.Parser.ParseFrom(bytes);
            return envuelto.Batalla?.Ticket ?? BattleTicket.Parser.ParseFrom(bytes);
        }

        /// <summary>
        /// El resultado: quién gana, por qué y qué le queda a cada escuadra y héroe. Ni experiencia ni botín: los pone el
        /// motor (D7 del 2026-11-12). Si el atacante gana un asalto de caravana, se pierden todos los carros en juego.
        /// </summary>
        public BattleResult Resultado(LadoId ganador, RazonVictoria razon, long finMs, string intento)
        {
            var r = new BattleResult
            {
                SchemaVersion = 1,
                BattleId = Ticket.BattleId,
                ResultId = Guid.NewGuid().ToString(),
                TicketRevision = Ticket.TicketRevision,
                IntentoAsignacionId = intento,
                InicioMs = InicioMs,
                FinMs = finMs,
                Ganador = ganador,
                Razon = razon,
                VersionServidor = VersionServidor,
            };
            // Quien salió a media batalla ya cerró lo suyo con su salida: ni él ni sus escuadras van en el resultado.
            foreach (var e in Escuadras.Where(e => e.Heroe < 0 || !Heroes[e.Heroe].Salio))
                r.PorEscuadra.Add(new ResultadoEscuadra
                {
                    SquadId = e.Id,
                    Desplegados = (uint)e.Desplegados,
                    SupervivientesAlCierre = (uint)(e.Autorizados - e.Muertos),
                    Muertos = (uint)e.Muertos,
                });
            foreach (var h in Heroes.Where(h => !h.Salio))
                r.PorHeroe.Add(new ResultadoHeroe
                {
                    HeroeId = h.Id,
                    Participo = h.Participa,
                    // Sin `derrotado`: el héroe gana o pierde con su bando, haya caído o no en la batalla (decisión del
                    // autor, 2026-11-12), y sin ese campo el motor hiere a los del bando perdedor.
                    SobrevivioAlCierre = !h.Caido,
                });

            var caravana = Ticket.ContextoEstrategico?.Caravana;
            if (caravana != null)
            {
                var objetivo = new ObjetivoResultado { ObjetivoId = "caravana" };
                if (ganador == LadoId.Atacante)
                {
                    objetivo.CapturadoPor = LadoId.Atacante;
                    r.CarrosPerdidos.AddRange(caravana.Caravanas.SelectMany(c => c.Carros).Select(c => c.CarroId));
                }
                r.Objetivos.Add(objetivo);
            }
            return r;
        }

        /// <summary>
        /// Lo que el motor comprueba del resultado y Unity puede saber (combate::resultado::validar_resultado, puntos 2 a 6
        /// y 8). Devuelve los fallos; vacío = el motor lo aceptaría si la asignación es la vigente.
        /// </summary>
        public static List<string> Comprobar(BattleTicket t, BattleResult r, IEnumerable<IncorporacionBatalla> incorporaciones = null,
            ICollection<string> salidos = null)
        {
            var fallos = new List<string>();
            // Cuentan los del ticket y los que se unieron después, menos los que ya salieron.
            var presentes = Lados(t).SelectMany(l => l.Participantes)
                .Concat((incorporaciones ?? Enumerable.Empty<IncorporacionBatalla>()).Select(i => i.Participante))
                .Where(p => salidos == null || !salidos.Contains(p.HeroeId)).ToList();
            var escuadras = presentes.SelectMany(p => p.Escuadras).Concat(Lados(t).SelectMany(l => l.EscuadrasSinHeroe)).ToList();
            var heroes = presentes.Select(p => p.HeroeId).ToList();

            if (r.PorEscuadra.Select(e => e.SquadId).Distinct().Count() != r.PorEscuadra.Count
                || r.PorHeroe.Select(h => h.HeroeId).Distinct().Count() != r.PorHeroe.Count)
                fallos.Add("ids repetidos");
            if (!r.PorEscuadra.Select(e => e.SquadId).OrderBy(x => x).SequenceEqual(escuadras.Select(e => e.SquadId).OrderBy(x => x)))
                fallos.Add("escuadras incompletas");
            if (!r.PorHeroe.Select(h => h.HeroeId).OrderBy(x => x).SequenceEqual(heroes.OrderBy(x => x)))
                fallos.Add("héroes incompletos");
            foreach (var e in r.PorEscuadra)
            {
                uint autorizados = escuadras.FirstOrDefault(x => x.SquadId == e.SquadId)?.EfectivosAutorizados ?? 0;
                if (e.SupervivientesAlCierre + e.Muertos != autorizados || e.Desplegados > autorizados)
                    fallos.Add($"la escuadra {e.SquadId} no cierra");
            }
            if (r.Razon == RazonVictoria.TiempoAgotado && r.Ganador != LadoId.Defensor)
                fallos.Add("tiempo agotado sin ganar el defensor");
            if (r.FinMs <= r.InicioMs || r.FinMs - r.InicioMs > t.Reglas.DuracionMaximaSegundos * 1000L)
                fallos.Add("duración fuera de plazo");
            var carros = new HashSet<string>(t.ContextoEstrategico?.Caravana?.Caravanas.SelectMany(c => c.Carros).Select(c => c.CarroId)
                                             ?? Enumerable.Empty<string>());
            if (r.CarrosPerdidos.Any(c => !carros.Contains(c)) || r.CarrosPerdidos.Distinct().Count() != r.CarrosPerdidos.Count)
                fallos.Add("carro inexistente o repetido");
            return fallos;
        }

        /// <summary>Los <c>tropaId</c> del ticket que <paramref name="conocida"/> no reconoce, sin repetir.</summary>
        public static List<string> TropasDesconocidas(BattleTicket t, Func<string, bool> conocida) =>
            Lados(t).SelectMany(l => l.Participantes.SelectMany(p => p.Escuadras).Concat(l.EscuadrasSinHeroe))
                .Select(e => e.TropaId).Distinct().Where(id => !conocida(id)).ToList();

        static IEnumerable<BattleSide> Lados(BattleTicket t) => new[] { t.Atacante, t.Defensor }.Where(l => l != null);

        /// <summary>Escribe el resultado como lo manda el servidor de batalla al motor: <c>.bin</c> y, al lado, el <c>.json</c>.</summary>
        public static void Escribir(string ruta, BattleResult resultado)
        {
            var mensaje = new MensajeDeCliente { Batalla = new MensajeDeServidorDeBatalla { Resultado = resultado } };
            var carpeta = Path.GetDirectoryName(Path.GetFullPath(ruta));
            Directory.CreateDirectory(carpeta);
            File.WriteAllBytes(ruta, mensaje.ToByteArray());
            File.WriteAllText(Path.ChangeExtension(ruta, ".json"), JsonFormatter.Default.Format(mensaje));
        }
    }
}

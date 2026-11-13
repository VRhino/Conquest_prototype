// Lanzador de prueba: hace con el proceso de batalla de Conquest lo mismo que el lanzador del motor
// (BronzeAge crates/lanzador, Batalla_Unity_Ciclo §8.8), sin motor. Sale con 0 si el proceso manda el inicio y un
// resultado y acaba bien tras su acuse. Con -incorpora <segundos>, pasado ese tiempo desde el inicio se unen dos héroes:
// uno de la IA al bando atacante y uno humano al defensor (con su token y su cliente); el resultado tiene que traerlos.
// Salidas: sin -cliente el humano no llega a conectarse y el proceso manda su salida; este lanzador hace entonces de motor
// (quita al que salió, sube la revisión y manda el ticket otra vez, o da la batalla por cancelada si no queda ningún
// humano). -desconecta <s> mata al cliente a los s segundos del inicio, -abandona <s> hace que el cliente abandone y
// -cae <s> hace caer a su héroe. Quien sale a media batalla no puede estar en el resultado.
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using BronzeAge.Contract.V1;
using Google.Protobuf;

if (args.Length < 2)
{
    Console.Error.WriteLine("uso: LanzadorDePrueba <PruebaRed.exe> <ticket.json|.bin> [-cliente] [-token-malo] [-incorpora 30] [-desconecta 15] [-abandona 15] [-cae 15] [-duracion 120] [-puerto 7790] [-espera 10]");
    return 2;
}
string exe = Path.GetFullPath(args[0]);
string rutaTicket = args[1];
bool conCliente = args.Contains("-cliente");
bool tokenMalo = args.Contains("-token-malo");
int puerto = Opcion("-puerto", 7790);
int espera = Opcion("-espera", 10);
int incorpora = Opcion("-incorpora", 0);
int desconecta = Opcion("-desconecta", 0);
int abandona = Opcion("-abandona", 0);
int cae = Opcion("-cae", 0);
int duracion = Opcion("-duracion", 120);
int Opcion(string nombre, int porDefecto)
{
    int i = Array.IndexOf(args, nombre);
    return i >= 0 && i + 1 < args.Length ? int.Parse(args[i + 1]) : porDefecto;
}

var ticket = rutaTicket.EndsWith(".json")
    ? JsonParser.Default.Parse<MensajeDeServidor>(File.ReadAllText(rutaTicket)).Batalla.Ticket
    : MensajeDeServidor.Parser.ParseFrom(File.ReadAllBytes(rutaTicket)).Batalla.Ticket;
string Hex(int bytes) => Convert.ToHexString(RandomNumberGenerator.GetBytes(bytes)).ToLowerInvariant();

// La asignación para la revisión del ticket que haya: un intento nuevo y un token nuevo para cada humano.
BattleServerAssignment Asignar()
{
    var a = new BattleServerAssignment
    {
        SchemaVersion = 1,
        BattleId = ticket.BattleId,
        TicketRevision = ticket.TicketRevision,
        IntentoAsignacionId = "lz-" + Hex(8),
        Instancia = new InstanciaDeBatalla { Host = "127.0.0.1", Puerto = (uint)puerto, Protocolo = "udp" },
    };
    foreach (var lado in new[] { ticket.Atacante, ticket.Defensor })
        foreach (var p in lado?.Participantes ?? new Google.Protobuf.Collections.RepeatedField<BattleParticipantSnapshot>())
            if (!p.EsBot)
                a.TokensParticipante.Add(new TokenParticipante { HeroeId = p.HeroeId, Token = Hex(32) });
    return a;
}
var asignacion = Asignar();

var oyente = new TcpListener(IPAddress.Loopback, 0);
oyente.Start();
int puertoLanzador = ((IPEndPoint)oyente.LocalEndpoint).Port;
string registros = Path.Combine(Path.GetDirectoryName(exe), "..", "..", "Logs", "red", "lanzador");
Directory.CreateDirectory(registros);

var servidor = Process.Start(exe,
    $"-batchmode -nographics -logFile \"{Path.Combine(registros, "servidor.log")}\" -espera {espera} -duracion {duracion} {(cae > 0 ? $"-caeA {cae} " : "")}" +
    $"--batalla {ticket.BattleId} --lanzador 127.0.0.1:{puertoLanzador} --puerto {puerto}");
Console.WriteLine($"Proceso de batalla {servidor.Id}; lanzador en 127.0.0.1:{puertoLanzador}");

var aceptar = oyente.AcceptTcpClientAsync();
if (await Task.WhenAny(aceptar, Task.Delay(TimeSpan.FromSeconds(120))) != aceptar)
{
    Console.Error.WriteLine("El proceso no se conectó en 120 s");
    servidor.Kill();
    return 1;
}
using var tcp = aceptar.Result;
var flujo = tcp.GetStream();
void Mandar(MensajeParaProcesoDeBatalla m) { lock (flujo) { m.WriteDelimitedTo(flujo); flujo.Flush(); } }
Mandar(new MensajeParaProcesoDeBatalla { Ticket = ticket });
Mandar(new MensajeParaProcesoDeBatalla { Asignacion = asignacion });
Console.WriteLine($"Ticket y asignación {asignacion.IntentoAsignacionId} con {asignacion.TokensParticipante.Count} tokens");

Process cliente = null;
if (conCliente && asignacion.TokensParticipante.Count > 0)
{
    string token = tokenMalo ? "un-token-que-no-es" : asignacion.TokensParticipante[0].Token;
    cliente = Process.Start(exe,
        $"-batchmode -nographics -logFile \"{Path.Combine(registros, "cliente.log")}\" -red cliente -ip 127.0.0.1 -puerto {puerto} -bot -token {token}{(abandona > 0 ? $" -abandonarA {abandona}" : "")}");
    Console.WriteLine($"Cliente {cliente.Id} con el token de {asignacion.TokensParticipante[0].HeroeId}{(tokenMalo ? " (falso)" : "")}");
}

bool inicio = false, cancelada = false;
BattleResult resultado = null;
var salieron = new List<string>();
Process unido = null;
string[] unidos = { "heroe-30", "heroe-31" };
// Los que se unen a media batalla, como los manda el lanzador del motor: la incorporación y, para el humano, su token.
async Task Incorporar()
{
    await Task.Delay(TimeSpan.FromSeconds(incorpora));
    BattleParticipantSnapshot Participante(string heroe, string nombre, bool bot, string escuadra, string tropa, uint efectivos)
    {
        var p = new BattleParticipantSnapshot { HeroeId = heroe, EsBot = bot, Heroe = new HeroSnapshot { DisplayName = nombre, ClassDefinitionId = "lanza_escudo_redondo", Nivel = 2 } };
        p.Escuadras.Add(new SquadSnapshot { SquadId = escuadra, HeroeId = heroe, TropaId = tropa, EfectivosAutorizados = efectivos, Nivel = 1 });
        return p;
    }
    Mandar(new MensajeParaProcesoDeBatalla { Incorporacion = new IncorporacionBatalla
        { SchemaVersion = 1, BattleId = ticket.BattleId, Secuencia = 1, Lado = LadoId.Atacante, Participante = Participante(unidos[0], "Dimas", true, "esc-50", "milicia_lanceros", 12) } });
    Mandar(new MensajeParaProcesoDeBatalla { Incorporacion = new IncorporacionBatalla
        { SchemaVersion = 1, BattleId = ticket.BattleId, Secuencia = 2, Lado = LadoId.Defensor, Participante = Participante(unidos[1], "Eira", false, "esc-51", "arqueros", 10) } });
    string token = Hex(32);
    var tokens = new TokensBatalla { SchemaVersion = 1, BattleId = ticket.BattleId };
    tokens.TokensParticipante.Add(new TokenParticipante { HeroeId = unidos[1], Token = token });
    Mandar(new MensajeParaProcesoDeBatalla { Tokens = tokens });
    unido = Process.Start(exe,
        $"-batchmode -nographics -logFile \"{Path.Combine(registros, "cliente-unido.log")}\" -red cliente -ip 127.0.0.1 -puerto {puerto} -bot -token {token}");
    Console.WriteLine($"→ incorporaciones de {unidos[0]} (IA) y {unidos[1]} (humano, cliente {unido.Id})");
}
try
{
    while (resultado == null)
    {
        var m = MensajeDeServidorDeBatalla.Parser.ParseDelimitedFrom(flujo);
        string nombre = m.CuerpoCase.ToString().ToLowerInvariant();
        Console.WriteLine($"← {nombre}");
        if (m.CuerpoCase == MensajeDeServidorDeBatalla.CuerpoOneofCase.Inicio)
        {
            inicio = true;
            if (incorpora > 0) _ = Incorporar();
            if (desconecta > 0 && cliente != null)
                _ = Task.Delay(TimeSpan.FromSeconds(desconecta)).ContinueWith(_ => { cliente.Kill(); Console.WriteLine("→ cliente muerto"); });
        }
        if (m.CuerpoCase == MensajeDeServidorDeBatalla.CuerpoOneofCase.Salida)
        {
            var s = m.Salida;
            salieron.Add(s.HeroeId);
            Console.WriteLine($"   {s.HeroeId} sale por {s.Motivo}, derrotado {s.Derrotado}: " +
                              string.Join(", ", s.Escuadras.Select(e => $"{e.SquadId} vuelven {e.Supervivientes} y mueren {e.Muertos}")));
        }
        if (m.CuerpoCase == MensajeDeServidorDeBatalla.CuerpoOneofCase.Resultado) resultado = m.Resultado;
        Mandar(new MensajeParaProcesoDeBatalla
        {
            Acuse = new AcuseBatalla { Mensaje = nombre, BattleId = ticket.BattleId, Resultado = Resultado.OrdenAceptada },
        });
        // Lo que hace el motor con quien no llegó a conectarse (§8.4): fuera del ticket, revisión subida y a empezar de
        // nuevo con otra asignación; si no queda ningún humano, la batalla se cancela y al proceso no le llega nada más.
        if (m.CuerpoCase == MensajeDeServidorDeBatalla.CuerpoOneofCase.Salida && m.Salida.Motivo == MotivoSalida.SalidaNoConectado)
        {
            foreach (var lado in new[] { ticket.Atacante, ticket.Defensor })
                for (int i = (lado?.Participantes.Count ?? 0) - 1; i >= 0; i--)
                    if (lado.Participantes[i].HeroeId == m.Salida.HeroeId) lado.Participantes.RemoveAt(i);
            ticket.TicketRevision++;
            bool quedanHumanos = new[] { ticket.Atacante, ticket.Defensor }.Any(l => l != null && l.Participantes.Any(p => !p.EsBot));
            if (!quedanHumanos)
            {
                Console.WriteLine("   sin humanos: batalla cancelada");
                cancelada = true;
                break;
            }
            asignacion = Asignar();
            Mandar(new MensajeParaProcesoDeBatalla { Ticket = ticket });
            Mandar(new MensajeParaProcesoDeBatalla { Asignacion = asignacion });
            Console.WriteLine($"→ ticket con la revisión {ticket.TicketRevision} y asignación {asignacion.IntentoAsignacionId}");
        }
    }
}
catch (Exception e) when (e is IOException || e is InvalidProtocolBufferException)
{
    Console.Error.WriteLine("El proceso cerró el socket sin mandar el resultado");
}

// Tras una cancelación el proceso espera un minuto al ticket revisado antes de darse por cancelado.
bool acabo = servidor.WaitForExit(cancelada ? 80_000 : 10_000);
if (!acabo) servidor.Kill();
if (cliente != null && !cliente.HasExited) cliente.Kill();
if (unido != null && !unido.HasExited) unido.Kill();
bool conLosUnidos = incorpora <= 0 || (resultado != null && unidos.All(h => resultado.PorHeroe.Any(x => x.HeroeId == h))
    && new[] { "esc-50", "esc-51" }.All(e => resultado.PorEscuadra.Any(x => x.SquadId == e)));
if (incorpora > 0) Console.WriteLine($"Los que se unieron {(conLosUnidos ? "están" : "NO están")} en el resultado");
Console.WriteLine($"Inicio: {inicio}; resultado: {(resultado == null ? "no" : JsonFormatter.Default.Format(resultado))}");
Console.WriteLine($"El proceso {(acabo ? $"salió con {servidor.ExitCode}" : "no salió en 10 s y se mató")}");
bool sinLosQueSalieron = resultado == null || !salieron.Any(h => resultado.PorHeroe.Any(x => x.HeroeId == h));
if (salieron.Count > 0) Console.WriteLine($"Salieron {string.Join(", ", salieron)} y {(sinLosQueSalieron ? "no están" : "SIGUEN")} en el resultado");
if (cancelada)
    return acabo && servidor.ExitCode == 0 ? 0 : 1;
return inicio && resultado != null && resultado.IntentoAsignacionId == asignacion.IntentoAsignacionId && acabo && servidor.ExitCode == 0 && conLosUnidos && sinLosQueSalieron ? 0 : 1;

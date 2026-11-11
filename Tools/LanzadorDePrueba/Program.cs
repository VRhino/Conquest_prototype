// Lanzador de prueba: hace con el proceso de batalla de Conquest lo mismo que el lanzador del motor
// (BronzeAge crates/lanzador, Batalla_Unity_Ciclo §8.8), sin motor. Sale con 0 si el proceso manda el inicio y un
// resultado y acaba bien tras su acuse.
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using BronzeAge.Contract.V1;
using Google.Protobuf;

if (args.Length < 2)
{
    Console.Error.WriteLine("uso: LanzadorDePrueba <PruebaRed.exe> <ticket.json|.bin> [-cliente] [-token-malo] [-puerto 7790] [-espera 10]");
    return 2;
}
string exe = Path.GetFullPath(args[0]);
string rutaTicket = args[1];
bool conCliente = args.Contains("-cliente");
bool tokenMalo = args.Contains("-token-malo");
int puerto = Opcion("-puerto", 7790);
int espera = Opcion("-espera", 10);
int Opcion(string nombre, int porDefecto)
{
    int i = Array.IndexOf(args, nombre);
    return i >= 0 && i + 1 < args.Length ? int.Parse(args[i + 1]) : porDefecto;
}

var ticket = rutaTicket.EndsWith(".json")
    ? JsonParser.Default.Parse<MensajeDeServidor>(File.ReadAllText(rutaTicket)).Batalla.Ticket
    : MensajeDeServidor.Parser.ParseFrom(File.ReadAllBytes(rutaTicket)).Batalla.Ticket;
string Hex(int bytes) => Convert.ToHexString(RandomNumberGenerator.GetBytes(bytes)).ToLowerInvariant();

var asignacion = new BattleServerAssignment
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
            asignacion.TokensParticipante.Add(new TokenParticipante { HeroeId = p.HeroeId, Token = Hex(32) });

var oyente = new TcpListener(IPAddress.Loopback, 0);
oyente.Start();
int puertoLanzador = ((IPEndPoint)oyente.LocalEndpoint).Port;
string registros = Path.Combine(Path.GetDirectoryName(exe), "..", "..", "Logs", "red", "lanzador");
Directory.CreateDirectory(registros);

var servidor = Process.Start(exe,
    $"-batchmode -nographics -logFile \"{Path.Combine(registros, "servidor.log")}\" -espera {espera} -duracion 120 " +
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
void Mandar(MensajeParaProcesoDeBatalla m) { m.WriteDelimitedTo(flujo); flujo.Flush(); }
Mandar(new MensajeParaProcesoDeBatalla { Ticket = ticket });
Mandar(new MensajeParaProcesoDeBatalla { Asignacion = asignacion });
Console.WriteLine($"Ticket y asignación {asignacion.IntentoAsignacionId} con {asignacion.TokensParticipante.Count} tokens");

Process cliente = null;
if (conCliente && asignacion.TokensParticipante.Count > 0)
{
    string token = tokenMalo ? "un-token-que-no-es" : asignacion.TokensParticipante[0].Token;
    cliente = Process.Start(exe,
        $"-batchmode -nographics -logFile \"{Path.Combine(registros, "cliente.log")}\" -red cliente -ip 127.0.0.1 -puerto {puerto} -bot -token {token}");
    Console.WriteLine($"Cliente {cliente.Id} con el token de {asignacion.TokensParticipante[0].HeroeId}{(tokenMalo ? " (falso)" : "")}");
}

bool inicio = false;
BattleResult resultado = null;
try
{
    while (resultado == null)
    {
        var m = MensajeDeServidorDeBatalla.Parser.ParseDelimitedFrom(flujo);
        string nombre = m.CuerpoCase.ToString().ToLowerInvariant();
        Console.WriteLine($"← {nombre}");
        if (m.CuerpoCase == MensajeDeServidorDeBatalla.CuerpoOneofCase.Inicio) inicio = true;
        if (m.CuerpoCase == MensajeDeServidorDeBatalla.CuerpoOneofCase.Resultado) resultado = m.Resultado;
        Mandar(new MensajeParaProcesoDeBatalla
        {
            Acuse = new AcuseBatalla { Mensaje = nombre, BattleId = ticket.BattleId, Resultado = Resultado.OrdenAceptada },
        });
    }
}
catch (Exception e) when (e is IOException || e is InvalidProtocolBufferException)
{
    Console.Error.WriteLine("El proceso cerró el socket sin mandar el resultado");
}

bool acabo = servidor.WaitForExit(10_000);
if (!acabo) servidor.Kill();
if (cliente != null && !cliente.HasExited) cliente.Kill();
Console.WriteLine($"Inicio: {inicio}; resultado: {(resultado == null ? "no" : JsonFormatter.Default.Format(resultado))}");
Console.WriteLine($"El proceso {(acabo ? $"salió con {servidor.ExitCode}" : "no salió en 10 s y se mató")}");
return inicio && resultado != null && resultado.IntentoAsignacionId == asignacion.IntentoAsignacionId && acabo && servidor.ExitCode == 0 ? 0 : 1;

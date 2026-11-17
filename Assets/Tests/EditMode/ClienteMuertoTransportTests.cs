using System.Threading;
using NUnit.Framework;
using Unity.Collections;
using Unity.Networking.Transport;

/// <summary>
/// El servidor sigue oyendo a los demás cuando muere un cliente cuya máquina contesta «puerto cerrado» (en Windows, el ICMP
/// vuelve como recepción fallida en el socket UDP). Con Unity Transport 6.5 sin parchear, cada recepción fallida perdía para
/// siempre un búfer de la cola de recepción: con la cola de serie (512) el servidor se quedaba sordo tras ~512 envíos al muerto.
/// </summary>
public class ClienteMuertoTransportTests
{
    const ushort Puerto = 7895;
    // Más del doble de la cola de recepción de serie: sin el arreglo, el servidor deja de oír mucho antes del final.
    const int Vueltas = 1500;

    [Test]
    public void ElServidorSigueOyendoAlVivoTrasMorirOtroCliente()
    {
        var servidor = NetworkDriver.Create(new NetworkSettings()); // la cola de serie, sin el margen de DriversDeBatalla
        var muerto = NetworkDriver.Create();
        var vivo = NetworkDriver.Create();
        var conexiones = new NativeList<NetworkConnection>(2, Allocator.Temp);
        try
        {
            Assert.AreEqual(0, servidor.Bind(NetworkEndpoint.LoopbackIpv4.WithPort(Puerto)));
            Assert.AreEqual(0, servidor.Listen());
            var destino = NetworkEndpoint.LoopbackIpv4.WithPort(Puerto);
            var aMuerto = muerto.Connect(destino);
            var aVivo = vivo.Connect(destino);

            for (int i = 0; i < 500 && (conexiones.Length < 2 || vivo.GetConnectionState(aVivo) != NetworkConnection.State.Connected
                                       || muerto.GetConnectionState(aMuerto) != NetworkConnection.State.Connected); i++)
            {
                Bombear(servidor, muerto, vivo);
                NetworkConnection nueva;
                while ((nueva = servidor.Accept()) != default)
                    conexiones.Add(nueva);
                Thread.Sleep(1);
            }
            Assert.AreEqual(2, conexiones.Length, "no entraron los dos clientes");

            // Muere el proceso de un cliente: su socket se cierra y los envíos del servidor a ese puerto vuelven como ICMP.
            muerto.Dispose();

            int oidosAlFinal = 0;
            for (int i = 0; i < Vueltas; i++)
            {
                foreach (var c in conexiones)
                    Mandar(ref servidor, c, i);
                Mandar(ref vivo, aVivo, i);
                servidor.ScheduleUpdate().Complete();
                vivo.ScheduleUpdate().Complete();
                NetworkEvent.Type tipo;
                // Solo el vivo puede mandar: el muerto ya no tiene socket.
                while ((tipo = servidor.PopEvent(out _, out _)) != NetworkEvent.Type.Empty)
                    if (tipo == NetworkEvent.Type.Data && i >= Vueltas - 200)
                        oidosAlFinal++;
                Vaciar(vivo);
                Thread.Sleep(1);
            }

            Assert.Greater(oidosAlFinal, 0, "el servidor dejó de oír al cliente vivo tras morir el otro");
        }
        finally
        {
            if (muerto.IsCreated) muerto.Dispose();
            vivo.Dispose();
            servidor.Dispose();
        }
    }

    static void Bombear(NetworkDriver servidor, NetworkDriver muerto, NetworkDriver vivo)
    {
        servidor.ScheduleUpdate().Complete();
        muerto.ScheduleUpdate().Complete();
        vivo.ScheduleUpdate().Complete();
        Vaciar(muerto);
        Vaciar(vivo);
    }

    static void Vaciar(NetworkDriver driver)
    {
        while (driver.PopEvent(out _, out _) != NetworkEvent.Type.Empty) { }
    }

    static void Mandar(ref NetworkDriver driver, NetworkConnection conexion, int dato)
    {
        if (driver.BeginSend(conexion, out var escritor) == 0)
        {
            escritor.WriteInt(dato);
            driver.EndSend(escritor);
        }
    }
}

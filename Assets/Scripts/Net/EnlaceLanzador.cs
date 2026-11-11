using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using BronzeAge.Contract.V1;
using Google.Protobuf;
using UnityEngine;

namespace ConquestTactics.Net
{
    /// <summary>
    /// El socket con el lanzador del motor (Batalla_Unity_Ciclo §8.8): TCP local, mensajes Protobuf delimitados por su
    /// longitud. Llegan <see cref="MensajeParaProcesoDeBatalla"/> (ticket, asignación, incorporaciones, tokens, acuses), que un
    /// hilo deja en <see cref="Recibidos"/> para el sistema de la batalla; salen <see cref="MensajeDeServidorDeBatalla"/>
    /// (inicio, salidas, resultado).
    /// </summary>
    public sealed class EnlaceLanzador : IDisposable
    {
        public static EnlaceLanzador Actual;

        readonly TcpClient _tcp;
        readonly Stream _flujo;
        readonly object _escritura = new object();
        public readonly ConcurrentQueue<MensajeParaProcesoDeBatalla> Recibidos = new ConcurrentQueue<MensajeParaProcesoDeBatalla>();
        /// <summary>El lanzador cerró el socket (o se cayó): sin él no hay a quién dar el resultado.</summary>
        public volatile bool Cerrado;

        EnlaceLanzador(TcpClient tcp)
        {
            _tcp = tcp;
            _flujo = tcp.GetStream();
            new Thread(Leer) { IsBackground = true, Name = "Lanzador" }.Start();
        }

        /// <summary>Se conecta a <c>host:puerto</c>. El lanzador ya escucha antes de arrancar el proceso.</summary>
        public static EnlaceLanzador Conectar(string direccion)
        {
            int dosPuntos = direccion.LastIndexOf(':');
            var tcp = new TcpClient { NoDelay = true };
            tcp.Connect(direccion.Substring(0, dosPuntos), int.Parse(direccion.Substring(dosPuntos + 1)));
            Debug.Log($"[Red] Conectado al lanzador en {direccion}");
            return new EnlaceLanzador(tcp);
        }

        void Leer()
        {
            try
            {
                while (true)
                    Recibidos.Enqueue(MensajeParaProcesoDeBatalla.Parser.ParseDelimitedFrom(_flujo));
            }
            catch (Exception e) when (e is IOException || e is InvalidProtocolBufferException || e is ObjectDisposedException)
            {
                Cerrado = true;
            }
        }

        public void Enviar(MensajeDeServidorDeBatalla mensaje)
        {
            lock (_escritura)
            {
                mensaje.WriteDelimitedTo(_flujo);
                _flujo.Flush();
            }
        }

        public void Dispose() => _tcp.Dispose();
    }
}

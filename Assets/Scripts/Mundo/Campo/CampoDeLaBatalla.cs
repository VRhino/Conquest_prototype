// El campo de la batalla en curso (Campo_De_Batalla_Del_Lugar.md §10): se genera en un hilo aparte en cuanto llega el ticket, en el cliente
// (durante la preparación) y en el servidor de batalla, y BattleScene lo monta al cargarse. Solo depende del recorte: una revisión del ticket
// que no lo cambie no lo regenera. Sin recorte (un motor sin relieve) no hay campo: se juega en el de siempre.
using System;
using System.Threading.Tasks;
using pb = BronzeAge.Contract.V1;

namespace ConquestTactics.Mundo.Campo
{
    public static class CampoDeLaBatalla
    {
        static string batalla;
        static pb.RecorteDelLugar recorte;
        static Task<CampoGenerado> tarea;

        /// <summary>La batalla del campo que hay (o se está generando).</summary>
        public static string Batalla => batalla;

        /// <summary>El campo, si ya está generado (y sin error); si no, null.</summary>
        public static CampoGenerado Listo => tarea != null && tarea.Status == TaskStatus.RanToCompletion ? tarea.Result : null;

        /// <summary>Lo que se está generando, o null sin campo.</summary>
        public static Task<CampoGenerado> Tarea => tarea;

        /// <summary>Empieza a generar el campo del ticket, si trae recorte y no es el que ya hay. `log` recibe lo que pasa (el de Unity).</summary>
        public static void Empezar(string batallaId, pb.BattleTicket ticket, pb.Punto puntoDeLaBatalla, Action<string> log = null)
        {
            var mapa = ticket?.MapaReferencia;
            if (mapa?.Recorte == null) return;
            if (batallaId == batalla && mapa.Recorte.Equals(recorte)) return;
            batalla = batallaId;
            recorte = mapa.Recorte;
            var punto = puntoDeLaBatalla ?? PuntoDeLaBatalla(ticket) ?? mapa.Centro;
            var reloj = System.Diagnostics.Stopwatch.StartNew();
            tarea = Task.Run(() =>
            {
                try
                {
                    var campo = GeneradorDelCampo.Generar(Lugar.DesdeContrato(mapa, punto));
                    log?.Invoke($"[Campo] {batallaId}: {campo.Informe.Tipo}{(campo.Informe.LlanoForzado ? " (llano forzado)" : "")}, {campo.Jugable / 1000} m jugables, " +
                                $"{campo.Informe.ArbolesJugables} árboles jugables de {campo.Informe.Arboles}, desnivel {campo.Informe.Desnivel / 1000} m, {reloj.ElapsedMilliseconds} ms" +
                                (campo.Informe.Avisos.Count > 0 ? "; " + string.Join("; ", campo.Informe.Avisos) : ""));
                    return campo;
                }
                catch (Exception e)
                {
                    log?.Invoke($"[Campo] {batallaId}: no se pudo generar el campo ({e.Message}): se juega en el de siempre");
                    throw;
                }
            });
        }

        /// <summary>El campo generado de `batallaId`, esperándolo si aún se genera; null si no hay (o falló).</summary>
        public static CampoGenerado Esperar(string batallaId)
        {
            if (tarea == null || (batallaId != null && batallaId != batalla)) return null;
            try { return tarea.Result; }
            catch (AggregateException) { return null; }
        }

        /// <summary>La posición del defensor: el `punto` del contexto del ticket.</summary>
        public static pb.Punto PuntoDeLaBatalla(pb.BattleTicket t)
        {
            var c = t?.ContextoEstrategico;
            return c?.CampoAbierto?.Punto ?? c?.Caravana?.Punto ?? c?.CampamentoBandidos?.Punto ?? c?.SaqueoPueblo?.Punto ?? c?.Avanzada?.Punto;
        }

        /// <summary>Se olvida el campo (al salir de la batalla).</summary>
        public static void Olvidar()
        {
            batalla = null;
            recorte = null;
            tarea = null;
        }
    }
}

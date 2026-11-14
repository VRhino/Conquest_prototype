// El panel de avisos y el aviso flotante (BronzeAgeClient: ui/panelAvisos.ts y `mostrar` de ui/avisos.ts). Arriba del todo, los urgentes
// destacados y con su ×; debajo, el resto por categoría, plegables, con cuántas líneas hay sin leer y su «Marcar leído». Una línea con «×N»
// son N avisos iguales fundidos. Lo urgente se da por visto al abrir el panel; lo demás, al marcarlo. El aviso flotante sale arriba al centro
// con cada urgente que llega en vivo; si ya hay uno, no lo pisa: suma «· +N más en Avisos».
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    public sealed class PanelAvisos
    {
        const string ClavePlegadas = "mundo.avisos.plegadas";
        const long ToastMs = 8000;

        readonly Avisos avisos;
        readonly VisualElement capa;
        readonly Label toast;
        readonly HashSet<string> plegadas;
        VisualElement panel;
        IVisualElementScheduledItem ocultarToast;
        string toastTexto;
        int toastExtra;

        public bool Abierto => panel != null;
        /// <summary>«Ver informe» en el aviso del final de una batalla: abre su informe (`HayInforme`: el motor aún la guarda).</summary>
        public Action<string> VerInforme;
        public Func<string, bool> HayInforme;

        public PanelAvisos(Avisos avisos, VisualElement capa)
        {
            this.avisos = avisos;
            this.capa = capa;
            toast = Dialogo.Texto("", "toast toast--oculto");
            toast.pickingMode = PickingMode.Ignore;
            capa.Add(toast);
            plegadas = new HashSet<string>(PlayerPrefs.GetString(ClavePlegadas, "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            avisos.AlCambiar += () => { if (panel != null) Pintar(); };
            avisos.AlUrgente += a => Mostrar(a.Texto, a.Clase == ClaseAviso.Peligro, true);
        }

        /// <summary>El aviso flotante. `agrupar`: si ya hay uno a la vista, no lo pisa y suma uno más.</summary>
        public void Mostrar(string texto, bool peligro = false, bool agrupar = false)
        {
            var visible = !toast.ClassListContains("toast--oculto");
            if (agrupar && visible && toastTexto != null)
            {
                toastExtra++;
                toast.text = $"{toastTexto} · +{toastExtra} más en Avisos";
                peligro |= toast.ClassListContains("toast--peligro");
            }
            else
            {
                toastTexto = texto;
                toastExtra = 0;
                toast.text = texto;
            }
            toast.EnableInClassList("toast--peligro", peligro);
            toast.RemoveFromClassList("toast--oculto");
            toast.BringToFront();
            ocultarToast?.Pause();
            ocultarToast = toast.schedule.Execute(() => { toast.AddToClassList("toast--oculto"); toastTexto = null; }).StartingIn(ToastMs);
        }

        public void Alternar()
        {
            if (panel != null) { panel.RemoveFromHierarchy(); panel = null; return; }
            panel = Dialogo.Caja("placa panel-avisos");
            capa.Add(panel);
            Pintar();
            avisos.MarcarLeidos(urgentes: true);
        }

        void Pintar()
        {
            panel.Clear();
            var filete = Dialogo.Caja("placa-filete");
            panel.Add(filete);
            var cab = Dialogo.Caja("avisos-cab");
            cab.Add(Dialogo.Texto("AVISOS", "kicker"));
            cab.Add(Dialogo.Boton("Cerrar", "boton-secundario", Alternar));
            filete.Add(cab);
            var lista = new ScrollView();
            lista.style.flexGrow = 1;
            filete.Add(lista);
            var h = avisos.Historial;
            if (h.Count == 0) { lista.Add(Dialogo.Texto("Nada que contar todavía.", "vacio")); return; }

            var urgentes = h.Where(a => a.Urgente).ToList();
            if (urgentes.Count > 0)
            {
                var sec = Dialogo.Caja("avisos-urgentes");
                sec.Add(Cabecera($"Urgentes ({urgentes.Count}{SinLeer(urgentes)})", null, urgentes.Any(a => !a.Leido) ? () => avisos.MarcarLeidos(urgentes: true) : (Action)null));
                foreach (var a in urgentes) sec.Add(Fila(a, true));
                lista.Add(sec);
            }
            foreach (var (categoria, nombre) in Avisos.Categorias)
            {
                var lineas = h.Where(a => !a.Urgente && a.Categoria == categoria).ToList();
                if (lineas.Count == 0) continue;
                var clave = categoria.ToString();
                var plegada = plegadas.Contains(clave);
                lista.Add(Cabecera($"{(plegada ? "▸" : "▾")} {nombre} ({lineas.Count}{SinLeer(lineas)})", () => Plegar(clave),
                    lineas.Any(a => !a.Leido) ? () => avisos.MarcarLeidos(categoria) : (Action)null));
                if (!plegada) foreach (var a in lineas) lista.Add(Fila(a, false));
            }
        }

        static string SinLeer(List<EntradaAviso> l)
        {
            var n = l.Count(a => !a.Leido);
            return n > 0 ? $", {n} sin leer" : "";
        }

        VisualElement Cabecera(string texto, Action alPulsar, Action marcarLeido)
        {
            var cab = Dialogo.Caja("avisos-cab");
            cab.Add(Dialogo.Boton(texto, "avisos-seccion-titulo", alPulsar ?? (() => { })));
            if (marcarLeido != null) cab.Add(Dialogo.Boton("Marcar leído", "enlace", marcarLeido));
            return cab;
        }

        VisualElement Fila(EntradaAviso a, bool conBorrar)
        {
            var fila = Dialogo.Caja("aviso");
            fila.EnableInClassList("aviso--urgente", a.Urgente);
            fila.EnableInClassList("aviso--nuevo", !a.Leido);
            var cuerpo = Dialogo.Caja("aviso-cuerpo");
            cuerpo.Add(Dialogo.Texto((a.Titulo ?? "Aviso") + (a.Veces > 1 ? $" ×{a.Veces}" : "") + (a.Leido ? "" : " •"), "aviso-titulo"));
            cuerpo.Add(Dialogo.Texto(a.Texto, "aviso-texto"));
            cuerpo.Add(Dialogo.Texto(Cuando(a), "aviso-cuando"));
            if (a.BatallaId != null && VerInforme != null && HayInforme?.Invoke(a.BatallaId) == true)
            {
                var ver = Dialogo.Boton("Ver informe", "enlace", () => { Alternar(); VerInforme(a.BatallaId); });
                ver.name = "ver-informe-" + a.BatallaId;
                cuerpo.Add(ver);
            }
            fila.Add(cuerpo);
            if (conBorrar) fila.Add(Dialogo.Boton("×", "aviso-borrar", () => avisos.Borrar(a)));
            return fila;
        }

        /// <summary>Una línea agrupada enseña de qué hora a qué hora (horas, no «hace 2 min»: no cambian con el reloj); una suelta, su fecha de mundo.</summary>
        static string Cuando(EntradaAviso a)
        {
            string Hora(long ms) => DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime().ToString("HH:mm");
            if (a.Veces > 1 && a.Desde != 0) return Hora(a.Desde) == Hora(a.Hasta) ? $"a las {Hora(a.Hasta)}" : $"de {Hora(a.Desde)} a {Hora(a.Hasta)}";
            return a.EnMs != 0 ? "En el mundo, " + Instante.AFecha(a.EnMs).ToString("dd/MM/yyyy HH:mm") : Hora(a.Hasta);
        }

        void Plegar(string clave)
        {
            if (!plegadas.Remove(clave)) plegadas.Add(clave);
            PlayerPrefs.SetString(ClavePlegadas, string.Join(",", plegadas));
            Pintar();
        }
    }
}

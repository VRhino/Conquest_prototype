using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    /// <summary>El cuadro de confirmación y aviso del juego (BronzeAgeClient: ui/dialogo.ts): un velo y un cuadro de bronce. Escape (o pulsar
    /// fuera) cancela; Intro acepta. Solo hay uno a la vez: abrir otro cierra el anterior como cancelado.</summary>
    public sealed class Dialogo
    {
        readonly VisualElement capa;
        Action<bool> cerrarAbierto;

        public Dialogo(VisualElement capa) { this.capa = capa; }

        /// <summary>Pide confirmación: true si el jugador acepta. `cancelar` null es un aviso con un solo botón.</summary>
        public Task<bool> Abrir(string texto, string titulo = null, string aceptar = "Aceptar", string cancelar = "Cancelar", bool peligro = false)
        {
            cerrarAbierto?.Invoke(false);
            var t = new TaskCompletionSource<bool>();
            var velo = new VisualElement();
            velo.AddToClassList("dialogo-velo");
            velo.focusable = true;
            var placa = Caja("placa dialogo");
            var filete = Caja("placa-filete");
            placa.Add(filete);
            filete.Add(Texto(titulo ?? (cancelar == null ? "Aviso" : "¿Seguro?"), "dialogo-titulo"));
            filete.Add(Texto(texto, "dialogo-texto"));
            var botones = Caja("dialogo-botones");
            filete.Add(botones);
            void Cerrar(bool ok)
            {
                if (cerrarAbierto == null) return;
                cerrarAbierto = null;
                velo.RemoveFromHierarchy();
                t.TrySetResult(ok);
            }
            if (cancelar != null) botones.Add(Boton(cancelar, "boton-secundario", () => Cerrar(false)));
            var si = Boton(aceptar, peligro ? "boton-primario boton-peligro" : "boton-primario", () => Cerrar(true));
            botones.Add(si);
            velo.Add(placa);
            velo.RegisterCallback<PointerDownEvent>(e => { if (e.target == velo) Cerrar(cancelar == null); });
            velo.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Escape) Cerrar(cancelar == null);
                else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) Cerrar(true);
            }, TrickleDown.TrickleDown);
            cerrarAbierto = Cerrar;
            capa.Add(velo);
            velo.schedule.Execute(() => si.Focus());
            return t.Task;
        }

        internal static VisualElement Caja(string clases)
        {
            var v = new VisualElement();
            foreach (var c in clases.Split(' ')) v.AddToClassList(c);
            return v;
        }

        internal static Label Texto(string texto, string clases)
        {
            var l = new Label(texto);
            foreach (var c in clases.Split(' ')) l.AddToClassList(c);
            return l;
        }

        /// <summary>El ✕ de cerrar una ventana, arriba a la derecha en su cabecera (el de la Armería del prototipo; Víctor, 2026-11-19).</summary>
        internal static Button Equis(Action alPulsar, string clases = "")
        {
            var b = Boton("✕", ("ventana-x " + clases).Trim(), alPulsar);
            b.tooltip = "Cerrar";
            return b;
        }

        internal static Button Boton(string texto, string clases, Action alPulsar)
        {
            var b = new Button(alPulsar) { text = texto };
            foreach (var c in clases.Split(' ')) b.AddToClassList(c);
            return b;
        }
    }
}

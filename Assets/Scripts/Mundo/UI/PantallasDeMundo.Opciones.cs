// La ventana «Opciones» (Víctor, 2026-11-15), desde el menú ☰ de la barra del mapa. Pinta lo que declara `OpcionesDelJuego`: una sección por
// grupo y una fila por opción (el interruptor de siempre y su explicación). Guarda al momento; Esc o «Cerrar» la cierran.
using UnityEngine;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    public sealed partial class PantallasDeMundo
    {
        VisualElement ventanaOpciones;

        public bool OpcionesAbiertas => ventanaOpciones?.panel != null;

        void AbrirOpciones()
        {
            if (OpcionesAbiertas) return;
            var velo = Dialogo.Caja("dialogo-velo opciones-velo");
            velo.name = "ventana-opciones";
            velo.focusable = true;
            // El interruptor es el de la plaza: se trae su hoja.
            if (Resources.Load<StyleSheet>("MundoUI/Plaza") is StyleSheet hoja) velo.styleSheets.Add(hoja);
            var placa = Dialogo.Caja("placa opciones-ventana");
            var filete = Dialogo.Caja("placa-filete");
            placa.Add(filete);
            var cab = Dialogo.Caja("opciones-cabecera");
            cab.Add(Dialogo.Texto("OPCIONES", "dialogo-titulo"));
            var cerrar = Dialogo.Equis(CerrarOpciones);
            cerrar.name = "opciones-cerrar";
            cab.Add(cerrar);
            filete.Add(cab);
            foreach (var s in OpcionesDelJuego.Secciones)
            {
                filete.Add(Kicker(s.Titulo));
                foreach (var o in s.Opciones)
                {
                    var fila = Dialogo.Caja("opciones-fila");
                    var clave = o.Clave;
                    fila.Add(Interruptor("opcion-" + clave, o.Texto, null, OpcionesDelJuego.Valor(clave), "Sí", "No", "opciones-interruptor",
                        nuevo => { OpcionesDelJuego.Poner(clave, nuevo); return System.Threading.Tasks.Task.FromResult(true); }));
                    if (!string.IsNullOrEmpty(o.Ayuda)) fila.Add(Dialogo.Texto(o.Ayuda, "opciones-ayuda"));
                    filete.Add(fila);
                }
            }
            // En una batalla: abandonarla, con su confirmación (Víctor, 2026-11-16: en las Opciones, que se abren con Esc; sin F10).
            if (BatallaEnCurso != null && !yendoAlInforme)
            {
                filete.Add(Kicker("Batalla"));
                var abandonar = Dialogo.Boton("Abandonar la batalla", "boton-peligro opciones-abandonar", AbandonarLaBatalla);
                abandonar.name = "opciones-abandonar";
                filete.Add(abandonar);
                // En la batalla el ratón va con la cámara: se suelta mientras están abiertas.
                SceneTransitionService.ApplyCursorState(true);
            }
            velo.Add(placa);
            velo.RegisterCallback<PointerDownEvent>(e => { if (e.target == velo) CerrarOpciones(); });
            capaEncima.Add(velo);
            ventanaOpciones = velo;
            velo.schedule.Execute(() => velo.Focus());
        }

        void CerrarOpciones()
        {
            if (ventanaOpciones == null) return;
            ventanaOpciones.RemoveFromHierarchy();
            ventanaOpciones = null;
            if (BatallaEnCurso != null && !yendoAlInforme) SceneTransitionService.ApplyCursorState(false);
        }

        /// <summary>Esc en la batalla abre las Opciones (salvo con el panel de suministro abierto, que Esc cierra), un fotograma después de cerrarlo.</summary>
        bool dialogoDeBatallaAntes;
        void EscEnLaBatalla()
        {
            bool dialogo = DialogueUIState.IsDialogueOpen;
            bool antes = dialogoDeBatallaAntes;
            dialogoDeBatallaAntes = dialogo;
            if (BatallaEnCurso == null || yendoAlInforme || OpcionesAbiertas || dialogo || antes) return;
            if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true) AbrirOpciones();
        }
    }
}

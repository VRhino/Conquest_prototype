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
            var cerrar = Dialogo.Boton("Cerrar", "boton-secundario", CerrarOpciones);
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
            velo.Add(placa);
            velo.RegisterCallback<PointerDownEvent>(e => { if (e.target == velo) CerrarOpciones(); });
            capaEncima.Add(velo);
            ventanaOpciones = velo;
            velo.schedule.Execute(() => velo.Focus());
        }

        void CerrarOpciones()
        {
            ventanaOpciones?.RemoveFromHierarchy();
            ventanaOpciones = null;
        }
    }
}

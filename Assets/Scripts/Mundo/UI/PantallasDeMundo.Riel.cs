// Los otros dos paneles del riel del mapa (Víctor, 2026-11-18, duda 39), calcados de BronzeAgeClient: ⚔ «Lo que llevas» (`ui/panelColumna.ts`) y
// 📍 «Mis cosas» (la rama «cosas» de `renderPanelRiel` en `main.ts`). El riel queda como el del prototipo: ⚔ ⚑ 📍 ⌂.
// - Lo que llevas: los hombres y el tipo de columna; tu tropa en orden de combate, con ↑ ↓ (`ordenarEscuadras`: en un ejército solo las tuyas);
//   cuántas llevan los demás héroes; el carro (el mismo de la Base: `CarroDeLaBase`) y las caravanas enganchadas (`CaravanasDeLaColumna`).
// - Mis cosas: los asentamientos y las columnas de tu Facción; cada uno abre el mapa 2D centrado en él.
// Un solo panel lateral a la vez (con el de fundar): abrir uno cierra los otros.
using System;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;
using ConquestTactics.Mundo.Mapa;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    public partial class PantallasDeMundo
    {
        /// <summary>El panel lateral abierto del riel: «columna» (⚔), «cosas» (📍) o null. El de fundar va aparte (`ventanaDeFundar`).</summary>
        string panelDelRiel;
        string firmaDelRiel;

        /// <summary>⚔ y 📍 en el riel, en su sitio: ⚔ ⚑ 📍 ⌂ (los otros dos los ponen `RielDelMapa` y `RielDeFundar`).</summary>
        void RielDeColumnaYCosas(VisualElement v)
        {
            var riel = v.Q("mapa-riel");
            Button Boton(string id, string glifo, string titulo)
            {
                var b = new Button(() =>
                {
                    panelDelRiel = panelDelRiel == id ? null : id;
                    if (panelDelRiel != null) { ventanaDeFundar = false; PintarVentanaDeFundar(v); }
                    firmaDelRiel = null;
                    PintarPanelDelRiel(v);
                }) { name = "riel-" + id, tooltip = titulo };
                b.Add(IconosDelMundo.Icono("glifo", glifo, 22, IconosDelMundo.OroClaro));
                b.AddToClassList("mapa-riel-boton");

                return b;
            }
            riel.Insert(0, Boton("columna", "atacar", "Lo que llevas: tropa y carro"));
            var fundar = riel.Q<Button>("riel-fundar");
            var cosas = Boton("cosas", "sitio", "Mis cosas");
            if (fundar != null) riel.Insert(riel.IndexOf(fundar), cosas); else riel.Add(cosas);
            // Abrir fundar cierra estos.
            fundar?.RegisterCallback<ClickEvent>(_ => { panelDelRiel = null; firmaDelRiel = null; PintarPanelDelRiel(v); }, TrickleDown.TrickleDown);
        }

        /// <summary>El panel lateral abierto (⚔ o 📍): se rehace cuando cambian tu columna, tu héroe, tus escuadras o lo que hay a la vista.</summary>
        void PintarPanelDelRiel(VisualElement v)
        {
            var col = MiColumna();
            var h = HeroeAhora();
            foreach (var id in new[] { "columna", "cosas" }) v.Q<Button>("riel-" + id)?.EnableInClassList("activo", panelDelRiel == id && col != null);
            var placa = v.Q("ventana-riel");
            if (panelDelRiel == null || col == null || h == null) { placa?.RemoveFromHierarchy(); firmaDelRiel = null; return; }
            var a = c.Sesion.Almacen;
            var firma = panelDelRiel == "columna"
                ? $"columna|{col.Version}|{h.Version}|{string.Join(",", EscuadrasDeLaColumna().Select(s => s.Version))}|{FirmaDeCaravanasDeColumna(col)}"
                : $"cosas|{h.Version}|{string.Join(",", a.Todas(Entidad.CuerpoOneofCase.Asentamiento).Select(e => e.Asentamiento.Id + ":" + e.Asentamiento.Version))}|{string.Join(",", a.Todas(Entidad.CuerpoOneofCase.Ejercito).Select(e => e.Ejercito.Id + ":" + e.Ejercito.Version))}";
            if (placa != null && firma == firmaDelRiel) return;
            firmaDelRiel = firma;
            placa?.RemoveFromHierarchy();
            placa = Dialogo.Caja("placa ventana-fundar ventana-riel");
            placa.name = "ventana-riel";
            var filete = Dialogo.Caja("placa-filete");
            placa.Add(filete);
            var cab = Dialogo.Caja("ventana-puerta-cab");
            cab.Add(TituloDeSeccion(panelDelRiel == "columna" ? "Lo que llevas" : "Mis cosas", null));
            var cerrar = Dialogo.Boton("×", "aviso-borrar", () => { panelDelRiel = null; PintarPanelDelRiel(v); });
            cerrar.name = "riel-cerrar";
            cab.Add(cerrar);
            filete.Add(cab);
            var cuerpo = new ScrollView(ScrollViewMode.Vertical) { name = "riel-cuerpo" };
            cuerpo.AddToClassList("riel-cuerpo");
            filete.Add(cuerpo);
            if (panelDelRiel == "columna") LoQueLlevas(cuerpo, col, h);
            else MisCosas(cuerpo, h);
            v.Add(placa);
        }

        /// <summary>⚔ Lo que llevas (`htmlColumna`).</summary>
        void LoQueLlevas(VisualElement v, Ejercito col, Heroe h)
        {
            var mias = EscuadrasDeLaColumna();
            var deOtros = col.EscuadronIds.Count - mias.Count;
            var datos = Dialogo.Caja("riel-datos");
            void Dato(string nombre, string valor) { var f = Dialogo.Caja("riel-dato"); f.Add(Dialogo.Texto(nombre, "riel-dato-nombre")); f.Add(Dialogo.Texto(valor, "riel-dato-valor")); datos.Add(f); }
            Dato("Hombres", mias.Sum(s => (long)s.Cantidad).ToString());
            Dato("Columna", col.Tipo == TipoColumna.ColumnaEjercito ? $"ejército · {col.Participantes.Count}" : "personal");
            v.Add(datos);
            v.Add(TituloDeSeccion("Tropa, en orden de combate", "La primera entra primero en combate. Se reordenan con las flechas. En un ejército con más héroes solo se ordenan las tuyas; el orden de las demás lo pone cada uno."));
            var error = Dialogo.Texto("", "error oculto");
            error.name = "riel-error";
            if (mias.Count == 0) v.Add(Dialogo.Texto("Tu columna no lleva tropa tuya.", "plaza-sub"));
            else
            {
                var lista = Dialogo.Caja("riel-orden");
                lista.name = "riel-orden";
                for (int i = 0; i < mias.Count; i++)
                {
                    var s = mias[i];
                    var fila = Dialogo.Caja("riel-orden-fila");
                    fila.name = "orden-" + s.Id;
                    fila.Add(Dialogo.Texto((i + 1).ToString(), "riel-orden-pos"));
                    if (PiezasDeTropa.Miniatura(s.TropaId, 30) is VisualElement mini) fila.Add(mini);
                    var texto = Dialogo.Caja("riel-orden-texto");
                    texto.Add(Dialogo.Texto(NombreDeTropa(s.TropaId), "riel-orden-nombre"));
                    texto.Add(Dialogo.Texto($"{s.Cantidad} hombres · moral {Math.Round(Instante.Valor(s.Moral, c.Sesion.AhoraMundo) / 1000)}{(s.HasPrestadaPorCampamentoId ? " · prestada" : "")}", "plaza-sub"));
                    fila.Add(texto);
                    var k = i;
                    foreach (var (simbolo, dir) in new[] { ("↑", -1), ("↓", 1) })
                    {
                        var j = k + dir;
                        var b = Dialogo.Boton(simbolo, "boton-secundario riel-orden-mover", async () =>
                        {
                            var ids = mias.Select(x => x.Id).ToList();
                            (ids[k], ids[j]) = (ids[j], ids[k]);
                            var o = new OrdenOrdenarEscuadras();
                            o.EscuadronIds.AddRange(ids);
                            if (await OrdenConError(new Orden { OrdenarEscuadras = o }, m => Poner(error, m))) firmaDelRiel = null;
                        });
                        b.name = (dir < 0 ? "subir-" : "bajar-") + s.Id;
                        b.SetEnabled(j >= 0 && j < mias.Count);
                        fila.Add(b);
                    }
                    lista.Add(fila);
                }
                v.Add(lista);
            }
            if (deOtros > 0) v.Add(Dialogo.Texto($"Además van {deOtros} escuadras de los otros héroes del ejército.", "plaza-sub"));
            v.Add(error);
            CarroDeLaBase(v, h, m => Poner(error, m));
            CaravanasDeLaColumna(v, col, h, m => Poner(error, m));
        }

        /// <summary>📍 Mis cosas (la rama «cosas»): los asentamientos y las columnas de tu Facción; cada uno abre el mapa 2D centrado en él.</summary>
        void MisCosas(VisualElement v, Heroe h)
        {
            var a = c.Sesion.Almacen;
            VisualElement Item(string nombre, string sub, (double x, double y) donde, string id)
            {
                var b = Dialogo.Boton("", "riel-cosa", () => AbrirMapa2D(donde));
                b.name = "cosa-" + id;
                b.Add(Dialogo.Texto(nombre, "riel-cosa-nombre"));
                b.Add(Dialogo.Texto(sub, "riel-cosa-sub"));
                return b;
            }
            v.Add(Dialogo.Texto("Asentamientos", "riel-cosas-titulo"));
            var plazas = a.Todas(Entidad.CuerpoOneofCase.Asentamiento).Select(e => e.Asentamiento).Where(x => h.HasFaccionId && x.FaccionId == h.FaccionId).ToList();
            if (plazas.Count == 0) v.Add(Dialogo.Texto("Ninguno a la vista.", "plaza-sub"));
            foreach (var p in plazas) v.Add(Item(p.HasNombre ? p.Nombre : p.Id, $"Nivel {p.Nivel}", Pt(p.Posicion), p.Id));
            v.Add(Dialogo.Texto("Columnas", "riel-cosas-titulo"));
            bool DeMiFaccion(Ejercito e) => e.Participantes.Any(x => x.HeroeId == h.Id
                || h.HasFaccionId && a.Buscar(TipoEntidad.Heroe, x.HeroeId)?.Heroe is Heroe o && o.HasFaccionId && o.FaccionId == h.FaccionId);
            var columnas = a.Todas(Entidad.CuerpoOneofCase.Ejercito).Select(e => e.Ejercito).Where(DeMiFaccion).ToList();
            if (columnas.Count == 0) v.Add(Dialogo.Texto("Ninguna.", "plaza-sub"));
            foreach (var e in columnas)
            {
                var p = Instante.Posicion(e.Plan, e.Posicion, AhoraMs);
                var mia = e.Participantes.Any(x => x.HeroeId == h.Id);
                v.Add(Item(mia ? "Tu columna" : e.Id, EstadoDeColumna.TryGetValue(e.Estado, out var est) ? est : e.Estado.ToString(), (p.x / 1000.0, p.y / 1000.0), e.Id));
            }
        }
    }
}

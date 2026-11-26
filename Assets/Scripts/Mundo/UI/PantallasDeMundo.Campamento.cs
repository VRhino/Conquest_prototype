// Los paneles del campamento de mercenarios por dentro (paso 4B del bloque 4; BronzeAgeClient: `ui/pantallaCampamento.ts`): la taberna (residir),
// el mercado (comprar) y el edificio militar (reclutar y pedir prestada, con el árbol de tropas: `PantallasDeMundo.Reclutamiento`). Se abren al acercarse a la pieza de la planta y pulsar F; con uno
// abierto el héroe no anda y el ratón queda libre. Las cifras salen del ámbito CAMPAMENTO y de `/v1/datos`; los rechazos, con su frase.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BronzeAge.Contract.V1;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    public sealed partial class PantallasDeMundo
    {
        /// <summary>Los nombres de las piezas del campamento (los del plano de BronzeAgeClient).</summary>
        static readonly Dictionary<string, string> NombreDePieza = new Dictionary<string, string>
        {
            ["taberna"] = "Taberna", ["vivienda"] = "Vivienda", ["mercado"] = "Mercado", ["puestoMercado"] = "Puesto de mercado",
            ["barracon"] = "Barracón", ["galeriaDeTiro"] = "Galería de tiro", ["caballerizas"] = "Caballerizas", ["plazaDeArmas"] = "Plaza de armas",
            ["plaza"] = "Plaza", ["pozo"] = "Pozo", ["parque"] = "Parque", ["armeria"] = "Armería",
        };

        static string Pieza(string tipo) => NombreDePieza.TryGetValue(tipo, out var n) ? n : tipo;

        /// <summary>A qué zona da cada pieza: la taberna (residir), el mercado y sus puestos (comprar), la armería (su tienda) y el edificio militar
        /// (`MERCENARIOS.edificiosMilitares`: reclutar).</summary>
        string ZonaDePieza(string tipo)
        {
            if (tipo == "taberna") return "taberna";
            if (tipo == "mercado" || tipo == "puestoMercado") return "mercado";
            if (tipo == "armeria") return "armeria";
            var militares = c.Datos.En("documentos.Campamentos_Mercenarios.MERCENARIOS.edificiosMilitares")?.Select(x => (string)x) ?? Enumerable.Empty<string>();
            return militares.Contains(tipo) ? "militar" : null;
        }

        /// <summary>El cartel de una zona: la tecla y lo que se hace ahí.</summary>
        static string CartelDeZona(string zona, CampamentoMercenarios camp) => zona switch
        {
            "taberna" => "F · Taberna",
            "mercado" => "F · Mercado",
            "armeria" => "F · Armería",
            "militar" => $"F · {Pieza(camp.Edificios.FirstOrDefault(e => e == "barracon" || e == "galeriaDeTiro" || e == "caballerizas") ?? "barracon")}: tropa",
            "puerta" => "F · Salir al mundo",
            _ => null,
        };

        // ─────────────── lo común ───────────────

        Heroe HeroeDelCampamento() => HeroeAhora();

        CampamentoMercenarios CampamentoActual(string id) => c.Sesion.Almacen.Buscar(TipoEntidad.CampamentoMercenarios, id)?.CampamentoMercenarios;

        List<Escuadron> EscuadrasDelHeroe(Heroe h) =>
            h.EscuadronesIds.Select(id => c.Sesion.Almacen.Buscar(TipoEntidad.Escuadron, id)?.Escuadron).Where(e => e != null).ToList();

        /// <summary>La columna en la que va el héroe (dentro o a la puerta), con su carro; null si no va en ninguna.</summary>
        Ejercito ColumnaDelHeroe(Heroe h) =>
            c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Ejercito).Select(e => e.Ejercito)
                .FirstOrDefault(e => e.LiderId == h.Id || e.Participantes.Any(p => p.HeroeId == h.Id));

        static double Milesimas(Cantidades q, string r) => q != null && q.PorId.TryGetValue(r, out var v) ? v / 1000.0 : 0;

        /// <summary>Lo de un almacén en piezas (`htmlPlacaDeRecursos`): el emoji del recurso y la cifra; el nombre, al pasar el ratón.</summary>
        static VisualElement PiezasDeAlmacen(IEnumerable<KeyValuePair<string, long>> almacen, string nombre)
        {
            var fila = Dialogo.Caja("fila-envuelta");
            fila.name = nombre;
            foreach (var kv in almacen.OrderBy(x => x.Key))
                fila.Add(IconosDelMundo.ChipDeRecurso(kv.Key, Math.Floor(kv.Value / 1000.0).ToString(), CosasDelMapa.NombreDeRecurso(kv.Key)));
            return fila;
        }

        /// <summary>El coste de Liderazgo de una tropa en el motor: `LIDERAZGO.costePorEscalon` de su escalón; null si no se sabe.</summary>
        int? LiderazgoDe(string tropaId)
        {
            var t = c.Datos.TropasReclutables?.FirstOrDefault(x => (string)x["id"] == tropaId);
            var escalon = (string)t?["escalon"];
            if (escalon == null) return null;
            try { return (int?)c.Datos.En("documentos.Heroe_Y_Tropa.LIDERAZGO.costePorEscalon")[escalon]; }
            catch (FaltaDato) { return null; }
        }

        string NombreDeTropa(string id)
        {
            var t = c.Datos.TropasReclutables?.FirstOrDefault(x => (string)x["id"] == id);
            return t != null ? (string)t["nombre"] : id;
        }

        static string DondeEsta(Escuadron s) => s.Contenedor?.DondeCase switch
        {
            ContenedorEscuadron.DondeOneofCase.Campamento => "en el campamento",
            ContenedorEscuadron.DondeOneofCase.CaravanaId => "escoltando una caravana",
            ContenedorEscuadron.DondeOneofCase.Fuera => "fuera del mundo",
            ContenedorEscuadron.DondeOneofCase.PuebloId => "guarneciendo un pueblo",
            ContenedorEscuadron.DondeOneofCase.AsentamientoId => "en la guarnición de otra plaza",
            ContenedorEscuadron.DondeOneofCase.AvanzadaId => "guarneciendo una avanzada",
            _ => "en tu columna",
        };

        /// <summary>Un panel del campamento: título, cuerpo que se repinta con lo último del almacén, la línea de error y «Cerrar» (o Esc). Con
        /// él abierto, el héroe no anda.</summary>
        void PanelDelCampamento(string nombre, string titulo, string edificio, Action<VisualElement, Action<string>, Action> pintar, bool completa = false)
        {
            var interior = SelectorDePersonaje.Interior;
            if (interior == null || interior.PanelAbierto) return;
            var velo = Dialogo.Caja("dialogo-velo");
            velo.name = nombre;
            velo.focusable = true;
            var placa = Dialogo.Caja("placa dialogo panel-campamento" + (completa ? " panel-campamento--completo" : ""));
            var filete = Dialogo.Caja("placa-filete");
            placa.Add(filete);
            velo.Add(placa);
            var cabeza = Dialogo.Caja("comparar-fila");
            cabeza.style.borderBottomWidth = 0;
            // El título con el icono de la parte (`htmlTituloDeParte`: `iconoDeParte` a 26 px).
            var tituloConIcono = IconosDelMundo.ConIcono(FamiliaDeIcono(edificio), edificio, Dialogo.Texto(titulo, "dialogo-titulo"), 26);
            tituloConIcono.AddToClassList("titulo-con-icono");
            cabeza.Add(tituloConIcono);
            filete.Add(cabeza);
            // La tienda de la Armería y sus fichas viven en Plaza.uss.
            if (HojaDePlaza is StyleSheet hoja) velo.styleSheets.Add(hoja);
            var cuerpo = new ScrollView { mode = ScrollViewMode.Vertical, horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            cuerpo.style.flexShrink = 1;
            filete.Add(cuerpo);
            var error = Dialogo.Texto("", "error oculto");
            error.name = "error";
            filete.Add(error);
            void Cerrar()
            {
                velo.RemoveFromHierarchy();
                interior.PanelAbierto = false;
            }
            var cerrar = Dialogo.Equis(Cerrar);
            cerrar.name = "cerrar";
            cabeza.Add(cerrar);
            velo.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Escape) Cerrar(); }, TrickleDown.TrickleDown);
            void Repintar()
            {
                if (velo.panel == null) return;
                cuerpo.Clear();
                pintar(cuerpo, t => Poner(error, t), Repintar);
            }
            interior.PanelAbierto = true;
            var cartel = pantalla.Q<Label>("cartel");
            if (cartel != null) Ver(cartel, false);
            capaEncima.Add(velo);
            Repintar();
        }

        /// <summary>Manda una orden desde un panel: la frase del rechazo en la línea de error; aceptada, repinta cuando llegue el cambio.</summary>
        async Task<ResultadoOrden> OrdenDelPanel(Orden orden, Action<string> error, Action repintar, Button boton)
        {
            boton?.SetEnabled(false);
            error(null);
            ResultadoOrden r = null;
            try { r = await c.Sesion.Ordenar(orden); }
            catch (ErrorDeSesion e) { error(e.Message); }
            if (r != null && r.Resultado != Resultado.OrdenAceptada) error(Textos.Explicar(r.Rechazo));
            boton?.SetEnabled(true);
            if (r?.Resultado == Resultado.OrdenAceptada)
            {
                // El cambio llega por el ámbito justo detrás de la respuesta.
                await Task.Delay(400);
                repintar();
            }
            return r;
        }

        // ─────────────── la taberna: residir ───────────────

        /// <summary>La taberna (BronzeAgeClient: la parte «Resumen»): residentes, si resides aquí, «Residir aquí» si no, tu tropa, tu almacén y los
        /// edificios del campamento. Mudarse retira la tropa prestada por el campamento anterior: se avisa antes.</summary>
        void PanelTaberna(string campId)
        {
            PanelDelCampamento("panel-taberna", "TABERNA", "taberna", (v, error, repintar) =>
            {
                var camp = CampamentoActual(campId);
                var h = HeroeDelCampamento();
                if (camp == null || h == null) return;
                // Resumen y, con Facción y sin plaza, el Fondo de refundación (paso 8).
                var seccion = SeccionesDelCampamento(v, h, repintar);
                if (seccion == "fondo") { PintarFondo(v, camp, h, error, repintar); return; }
                if (seccion == "intel") { PanelDeIntel(v, TabernaDeCampamento(camp, h), h, null, repintar); return; }
                PintarResumen(v, camp, h, campId, error, repintar);
            });
        }

        /// <summary>El Resumen del campamento (BronzeAgeClient: `resumen`): residentes, si resides aquí, «Residir aquí» si no, tu tropa, tu almacén y
        /// los edificios. Mudarse retira la tropa prestada por el campamento anterior: se avisa antes.</summary>
        void PintarResumen(VisualElement v, CampamentoMercenarios camp, Heroe h, string campId, Action<string> error, Action repintar)
        {
            bool resides = camp.ResidentesIds.Contains(h.Id);
            v.Add(Dialogo.Texto(resides
                ? "Este campamento es tu residencia: aquí guardas tu almacén, te prestan tropa y compras en el mercado completo."
                : "No resides aquí: sales con la columna con la que entraste y solo te venden trigo. Si te mudas, la tropa prestada por tu campamento anterior se te retira.",
                "dialogo-texto"));
            var datos = new VisualElement();
            Fichas(datos, ("Residentes", camp.ResidentesIds.Count.ToString()), ("Resides aquí", resides ? "Sí" : "No"));
            v.Add(datos);
            if (!resides)
            {
                Button residir = null;
                residir = Dialogo.Boton("Residir aquí", "boton-primario", async () =>
                {
                    var prestadas = EscuadrasDelHeroe(h).Where(s => s.HasPrestadaPorCampamentoId).ToList();
                    if (prestadas.Count > 0 && !await dialogo.Abrir(
                            $"Si resides aquí, el campamento donde te prestaron tropa la retira ({string.Join(", ", prestadas.Select(s => $"{s.Nombre}: {s.Cantidad}"))}).",
                            "¿Residir aquí?", "Residir aquí")) return;
                    await OrdenDelPanel(new Orden { ResidirEnCampamento = new OrdenResidirEnCampamento { HeroeId = h.Id, CampamentoId = campId } }, error, repintar, residir);
                });
                residir.name = "residir";
                v.Add(residir);
            }
            v.Add(Dialogo.Texto("TU TROPA", "kicker"));
            var escuadras = EscuadrasDelHeroe(h);
            if (escuadras.Count == 0) v.Add(Dialogo.Texto("No tienes tropa. En el edificio militar te prestan una.", "campo-ayuda"));
            foreach (var s in escuadras)
            {
                // La miniatura de la tropa (la de Conquest, `Squad_Icon`) y su línea.
                var fila = Dialogo.Caja("con-icono");
                fila.style.marginBottom = 4;
                if (PiezasDeTropa.Miniatura(s.TropaId) is VisualElement mini) fila.Add(mini);
                var texto = Dialogo.Texto($"{s.Nombre} · {s.Cantidad} hombres · {DondeEsta(s)}{(s.HasPrestadaPorCampamentoId ? " · prestada" : "")}", "aviso-texto");
                texto.style.flexShrink = 1;
                fila.Add(texto);
                v.Add(fila);
            }
            v.Add(Dialogo.Texto("TU ALMACÉN", "kicker"));
            var almacen = h.AlmacenPersonal?.PorId.Where(kv => kv.Value >= 1000).ToList() ?? new List<KeyValuePair<string, long>>();
            if (almacen.Count == 0) v.Add(Dialogo.Texto("Nada.", "campo-ayuda"));
            else
            {
                var fichas = new VisualElement { name = "almacen" };
                Fichas(fichas, almacen.Select(kv => (CosasDelMapa.NombreDeRecurso(kv.Key), Math.Floor(kv.Value / 1000.0).ToString())).ToArray());
                v.Add(fichas);
            }
            v.Add(Dialogo.Texto("EDIFICIOS DEL CAMPAMENTO", "kicker"));
            v.Add(Dialogo.Texto(string.Join(" · ", camp.Edificios.Distinct().Select(Pieza)), "campo-ayuda"));
        }

        // ─────────────── el mercado: comprar ───────────────

        string bienElegido, ultimaCompra;
        bool pagarConCarro;

        /// <summary>El mercado (BronzeAgeClient: `mercado` y `cablearMercado`): lo que hay en venta y, del bien elegido, lo que hay, lo que tienes, el
        /// cupo de hoy, tu oro, el precio, la cantidad y lo que pagas. El residente paga con su almacén o con el carro que lleva aquí; el visitante,
        /// con el carro, y solo le venden trigo. Pagando con el carro no hay trigo (se reposta gratis).</summary>
        void PanelMercado(string campId)
        {
            ultimaCompra = null;
            PanelDelCampamento("panel-mercado", "MERCADO", "mercado", (v, error, repintar) =>
            {
                var camp = CampamentoActual(campId);
                var h = HeroeDelCampamento();
                if (camp == null || h == null) return;
                bool resides = camp.ResidentesIds.Contains(h.Id);
                var columna = ColumnaDelHeroe(h);
                if (!resides) pagarConCarro = true;
                else if (pagarConCarro && columna == null) pagarConCarro = false;
                var donde = pagarConCarro ? "el carro de tu columna" : "tu almacén";
                v.Add(Dialogo.Texto((resides ? "" : "Si no resides aquí, solo te venden trigo, y va al carro de tu columna. ")
                    + "Se paga con el oro de tu almacén personal o del carro que llevas aquí, y lo comprado va adonde salió el oro. Si no cabe todo o no te llega, se compra lo que se pueda.",
                    "campo-ayuda"));
                if (resides && columna != null)
                {
                    var fila = Dialogo.Caja("fila-envuelta");
                    fila.Add(Dialogo.Texto("Pagar con", "aviso-texto"));
                    var almacenB = Dialogo.Boton($"Mi almacén ({Math.Floor(Milesimas(h.AlmacenPersonal, "oro"))} de oro)", "boton-secundario", () => { pagarConCarro = false; ultimaCompra = null; repintar(); });
                    var carroB = Dialogo.Boton($"El carro ({Math.Floor(Milesimas(columna.Suministro, "oro"))} de oro)", "boton-secundario", () => { pagarConCarro = true; ultimaCompra = null; repintar(); });
                    almacenB.EnableInClassList("clase--elegida", !pagarConCarro);
                    carroB.EnableInClassList("clase--elegida", pagarConCarro);
                    fila.Add(almacenB);
                    fila.Add(carroB);
                    v.Add(fila);
                }
                var enVenta = camp.Mercado?.PorId.Where(kv => kv.Value >= 1000 && kv.Key != "oro" && (resides ? !(pagarConCarro && kv.Key == "trigo") : kv.Key == "trigo"))
                    .OrderBy(kv => kv.Key).ToList() ?? new List<KeyValuePair<string, long>>();
                if (enVenta.Count == 0)
                {
                    v.Add(Dialogo.Texto(resides && pagarConCarro ? "Con el carro no hay nada en venta: el trigo no va al carro." : "Nada en venta.", "campo-ayuda"));
                    return;
                }
                if (bienElegido == null || !enVenta.Any(kv => kv.Key == bienElegido)) bienElegido = enVenta[0].Key;
                var fuente = pagarConCarro ? columna?.Suministro : h.AlmacenPersonal;
                int Tienes(string r) => (int)Math.Floor(Milesimas(fuente, r));
                var oro = Tienes("oro");
                double? Precio(string r) => camp.PreciosMercado.TryGetValue(r, out var p) && p > 0 ? p / 1_000_000.0 : (double?)null;

                var lista = Dialogo.Caja("fila-envuelta");
                lista.name = "bienes";
                foreach (var kv in enVenta)
                {
                    var r = kv.Key;
                    // `.mercado-bien`: el emoji, el nombre y, debajo, cuánto hay y a cuánto.
                    var b = new Button(() => { bienElegido = r; ultimaCompra = null; repintar(); }) { name = "bien-" + r };
                    b.AddToClassList("mercado-bien");
                    b.Add(IconosDelMundo.Icono("recurso", r, 22));
                    var textos = new VisualElement();
                    textos.Add(Dialogo.Texto(CosasDelMapa.NombreDeRecurso(r), "mercado-bien-nombre"));
                    textos.Add(Dialogo.Texto($"{Math.Floor(kv.Value / 1000.0)} en venta{(Precio(r) is double p ? $" · {p:0.#} oro/u" : "")}", "mercado-bien-detalle"));
                    b.Add(textos);
                    b.EnableInClassList("clase--elegida", r == bienElegido);
                    lista.Add(b);
                }
                v.Add(lista);

                var bien = bienElegido;
                var stock = (int)Math.Floor(Milesimas(camp.Mercado, bien));
                var precio = Precio(bien);
                int? cupo = null;
                if (resides && camp.CupoDiario != null && camp.CupoDiario.PorId.TryGetValue(bien, out var tope))
                {
                    var hoy = (uint)Math.Floor(c.Sesion.AhoraMundo / 86_400_000.0);
                    long comprado = h.CupoCampamento != null && h.CupoCampamento.Dia == hoy && h.CupoCampamento.Comprado.TryGetValue(bien, out var cc) ? cc : 0;
                    cupo = (int)Math.Max(0, Math.Floor((tope - comprado) / 1000.0));
                }
                var maximo = Math.Min(stock, cupo ?? int.MaxValue);
                if (precio is double pr)
                {
                    maximo = Math.Min(maximo, (int)Math.Floor(oro / pr));
                    while (maximo > 0 && Math.Ceiling(maximo * pr) > oro) maximo--;
                }
                var cabezaBien = IconosDelMundo.ConIcono("recurso", bien, Dialogo.Texto(CosasDelMapa.NombreDeRecurso(bien).ToUpperInvariant(), "kicker"), 20);
                v.Add(cabezaBien);
                var datos = new VisualElement { name = "detalle" };
                var filas = new List<(string, string)> { ("En venta", stock.ToString()), ($"Tienes en {donde}", Tienes(bien).ToString()) };
                if (cupo != null) filas.Add(("Cupo de hoy", cupo.ToString()));
                filas.Add(("Tu oro", oro.ToString()));
                if (precio != null) filas.Add(("Precio por unidad", $"{precio:0.#}"));
                Fichas(datos, filas.ToArray());
                v.Add(datos);
                if (maximo < 1)
                {
                    v.Add(Dialogo.Texto(cupo == 0 ? "Ya has comprado hoy todo lo que se te vende de esto." : precio != null && oro < precio ? "No te llega el oro ni para una unidad." : "No se puede comprar ahora.", "campo-ayuda"));
                }
                else
                {
                    var cantidad = new SliderInt("Cantidad", 1, maximo) { value = Math.Min(10, maximo), name = "cantidad", showInputField = true };
                    v.Add(cantidad);
                    // `.mercado-total`: tres cajas, lo que pagas, lo que te queda y lo que tendrás.
                    var total = Dialogo.Caja("mercado-total");
                    total.name = "total";
                    Label Caja(string titulo)
                    {
                        var caja = Dialogo.Caja("mercado-total-caja");
                        caja.Add(Dialogo.Texto(titulo, "mercado-total-titulo"));
                        var valor = Dialogo.Texto("", "mercado-total-valor");
                        caja.Add(valor);
                        total.Add(caja);
                        return valor;
                    }
                    var pagas = precio != null ? Caja("Pagas") : null;
                    var teQueda = precio != null ? Caja("Te queda") : null;
                    var tendras = Caja("Tendrás");
                    v.Add(total);
                    Button comprar = null;
                    void Actualizar(int n)
                    {
                        var paga = precio is double p ? (int)Math.Ceiling(n * p) : 0;
                        if (pagas != null) { pagas.text = $"{paga} de oro"; teQueda.text = $"{oro - paga} de oro"; }
                        tendras.text = $"{Tienes(bien) + n} de {CosasDelMapa.NombreDeRecurso(bien)}";
                        if (comprar != null) comprar.text = $"Comprar {n}" + (precio != null ? $" por {paga} de oro" : "");
                    }
                    comprar = Dialogo.Boton("Comprar", "boton-primario", async () =>
                    {
                        var pedida = cantidad.value;
                        var tienesAntes = Tienes(bien);
                        var oroAntes = oro;
                        var r = await OrdenDelPanel(new Orden { ComprarEnCampamento = new OrdenComprarEnCampamento
                        {
                            Recurso = bien, Cantidad = pedida * 1000L, CampamentoId = campId, PagarCon = pagarConCarro ? "carro" : "almacenPersonal",
                        } }, error, () => { }, comprar);
                        if (r?.Resultado != Resultado.OrdenAceptada) return;
                        // La respuesta no trae lo servido (`datos` va vacío en la sesión): se ve en lo que tienes antes y después.
                        var yo = HeroeDelCampamento();
                        var col = ColumnaDelHeroe(yo);
                        var ahora = pagarConCarro ? col?.Suministro : yo?.AlmacenPersonal;
                        var servida = Math.Floor(Milesimas(ahora, bien)) - tienesAntes;
                        var pagado = oroAntes - Math.Floor(Milesimas(ahora, "oro"));
                        ultimaCompra = $"Compraste {servida:0} de {CosasDelMapa.NombreDeRecurso(bien)} por {pagado:0} de oro.{(servida < pedida ? " No cabía o no alcanzaba para más." : "")}";
                        repintar();
                    });
                    comprar.name = "comprar";
                    cantidad.RegisterValueChangedCallback(e => Actualizar(e.newValue));
                    Actualizar(cantidad.value);
                    v.Add(comprar);
                }
                if (ultimaCompra != null) { var hecho = Dialogo.Texto(ultimaCompra, "aviso-texto mercado-hecho"); hecho.name = "ultima-compra"; v.Add(hecho); }
            });
        }
    }
}

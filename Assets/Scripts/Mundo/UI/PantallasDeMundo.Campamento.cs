// Los paneles del campamento de mercenarios por dentro (paso 4B del bloque 4; BronzeAgeClient: `ui/pantallaCampamento.ts`): la taberna (residir),
// el mercado (comprar) y el edificio militar (reclutar y pedir prestada). Se abren al acercarse a la pieza de la planta y pulsar F; con uno
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
            ["plaza"] = "Plaza", ["pozo"] = "Pozo", ["parque"] = "Parque",
        };

        static string Pieza(string tipo) => NombreDePieza.TryGetValue(tipo, out var n) ? n : tipo;

        /// <summary>A qué zona da cada pieza: la taberna (residir), el mercado y sus puestos (comprar) y el edificio militar
        /// (`MERCENARIOS.edificiosMilitares`: reclutar).</summary>
        string ZonaDePieza(string tipo)
        {
            if (tipo == "taberna") return "taberna";
            if (tipo == "mercado" || tipo == "puestoMercado") return "mercado";
            var militares = c.Datos.En("documentos.Campamentos_Mercenarios.MERCENARIOS.edificiosMilitares")?.Select(x => (string)x) ?? Enumerable.Empty<string>();
            return militares.Contains(tipo) ? "militar" : null;
        }

        /// <summary>El cartel de una zona: la tecla y lo que se hace ahí.</summary>
        static string CartelDeZona(string zona, CampamentoMercenarios camp) => zona switch
        {
            "taberna" => "F · Taberna",
            "mercado" => "F · Mercado",
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
        void PanelDelCampamento(string nombre, string titulo, Action<VisualElement, Action<string>, Action> pintar)
        {
            var interior = SelectorDePersonaje.Interior;
            if (interior == null || interior.PanelAbierto) return;
            var velo = Dialogo.Caja("dialogo-velo");
            velo.name = nombre;
            velo.focusable = true;
            var placa = Dialogo.Caja("placa dialogo panel-campamento");
            var filete = Dialogo.Caja("placa-filete");
            placa.Add(filete);
            velo.Add(placa);
            var cabeza = Dialogo.Caja("comparar-fila");
            cabeza.style.borderBottomWidth = 0;
            cabeza.Add(Dialogo.Texto(titulo, "dialogo-titulo"));
            filete.Add(cabeza);
            var cuerpo = new ScrollView { mode = ScrollViewMode.Vertical, horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            cuerpo.style.flexShrink = 1;
            filete.Add(cuerpo);
            var error = Dialogo.Texto("", "error oculto");
            error.name = "error";
            filete.Add(error);
            var botones = Dialogo.Caja("dialogo-botones");
            filete.Add(botones);
            void Cerrar()
            {
                velo.RemoveFromHierarchy();
                interior.PanelAbierto = false;
            }
            var cerrar = Dialogo.Boton("Cerrar", "boton-secundario", Cerrar);
            cerrar.name = "cerrar";
            botones.Add(cerrar);
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
            PanelDelCampamento("panel-taberna", "TABERNA", (v, error, repintar) =>
            {
                var camp = CampamentoActual(campId);
                var h = HeroeDelCampamento();
                if (camp == null || h == null) return;
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
                    v.Add(Dialogo.Texto($"{s.Nombre} · {s.Cantidad} hombres · {DondeEsta(s)}{(s.HasPrestadaPorCampamentoId ? " · prestada" : "")}", "aviso-texto"));
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
            });
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
            PanelDelCampamento("panel-mercado", "MERCADO", (v, error, repintar) =>
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
                    var b = Dialogo.Boton($"{CosasDelMapa.NombreDeRecurso(r)} · {Math.Floor(kv.Value / 1000.0)} en venta{(Precio(r) is double p ? $" · {p:0.#} oro/u" : "")}",
                        "boton-secundario", () => { bienElegido = r; ultimaCompra = null; repintar(); });
                    b.name = "bien-" + r;
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
                v.Add(Dialogo.Texto(CosasDelMapa.NombreDeRecurso(bien).ToUpperInvariant(), "kicker"));
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
                    var total = Dialogo.Texto("", "aviso-texto");
                    v.Add(total);
                    Button comprar = null;
                    void Actualizar(int n)
                    {
                        var paga = precio is double p ? (int)Math.Ceiling(n * p) : 0;
                        total.text = (precio != null ? $"Pagas {paga} de oro · te queda {oro - paga} · " : "") + $"tendrás {Tienes(bien) + n} de {CosasDelMapa.NombreDeRecurso(bien)}";
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
                if (ultimaCompra != null) { var hecho = Dialogo.Texto(ultimaCompra, "aviso-texto"); hecho.name = "ultima-compra"; v.Add(hecho); }
            });
        }

        // ─────────────── el edificio militar: reclutar y pedir prestada ───────────────

        bool reclutarConCarro;

        /// <summary>La tropa (BronzeAgeClient: `tropa`, `lugarCampamento` y `accionesCampamento`), en lista: los reclutas del campamento, de dónde sale
        /// el oro, las tropas que se prestan gratis (la leva comunal, como dice el motor) y las que ofrece este campamento con su precio. Solo el
        /// residente. «Reclutar» sale desactivado con su motivo si faltan reclutas o no llega el oro.</summary>
        void PanelTropa(string campId)
        {
            PanelDelCampamento("panel-tropa", "TROPA", (v, error, repintar) =>
            {
                var camp = CampamentoActual(campId);
                var h = HeroeDelCampamento();
                if (camp == null || h == null) return;
                if (!camp.ResidentesIds.Contains(h.Id))
                {
                    v.Add(Dialogo.Texto("Solo se presta o se recluta tropa en el campamento donde resides: puedes residir aquí en la taberna.", "dialogo-texto"));
                    return;
                }
                var ahora = c.Sesion.AhoraMundo;
                var poblacion = Math.Floor(Instante.Valor(camp.Poblacion, ahora) / 1000);
                var tope = camp.Poblacion != null && camp.Poblacion.HasMaximo ? Math.Floor(camp.Poblacion.Maximo / 1000.0) : 0;
                var columna = ColumnaDelHeroe(h);
                if (reclutarConCarro && columna == null) reclutarConCarro = false;
                var oroAlmacen = (int)Math.Floor(Milesimas(h.AlmacenPersonal, "oro"));
                var oroCarro = (int)Math.Floor(Milesimas(columna?.Suministro, "oro"));
                var oro = reclutarConCarro ? oroCarro : oroAlmacen;
                var datos = new VisualElement();
                Fichas(datos, ("Reclutas del campamento", $"{poblacion}/{tope}"), ("Tu oro", $"{oro} ({(reclutarConCarro ? "carro" : "almacén")})"));
                v.Add(datos);
                if (columna != null)
                {
                    var fila = Dialogo.Caja("fila-envuelta");
                    fila.Add(Dialogo.Texto("Pagar con", "aviso-texto"));
                    var a = Dialogo.Boton($"Mi almacén ({oroAlmacen} de oro)", "boton-secundario", () => { reclutarConCarro = false; repintar(); });
                    var b = Dialogo.Boton($"El carro ({oroCarro} de oro; a la puerta)", "boton-secundario", () => { reclutarConCarro = true; repintar(); });
                    a.EnableInClassList("clase--elegida", !reclutarConCarro);
                    b.EnableInClassList("clase--elegida", reclutarConCarro);
                    fila.Add(a);
                    fila.Add(b);
                    v.Add(fila);
                }
                var mias = EscuadrasDelHeroe(h);
                var tropas = c.Datos.TropasReclutables?.OfType<JObject>().ToList() ?? new List<JObject>();

                // El carril del campamento: lo que se presta gratis (la leva comunal; no gana experiencia y se retira si dejas de residir aquí).
                v.Add(Dialogo.Texto("TE LA PRESTAN", "kicker"));
                v.Add(Dialogo.Texto("Gratis, para aprender a usar tropa antes de tener la tuya. No gana experiencia y se retira si dejas de residir en este campamento.", "campo-ayuda"));
                foreach (var t in tropas.Where(x => (string)x["tecnologia"] == "leva_comunal"))
                {
                    var id = (string)t["id"];
                    var unidades = (int?)t["unidadesPorDefecto"] ?? 0;
                    var prestada = mias.FirstOrDefault(s => s.TropaId == id && s.HasPrestadaPorCampamentoId);
                    var fila = Dialogo.Caja("salida-fila");
                    fila.name = "tropa-" + id;
                    fila.Add(Dialogo.Texto($"{(string)t["nombre"]} · {(prestada != null ? $"prestada: {prestada.Cantidad} hombres, {DondeEsta(prestada)}" : $"{unidades} hombres")}", "aviso-texto"));
                    if (prestada == null)
                    {
                        Button pedir = null;
                        pedir = Dialogo.Boton("Pedir prestada", "boton-secundario", async () =>
                        {
                            var o = new OrdenPedirPrestamo();
                            o.TropaIds.Add(id);
                            await OrdenDelPanel(new Orden { PedirPrestamo = o }, error, repintar, pedir);
                        });
                        pedir.name = "pedir-" + id;
                        fila.Add(pedir);
                    }
                    v.Add(fila);
                }

                // El edificio militar del campamento: lo que ofrece, con su precio (con el recargo, la reputación y el descuento ya aplicados).
                var militar = camp.Edificios.FirstOrDefault(e => ZonaDePieza(e) == "militar");
                v.Add(Dialogo.Texto((militar != null ? Pieza(militar) : "Edificio militar").ToUpperInvariant(), "kicker"));
                if (camp.Reclutables.Count == 0) v.Add(Dialogo.Texto("El campamento aún no ofrece tropa aquí.", "campo-ayuda"));
                foreach (var o in camp.Reclutables)
                {
                    var t = tropas.FirstOrDefault(x => (string)x["id"] == o.TropaId);
                    var nombre = t != null ? (string)t["nombre"] : o.TropaId;
                    var unidades = (int)o.Unidades;
                    var mia = mias.FirstOrDefault(s => s.TropaId == o.TropaId && !s.HasPrestadaPorCampamentoId);
                    var fila = Dialogo.Caja("salida-fila");
                    fila.name = "tropa-" + o.TropaId;
                    var porSoldado = o.PrecioPorSoldado / 1000.0;
                    var coste = (int)Math.Ceiling(o.PrecioEscuadra / 1000.0);
                    string estado = mia != null ? $"tu escuadra {mia.Cantidad}/{unidades}"
                        : !o.Desbloqueada ? "el campamento aún no la ofrece"
                        : $"{porSoldado:0.#} de oro por soldado";
                    fila.Add(Dialogo.Texto($"{nombre} · {estado}", "aviso-texto"));
                    if (mia == null && o.Desbloqueada)
                    {
                        var motivo = poblacion < unidades ? $"Faltan reclutas: hay {poblacion} y hacen falta {unidades}."
                            : coste > oro ? $"No te llega el oro: {coste} y tienes {oro} en {(reclutarConCarro ? "el carro" : "tu almacén")}." : null;
                        Button reclutar = null;
                        var id = o.TropaId;
                        reclutar = Dialogo.Boton($"Reclutar {unidades} · {coste} de oro", "boton-primario", async () =>
                        {
                            await OrdenDelPanel(new Orden { ReclutarEnCampamento = new OrdenReclutarEnCampamento { TropaId = id, PagarCon = reclutarConCarro ? "carro" : "almacenPersonal" } },
                                error, repintar, reclutar);
                        });
                        reclutar.name = "reclutar-" + id;
                        reclutar.style.marginTop = 0;
                        reclutar.SetEnabled(motivo == null);
                        if (motivo != null) { var m = Dialogo.Texto(motivo, "campo-ayuda"); m.name = "motivo-" + id; fila.Add(m); }
                        fila.Add(reclutar);
                    }
                    v.Add(fila);
                }
            });
        }
    }
}

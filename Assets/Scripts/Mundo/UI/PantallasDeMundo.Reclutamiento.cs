// La tropa del campamento (paso 6 del bloque 4, tanda 3): el árbol de reclutamiento de BronzeAgeClient (`arbolReclutamiento.ts`, `arbolTablero.ts`
// y `lugarCampamento` de `pantallaCampamento.ts`) calcado en UI Toolkit, con la tarjeta de tropa de Conquest en cada nudo. Sustituye a la lista del
// edificio militar. Víctor (2026-11-15): el árbol se ve entero, cada tropa con su estado y lo que le falta; solo se recluta lo que ofrece el
// campamento; al pulsar, la ficha de la tropa en el panel derecho de Tropas de la Base (Características | Atributos), con «Reclutar» o «Pedir
// prestada». Sin Facción ni tecnologías (paso 8): las líneas salen del catálogo, no de lo adoptado.
//   - Carriles: Campamento (las de leva comunal que presta), Barracón, Galería de tiro, Caballerizas. Columnas: las Eras.
//   - Líneas: B va tras A cuando B necesita la tecnología que habilita a A (continua, a rayas o punteada). Las de otro carril, solo al pasar el
//     ratón o con «Ver todas las conexiones». Al pasar el ratón se ilumina su camino y sale su ficha corta.
//   - El tablero se arrastra con el ratón; las cabeceras de carril y de Era se quedan a la vista.
using System;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    public sealed partial class PantallasDeMundo
    {
        // Medidas del tablero (`--ta-carril`, `--ta-hueco`, `--ta-canal` del prototipo; la carta es la tarjeta de Conquest).
        const float ArbolCarril = 150, ArbolHueco = 46, ArbolCanal = 20, ArbolCabecera = 104, ArbolTarjeta = 84, ArbolMargen = 7, ArbolEstado = 30;
        static float ArbolAltoTarjeta => 236.75f * ArbolTarjeta / 150f;
        static float ArbolNodo => ArbolTarjeta + 8;
        static float ArbolAltoNodo => 4 + ArbolAltoTarjeta + 3 + ArbolEstado + 4;
        static float ArbolFila => ArbolAltoNodo + 2 * ArbolMargen;
        static readonly string[] Romano = { "", "I", "II", "III", "IV", "V", "VI" };

        ArbolDeTropas arbolDeTropas;
        string huellaDelArbol;
        string tropaDelArbol;
        bool arbolTodas;
        string subpestanaRecluta = "caracteristicas";
        Vector2 scrollDelArbol;

        /// <summary>El árbol del balance (se hace una vez por huella de reglas).</summary>
        ArbolDeTropas ArbolDelBalance()
        {
            if (arbolDeTropas != null && huellaDelArbol == c.Datos.Huella) return arbolDeTropas;
            JToken Bloque(string n) => (c.Datos.Raiz["documentos"] as JObject)?.Properties().Select(p => p.Value[n]).FirstOrDefault(x => x != null);
            if (!(Bloque("TECNOLOGIAS") is JObject tecs) || !(Bloque("ERAS") is JObject eras) || !(Bloque("EDIFICIO_CATALOGO") is JObject cat) || !(Bloque("TROPAS_RECLUTABLES") is JArray tropas)) return null;
            huellaDelArbol = c.Datos.Huella;
            return arbolDeTropas = new ArbolDeTropas(tecs, eras, cat, tropas);
        }

        /// <summary>Lo que el lugar hace con cada tropa: el campamento (`lugarCampamento`) o, con `Plaza`, una plaza de tu Facción (`lugarPlaza`, paso 7).</summary>
        sealed class LugarCampamento
        {
            public CampamentoMercenarios Camp;
            /// <summary>En una plaza: ella, las tecnologías de tu Facción (adoptadas y aparecidas, null sin llegar) y el nivel de sus edificios.</summary>
            public Asentamiento Plaza;
            public HashSet<string> Adoptadas, Aparecidas;
            public Dictionary<string, int> Niveles;
            public Dictionary<string, Escuadron> Prestadas, Propias;
            public Dictionary<string, TropaDeCampamento> Oferta;
            public HashSet<string> Presta;
            public int? EraMundo;
            public ArbolDeTropas A;

            public string Estado(string id)
            {
                var n = A.Nodos[id];
                if (Plaza != null)
                {
                    // Como el motor (`estadoTropa`): su Era, su tecnología adoptada (o aparecida, que falta adoptar) y su edificio al nivel en la plaza.
                    if (EraMundo != null && n.Era > EraMundo) return "futura";
                    if (Adoptadas == null || !Adoptadas.Contains(n.T.Tecnologia)) return Aparecidas != null && Aparecidas.Contains(n.T.Tecnologia) ? "adoptable" : "bloqueada";
                    var nivel = n.T.Edificio == "centroUrbano" ? 99 : Niveles.TryGetValue(n.T.Edificio, out var x) ? x : 0;
                    if (nivel < n.T.NivelRequerido) return "edificio";
                    return Propias.ContainsKey(id) ? "tuya" : "lista";
                }
                if (n.Carril == "centroUrbano") return Prestadas.ContainsKey(id) ? "tuya" : "lista";
                if (!Oferta.TryGetValue(id, out var o)) return EraMundo != null && n.Era > EraMundo ? "futura" : "edificio";
                if (!o.Desbloqueada) return "bloqueada";
                return Propias.ContainsKey(id) ? "tuya" : "lista";
            }
        }

        LugarCampamento LugarDe(CampamentoMercenarios camp, Heroe h, ArbolDeTropas a)
        {
            var mias = EscuadrasDelHeroe(h);
            var eraMundo = c.Sesion.Almacen.Buscar(TipoEntidad.EstadoTecnologia, "tecnologia")?.EstadoTecnologia?.Era;
            return new LugarCampamento
            {
                Camp = camp, A = a,
                Prestadas = mias.Where(s => s.HasPrestadaPorCampamentoId).GroupBy(s => s.TropaId).ToDictionary(g => g.Key, g => g.First()),
                Propias = mias.Where(s => !s.HasPrestadaPorCampamentoId).GroupBy(s => s.TropaId).ToDictionary(g => g.Key, g => g.First()),
                Oferta = camp.Reclutables.GroupBy(o => o.TropaId).ToDictionary(g => g.Key, g => g.First()),
                Presta = new HashSet<string>(a.Tropas.Where(t => t.Tecnologia == "leva_comunal").Select(t => t.Id)),
                EraMundo = string.IsNullOrEmpty(eraMundo) ? (int?)null : a.EraDe(eraMundo),
            };
        }

        static string NombreDeCarril(string id) => id == "centroUrbano" ? "Campamento" : Pieza(id);

        /// <summary>La franja de estado de la carta (`carta` de `lugarCampamento`).</summary>
        string EstadoEnCarta(LugarCampamento L, string id, string est)
        {
            if (L.Plaza != null) return EstadoEnCartaDePlaza(L, id, est);
            var t = L.A.Nodos[id].T;
            if (t.Edificio == "centroUrbano") return est == "tuya" ? $"Prestada: {L.Prestadas[id].Cantidad} hombres" : "Te la prestan gratis";
            switch (est)
            {
                case "lista": return $"Lista · {L.Oferta[id].PrecioPorSoldado / 1000.0:0.#} de oro por soldado";
                case "tuya": return $"Tu escuadra {L.Propias[id].Cantidad}/{t.Unidades}";
                case "edificio": return L.Camp.Edificios.Contains(t.Edificio) ? $"Falta {Pieza(t.Edificio)} {t.NivelRequerido}" : $"Falta {Pieza(t.Edificio)}";
                case "futura": return $"Bloqueada: la Era {Romano[L.A.Nodos[id].Era]} aún no empieza";
                default: return "El campamento aún no la ofrece";
            }
        }

        /// <summary>La caja de estado de la ficha corta (`caja` de `lugarCampamento`).</summary>
        string EstadoEnCaja(LugarCampamento L, string id, string est, Heroe h)
        {
            if (L.Plaza != null) return EstadoEnCajaDePlaza(L, id, est);
            var t = L.A.Nodos[id].T;
            if (t.Edificio == "centroUrbano")
                return est == "tuya"
                    ? $"Te la han prestado ({L.Prestadas[id].Cantidad} hombres, {DondeEsta(L.Prestadas[id])}). Se retira si dejas de residir aquí."
                    : "Te la prestan gratis para aprender a usar tropa antes de tener la tuya. No gana experiencia y se retira si dejas de residir en este campamento.";
            switch (est)
            {
                case "lista": return $"Lista para reclutar en este campamento por {L.Oferta[id].PrecioPorSoldado / 1000.0:0.#} de oro por soldado, con el equipo dentro.";
                case "tuya": return $"Ya tienes su escuadra ({L.Propias[id].Cantidad} de {t.Unidades} hombres). Un héroe tiene una escuadra por tropa.";
                case "edificio":
                    return L.Camp.Edificios.Contains(t.Edificio)
                        ? $"El campamento tiene {Pieza(t.Edificio)}, pero esta tropa pide {Pieza(t.Edificio)} {t.NivelRequerido}."
                        : $"El campamento no tiene {Pieza(t.Edificio)}.";
                case "futura": return $"Bloqueada: la Era {Romano[L.A.Nodos[id].Era]} aún no ha empezado en el mundo.";
                default: return "El campamento aún no la ofrece.";
            }
        }

        static readonly Dictionary<string, string> LeyendaDeEstado = new Dictionary<string, string>
        {
            ["lista"] = "se recluta o te la prestan aquí", ["tuya"] = "ya la tienes", ["edificio"] = "el campamento no tiene el edificio a su nivel",
            ["bloqueada"] = "el campamento aún no la ofrece", ["futura"] = "bloqueada: su Era aún no empieza",
        };

        // ─────────────── la ventana ───────────────

        /// <summary>La tropa del campamento: a pantalla completa, el árbol a la izquierda y la ficha de la tropa elegida a la derecha.</summary>
        void PanelTropa(string campId, string plazaId = null)
        {
            var interior = SelectorDePersonaje.Interior;
            // (en la plaza se abre desde el riel o desde la ficha de un edificio militar, que ya tienen el ratón suelto)
            if (interior == null || interior.PanelAbierto && plazaId == null) return;
            var velo = Dialogo.Caja("dialogo-velo recl-velo");
            velo.name = "panel-tropa";
            velo.focusable = true;
            var ventana = Dialogo.Caja("base-ventana recl-ventana");
            velo.Add(ventana);
            var tip = Dialogo.Caja("recl-tip");
            tip.name = "recl-tip";
            tip.pickingMode = PickingMode.Ignore;
            tip.style.display = DisplayStyle.None;
            velo.Add(tip);
            string firma = null;
            void Cerrar()
            {
                velo.RemoveFromHierarchy();
                interior.PanelAbierto = false;
            }
            velo.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Escape) Cerrar(); }, TrickleDown.TrickleDown);
            string Firma()
            {
                var h = HeroeDelCampamento();
                var camp = plazaId != null ? null : CampamentoActual(campId);
                var plaza = plazaId != null ? PlazaActual(plazaId) : null;
                var escuadras = h == null ? "" : string.Join(",", h.EscuadronesIds.Select(id => c.Sesion.Almacen.Buscar(TipoEntidad.Escuadron, id)?.Escuadron?.Version ?? 0));
                return $"{h?.Version}|{camp?.Version}|{plaza?.Version}|{plaza?.VersionInterior}|{(plaza != null ? TecnologiasDeMiFaccion()?.Count : 0)}|{escuadras}|{ColumnaDelHeroe(h)?.Version}|{tropaDelArbol}|{arbolTodas}|{subpestanaRecluta}|{pagarConBase}";
            }
            void Repintar()
            {
                if (velo.panel == null) return;
                var foco = velo.panel.focusController?.focusedElement;
                firma = Firma();
                var marco = ventana.Q<ScrollView>("recl-marco");
                if (marco != null) scrollDelArbol = marco.scrollOffset;
                ventana.Clear();
                tip.style.display = DisplayStyle.None;
                if (plazaId != null) PintarTropaDeLaPlaza(ventana, tip, plazaId, Cerrar, Repintar);
                else PintarTropaDelCampamento(ventana, tip, campId, Cerrar, Repintar);
            }
            interior.PanelAbierto = true;
            var cartel = pantalla.Q<Label>("cartel");
            if (cartel != null) Ver(cartel, false);
            capaEncima.Add(velo);
            Repintar();
            RepintarCada(velo, 250, () => { if (Firma() != firma) Repintar(); });
            velo.Focus();
        }

        void PintarTropaDelCampamento(VisualElement v, VisualElement tip, string campId, Action cerrar, Action repintar)
        {
            var camp = CampamentoActual(campId);
            var h = HeroeDelCampamento();
            var a = ArbolDelBalance();
            var error = Dialogo.Texto("", "error oculto");
            error.name = "error";
            void Error(string t) => Poner(error, t);

            // La cabecera: el título con el icono del edificio militar, los reclutas del campamento y «Cerrar».
            var cab = Dialogo.Caja("recl-cabecera");
            var militar = camp?.Edificios.FirstOrDefault(e => ZonaDePieza(e) == "militar");
            var titulo = IconosDelMundo.ConIcono("edificio", militar ?? "barracon", Dialogo.Texto("TROPA", "dialogo-titulo"), 26);
            titulo.AddToClassList("titulo-con-icono");
            cab.Add(titulo);
            var chips = Dialogo.Caja("fila-envuelta recl-chips");
            chips.name = "chips";
            cab.Add(chips);
            cab.Add(Dialogo.Caja("recl-hueco"));
            var cerrarBoton = Dialogo.Boton("Cerrar", "boton-secundario", cerrar);
            cerrarBoton.name = "cerrar";
            cab.Add(cerrarBoton);
            v.Add(cab);
            if (camp == null || h == null || a == null) { v.Add(error); return; }
            if (!camp.ResidentesIds.Contains(h.Id))
            {
                v.Add(Dialogo.Texto("Solo se presta o se recluta tropa en el campamento donde resides: puedes residir aquí en la taberna.", "dialogo-texto"));
                return;
            }
            var poblacion = Math.Floor(Instante.Valor(camp.Poblacion, c.Sesion.AhoraMundo) / 1000);
            var tope = camp.Poblacion != null && camp.Poblacion.HasMaximo ? Math.Floor(camp.Poblacion.Maximo / 1000.0) : 0;
            var reclutas = IconosDelMundo.ConIcono("glifo", "reclutas", Dialogo.Texto($"{poblacion}/{tope} reclutas", "chip-texto"), 16);
            reclutas.AddToClassList("chip");
            reclutas.tooltip = "Reclutas del campamento";
            chips.Add(reclutas);

            ArbolDelLugar(v, tip, a, LugarDe(camp, h, a), h, error, Error, repintar);
        }

        /// <summary>El árbol de un lugar con su ficha: los mandos, la leyenda, el tablero y la ficha de la tropa elegida.</summary>
        void ArbolDelLugar(VisualElement v, VisualElement tip, ArbolDeTropas a, LugarCampamento L, Heroe h, Label error, Action<string> Error, Action repintar)
        {
            // En la plaza, el árbol entero; en el campamento, del Campamento solo las que presta.
            var ocultas = L.Plaza != null ? new HashSet<string>() : new HashSet<string>(a.Nodos.Values.Where(n => n.Carril == "centroUrbano" && !L.Presta.Contains(n.T.Id)).Select(n => n.T.Id));
            var eraVisible = a.Eras.Count > 0 ? a.Eras.Max(e => e.Orden) : 1;
            var lay = a.Disponer(eraVisible, null, ocultas);
            var visibles = a.OrdenNodos.Where(id => a.Nodos[id].Col != null).ToList();
            if (tropaDelArbol == null || !visibles.Contains(tropaDelArbol)) tropaDelArbol = visibles.FirstOrDefault(id => a.Nodos[id].Carril == "centroUrbano") ?? visibles.FirstOrDefault();

            // Los mandos y la leyenda (`ta-mandos`, `ta-leyenda`).
            var mandos = Dialogo.Caja("recl-mandos");
            var todas = Dialogo.Boton("", "recl-boton-todas", () => { arbolTodas = !arbolTodas; repintar(); });
            todas.name = "ver-todas";
            todas.EnableInClassList("activo", arbolTodas);
            todas.Add(Dialogo.Texto(arbolTodas ? "✓" : "", "recl-caja"));
            todas.Add(Dialogo.Texto("Ver todas las conexiones", "recl-boton-texto"));
            mandos.Add(todas);
            mandos.Add(Dialogo.Texto(L.Plaza != null ? "Pasa el ratón por una tropa para ver su camino; haz clic para abrir su ficha y reclutarla. Arrastra el tablero para moverte."
                : "Pasa el ratón por una tropa para ver qué pide; haz clic para abrir su ficha y pedirla prestada o reclutarla. Arrastra el tablero para moverte.", "recl-ayuda"));
            v.Add(mandos);
            var estados = visibles.ToDictionary(id => id, L.Estado);
            var leyenda = Dialogo.Caja("recl-leyenda");
            leyenda.name = "leyenda";
            var tipos = new HashSet<string>(visibles.SelectMany(id => a.Nodos[id].Preds.Where(p => a.Nodos[p.De].Col != null).Select(p => p.Tipo)));
            if (tipos.Count > 0) leyenda.Add(Dialogo.Texto("B va tras A cuando B necesita la tecnología que habilita a A:", "recl-leyenda-texto"));
            foreach (var (tp, texto) in new[] { ("hito", "su tecnología la pide en el hito"), ("implicita", "la pide su edificio o su equipo"), ("logro", "la pide el logro de su tecnología (dependencia oculta)") })
                if (tipos.Contains(tp)) leyenda.Add(MuestraDeLinea(tp, texto));
            foreach (var est in new[] { "lista", "tuya", "edificio", "adoptable", "bloqueada", "futura" })
                if (estados.ContainsValue(est))
                {
                    var f = Dialogo.Caja("recl-leyenda-item");
                    f.Add(Dialogo.Caja("recl-muestra " + (est == "adoptable" ? "bloqueada adoptable" : est)));
                    f.Add(Dialogo.Texto(L.Plaza != null ? LeyendaDePlaza[est] : LeyendaDeEstado[est], "recl-leyenda-texto"));
                    leyenda.Add(f);
                }
            v.Add(leyenda);

            var cuerpo = Dialogo.Caja("recl-cuerpo");
            v.Add(cuerpo);
            cuerpo.Add(Tablero(a, L, lay, visibles, estados, tip, repintar));
            var datos = Dialogo.Caja("tropas-datos recl-ficha");
            datos.name = "recl-ficha";
            cuerpo.Add(datos);
            if (tropaDelArbol != null) FichaDeRecluta(datos, a, L, tropaDelArbol, estados[tropaDelArbol], h, Error, repintar);
            datos.Add(error);
        }

        static VisualElement MuestraDeLinea(string tipo, string texto)
        {
            var f = Dialogo.Caja("recl-leyenda-item");
            var m = new VisualElement { pickingMode = PickingMode.Ignore };
            m.style.width = 26;
            m.style.height = 8;
            m.generateVisualContent += ctx => Trazo(ctx.painter2D, new List<Vector2> { new Vector2(1, 4), new Vector2(25, 4) }, tipo, Bronce, 1, false);
            f.Add(m);
            f.Add(Dialogo.Texto(texto, "recl-leyenda-texto"));
            return f;
        }

        // ─────────────── el tablero ───────────────

        static readonly Color Bronce = new Color32(0xa9, 0x79, 0x3f, 0xff), OroClaroLinea = new Color32(0xf1, 0xdc, 0xae, 0xff);

        sealed class Linea { public string De, A, Tipo; public bool Cruce; public List<Vector2> P; }

        VisualElement Tablero(ArbolDeTropas a, LugarCampamento L, ArbolDeTropas.Disposicion lay, List<string> visibles, Dictionary<string, string> estados,
            VisualElement tip, Action repintar)
        {
            // Una Era de una sola columna ocupa dos (`MIN_COLS` de arbolTablero.ts) y las de después se corren.
            var corrimiento = new Dictionary<int, int>();
            var eras = new List<(ArbolDeTropas.Era e, int baseCol, int cols)>();
            var extra = 0;
            foreach (var e in lay.ErasVis)
            {
                var b = lay.BaseEra[e.Orden];
                var n0 = lay.ColsEra[e.Orden];
                for (int k = b; k < b + n0; k++) corrimiento[k] = extra;
                eras.Add((e, b + extra, Math.Max(2, n0)));
                extra += Math.Max(2, n0) - n0;
            }
            var NCOL = lay.NCOL + extra;
            int Col(int col) => col + (corrimiento.TryGetValue(col, out var x) ? x : extra);
            // La pista i (0, la de los carriles) y el canal a la derecha de la columna c.
            float XPista(int i) => i == 0 ? 0 : ArbolCarril + ArbolHueco + (i - 1) * (ArbolNodo + ArbolHueco);
            float XHueco(int col) => XPista(col + 1) + ArbolNodo + ArbolHueco / 2;
            var ancho = XPista(NCOL) + ArbolNodo + ArbolHueco / 2;
            var carriles = a.Carriles.Where(cr => cr.Tropas.Any(visibles.Contains)).ToList();
            var bandas = new Dictionary<string, (float y, float h)>();
            var y0 = ArbolCabecera;
            foreach (var cr in carriles)
            {
                y0 += ArbolCanal;
                var alto = Math.Max(1, lay.Filas[cr.Id]) * ArbolFila;
                bandas[cr.Id] = (y0, alto);
                y0 += alto;
            }
            var altoTotal = y0 + ArbolCanal;

            var marco = new ScrollView(ScrollViewMode.VerticalAndHorizontal) { name = "recl-marco" };
            marco.AddToClassList("recl-marco");
            marco.horizontalScrollerVisibility = ScrollerVisibility.Auto;
            marco.verticalScrollerVisibility = ScrollerVisibility.Auto;
            var t = new VisualElement { name = "recl-tablero" };
            t.AddToClassList("recl-tablero");
            t.style.width = ancho;
            t.style.height = altoTotal;
            marco.Add(t);
            VisualElement Abs(string clases, float x, float y, float w, float hh)
            {
                var e = Dialogo.Caja(clases);
                e.style.position = Position.Absolute;
                e.style.left = x; e.style.top = y; e.style.width = w; e.style.height = hh;
                return e;
            }

            // Fondos de las Eras y de los carriles.
            for (int i = 0; i < eras.Count; i++)
            {
                var (e, b, cols) = eras[i];
                var f = Abs("recl-era-fondo" + (i % 2 == 1 ? " par" : "") + (i > 0 ? " raya" : "") + (L.EraMundo != null && e.Orden > L.EraMundo ? " futura" : ""),
                    XPista(1 + b) - ArbolHueco / 2, 0, cols * (ArbolNodo + ArbolHueco), altoTotal);
                f.pickingMode = PickingMode.Ignore;
                t.Add(f);
            }
            for (int i = 0; i < carriles.Count; i++)
            {
                var (y, hh) = bandas[carriles[i].Id];
                var f = Abs("recl-carril-fondo" + (i % 2 == 1 ? " par" : ""), 0, y, ancho, hh);
                f.pickingMode = PickingMode.Ignore;
                t.Add(f);
            }

            // Las cartas: la tarjeta de Conquest, la medalla y la franja de estado.
            var cajas = new Dictionary<string, (float x, float y)>();
            var cartas = new Dictionary<string, VisualElement>();
            foreach (var id in visibles)
            {
                var n = a.Nodos[id];
                var x = XPista(1 + Col(n.Col.Value));
                var y = bandas[n.Carril].y + n.Fila.Value * ArbolFila + ArbolMargen;
                cajas[id] = (x, y);
            }
            // Las líneas (`trazar`): en ángulo recto, por los canales entre columnas y las franjas entre carriles.
            var lineas = new List<Linea>();
            var canales = new Dictionary<int, List<string>>();
            float Canal(int col, string clave)
            {
                if (!canales.TryGetValue(col, out var m)) canales[col] = m = new List<string>();
                if (!m.Contains(clave)) m.Add(clave);
                var k = m.IndexOf(clave);
                return XHueco(col) + (k % 2 == 1 ? 1 : -1) * (float)Math.Ceiling(k / 2.0) * 7;
            }
            var usoCanal = new Dictionary<string, int>();
            float YCanal(string carril, bool arriba)
            {
                var (by, bh) = bandas[carril];
                var bas = arriba ? by - ArbolCanal / 2 - 1 : by + bh + ArbolCanal / 2 + 1;
                var clave = carril + (arriba ? "↑" : "↓");
                var k = usoCanal.TryGetValue(clave, out var u) ? u : 0;
                usoCanal[clave] = k + 1;
                return bas + (k % 2 == 1 ? 1 : -1) * (float)Math.Ceiling(k / 2.0) * 5;
            }
            bool LibreEnFila(string carril, int fila, int c0, int c1)
            {
                var occ = lay.Ocupa[carril];
                for (int k = c0; k <= c1; k++) if (fila < occ.Count && occ[fila] != null && occ[fila].Contains(k)) return false;
                return true;
            }
            var ordenCarriles = a.Carriles.Select(cr => cr.Id).ToList();
            var ancla = 4 + ArbolAltoTarjeta / 2;
            foreach (var id in a.OrdenNodos)
            {
                if (!cajas.ContainsKey(id)) continue;
                var n = a.Nodos[id];
                foreach (var p in n.Preds)
                {
                    if (!cajas.ContainsKey(p.De)) continue;
                    var an = a.Nodos[p.De];
                    var (ax, ay) = cajas[p.De];
                    var (bx, by) = cajas[id];
                    float x1 = ax + ArbolNodo, y1 = ay + ancla, x2 = bx - 1, y2 = by + ancla;
                    var ac = Col(an.Col.Value);
                    var nc = Col(n.Col.Value);
                    var xa = Canal(ac, "S" + p.De);
                    var mismo = an.Carril == n.Carril;
                    var P = new List<Vector2> { new Vector2(x1, y1) };
                    void H(float xx) => P.Add(new Vector2(xx, P[P.Count - 1].y));
                    void V(float yy) => P.Add(new Vector2(P[P.Count - 1].x, yy));
                    if (mismo && an.Fila == n.Fila && LibreEnFila(n.Carril, n.Fila.Value, an.Col.Value + 1, n.Col.Value - 1)) H(x2);
                    else if (nc == ac + 1) { H(xa); V(y2); H(x2); }
                    else if (mismo && LibreEnFila(n.Carril, n.Fila.Value, an.Col.Value + 1, n.Col.Value - 1)) { H(xa); V(y2); H(x2); }
                    else if (mismo && LibreEnFila(n.Carril, an.Fila.Value, an.Col.Value + 1, n.Col.Value - 1)) { H(Canal(nc - 1, "T" + id)); V(y2); H(x2); }
                    else
                    {
                        var yg = mismo ? YCanal(n.Carril, false) : YCanal(n.Carril, ordenCarriles.IndexOf(n.Carril) > ordenCarriles.IndexOf(an.Carril));
                        H(xa); V(yg); H(Canal(nc - 1, "T" + id)); V(y2); H(x2);
                    }
                    lineas.Add(new Linea { De = p.De, A = id, Tipo = p.Tipo, Cruce = !mismo, P = P });
                }
            }
            string resaltado = null;
            HashSet<string> luz = new HashSet<string>(), baja = new HashSet<string>();
            var capaLineas = new VisualElement { name = "recl-lineas", pickingMode = PickingMode.Ignore };
            capaLineas.style.position = Position.Absolute;
            capaLineas.style.left = 0; capaLineas.style.top = 0; capaLineas.style.width = ancho; capaLineas.style.height = altoTotal;
            capaLineas.generateVisualContent += ctx =>
            {
                foreach (var l in lineas)
                {
                    float alfa;
                    var color = Bronce;
                    if (resaltado != null)
                    {
                        if (luz.Contains(l.De) && luz.Contains(l.A)) { alfa = 1; color = OroClaroLinea; }
                        else if (baja.Contains(l.A) && (baja.Contains(l.De) || l.De == resaltado)) alfa = 0.5f;
                        else alfa = 0.1f;
                    }
                    else alfa = l.Cruce ? (arbolTodas ? 0.5f : 0) : 0.85f;
                    if (alfa > 0) Trazo(ctx.painter2D, l.P, l.Tipo, color, alfa, true);
                }
            };
            t.Add(capaLineas);

            void Resaltar(string id)
            {
                resaltado = id;
                luz = id == null ? new HashSet<string>() : a.Antes(id);
                if (id != null) luz.Add(id);
                baja = id == null ? new HashSet<string>() : a.Despues(id);
                t.EnableInClassList("activo", id != null);
                foreach (var (cid, carta) in cartas)
                {
                    carta.EnableInClassList("luz", luz.Contains(cid));
                    carta.EnableInClassList("baja", !luz.Contains(cid) && baja.Contains(cid));
                    carta.EnableInClassList("foco", cid == id);
                }
                capaLineas.MarkDirtyRepaint();
            }

            foreach (var id in visibles)
            {
                var n = a.Nodos[id];
                var est = estados[id];
                var (x, y) = cajas[id];
                var carta = new Button { name = "nodo-" + id };
                carta.AddToClassList("recl-carta");
                carta.AddToClassList(est == "adoptable" ? "bloqueada" : est);
                carta.EnableInClassList("elegida", id == tropaDelArbol);
                carta.style.position = Position.Absolute;
                carta.style.left = x; carta.style.top = y; carta.style.width = ArbolNodo; carta.style.height = ArbolAltoNodo;
                var vivos = est == "tuya" ? (int?)(n.Carril == "centroUrbano" && L.Plaza == null ? L.Prestadas[id].Cantidad : L.Propias[id].Cantidad) : n.T.Unidades;
                var tarjeta = PiezasDeTropa.Tarjeta(id, LiderazgoDe(id), vivos, n.T.Unidades, null, ArbolTarjeta);
                if (tarjeta != null) { tarjeta.tooltip = null; tarjeta.pickingMode = PickingMode.Ignore; carta.Add(tarjeta); }
                else carta.Add(Dialogo.Texto(n.T.Nombre, "recl-carta-nombre"));
                // La medalla: ✓ si se recluta o se presta aquí; la bandera (⚑ en el prototipo) si ya la tienes.
                if (est == "lista" || est == "tuya")
                {
                    var m = est == "lista" ? (VisualElement)Dialogo.Texto("✓", "recl-medalla") : Dialogo.Caja("recl-medalla");
                    if (est == "tuya" && IconosDelMundo.Icono("simbolo", "bandera", 12, new Color32(0x20, 0x15, 0x09, 0xff)) is VisualElement bandera) m.Add(bandera);
                    m.pickingMode = PickingMode.Ignore;
                    carta.Add(m);
                }
                var franja = Dialogo.Texto(EstadoEnCarta(L, id, est), "recl-carta-estado");
                franja.name = "estado-" + id;
                franja.pickingMode = PickingMode.Ignore;
                carta.Add(franja);
                var tid = id;
                carta.RegisterCallback<PointerEnterEvent>(e => { Resaltar(tid); MostrarTip(tip, a, L, tid, estados[tid], e.position); });
                carta.RegisterCallback<PointerMoveEvent>(e => ColocarTip(tip, e.position));
                carta.RegisterCallback<PointerLeaveEvent>(_ => { Resaltar(null); tip.style.display = DisplayStyle.None; });
                carta.RegisterCallback<FocusInEvent>(_ => { Resaltar(tid); MostrarTip(tip, a, L, tid, estados[tid], carta.worldBound.max); });
                carta.RegisterCallback<FocusOutEvent>(_ => { Resaltar(null); tip.style.display = DisplayStyle.None; });
                carta.clicked += () => { tropaDelArbol = tid; repintar(); };
                cartas[id] = carta;
                t.Add(carta);
            }

            // Las cabeceras de Era y de carril, y la esquina: se quedan a la vista al desplazar (`position: sticky` del prototipo).
            var fijasArriba = new List<VisualElement>();
            var fijasIzquierda = new List<VisualElement>();
            var ultima = a.Eras.Count > 0 ? a.Eras.Max(e => e.Orden) : 1;
            foreach (var (e, b, cols) in eras)
            {
                var cabEra = Abs("recl-era-cab", XPista(1 + b), 0, cols * ArbolNodo + (cols - 1) * ArbolHueco, ArbolCabecera);
                cabEra.name = "era-" + e.Orden;
                var num = Dialogo.Texto(Romano[Math.Min(e.Orden, Romano.Length - 1)], "recl-era-num");
                cabEra.Add(num);
                var textos = Dialogo.Caja("recl-era-textos");
                textos.Add(Dialogo.Texto($"Era {Romano[Math.Min(e.Orden, Romano.Length - 1)]} · {e.Nombre}", "recl-era-nombre"));
                var cuantas = visibles.Count(id => a.Nodos[id].Era == e.Orden);
                var dura = e.Orden >= ultima ? "la última Era, sin plazo" : e.MinimoSemanas != null ? $"dura de {e.MinimoSemanas} a {e.PlazoSemanas} semanas" : $"dura hasta {e.PlazoSemanas} semanas";
                textos.Add(Dialogo.Texto($"{cuantas} tropa{(cuantas == 1 ? "" : "s")} · {dura}", "recl-era-sub"));
                if (L.EraMundo == e.Orden) textos.Add(Dialogo.Texto("ERA DEL MUNDO", "recl-era-marca actual"));
                else if (L.EraMundo != null && e.Orden > L.EraMundo) textos.Add(Dialogo.Texto("BLOQUEADA", "recl-era-marca futura"));
                cabEra.Add(textos);
                fijasArriba.Add(cabEra);
                t.Add(cabEra);
            }
            foreach (var cr in carriles)
            {
                var (y, hh) = bandas[cr.Id];
                var nombre = Abs("recl-carril-nombre", 0, y, ArbolCarril, hh);
                nombre.name = "carril-" + cr.Id;
                var icono = cr.Id == "centroUrbano" && L.Plaza == null ? IconosDelMundo.Icono("parte", "campamento", 30, IconosDelMundo.Bronce) : IconosDelMundo.Icono("edificio", cr.Id, 30, IconosDelMundo.Bronce);
                if (icono != null) nombre.Add(icono);
                nombre.Add(Dialogo.Texto((L.Plaza != null && cr.Id == "centroUrbano" ? "Centro Urbano" : NombreDeCarril(cr.Id)).ToUpperInvariant(), "recl-carril-texto"));
                var n = cr.Tropas.Count(visibles.Contains);
                nombre.Add(Dialogo.Texto($"{n} tropa{(n == 1 ? "" : "s")}", "recl-carril-sub"));
                fijasIzquierda.Add(nombre);
                t.Add(nombre);
            }
            var esquina = Abs("recl-esquina", 0, 0, ArbolCarril, ArbolCabecera);
            esquina.Add(Dialogo.Texto("SE RECLUTA EN · ERA", "recl-esquina-texto"));
            t.Add(esquina);
            void Fijar(Vector2 o)
            {
                foreach (var e in fijasArriba) e.style.translate = new Translate(0, o.y);
                foreach (var e in fijasIzquierda) e.style.translate = new Translate(o.x, 0);
                esquina.style.translate = new Translate(o.x, o.y);
            }
            marco.horizontalScroller.valueChanged += _ => { Fijar(marco.scrollOffset); scrollDelArbol = marco.scrollOffset; tip.style.display = DisplayStyle.None; };
            marco.verticalScroller.valueChanged += _ => { Fijar(marco.scrollOffset); scrollDelArbol = marco.scrollOffset; tip.style.display = DisplayStyle.None; };
            var inicial = scrollDelArbol;
            marco.schedule.Execute(() => { marco.scrollOffset = inicial; Fijar(marco.scrollOffset); });

            // Arrastrar el tablero con el ratón, como el mapa de un juego.
            Vector2? desde = null;
            Vector2 scroll0 = default;
            t.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0 || (e.target as VisualElement)?.GetFirstAncestorOfType<Button>() != null || e.target is Button) return;
                desde = e.position;
                scroll0 = marco.scrollOffset;
                t.CapturePointer(e.pointerId);
                t.AddToClassList("arrastrando");
            });
            t.RegisterCallback<PointerMoveEvent>(e => { if (desde != null) marco.scrollOffset = scroll0 - ((Vector2)e.position - desde.Value); });
            void Soltar(IPointerEvent e) { desde = null; if (t.HasPointerCapture(e.pointerId)) t.ReleasePointer(e.pointerId); t.RemoveFromClassList("arrastrando"); }
            t.RegisterCallback<PointerUpEvent>(e => Soltar(e));
            t.RegisterCallback<PointerCaptureOutEvent>(_ => { desde = null; t.RemoveFromClassList("arrastrando"); });
            return marco;
        }

        /// <summary>Una línea del árbol: continua (hito), a rayas (implícita) o punteada (logro), con su punta al final.</summary>
        static void Trazo(Painter2D p, List<Vector2> puntos, string tipo, Color color, float alfa, bool punta)
        {
            color.a = alfa;
            p.strokeColor = color;
            p.lineWidth = tipo == "logro" ? 2.6f : 2;
            p.lineJoin = LineJoin.Miter;
            p.lineCap = tipo == "logro" ? LineCap.Round : LineCap.Butt;
            if (tipo == "hito")
            {
                p.BeginPath();
                p.MoveTo(puntos[0]);
                for (int i = 1; i < puntos.Count; i++) p.LineTo(puntos[i]);
                p.Stroke();
            }
            else
            {
                // `stroke-dasharray`: 6 4 a rayas, 1 5 punteada.
                float trazo = tipo == "logro" ? 0.01f : 6, hueco = tipo == "logro" ? 6 : 4, resto = 0;
                bool pinta = true;
                for (int i = 1; i < puntos.Count; i++)
                {
                    var a = puntos[i - 1]; var b = puntos[i];
                    var largo = Vector2.Distance(a, b);
                    if (largo <= 0) continue;
                    var dir = (b - a) / largo;
                    float hecho = 0;
                    while (hecho < largo)
                    {
                        var tramo = (pinta ? trazo : hueco) - resto;
                        var paso = Mathf.Min(tramo, largo - hecho);
                        if (pinta) { p.BeginPath(); p.MoveTo(a + dir * hecho); p.LineTo(a + dir * (hecho + paso)); p.Stroke(); }
                        hecho += paso;
                        resto += paso;
                        if (resto >= (pinta ? trazo : hueco) - 1e-4f) { resto = 0; pinta = !pinta; }
                    }
                }
            }
            if (!punta) return;
            var fin = puntos[puntos.Count - 1];
            p.lineCap = LineCap.Butt;
            p.lineWidth = 1.6f;
            p.BeginPath();
            p.MoveTo(fin + new Vector2(-5.5f, -3.5f));
            p.LineTo(fin);
            p.LineTo(fin + new Vector2(-5.5f, 3.5f));
            p.Stroke();
        }

        // ─────────────── la ficha corta (el tooltip del prototipo) ───────────────

        void MostrarTip(VisualElement tip, ArbolDeTropas a, LugarCampamento L, string id, string est, Vector2 donde)
        {
            var n = a.Nodos[id];
            var t = n.T;
            tip.Clear();
            var cab = Dialogo.Caja("recl-tip-cab");
            if (PiezasDeTropa.Miniatura(id, 34) is VisualElement mini) cab.Add(mini);
            var textos = Dialogo.Caja("recl-tip-textos");
            textos.Add(Dialogo.Texto(t.Nombre, "recl-tip-nombre"));
            var donde0 = t.Edificio == "centroUrbano" ? (L.Plaza != null ? "Centro Urbano" : "Campamento") : $"{Pieza(t.Edificio)} {t.NivelRequerido}";
            textos.Add(Dialogo.Texto($"{donde0} · Era {Romano[Math.Min(n.Era, Romano.Length - 1)]} · escalón {t.Escalon} · escuadra de {t.Unidades}", "recl-tip-meta"));
            cab.Add(textos);
            tip.Add(cab);
            var caja = Dialogo.Caja("recl-tip-estado " + est);
            caja.Add(Dialogo.Texto(L.Plaza != null ? "En esta plaza" : "En este campamento", "recl-tip-meta"));
            caja.Add(Dialogo.Texto(EstadoEnCaja(L, id, est, null), "recl-tip-texto"));
            tip.Add(caja);
            if (L.Plaza != null) PorSoldadoEnPlaza(tip, L, id);
            else if (t.Edificio == "centroUrbano") { tip.Add(Dialogo.Texto("PRÉSTAMO", "recl-tip-seccion")); tip.Add(Dialogo.Texto("Gratis. La escuadra no gana experiencia.", "recl-tip-texto")); }
            else if (L.Oferta.TryGetValue(id, out var o))
            {
                tip.Add(Dialogo.Texto("POR SOLDADO", "recl-tip-seccion"));
                tip.Add(Dialogo.Texto($"{o.PrecioPorSoldado / 1000.0:0.#} de oro con el equipo dentro (recargo, reputación y descuento ya aplicados)", "recl-tip-texto"));
                tip.Add(Dialogo.Texto("1 recluta del campamento", "recl-tip-texto"));
            }
            var tras = n.Todos.Select(x => a.Nodos[x.De].T).ToList();
            var lleva = n.HijosTodos.Where(x => a.Nodos[x].Col != null).Select(x => a.Nodos[x].T).ToList();
            if (tras.Count > 0 || lleva.Count > 0)
            {
                tip.Add(Dialogo.Texto("EN EL ÁRBOL", "recl-tip-seccion"));
                if (tras.Count > 0) tip.Add(Dialogo.Texto("Va tras: " + string.Join(", ", tras.Select(x => x.Nombre)), "recl-tip-texto"));
                if (lleva.Count > 0) tip.Add(Dialogo.Texto("Lleva a: " + string.Join(", ", lleva.Select(x => x.Nombre)), "recl-tip-texto"));
            }
            tip.style.display = DisplayStyle.Flex;
            ColocarTip(tip, donde);
        }

        static void ColocarTip(VisualElement tip, Vector2 raton)
        {
            if (tip.userData == null) tip.RegisterCallback<GeometryChangedEvent>(_ => { if (tip.userData is Vector2 r) ColocarTip(tip, r); });
            tip.userData = raton;
            if (tip.style.display == DisplayStyle.None || tip.parent == null) return;
            var raiz = tip.parent.worldBound;
            var local = raton - raiz.position;
            var w = float.IsNaN(tip.layout.width) || tip.layout.width < 1 ? 380 : tip.layout.width;
            var h = float.IsNaN(tip.layout.height) || tip.layout.height < 1 ? 260 : tip.layout.height;
            var x = local.x + 16;
            var y = local.y + 16;
            if (x + w > raiz.width - 8) x = Mathf.Max(8, local.x - w - 16);
            if (y + h > raiz.height - 8) y = Mathf.Max(8, raiz.height - h - 8);
            tip.style.left = x;
            tip.style.top = y;
        }

        // ─────────────── la ficha de la tropa (el panel derecho de Tropas de la Base) ───────────────

        void FichaDeRecluta(VisualElement datos, ArbolDeTropas a, LugarCampamento L, string id, string est, Heroe h, Action<string> error, Action repintar)
        {
            var n = a.Nodos[id];
            var t = n.T;
            var d = PiezasDeTropa.Datos(id);
            var sub = Dialogo.Caja("tropas-subpestanas");
            foreach (var (sid, nombre) in new[] { ("caracteristicas", "Características"), ("atributos", "Atributos") })
            {
                var p = sid;
                var b = Dialogo.Boton(nombre, "tropas-subpestana", () => { subpestanaRecluta = p; repintar(); });
                b.name = "subpestana-" + sid;
                b.EnableInClassList("activo", subpestanaRecluta == sid);
                if (sub.childCount > 0) sub.Add(Dialogo.Texto("|", "tropas-subpestana-raya"));
                sub.Add(b);
            }
            datos.Add(sub);
            var v = new ScrollView(ScrollViewMode.Vertical) { name = "recl-contenido", horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            v.AddToClassList("tropas-contenido");
            datos.Add(v);
            if (subpestanaRecluta == "atributos") { AtributosDeTropa(v, id, t.Unidades.ToString(), d); return; }

            var cab = Dialogo.Caja("tropas-cabeza");
            cab.Add(Dialogo.Texto(t.Nombre, "tropas-nombre"));
            cab.Add(Dialogo.Texto(TipoDeTropa(d), "tropas-tipo"));
            var fila = Dialogo.Caja("tropas-estrellas-fila");
            if (PiezasDeTropa.Estrellas(id, 18) is VisualElement estrellas) fila.Add(estrellas);
            if (est == "tuya") fila.Add(Dialogo.Texto(n.Carril == "centroUrbano" && L.Plaza == null ? "prestada" : "tuya", "base-marca"));
            cab.Add(fila);
            if (PiezasDeTropa.Miniatura(id, 64) is VisualElement mini) { var arriba = Dialogo.Caja("tropas-cabeza-fila"); arriba.Add(cab); arriba.Add(mini); v.Add(arriba); }
            else v.Add(cab);
            var cifras = Dialogo.Caja("tropas-cifras");
            cifras.name = "tropas-cifras";
            cifras.Add(Cifra("liderazgo", (LiderazgoDe(id) ?? 0).ToString(), "Liderazgo"));
            cifras.Add(Cifra("hombres", t.Unidades.ToString(), "Hombres de la escuadra"));
            v.Add(cifras);
            var estado = Dialogo.Texto(EstadoEnCaja(L, id, est, h), "recl-ficha-estado " + est);
            estado.name = "estado-ficha";
            v.Add(estado);

            // Formaciones y órdenes de su tropa en Conquest, solo para ver.
            if (d != null)
            {
                var permitidas = FormacionesPermitidas(h);
                var deLaTropa = new[] { d.restFormation }.Concat(d.formations ?? new FormationDefinition[0]).Where(x => x != null).ToList();
                var forma = Dialogo.Caja("tropas-botones");
                forma.name = "formaciones";
                foreach (var (fid, fnombre) in permitidas)
                {
                    var def = deLaTropa.FirstOrDefault(x => x.formationType == FamiliaDeFormacion(fid));
                    if (def == null) continue;
                    var b = Dialogo.Caja("tropas-boton tropas-boton--quieto");
                    b.tooltip = fnombre;
                    if (def.formationIcon != null) { var ic = Dialogo.Caja("tropas-boton-icono"); ic.style.backgroundImage = new StyleBackground(def.formationIcon); b.Add(ic); }
                    else b.Add(Dialogo.Texto(fnombre.Substring(0, 1), "tropas-boton-letra"));
                    forma.Add(b);
                }
                if (forma.childCount > 0) { v.Add(Seccion("Formaciones")); v.Add(forma); }
                OrdenesDeTropa(v, d);
            }

            // Pedir prestada (las del campamento) o reclutar (lo que ofrece), con «Pagar con».
            var acciones = Dialogo.Caja("tropas-acciones");
            acciones.name = "recl-acciones";
            if (L.Plaza != null) ReclutarEnLaPlaza(acciones, L, id, est, h, error, repintar);
            else if (n.Carril == "centroUrbano" && est != "tuya")
            {
                Button pedir = null;
                pedir = Dialogo.Boton($"Pedir prestada · {t.Unidades} hombres, gratis", "boton-primario", async () =>
                {
                    var o = new OrdenPedirPrestamo();
                    o.TropaIds.Add(id);
                    await OrdenDelPanel(new Orden { PedirPrestamo = o }, error, repintar, pedir);
                });
                pedir.name = "pedir-" + id;
                acciones.Add(pedir);
            }
            else if (n.Carril != "centroUrbano" && est == "lista")
            {
                var o = L.Oferta[id];
                var fuentes = FuentesDePago(h, L.Camp);
                var fuente = FuenteDePago(h, L.Camp);
                if (fuentes.Count > 1)
                {
                    var opciones = fuentes.Select(f => $"{f.nombre} ({Math.Floor(f.oro)} de oro)").ToList();
                    var actual = Math.Max(0, fuentes.FindIndex(f => f.pagarCon == fuente?.pagarCon));
                    var pagar = new DropdownField("Pagar con", opciones, actual, null, null) { name = "pagar-con" };
                    pagar.AddToClassList("base-pagar-con");
                    pagar.RegisterValueChangedCallback(_ => { pagarConBase = fuentes[pagar.index].pagarCon; repintar(); });
                    v.Add(pagar);
                }
                var unidades = (int)o.Unidades;
                var coste = (int)Math.Ceiling(o.PrecioEscuadra / 1000.0);
                var poblacion = Math.Floor(Instante.Valor(L.Camp.Poblacion, c.Sesion.AhoraMundo) / 1000);
                var oro = fuente?.oro ?? 0;
                var motivo = fuente == null ? "No tienes con qué pagar aquí."
                    : poblacion < unidades ? $"Faltan reclutas: hay {poblacion} y hacen falta {unidades}."
                    : coste > oro ? $"No te llega el oro: {coste} y tienes {Math.Floor(oro)} en {fuente.Value.nombre}." : null;
                Button reclutar = null;
                reclutar = Dialogo.Boton("", "boton-primario base-reponer", async () =>
                    await OrdenDelPanel(new Orden { ReclutarEnCampamento = new OrdenReclutarEnCampamento { TropaId = id, PagarCon = FuenteDePago(h, L.Camp)?.pagarCon ?? "almacenPersonal" } },
                        error, repintar, reclutar));
                reclutar.name = "reclutar-" + id;
                reclutar.Add(Dialogo.Texto($"Reclutar {unidades}", "boton-texto"));
                var chip = IconosDelMundo.ChipDeRecurso("oro", coste.ToString(), $"{coste} de oro");
                chip.EnableInClassList("chip-recurso--falta", coste > oro);
                reclutar.Add(chip);
                reclutar.SetEnabled(motivo == null);
                acciones.Add(reclutar);
                if (motivo != null) { var m = Dialogo.Texto(motivo, "campo-ayuda"); m.name = "motivo-" + id; acciones.Add(m); }
            }
            if (acciones.childCount > 0) v.Add(acciones);
        }
    }
}

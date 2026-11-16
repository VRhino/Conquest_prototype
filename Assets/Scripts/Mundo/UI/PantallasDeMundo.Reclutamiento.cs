// La tropa del campamento (paso 6 del bloque 4, tanda 3): el árbol de reclutamiento de BronzeAgeClient (`arbolReclutamiento.ts`, `arbolTablero.ts`
// y `lugarCampamento` de `pantallaCampamento.ts`) calcado en UI Toolkit, con la tarjeta de tropa de Conquest en cada nudo. Sustituye a la lista del
// edificio militar. Víctor (2026-11-15): el árbol se ve entero, cada tropa con su estado y lo que le falta; solo se recluta lo que ofrece el
// campamento; al pulsar, la ficha de la tropa en el panel derecho de Tropas de la Base (Características | Atributos), con «Reclutar» o «Pedir
// prestada». Las líneas salen del catálogo; lo adoptado por tu Facción lo marca la ficha corta («Tecnologías de tu Facción», paso 8) y, en la
// plaza, la carta «adoptable» con su etiqueta «Adoptar», que abre la pestaña Tecnología de tu Facción en esa tecnología.
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
        /// <summary>Cierra la ventana de la tropa abierta (la etiqueta «Adoptar» la cierra para abrir la Facción).</summary>
        Action cerrarPanelTropa;

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
            var tecs = h.HasFaccionId ? c.Sesion.Almacen.Buscar(TipoEntidad.TecnologiaFaccion, h.FaccionId)?.Tecnologias : null;
            return new LugarCampamento
            {
                Camp = camp, A = a,
                Adoptadas = tecs == null ? null : new HashSet<string>(tecs.Adoptadas),
                Aparecidas = tecs == null ? null : new HashSet<string>(tecs.Aparecidas),
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
                cerrarPanelTropa = null;
            }
            cerrarPanelTropa = Cerrar;
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

        /// <summary>La pestaña de la pieza Militar del campamento: «tropa» (reclutar) o «mercenarios» (el tablón).</summary>
        string pestanaMilitar = "tropa";

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
            // Con Facción y alguien en el tablón, junto a reclutar, el tablón de mercenarios del campamento (paso 12, tanda 2; Víctor, 2b).
            if (h != null && h.HasFaccionId && camp != null && camp.Tablon.Count > 0)
            {
                if (Resources.Load<StyleSheet>("MundoUI/Plaza") is StyleSheet hojaPlaza && !v.styleSheets.Contains(hojaPlaza)) v.styleSheets.Add(hojaPlaza);
                var pestanas = Dialogo.Caja("recl-pestanas");
                foreach (var (id, nombre) in new[] { ("tropa", "Tropa"), ("mercenarios", "Mercenarios") })
                {
                    var b = Dialogo.Boton(nombre, "plaza-subtab" + (id == pestanaMilitar ? " activo" : ""), () => { pestanaMilitar = id; repintar(); });
                    b.name = "militar-" + id;
                    pestanas.Add(b);
                }
                cab.Add(pestanas);
            }
            else pestanaMilitar = "tropa";
            cab.Add(Dialogo.Caja("recl-hueco"));
            var cerrarBoton = Dialogo.Boton("Cerrar", "boton-secundario", cerrar);
            cerrarBoton.name = "cerrar";
            cab.Add(cerrarBoton);
            v.Add(cab);
            if (camp == null || h == null || a == null) { v.Add(error); return; }
            if (pestanaMilitar == "mercenarios")
            {
                var cuerpo = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden, name = "merc-cuerpo" };
                cuerpo.AddToClassList("merc-cuerpo");
                TablonDeMercenarios(cuerpo, camp.Tablon, h, FuentesDeContrato(h, camp), null, Error, repintar);
                v.Add(cuerpo);
                v.Add(error);
                return;
            }
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

        /// <summary>Lo que un árbol pone en el tablero común (`htmlTablero` y `montarTablero` de `arbolTablero.ts`): sus Eras, sus carriles y sus cartas,
        /// ya hechas y con sus eventos. El tablero las coloca, traza las líneas, ilumina el camino (`Resaltar`), se arrastra y deja fijas las cabeceras.
        /// Con `AltoCarta` o `Cabecera` en NaN, las mide (las cartas de alto variable de las tecnologías, como las filas `auto` del prototipo).</summary>
        sealed class DibujoDeTablero
        {
            public string Esquina;
            public int NCOL;
            /// <summary>Por Era: su primera columna, cuántas ocupa, si aún no empieza en el mundo y su cabecera.</summary>
            public readonly List<(int baseCol, int cols, bool futura, VisualElement cab)> Eras = new List<(int, int, bool, VisualElement)>();
            /// <summary>Los carriles que se ven, en orden: su id, sus filas y su nombre (la columna fija de la izquierda).</summary>
            public readonly List<(string id, int filas, VisualElement nombre)> Carriles = new List<(string, int, VisualElement)>();
            /// <summary>Todos los carriles en orden (para saber si una línea entre carriles baja o sube).</summary>
            public List<string> OrdenCarriles;
            public Dictionary<string, List<HashSet<int>>> Ocupa;
            /// <summary>Las cartas en el orden de sus líneas: dónde va cada una, de qué viene y la carta.</summary>
            public readonly List<(string id, string carril, int col, int fila, List<(string de, string tipo)> preds, Button carta)> Cartas =
                new List<(string, string, int, int, List<(string, string)>, Button)>();
            public Func<string, HashSet<string>> Antes, Despues;
            public float AnchoCarta, AltoCarta, Ancla, Cabecera = ArbolCabecera;
            public bool Todas;
            public Vector2 Scroll;
            public Action<Vector2> AlDesplazar;
            /// <summary>Una carta a la vista al abrir (se lleva el tablero hasta ella).</summary>
            public string Enfocar;
            /// <summary>Lo pone el tablero: ilumina el camino de una carta (null, nada).</summary>
            public Action<string> Resaltar;
            /// <summary>Lo llama el tablero cuando ha colocado las cartas (y cada vez que las recoloca).</summary>
            public Action Colocado;
        }

        VisualElement TableroComun(DibujoDeTablero d)
        {
            // Una Era de una sola columna ocupa dos (`MIN_COLS` de arbolTablero.ts) y las de después se corren.
            var corrimiento = new Dictionary<int, int>();
            var eras = new List<(int baseCol, int cols, bool futura, VisualElement cab)>();
            var extra = 0;
            foreach (var (b, n0, futura, cab) in d.Eras)
            {
                for (int k = b; k < b + n0; k++) corrimiento[k] = extra;
                eras.Add((b + extra, Math.Max(2, n0), futura, cab));
                extra += Math.Max(2, n0) - n0;
            }
            var NCOL = d.NCOL + extra;
            int Col(int col) => col + (corrimiento.TryGetValue(col, out var x) ? x : extra);
            // La pista i (0, la de los carriles) y el canal a la derecha de la columna c.
            float XPista(int i) => i == 0 ? 0 : ArbolCarril + ArbolHueco + (i - 1) * (d.AnchoCarta + ArbolHueco);
            float XHueco(int col) => XPista(col + 1) + d.AnchoCarta + ArbolHueco / 2;
            var ancho = XPista(NCOL) + d.AnchoCarta + ArbolHueco / 2;
            var medirCartas = float.IsNaN(d.AltoCarta);
            var medirCabecera = float.IsNaN(d.Cabecera);
            // El alto de cada fila de cada carril (la carta más alta y su margen) y el de la cabecera.
            var altos = new Dictionary<string, float>();
            var altoCab = medirCabecera ? ArbolCabecera : d.Cabecera;
            float AltoFila(string carril, int fila) => altos.TryGetValue(carril + "/" + fila, out var hh) ? hh : (medirCartas ? 120 : d.AltoCarta) + 2 * ArbolMargen;

            var marco = new ScrollView(ScrollViewMode.VerticalAndHorizontal) { name = "recl-marco" };
            marco.AddToClassList("recl-marco");
            marco.horizontalScrollerVisibility = ScrollerVisibility.Auto;
            marco.verticalScrollerVisibility = ScrollerVisibility.Auto;
            var t = new VisualElement { name = "recl-tablero" };
            t.AddToClassList("recl-tablero");
            t.EnableInClassList("todas", d.Todas);
            t.style.width = ancho;
            marco.Add(t);
            VisualElement Abs(VisualElement e, float x, float w)
            {
                e.style.position = Position.Absolute;
                e.style.left = x; e.style.width = w;
                return e;
            }

            // Fondos de las Eras y de los carriles.
            var fondosEra = new List<VisualElement>();
            for (int i = 0; i < eras.Count; i++)
            {
                var (b, cols, futura, _) = eras[i];
                var f = Abs(Dialogo.Caja("recl-era-fondo" + (i % 2 == 1 ? " par" : "") + (i > 0 ? " raya" : "") + (futura ? " futura" : "")),
                    XPista(1 + b) - ArbolHueco / 2, cols * (d.AnchoCarta + ArbolHueco));
                f.pickingMode = PickingMode.Ignore;
                f.style.top = 0;
                fondosEra.Add(f);
                t.Add(f);
            }
            var fondosCarril = new List<VisualElement>();
            for (int i = 0; i < d.Carriles.Count; i++)
            {
                var f = Abs(Dialogo.Caja("recl-carril-fondo" + (i % 2 == 1 ? " par" : "")), 0, ancho);
                f.pickingMode = PickingMode.Ignore;
                fondosCarril.Add(f);
                t.Add(f);
            }

            // Las líneas (`trazar`): en ángulo recto, por los canales entre columnas y las franjas entre carriles.
            var lineas = new List<Linea>();
            string resaltado = null;
            HashSet<string> luz = new HashSet<string>(), baja = new HashSet<string>();
            var capaLineas = new VisualElement { name = "recl-lineas", pickingMode = PickingMode.Ignore };
            capaLineas.style.position = Position.Absolute;
            capaLineas.style.left = 0; capaLineas.style.top = 0; capaLineas.style.width = ancho;
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
                    else alfa = l.Cruce ? (d.Todas ? 0.5f : 0) : 0.85f;
                    if (alfa > 0) Trazo(ctx.painter2D, l.P, l.Tipo, color, alfa, true);
                }
            };
            t.Add(capaLineas);

            // Las cartas.
            var cartas = new Dictionary<string, VisualElement>();
            foreach (var k in d.Cartas)
            {
                Abs(k.carta, XPista(1 + Col(k.col)), d.AnchoCarta);
                if (!medirCartas) k.carta.style.height = d.AltoCarta;
                cartas[k.id] = k.carta;
                t.Add(k.carta);
            }
            d.Resaltar = id =>
            {
                resaltado = id;
                luz = id == null ? new HashSet<string>() : d.Antes(id);
                if (id != null) luz.Add(id);
                baja = id == null ? new HashSet<string>() : d.Despues(id);
                t.EnableInClassList("activo", id != null);
                foreach (var (cid, carta) in cartas)
                {
                    carta.EnableInClassList("luz", luz.Contains(cid));
                    carta.EnableInClassList("baja", !luz.Contains(cid) && baja.Contains(cid));
                    carta.EnableInClassList("foco", cid == id);
                }
                capaLineas.MarkDirtyRepaint();
            };

            // Las cabeceras de Era y de carril, y la esquina: se quedan a la vista al desplazar (`position: sticky` del prototipo).
            var fijasArriba = new List<VisualElement>();
            var fijasIzquierda = new List<VisualElement>();
            foreach (var (b, cols, _, cab) in eras)
            {
                Abs(cab, XPista(1 + b), cols * d.AnchoCarta + (cols - 1) * ArbolHueco);
                cab.style.top = 0;
                if (!medirCabecera) cab.style.height = altoCab;
                fijasArriba.Add(cab);
                t.Add(cab);
            }
            foreach (var (_, _, nombre) in d.Carriles)
            {
                Abs(nombre, 0, ArbolCarril);
                fijasIzquierda.Add(nombre);
                t.Add(nombre);
            }
            var esquina = Abs(Dialogo.Caja("recl-esquina"), 0, ArbolCarril);
            esquina.style.top = 0;
            esquina.Add(Dialogo.Texto(d.Esquina, "recl-esquina-texto"));
            t.Add(esquina);

            // Coloca en vertical lo que depende del alto de las filas, y traza las líneas.
            var bandas = new Dictionary<string, (float y, float h)>();
            var cajas = new Dictionary<string, (float x, float y)>();
            void Trazar()
            {
                lineas.Clear();
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
                    var occ = d.Ocupa[carril];
                    for (int k = c0; k <= c1; k++) if (fila < occ.Count && occ[fila] != null && occ[fila].Contains(k)) return false;
                    return true;
                }
                var pos = d.Cartas.ToDictionary(k => k.id, k => (k.carril, k.col, k.fila));
                foreach (var k in d.Cartas)
                {
                    foreach (var (de, tipo) in k.preds)
                    {
                        if (!cajas.ContainsKey(de)) continue;
                        var (ac, acol, afila) = pos[de];
                        var (ax, ay) = cajas[de];
                        var (bx, by) = cajas[k.id];
                        float x1 = ax + d.AnchoCarta, y1 = ay + d.Ancla, x2 = bx - 1, y2 = by + d.Ancla;
                        var cA = Col(acol);
                        var cN = Col(k.col);
                        var xa = Canal(cA, "S" + de);
                        var mismo = ac == k.carril;
                        var P = new List<Vector2> { new Vector2(x1, y1) };
                        void H(float xx) => P.Add(new Vector2(xx, P[P.Count - 1].y));
                        void V(float yy) => P.Add(new Vector2(P[P.Count - 1].x, yy));
                        if (mismo && afila == k.fila && LibreEnFila(k.carril, k.fila, acol + 1, k.col - 1)) H(x2);
                        else if (cN == cA + 1) { H(xa); V(y2); H(x2); }
                        else if (mismo && LibreEnFila(k.carril, k.fila, acol + 1, k.col - 1)) { H(xa); V(y2); H(x2); }
                        else if (mismo && LibreEnFila(k.carril, afila, acol + 1, k.col - 1)) { H(Canal(cN - 1, "T" + k.id)); V(y2); H(x2); }
                        else
                        {
                            var yg = mismo ? YCanal(k.carril, false) : YCanal(k.carril, d.OrdenCarriles.IndexOf(k.carril) > d.OrdenCarriles.IndexOf(ac));
                            H(xa); V(yg); H(Canal(cN - 1, "T" + k.id)); V(y2); H(x2);
                        }
                        lineas.Add(new Linea { De = de, A = k.id, Tipo = tipo, Cruce = !mismo, P = P });
                    }
                }
                capaLineas.MarkDirtyRepaint();
            }
            void Colocar()
            {
                bandas.Clear();
                var filaY = new Dictionary<string, float>();
                var y0 = altoCab;
                foreach (var (id, filas, _) in d.Carriles)
                {
                    y0 += ArbolCanal;
                    var inicio = y0;
                    for (int f = 0; f < Math.Max(1, filas); f++) { filaY[id + "/" + f] = y0; y0 += AltoFila(id, f); }
                    bandas[id] = (inicio, y0 - inicio);
                }
                var altoTotal = y0 + ArbolCanal;
                t.style.height = altoTotal;
                capaLineas.style.height = altoTotal;
                foreach (var f in fondosEra) f.style.height = altoTotal;
                for (int i = 0; i < d.Carriles.Count; i++)
                {
                    var (yy, hh) = bandas[d.Carriles[i].id];
                    fondosCarril[i].style.top = yy; fondosCarril[i].style.height = hh;
                    d.Carriles[i].nombre.style.top = yy; d.Carriles[i].nombre.style.height = hh;
                }
                esquina.style.height = altoCab;
                cajas.Clear();
                foreach (var k in d.Cartas)
                {
                    var y = filaY[k.carril + "/" + k.fila] + ArbolMargen;
                    k.carta.style.top = y;
                    cajas[k.id] = (XPista(1 + Col(k.col)), y);
                }
                Trazar();
                d.Colocado?.Invoke();
            }
            Colocar();
            // Las cartas y cabeceras de alto variable se miden en cuanto el tablero tiene su disposición, y se recoloca si algo cambió.
            if (medirCartas || medirCabecera)
                t.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    var nuevos = new Dictionary<string, float>();
                    if (medirCartas)
                        foreach (var k in d.Cartas)
                        {
                            var hh = k.carta.resolvedStyle.height;
                            if (float.IsNaN(hh) || hh < 1) return;
                            var clave = k.carril + "/" + k.fila;
                            nuevos[clave] = Math.Max(nuevos.TryGetValue(clave, out var ya) ? ya : 0, hh + 2 * ArbolMargen);
                        }
                    var cab = altoCab;
                    if (medirCabecera)
                    {
                        cab = 0;
                        foreach (var e in fijasArriba) { var hh = e.resolvedStyle.height; if (float.IsNaN(hh) || hh < 1) return; cab = Math.Max(cab, hh); }
                    }
                    if (Math.Abs(cab - altoCab) < 0.5f && nuevos.Count == altos.Count && nuevos.All(p => altos.TryGetValue(p.Key, out var v) && Math.Abs(v - p.Value) < 0.5f)) return;
                    if (medirCartas) { altos.Clear(); foreach (var p in nuevos) altos[p.Key] = p.Value; }
                    altoCab = cab;
                    Colocar();
                });

            void Fijar(Vector2 o)
            {
                foreach (var e in fijasArriba) e.style.translate = new Translate(0, o.y);
                foreach (var e in fijasIzquierda) e.style.translate = new Translate(o.x, 0);
                esquina.style.translate = new Translate(o.x, o.y);
            }
            marco.horizontalScroller.valueChanged += _ => { Fijar(marco.scrollOffset); d.AlDesplazar?.Invoke(marco.scrollOffset); };
            marco.verticalScroller.valueChanged += _ => { Fijar(marco.scrollOffset); d.AlDesplazar?.Invoke(marco.scrollOffset); };
            var inicial = d.Scroll;
            marco.schedule.Execute(() =>
            {
                // La carta a la que se viene (la etiqueta «Adoptar» del árbol de tropas), a la vista.
                if (d.Enfocar != null && cajas.TryGetValue(d.Enfocar, out var c0))
                    inicial = new Vector2(Mathf.Max(0, c0.x + d.AnchoCarta / 2 - marco.contentViewport.layout.width / 2), Mathf.Max(0, c0.y - marco.contentViewport.layout.height / 3));
                marco.scrollOffset = inicial;
                Fijar(marco.scrollOffset);
            }).ExecuteLater(1);

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

        /// <summary>El árbol de tropas en el tablero común: la tarjeta de Conquest en cada carta, con su medalla y su franja de estado.</summary>
        VisualElement Tablero(ArbolDeTropas a, LugarCampamento L, ArbolDeTropas.Disposicion lay, List<string> visibles, Dictionary<string, string> estados,
            VisualElement tip, Action repintar)
        {
            var d = new DibujoDeTablero
            {
                Esquina = "SE RECLUTA EN · ERA", NCOL = lay.NCOL, OrdenCarriles = a.Carriles.Select(cr => cr.Id).ToList(), Ocupa = lay.Ocupa,
                Antes = a.Antes, Despues = a.Despues, AnchoCarta = ArbolNodo, AltoCarta = ArbolAltoNodo, Ancla = 4 + ArbolAltoTarjeta / 2, Todas = arbolTodas,
                Scroll = scrollDelArbol, AlDesplazar = o => { scrollDelArbol = o; tip.style.display = DisplayStyle.None; },
            };
            var ultima = a.Eras.Count > 0 ? a.Eras.Max(e => e.Orden) : 1;
            foreach (var e in lay.ErasVis)
            {
                var cabEra = Dialogo.Caja("recl-era-cab");
                cabEra.name = "era-" + e.Orden;
                cabEra.Add(Dialogo.Texto(Romano[Math.Min(e.Orden, Romano.Length - 1)], "recl-era-num"));
                var textos = Dialogo.Caja("recl-era-textos");
                textos.Add(Dialogo.Texto($"Era {Romano[Math.Min(e.Orden, Romano.Length - 1)]} · {e.Nombre}", "recl-era-nombre"));
                var cuantas = visibles.Count(id => a.Nodos[id].Era == e.Orden);
                textos.Add(Dialogo.Texto($"{cuantas} tropa{(cuantas == 1 ? "" : "s")} · {DuracionDeEra(e, ultima)}", "recl-era-sub"));
                if (L.EraMundo == e.Orden) textos.Add(Dialogo.Texto("ERA DEL MUNDO", "recl-era-marca actual"));
                else if (L.EraMundo != null && e.Orden > L.EraMundo) textos.Add(Dialogo.Texto("BLOQUEADA", "recl-era-marca futura"));
                cabEra.Add(textos);
                d.Eras.Add((lay.BaseEra[e.Orden], lay.ColsEra[e.Orden], L.EraMundo != null && e.Orden > L.EraMundo, cabEra));
            }
            foreach (var cr in a.Carriles.Where(cr => cr.Tropas.Any(visibles.Contains)))
            {
                var nombre = Dialogo.Caja("recl-carril-nombre");
                nombre.name = "carril-" + cr.Id;
                var icono = cr.Id == "centroUrbano" && L.Plaza == null ? IconosDelMundo.Icono("parte", "campamento", 30, IconosDelMundo.Bronce) : IconosDelMundo.Icono("edificio", cr.Id, 30, IconosDelMundo.Bronce);
                if (icono != null) nombre.Add(icono);
                nombre.Add(Dialogo.Texto((L.Plaza != null && cr.Id == "centroUrbano" ? "Centro Urbano" : NombreDeCarril(cr.Id)).ToUpperInvariant(), "recl-carril-texto"));
                var n = cr.Tropas.Count(visibles.Contains);
                nombre.Add(Dialogo.Texto($"{n} tropa{(n == 1 ? "" : "s")}", "recl-carril-sub"));
                d.Carriles.Add((cr.Id, Math.Max(1, lay.Filas[cr.Id]), nombre));
            }
            foreach (var id in a.OrdenNodos.Where(visibles.Contains))
            {
                var n = a.Nodos[id];
                var est = estados[id];
                var carta = new Button { name = "nodo-" + id };
                carta.AddToClassList("recl-carta");
                carta.AddToClassList(est);
                carta.EnableInClassList("elegida", id == tropaDelArbol);
                var vivos = est == "tuya" ? (int?)(n.Carril == "centroUrbano" && L.Plaza == null ? L.Prestadas[id].Cantidad : L.Propias[id].Cantidad) : n.T.Unidades;
                var tarjeta = PiezasDeTropa.Tarjeta(id, LiderazgoDe(id), vivos, n.T.Unidades, null, ArbolTarjeta);
                if (tarjeta != null) { tarjeta.tooltip = null; tarjeta.pickingMode = PickingMode.Ignore; carta.Add(tarjeta); }
                else carta.Add(Dialogo.Texto(n.T.Nombre, "recl-carta-nombre"));
                // La medalla: ✓ si se recluta o se presta aquí; la bandera (⚑ en el prototipo) si ya la tienes; ! si solo falta adoptar su tecnología.
                if (est == "lista" || est == "tuya" || est == "adoptable")
                {
                    var m = est == "tuya" ? Dialogo.Caja("recl-medalla") : (VisualElement)Dialogo.Texto(est == "lista" ? "✓" : "!", "recl-medalla");
                    if (est == "tuya" && IconosDelMundo.Icono("simbolo", "bandera", 12, new Color32(0x20, 0x15, 0x09, 0xff)) is VisualElement bandera) m.Add(bandera);
                    m.pickingMode = PickingMode.Ignore;
                    carta.Add(m);
                }
                carta.Add(FranjaDeTropa(L, id, est));
                var tid = id;
                carta.RegisterCallback<PointerEnterEvent>(e => { d.Resaltar(tid); MostrarTip(tip, a, L, tid, estados[tid], e.position); });
                carta.RegisterCallback<PointerMoveEvent>(e => ColocarTip(tip, e.position));
                carta.RegisterCallback<PointerLeaveEvent>(_ => { d.Resaltar(null); tip.style.display = DisplayStyle.None; });
                carta.RegisterCallback<FocusInEvent>(_ => { d.Resaltar(tid); MostrarTip(tip, a, L, tid, estados[tid], carta.worldBound.max); });
                carta.RegisterCallback<FocusOutEvent>(_ => { d.Resaltar(null); tip.style.display = DisplayStyle.None; });
                carta.clicked += () => { tropaDelArbol = tid; repintar(); };
                d.Cartas.Add((id, n.Carril, n.Col.Value, n.Fila.Value, n.Preds.Select(p => (p.De, p.Tipo)).ToList(), carta));
            }
            return TableroComun(d);
        }

        /// <summary>La franja de estado de la carta. La de «adoptable» lleva la etiqueta «Adoptar» del prototipo, que aquí abre la pestaña Tecnología
        /// de tu Facción en esa tecnología (Víctor, duda 7 del paso 8).</summary>
        VisualElement FranjaDeTropa(LugarCampamento L, string id, string est)
        {
            if (est == "adoptable")
            {
                var fila = Dialogo.Caja("recl-carta-estado recl-carta-estado--adoptar");
                fila.name = "estado-" + id;
                var tec = L.A.Nodos[id].T.Tecnologia;
                var adoptar = Dialogo.Boton("ADOPTAR", "recl-boton-mini", () => AbrirTecnologiaEn(tec));
                adoptar.name = "adoptar-" + id;
                fila.Add(adoptar);
                var nombre = Dialogo.Texto(NombreDeTecnologia(tec), "recl-carta-estado-texto");
                nombre.pickingMode = PickingMode.Ignore;
                fila.Add(nombre);
                return fila;
            }
            var franja = Dialogo.Texto(EstadoEnCarta(L, id, est), "recl-carta-estado");
            franja.name = "estado-" + id;
            franja.pickingMode = PickingMode.Ignore;
            return franja;
        }

        static string DuracionDeEra(ArbolDeTropas.Era e, int ultima) =>
            e.Orden >= ultima ? "la última Era, sin plazo" : e.MinimoSemanas != null ? $"dura de {e.MinimoSemanas} a {e.PlazoSemanas} semanas" : $"dura hasta {e.PlazoSemanas} semanas";

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
            // Las tecnologías que pide, marcadas con lo de tu Facción (sin Facción, o sin nada adoptado, no hay qué marcar).
            if (L.Adoptadas != null && L.Adoptadas.Count > 0 && ArbolDeTecnologiasDelBalance() is ArbolDeTecnologias T)
            {
                tip.Add(Dialogo.Texto("TECNOLOGÍAS DE TU FACCIÓN", "recl-tip-seccion"));
                foreach (var x in n.Req.Orden.Where(T.Nodos.ContainsKey).OrderBy(x => a.EraDe(T.Nodos[x].EraId)))
                {
                    var k = T.Nodos[x];
                    var por = n.Req.Por[x];
                    // Por qué la pide, en iconos como el prototipo: «la habilita», «para» y los recursos y edificios que la piden.
                    var motivos = por.Where(q => q.StartsWith("r:")).Select(q => ChipDeTec(q.Substring(2)))
                        .Concat(por.Where(q => q.StartsWith("e:")).Select(q => { var p = q.Split(':'); return ChipDeEdificio(p[1], p.Length > 2 ? p[2] : null); })).ToList();
                    var (marca, clase) = L.Adoptadas.Contains(x) ? ("✓", "ok") : L.Aparecidas != null && L.Aparecidas.Contains(x) ? ("!", "ap") : ("✗", "no");
                    var habilita = por.Contains("la habilita");
                    var era = $"<color=#8d7c63>(Era {Romano[Math.Min(a.EraDe(k.EraId), Romano.Length - 1)]}{(k.DeArranque ? ", de arranque" : "")}{(habilita || motivos.Count > 0 ? " ·" : ")")}</color>";
                    var fila = Renglon("recl-tip-tec", Dialogo.Texto(marca, "recl-tip-marca " + clase), k.Nombre, era,
                        habilita ? $"<color=#8d7c63>la habilita{(motivos.Count > 0 ? "," : ")")}</color>" : "",
                        motivos.Count > 0 ? "<color=#8d7c63>para</color>" : "", motivos, motivos.Count > 0 ? "<color=#8d7c63>)</color>" : "");
                    tip.Add(fila);
                }
            }
            if (L.Plaza != null) EdificiosYPueblos(tip, a, id);
            if (L.Plaza != null) PorSoldadoEnPlaza(tip, L, id);
            else if (t.Edificio == "centroUrbano") { tip.Add(Dialogo.Texto("PRÉSTAMO", "recl-tip-seccion")); tip.Add(Dialogo.Texto("Gratis. La escuadra no gana experiencia.", "recl-tip-texto")); }
            else if (L.Oferta.TryGetValue(id, out var o))
            {
                tip.Add(Dialogo.Texto("POR SOLDADO", "recl-tip-seccion"));
                tip.Add(Renglon("", ChipDeTec("oro", Math.Round(o.PrecioPorSoldado / 1000.0, 1)), "con el equipo dentro (recargo, reputación y descuento ya aplicados)"));
                tip.Add(Dialogo.Texto("1 recluta del campamento", "recl-tip-texto"));
            }
            EnCampanaYCombate(tip, id);
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

        /// <summary>Al cambiar de tamaño, la ficha corta se recoloca: junto al ratón (`Vector2`) o junto a su carta (`Rect`). Se engancha una vez.</summary>
        static void VigilarTip(VisualElement tip)
        {
            if (tip.userData != null) return;
            tip.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                if (tip.userData is Vector2 r) ColocarTip(tip, r);
                else if (tip.userData is Rect b) ColocarTipJunto(tip, b);
            });
        }

        static void ColocarTip(VisualElement tip, Vector2 raton)
        {
            VigilarTip(tip);
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

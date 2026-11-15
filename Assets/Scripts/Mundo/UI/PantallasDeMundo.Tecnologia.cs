// La tecnología de tu Facción (paso 8 del bloque 4, tanda 3), calcada de BronzeAgeClient (`ui/panelTecnologia.ts` y `ui/arbolTecnologias.ts`, sobre
// el tablero de `ui/arbolTablero.ts`): la pestaña Tecnología con el árbol por carriles y Eras, en el tablero común del árbol de tropas (arrastrar, el
// camino al pasar el ratón, «Ver todas las conexiones», las cabeceras fijas), la ficha corta al pasar el ratón y la ficha fija al pulsar, con
// «Adoptar» solo para el Rey dentro de la capital (`adoptarTecnologia`, con la tarifa de su Era del almacén de la capital), y debajo los logros del
// mundo cumplidos. La etiqueta «Adoptar» del árbol de tropas abre esta pestaña con la ficha de esa tecnología (`AbrirTecnologiaEn`).
using System;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;
using ConquestTactics.Mundo.Mapa;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    public sealed partial class PantallasDeMundo
    {
        // `--ta-carta` del árbol de tecnologías; las líneas entran y salen a 17 px del borde de arriba de la carta (`ancla`).
        const float TecCarta = 196, TecAncla = 17;

        ArbolDeTecnologias arbolDeTecnologias;
        string huellaDeTecnologias;
        /// <summary>La carta con la ficha fija (`fijado`) y la carta a la que llevar el tablero al abrir la pestaña.</summary>
        string tecFijada, tecEnfocar;
        /// <summary>La carta bajo el ratón y dónde está el ratón (para que la ficha corta siga tras un repintado).</summary>
        string tecSobre;
        Vector2 ratonSobre;
        bool tecTodas;
        Vector2 scrollDeTecnologias;
        /// <summary>Cierra la ficha fija (Esc o un clic fuera); null si no está la pestaña.</summary>
        Action cerrarFichaDeTecnologia;
        EventCallback<PointerDownEvent> clicFueraDeTecnologia;

        static readonly Dictionary<string, string> MedallaDeTecnologia = new Dictionary<string, string> { ["adoptada"] = "✓", ["adoptable"] = "!", ["revelada"] = "♪", ["logro"] = "✦" };

        /// <summary>El texto de cada contador de logro (`CONTADOR`).</summary>
        static readonly Dictionary<string, string> TextoDeContador = new Dictionary<string, string>
        {
            ["extraido.cobre"] = "cobre extraído", ["extraido.estano"] = "estaño extraído", ["extraido.piedra"] = "piedra extraída", ["extraido.hierro"] = "hierro extraído",
            ["fabricado.equipoBronce"] = "piezas de equipo de bronce fabricadas", ["fabricado.armaduraBronce"] = "armaduras de bronce fabricadas",
            ["batallas.libradas"] = "batallas libradas", ["batallas.conHoplitas"] = "batallas con hoplitas ciudadanos", ["batallas.campoAbierto"] = "batallas en campo abierto",
            ["asedios.resistidosEnCombate"] = "asedios resistidos combatiendo", ["asedios.resistidosConResidentes"] = "asedios resistidos con residentes en persona",
            ["asedios.contraMurallaCompleta"] = "asedios contra muralla completa", ["conquistas.conMurallaCompleta"] = "conquistas de plaza con muralla completa",
            ["caravanas.destruidasOCapturadas"] = "caravanas destruidas o capturadas", ["bandidos.campamentosDestruidos"] = "campamentos de bandidos destruidos",
            ["animales.comprados"] = "animales comprados a caravanas", ["reclutados.escuadrones"] = "escuadras nuevas reclutadas",
            ["reclutados.caballeria"] = "jinetes reclutados (cada carro cuenta 5)", ["reclutados.arqueros"] = "arqueros reclutados",
            ["reclutados.escaramuzadores_jabalina"] = "escaramuzadores con jabalina reclutados", ["reclutados.arqueros_compuesto"] = "arqueros con arco compuesto reclutados",
            ["reclutados.jinetes_asirios"] = "jinetes asirios reclutados", ["plazasEnNivel.2"] = "plazas a la vez en nivel 2 o más (récord)", ["plazasEnNivel.3"] = "plazas a la vez en nivel 3 o más (récord)",
            ["fabricado.armaHierroCalidad"] = "armas de hierro de calidad fabricadas", ["caminos.compartidos"] = "el primer camino compartido abierto",
            ["escuadras.veteranas"] = "escuadras que llegan al nivel 5", ["batallas.conTresHeroes"] = "batallas con tres héroes o más en un bando",
            ["batallas.ganadasEnInferioridad"] = "batallas ganadas en inferioridad", ["escuadras.eliteDestruidas"] = "escuadras de élite destruidas",
            ["batallas.campoAbiertoConCaballeria"] = "batallas ganadas en campo abierto con caballería", ["batallas.campoAbiertoConCaballeriaElite"] = "batallas ganadas en campo abierto con caballería de élite",
            ["batallas.armasCombinadas"] = "batallas con armas combinadas", ["batallas.contraCoalicion"] = "batallas ganadas por una sola Facción contra una coalición",
            ["batallas.conRey"] = "batallas en las que combate un Rey", ["persecuciones.deCaballeria"] = "ataques de columnas con caballería",
        };
        static string TextoContador(string c) => c != null && TextoDeContador.TryGetValue(c, out var t) ? t : (c ?? "").Replace('.', ' ').Replace('_', ' ');

        /// <summary>El hito «edificio» de estos tipos se cumple con un pueblo o una aldea (`HITO_PUEBLO`).</summary>
        static readonly Dictionary<string, string> HitoDePueblo = new Dictionary<string, string>
        {
            ["corral"] = "una aldea ganadera activa", ["cantera"] = "un pueblo de cantera activo", ["minaHierro"] = "un pueblo de mina de hierro activo",
        };
        /// <summary>Lo que el canon dice que abren y el motor aún no aplica; lo que cambia una cifra (en corto para la carta).</summary>
        static readonly Dictionary<string, string> CanonCorto = new Dictionary<string, string> { ["carpinteria_militar"] = "Escalas y defensas", ["trabajos_asedio"] = "Equipo de asedio" };
        static readonly Dictionary<string, string> EfectoCorto = new Dictionary<string, string>
        {
            ["disciplina_formacion"] = "Formaciones cerradas", ["ejercito_permanente"] = "+10 % experiencia de escuadra", ["logistica_campana"] = "Víveres ×1,5",
        };

        ArbolDeTecnologias ArbolDeTecnologiasDelBalance()
        {
            var a = ArbolDelBalance();
            if (a == null) return null;
            if (arbolDeTecnologias != null && huellaDeTecnologias == c.Datos.Huella && arbolDeTecnologias.A == a) return arbolDeTecnologias;
            if (!(BloqueDeDatos("TECNOLOGIAS") is JObject tecs)) return null;
            huellaDeTecnologias = c.Datos.Huella;
            return arbolDeTecnologias = new ArbolDeTecnologias(tecs, BloqueDeDatos("TARIFA_ADOPCION") as JObject, a);
        }

        /// <summary>Lo de tu Facción y del mundo que pinta el árbol (`estadoDesdeProyeccion`), o null si aún no ha llegado.</summary>
        ArbolDeTecnologias.Estado EstadoDeTecnologias(string faccionId)
        {
            var al = c.Sesion.Almacen;
            var t = al.Buscar(TipoEntidad.TecnologiaFaccion, faccionId)?.Tecnologias;
            if (t == null) return null;
            var m = al.Buscar(TipoEntidad.EstadoTecnologia, "tecnologia")?.EstadoTecnologia;
            var e = new ArbolDeTecnologias.Estado { Adoptadas = new HashSet<string>(t.Adoptadas), Aparecidas = new HashSet<string>(t.Aparecidas), EraMundo = m?.Era };
            for (int i = 0; i < t.Reveladas.Count; i++) e.Reveladas[t.Reveladas[i]] = i < t.DetalleReveladas.Count ? t.DetalleReveladas[i].Cumplidas.ToList() : new List<bool>();
            if (m != null)
            {
                e.Logros = m.LogrosCumplidos.Select(l => (l.Contador, l.Umbral)).ToList();
                e.Contadores = m.Contadores.ToDictionary(x => x.Key, x => x.Value);
                e.Plazas = (int)m.Plazas;
                e.Pendientes = m.LogrosPendientes.Select(l => (l.Contador, l.Umbral)).ToList();
            }
            return e;
        }

        /// <summary>Lo que repinta la pestaña: lo de tu Facción, el mundo, dónde está el héroe y el almacén de la capital.</summary>
        string FirmaDeTecnologia(Heroe h, Faccion f)
        {
            var al = c.Sesion.Almacen;
            var t = al.Buscar(TipoEntidad.TecnologiaFaccion, f.Id)?.Tecnologias;
            var m = al.Buscar(TipoEntidad.EstadoTecnologia, "tecnologia")?.EstadoTecnologia;
            var capital = f.HasCapitalId ? PlazaActual(f.CapitalId) : null;
            return $"{t?.Version}|{m?.Era}|{m?.LogrosCumplidos.Count}|{m?.LogrosPendientes.Count}|{m?.Contadores.Values.Sum()}|{m?.Plazas}|{h.Ubicacion}|{capital?.Version}|{tecTodas}";
        }

        /// <summary>Abre la pestaña Tecnología de tu Facción con la ficha fija de una tecnología (la etiqueta «Adoptar» del árbol de tropas).</summary>
        void AbrirTecnologiaEn(string tecId)
        {
            cerrarPanelTropa?.Invoke();
            pestanaFaccion = "tecnologia";
            tecFijada = tecId;
            tecEnfocar = tecId;
            if (FaccionAbierta) { firmaFaccion = null; PintarFaccion(); }
            else AbrirFaccion();
        }

        // ─────────────── la pestaña ───────────────

        void PestanaTecnologia(VisualElement v, Heroe h, Faccion f)
        {
            cerrarFichaDeTecnologia = null;            var T = ArbolDeTecnologiasDelBalance();
            var E = EstadoDeTecnologias(f.Id);
            var panel = Dialogo.Caja("tec-panel");
            panel.name = "tec-panel";
            v.Add(panel);
            if (T == null || E == null) { panel.Add(Dialogo.Texto(E == null ? "Cargando las tecnologías de tu Facción…" : "Cargando el árbol de tecnologías…", "faccion-vacia")); return; }

            var A = T.A;
            var eraVisible = T.UltimaEraVisible(E.Adoptadas);
            var lay = T.Disponer(eraVisible);
            var visibles = T.Orden.Where(id => T.Nodos[id].Col != null).ToList();
            var visiblesSet = new HashSet<string>(visibles);
            var estados = visibles.ToDictionary(id => id, id => T.EstadoDe(E, id));
            var mundo = A.EraDe(E.EraMundo);
            var ultima = A.Eras.Count > 0 ? A.Eras.Max(x => x.Orden) : 1;
            if (tecFijada != null && !visiblesSet.Contains(tecFijada)) tecFijada = null;

            // La ficha corta y la fija viven encima de toda la ventana.
            var tip = Dialogo.Caja("recl-tip tec-tip");
            tip.name = "tec-tip";
            tip.pickingMode = PickingMode.Ignore;
            tip.style.display = DisplayStyle.None;
            faccionVentana.Add(tip);

            // Los mandos y la leyenda (`ta-mandos`, `ta-leyenda`).
            var arbol = Dialogo.Caja("tec-arbol");
            arbol.name = "tec-arbol";
            panel.Add(arbol);
            var mandos = Dialogo.Caja("recl-mandos");
            var todas = Dialogo.Boton("", "recl-boton-todas", () => { tecTodas = !tecTodas; firmaFaccion = null; PintarFaccion(); });
            todas.name = "tec-ver-todas";
            todas.EnableInClassList("activo", tecTodas);
            todas.Add(Dialogo.Texto(tecTodas ? "✓" : "", "recl-caja"));
            todas.Add(Dialogo.Texto("Ver todas las conexiones", "recl-boton-texto"));
            mandos.Add(todas);
            mandos.Add(Dialogo.Texto("Pasa el ratón por una tecnología para ver su camino; haz clic para abrir su ficha. Arrastra el tablero para moverte.", "recl-ayuda"));
            arbol.Add(mandos);
            var leyenda = Dialogo.Caja("recl-leyenda");
            leyenda.name = "tec-leyenda";
            if (visibles.Any(id => T.Nodos[id].Preds.Count > 0)) leyenda.Add(MuestraDeLinea("hito", "depende de la anterior"));
            foreach (var (est, texto) in new[] { ("adoptada", "adoptada"), ("adoptable", "aparecida: se puede adoptar"), ("revelada", "revelada por un Aeda"),
                ("logro", "logro del mundo cumplido"), ("bloqueada", "sin logro aún"), ("futura", "bloqueada: su Era aún no empieza") })
                if (estados.ContainsValue(est))
                {
                    var fila = Dialogo.Caja("recl-leyenda-item");
                    fila.Add(Dialogo.Caja("tec-muestra " + est));
                    fila.Add(Dialogo.Texto(texto, "recl-leyenda-texto"));
                    leyenda.Add(fila);
                }
            arbol.Add(leyenda);

            // El tablero.
            var d = new DibujoDeTablero
            {
                Esquina = "CARRIL · ERA", NCOL = lay.NCOL, OrdenCarriles = T.Carriles.Select(x => x.Id).ToList(), Ocupa = lay.Ocupa,
                Antes = T.Antes, Despues = T.Despues, AnchoCarta = TecCarta, AltoCarta = float.NaN, Ancla = TecAncla, Cabecera = float.NaN,
                Todas = tecTodas, Scroll = scrollDeTecnologias, Enfocar = tecEnfocar,
            };
            tecEnfocar = null;
            var cartas = new Dictionary<string, Button>();
            void Ocultar() { tip.style.display = DisplayStyle.None; tip.RemoveFromClassList("fijo"); tip.pickingMode = PickingMode.Ignore; }
            void CerrarFicha() { tecFijada = null; d.Resaltar?.Invoke(null); Ocultar(); }
            void Fijar(string id)
            {
                tecFijada = id;
                d.Resaltar(id);
                MostrarTipDeTecnologia(tip, T, E, h, f, id, estados[id], visiblesSet, true);
                ColocarTipJunto(tip, cartas[id].worldBound);
            }
            d.AlDesplazar = o =>
            {
                scrollDeTecnologias = o;
                if (tecFijada == null) Ocultar();
                else if (cartas.TryGetValue(tecFijada, out var cf)) ColocarTipJunto(tip, cf.worldBound);
            };
            // Tras pintar (llega un cambio), la ficha que estaba abierta sigue abierta, con sus datos nuevos.
            // Y la corta de la carta bajo el ratón (los contadores del mundo suben y repintan a menudo).
            d.Colocado = () => tip.schedule.Execute(() =>
            {
                if (tip.panel == null) return;
                if (tecFijada != null) { if (cartas.ContainsKey(tecFijada)) Fijar(tecFijada); }
                else if (tecSobre != null && cartas.ContainsKey(tecSobre))
                {
                    d.Resaltar(tecSobre);
                    MostrarTipDeTecnologia(tip, T, E, h, f, tecSobre, estados[tecSobre], visiblesSet, false);
                    ColocarTip(tip, ratonSobre);
                }
            }).ExecuteLater(1);
            cerrarFichaDeTecnologia = () => { if (tecFijada != null) CerrarFicha(); };

            foreach (var x in lay.ErasVis)
                d.Eras.Add((lay.BaseEra[x.Orden], lay.ColsEra[x.Orden], x.Orden > mundo, CabeceraDeEraDeTecnologia(T, E, x, eraVisible, mundo, ultima)));
            foreach (var cr in T.Carriles.Where(x => lay.Filas.TryGetValue(x.Id, out var n) && n > 0))
            {
                var nombre = Dialogo.Caja("recl-carril-nombre");
                nombre.name = "carril-" + cr.Id;
                if (IconosDelMundo.Icono("edificio", cr.Icono, 30, IconosDelMundo.Bronce) is VisualElement icono) nombre.Add(icono);
                nombre.Add(Dialogo.Texto(cr.Nombre.ToUpperInvariant(), "recl-carril-texto"));
                var n = cr.Tecs.Count(visiblesSet.Contains);
                nombre.Add(Dialogo.Texto($"{n} tecnología{(n > 1 ? "s" : "")}", "recl-carril-sub"));
                d.Carriles.Add((cr.Id, lay.Filas[cr.Id], nombre));
            }
            foreach (var id in visibles)
            {
                var n = T.Nodos[id];
                var carta = CartaDeTecnologia(T, E, id, estados[id]);
                var tid = id;
                carta.RegisterCallback<PointerEnterEvent>(e => { tecSobre = tid; ratonSobre = e.position; if (tecFijada != null) return; d.Resaltar(tid); MostrarTipDeTecnologia(tip, T, E, h, f, tid, estados[tid], visiblesSet, false); ColocarTip(tip, e.position); });
                carta.RegisterCallback<PointerMoveEvent>(e => { ratonSobre = e.position; if (tecFijada == null) ColocarTip(tip, e.position); });
                // (al quitar la carta en un repintado también sale un «leave», con el ratón aún encima: ese no cuenta)
                carta.RegisterCallback<PointerLeaveEvent>(e =>
                {
                    if (carta.panel == null || carta.worldBound.Contains(e.position)) return;
                    if (tecSobre == tid) tecSobre = null;
                    if (tecFijada != null) return;
                    d.Resaltar(null);
                    Ocultar();
                });
                carta.RegisterCallback<FocusInEvent>(_ => { if (tecFijada != null) return; d.Resaltar(tid); MostrarTipDeTecnologia(tip, T, E, h, f, tid, estados[tid], visiblesSet, false); ColocarTipJunto(tip, carta.worldBound); });
                carta.RegisterCallback<FocusOutEvent>(_ => { if (tecFijada != null) return; d.Resaltar(null); Ocultar(); });
                carta.clicked += () => { if (tecFijada == tid) CerrarFicha(); else Fijar(tid); };
                cartas[id] = carta;
                d.Cartas.Add((id, n.Carril, n.Col.Value, n.Fila.Value, n.Preds.Select(p => (p, "hito")).ToList(), carta));
            }
            var marco = TableroComun(d);
            marco.AddToClassList("tec-marco");
            arbol.Add(marco);
            // Un clic fuera de la ficha (y de las cartas) la cierra. La ventana sigue de un repintado a otro: se quita el del anterior.
            if (clicFueraDeTecnologia != null) faccionVentana.UnregisterCallback(clicFueraDeTecnologia, TrickleDown.TrickleDown);
            clicFueraDeTecnologia = e =>
            {
                if (tecFijada == null || !(e.target is VisualElement x)) return;
                if (tip.Contains(x) || cartas.Values.Any(k => k.Contains(x))) return;
                CerrarFicha();
            };
            faccionVentana.RegisterCallback(clicFueraDeTecnologia, TrickleDown.TrickleDown);

            var error = Dialogo.Texto("", "error oculto");
            error.name = "error-tecnologia";
            panel.Add(error);

            // Los logros del mundo cumplidos.
            var extras = Dialogo.Caja("tec-extras");
            var extra = Dialogo.Caja("tec-extra");
            extra.name = "tec-logros";
            var nombreEra = A.Eras.FirstOrDefault(x => x.Id == E.EraMundo)?.Nombre ?? E.EraMundo;
            extra.Add(ConAyuda(Dialogo.Texto("Logros del mundo cumplidos", "tec-extra-titulo"), $"Lo que ha pasado entre todas las Facciones; no dicen qué tecnología abren. Era del mundo: {nombreEra}."));
            if (E.Logros.Count == 0) extra.Add(Dialogo.Texto("Aún no se ha cumplido ningún logro del mundo.", "tec-nota"));
            else foreach (var (cont, umbral) in E.Logros) extra.Add(Dialogo.Texto($"{Miles(umbral)} {TextoContador(cont)}", "tec-logro"));
            extras.Add(extra);
            panel.Add(extras);
        }

        // ─────────────── las piezas ───────────────

        static string Num(double n) => n == Math.Floor(n) ? n.ToString("0", Es) : Math.Round(n, 2).ToString("0.##", Es);

        /// <summary>Un recurso en línea (`.ta-rec`): su icono y, si la hay, la cifra; en rojo si falta.</summary>
        static VisualElement ChipDeTec(string recurso, double? n = null, bool falta = false)
        {
            var chip = Dialogo.Caja("tec-rec" + (falta ? " falta" : ""));
            chip.tooltip = CosasDelMapa.NombreDeRecurso(recurso);
            if (IconosDelMundo.Icono("recurso", recurso, 14) is VisualElement i) chip.Add(i);
            if (n != null) chip.Add(Dialogo.Texto(Num(n.Value), "tec-rec-cifra"));
            return chip;
        }

        /// <summary>Un edificio en línea: su icono y, si lo hay, el nivel.</summary>
        static VisualElement ChipDeEdificio(string tipo, string nivel = null, string nombre = null)
        {
            var chip = Dialogo.Caja("tec-rec tec-edi");
            chip.tooltip = (nombre ?? Pieza(tipo)) + (nivel != null ? " nivel " + nivel : "");
            if (IconosDelMundo.Icono("edificio", tipo, 16, IconosDelMundo.OroClaro) is VisualElement i) chip.Add(i);
            if (nivel != null) chip.Add(Dialogo.Texto(nivel, "tec-rec-cifra"));
            return chip;
        }

        /// <summary>Una línea de texto con piezas en medio (`<li>` con iconos del prototipo): cada cadena es un texto y lo demás va tal cual.</summary>
        static VisualElement Renglon(string clases, params object[] piezas)
        {
            var fila = Dialogo.Caja("tec-renglon " + clases);
            foreach (var p in piezas)
                if (p is string s) { if (s.Length > 0) fila.Add(Dialogo.Texto(s, "tec-renglon-texto")); }
                else if (p is VisualElement e) fila.Add(e);
                else if (p is IEnumerable<VisualElement> l) foreach (var x in l) fila.Add(x);
            return fila;
        }

        Dictionary<string, long> TarifaDe(ArbolDeTecnologias T, string eraId) => eraId != null && T.Tarifa.TryGetValue(eraId, out var t) ? t : new Dictionary<string, long>();

        IEnumerable<VisualElement> ChipsDeCoste(Dictionary<string, long> coste, Asentamiento almacen = null) =>
            coste.Select(p => ChipDeTec(p.Key, p.Value, almacen != null && Hay(almacen, p.Key) < p.Value));

        /// <summary>La barra de una parte de logro del mundo con «x / umbral de ahora» dentro; cumplida, llena.</summary>
        static VisualElement BarraDeLogro(ArbolDeTecnologias.Estado E, JObject parte, bool hecho, string clase = "")
        {
            var u = ArbolDeTecnologias.UmbralDe(E, parte);
            var cont = (string)parte["contador"];
            var x = hecho ? u : Math.Min(E.Contadores.TryGetValue(cont ?? "", out var v) ? v : 0, u);
            var barra = Dialogo.Caja("tec-barra" + (hecho ? " hecha" : "") + (clase.Length > 0 ? " " + clase : ""));
            barra.tooltip = Mayuscula(TextoContador(cont));
            var lleno = Dialogo.Caja("tec-barra-lleno");
            lleno.style.width = Length.Percent(Mathf.Min(100, (float)(x * 100.0 / Math.Max(1, u))));
            barra.Add(lleno);
            barra.Add(Dialogo.Texto($"{Miles(x)} / {Miles(u)}", "tec-barra-texto"));
            return barra;
        }

        /// <summary>Lo que cobraría un Aeda itinerante por la tecnología (el doble del oro de la tarifa de su Era), o null si no las vende.</summary>
        long? PrecioDeAeda(ArbolDeTecnologias T, string id)
        {
            var venta = BloqueDeDatos("AEDAS")?["venta"];
            var era = T.Nodos[id].EraId;
            if (venta == null || T.A.EraDe(era) > ((int?)venta["ordenEraMaximo"] ?? 0)) return null;
            return (long)((TarifaDe(T, era).TryGetValue("oro", out var oro) ? oro : 0) * ((double?)venta["factorOro"] ?? 0));
        }

        /// <summary>Lo que abre una tecnología, en iconos (`abreDe`): tropas, edificios, niveles, recetas, bonus, efecto, metal y canon.</summary>
        VisualElement AbreDe(ArbolDeTecnologias T, string id)
        {
            var fila = Dialogo.Caja("tec-carta-abre");
            VisualElement Ico(VisualElement icono, string nombre, string nivel = null)
            {
                var c0 = Dialogo.Caja("tec-abre-ico");
                c0.tooltip = nombre;
                if (icono != null) c0.Add(icono);
                if (nivel != null) c0.Add(Dialogo.Texto(nivel, "tec-abre-nivel"));
                return c0;
            }
            foreach (var t in T.A.Tropas.Where(x => x.Tecnologia == id)) fila.Add(Ico(IconosDelMundo.Icono("tropa", t.Id, 24, IconosDelMundo.OroClaro), t.Nombre));
            foreach (var e in T.A.EdificiosQueAbre(id)) fila.Add(Ico(IconosDelMundo.Icono("edificio", e, 24, IconosDelMundo.OroClaro), Pieza(e)));
            foreach (var (e, nv) in T.A.NivelesQueAbre(id)) fila.Add(Ico(IconosDelMundo.Icono("edificio", e, 24, IconosDelMundo.OroClaro), $"{Pieza(e)} {nv}", nv.ToString()));
            foreach (var r in T.A.RecetasQueAbre(id)) { var c0 = Ico(IconosDelMundo.Icono("recurso", r, 18), CosasDelMapa.NombreDeRecurso(r)); c0.AddToClassList("tec-abre-rec"); fila.Add(c0); }
            if (T.Nodos[id].D["bonusProduccion"] is JObject bonus)
            {
                var r = (string)bonus["recurso"];
                var c0 = Ico(IconosDelMundo.Icono("recurso", r, 18), $"{CosasDelMapa.NombreDeRecurso(r)} ×{Num((double)bonus["factor"])}");
                c0.AddToClassList("tec-abre-rec");
                c0.Add(Dialogo.Texto("×" + Num((double)bonus["factor"]), "tec-abre-por"));
                fila.Add(c0);
            }
            if (EfectoCorto.TryGetValue(id, out var efecto)) fila.Add(Dialogo.Texto(efecto, "tec-abre-efecto"));
            if (MetalDe(id) is string metal) { var m = Dialogo.Texto("⚒ " + metal, "tec-abre-efecto"); m.tooltip = $"Las piezas de {metal} del héroe, en la Armería"; fila.Add(m); }
            if (CanonCorto.TryGetValue(id, out var canon)) fila.Add(Dialogo.Texto(canon + " · canon", "tec-abre-canon"));
            return fila;
        }

        /// <summary>Las piezas de metal del héroe que abre una tecnología (`OBJETOS.metales`): «acero».</summary>
        string MetalDe(string id) =>
            (BloqueDeDatos("OBJETOS")?["metales"] as JObject)?.Properties().FirstOrDefault(p => (string)p.Value == id)?.Name.Replace('_', ' ');

        Button CartaDeTecnologia(ArbolDeTecnologias T, ArbolDeTecnologias.Estado E, string id, string est)
        {
            var n = T.Nodos[id];
            var carta = new Button { name = "tec-" + id };
            carta.AddToClassList("tec-carta");
            carta.AddToClassList(est);
            carta.tooltip = null;
            var cab = Dialogo.Caja("tec-carta-cab");
            cab.Add(Dialogo.Texto(n.Nombre, "tec-carta-nombre"));
            if (MedallaDeTecnologia.TryGetValue(est, out var medalla)) cab.Add(Dialogo.Texto(medalla, "tec-medalla"));
            carta.Add(cab);
            carta.Add(AbreDe(T, id));
            var cruces = n.Preds.Where(p => T.Nodos[p].Carril != n.Carril).ToList();
            if (cruces.Count > 0)
            {
                var pide = Dialogo.Caja("tec-carta-pide");
                foreach (var p in cruces) pide.Add(MuestraDeLinea("hito", "Pide " + T.Nodos[p].Nombre));
                carta.Add(pide);
            }
            if (n.DeArranque) carta.Add(Dialogo.Texto("DE ARRANQUE", "tec-arranque"));
            else
            {
                var estado = Dialogo.Caja("tec-carta-estado");
                estado.name = "estado-" + id;
                switch (est)
                {
                    case "adoptada": estado.Add(Dialogo.Texto("Adoptada", "tec-estado-texto")); break;
                    case "adoptable":
                        estado.Add(Dialogo.Texto("ADOPTAR", "recl-boton-mini"));
                        foreach (var chip in ChipsDeCoste(TarifaDe(T, n.EraId))) estado.Add(chip);
                        break;
                    case "revelada":
                        var r = E.Reveladas[id];
                        var pr = PrecioDeAeda(T, id);
                        estado.Add(Dialogo.Texto($"Revelada · hito {r.Count(x => x)}/{r.Count}{(pr != null ? " · Aeda" : "")}", "tec-estado-texto"));
                        if (pr != null) estado.Add(ChipDeTec("oro", pr.Value));
                        break;
                    case "logro": estado.Add(Dialogo.Texto("Logro del mundo ✓ · falta tu hito", "tec-estado-texto")); break;
                    case "futura": estado.Add(Dialogo.Texto($"Bloqueada: la Era {Romano[Math.Min(n.Era, Romano.Length - 1)]} aún no empieza", "tec-estado-texto")); break;
                    default:
                        // Sin texto: la barra del logro con «x / y» dentro; la explicación, en la ficha (Víctor, 2026-11-07).
                        var partes = ArbolDeTecnologias.PartesLogro(n.D);
                        if (partes.Count == 0) estado.Add(Dialogo.Texto("Falta el logro del mundo", "tec-estado-texto"));
                        foreach (var x in partes) estado.Add(BarraDeLogro(E, x, ArbolDeTecnologias.ParteCumplida(E, x), "tec-barra-carta"));
                        break;
                }
                carta.Add(estado);
            }
            return carta;
        }

        VisualElement CabeceraDeEraDeTecnologia(ArbolDeTecnologias T, ArbolDeTecnologias.Estado E, ArbolDeTropas.Era x, int eraVisible, int mundo, int ultima)
        {
            var cab = Dialogo.Caja("recl-era-cab tec-era-cab");
            cab.name = "era-" + x.Orden;
            var romano = Romano[Math.Min(x.Orden, Romano.Length - 1)];
            cab.Add(Dialogo.Texto(romano, "recl-era-num"));
            var textos = Dialogo.Caja("recl-era-textos");
            textos.Add(Dialogo.Texto($"Era {romano} · {x.Nombre}", "recl-era-nombre"));
            textos.Add(Dialogo.Texto(DuracionDeEra(x, ultima), "recl-era-sub"));
            textos.Add(Renglon("tec-era-coste", "Adoptar cada una:", ChipsDeCoste(TarifaDe(T, x.Id))));
            if (x.Orden == eraVisible && x.Orden < ultima)
            {
                var llevas = T.AdoptadasEn(E.Adoptadas, x.Orden);
                var cupo = T.CupoEra(x.Orden);
                var sigue = Dialogo.Caja("tec-era-sigue");
                sigue.name = "era-sigue";
                sigue.Add(Dialogo.Texto($"La Era {Romano[Math.Min(x.Orden + 1, Romano.Length - 1)]} aparece con {cupo} de {T.TecsDeEra(x.Orden).Count} adoptadas: llevas <b>{llevas}</b>", "tec-era-sigue-texto"));
                var barra = Dialogo.Caja("tec-era-barra");
                var lleno = Dialogo.Caja("tec-era-barra-lleno");
                lleno.style.width = Length.Percent(100f * Mathf.Min(1, llevas / (float)Math.Max(1, cupo)));
                barra.Add(lleno);
                sigue.Add(barra);
                textos.Add(sigue);
            }
            if (x.Orden == mundo) textos.Add(Dialogo.Texto("ERA DEL MUNDO", "recl-era-marca actual"));
            else if (x.Orden > mundo) textos.Add(Dialogo.Texto("BLOQUEADA", "recl-era-marca futura"));
            cab.Add(textos);
            return cab;
        }

        // ─────────────── la ficha (`tipTec` y las acciones de `fichaAcciones`) ───────────────

        VisualElement HitoEnFicha(ArbolDeTecnologias T, JObject c0, string marca)
        {
            var tipo = (string)c0["tipo"];
            var pre = marca ?? "";
            int Nivel(string campo) => (int?)c0[campo] is int v && v > 0 ? v : 1;
            string TN(string id) => id != null && T.Nodos.TryGetValue(id, out var n) ? n.Nombre : id;
            switch (tipo)
            {
                case "edificio":
                    var ed = (string)c0["edificio"];
                    return HitoDePueblo.TryGetValue(ed, out var pueblo)
                        ? Renglon("", pre, ChipDeEdificio(ed, null, pueblo), pueblo)
                        : Renglon("", pre, ChipDeEdificio(ed, Nivel("nivelInterno").ToString()), "en alguna plaza");
                case "tecnologia": return Renglon("", pre, $"<b>{TN((string)c0["id"])}</b> adoptada");
                case "recursoEnCapital": return Renglon("", pre, ChipDeTec((string)c0["recurso"]), "en el almacén de la capital");
                case "capitalEnNivel": return Renglon("", pre, $"Capital a nivel {(int?)c0["nivel"]} con", ChipDeEdificio((string)c0["conEdificio"]));
                case "yacimientoEnTerritorio": return Renglon("", pre, "Un yacimiento de", ChipDeTec((string)c0["recurso"]), "en tu territorio");
                case "escuadrasVivas": return Renglon("", pre, $"{Miles((double?)c0["cantidad"] ?? 0)} escuadras con soldados entre los héroes de la Facción");
                case "heroesEnFaccion": return Renglon("", pre, $"{Miles((double?)c0["cantidad"] ?? 0)} héroes en la Facción");
                case "plazaEnNivel": return Renglon("", pre, $"Una plaza de la Facción a nivel {(int?)c0["nivel"]}");
                case "nivelFaccion": return Renglon("", pre, $"La Facción a nivel {(int?)c0["nivel"]}");
                case "columnasEnMarcha": return Renglon("", pre, $"{Miles((double?)c0["cantidad"] ?? 0)} columnas de la Facción en marcha a la vez");
                case "edificioEnPlazaDeNivel": return Renglon("", pre, ChipDeEdificio((string)c0["edificio"], Nivel("nivelInterno").ToString()), $"en una plaza de nivel {(int?)c0["nivelPlaza"]}");
                default: return Renglon("", pre, tipo ?? "");
            }
        }

        void MostrarTipDeTecnologia(VisualElement tip, ArbolDeTecnologias T, ArbolDeTecnologias.Estado E, Heroe h, Faccion f, string id, string est, HashSet<string> visibles, bool fijo)
        {
            var A = T.A;
            var n = T.Nodos[id];
            var k = n.D;
            var carril = T.Carriles.First(x => x.Id == n.Carril);
            string TN(string x) => x != null && T.Nodos.TryGetValue(x, out var y) ? y.Nombre : x;
            tip.Clear();
            tip.EnableInClassList("fijo", fijo);
            tip.pickingMode = fijo ? PickingMode.Position : PickingMode.Ignore;

            var cab = Dialogo.Caja("recl-tip-cab");
            if (IconosDelMundo.Icono("edificio", carril.Icono, 34, IconosDelMundo.Bronce) is VisualElement icono) { icono.AddToClassList("tec-tip-icono"); cab.Add(icono); }
            var textos = Dialogo.Caja("recl-tip-textos");
            textos.Add(Dialogo.Texto(n.Nombre, "recl-tip-nombre"));
            var era = A.Eras.FirstOrDefault(x => x.Id == n.EraId);
            textos.Add(Dialogo.Texto($"{carril.Nombre} · Era {Romano[Math.Min(A.EraDe(n.EraId), Romano.Length - 1)]} · {era?.Nombre ?? n.EraId}", "recl-tip-meta"));
            cab.Add(textos);
            tip.Add(cab);

            // El estado en tu Facción.
            string t;
            if (n.DeArranque) t = "<b>Adoptada</b> desde que nació la Facción.";
            else if (est == "adoptada") t = "<b>Adoptada.</b> Todo lo que abre ya está disponible en tus plazas.";
            else if (est == "adoptable") t = "<b>Aparecida.</b> El Rey, dentro de la capital, puede adoptarla ya por:";
            else if (est == "revelada")
            {
                var pr = PrecioDeAeda(T, id);
                t = "<b>Revelada por un Aeda</b> (la descubrió otra Facción). El logro del mundo ya está; falta lo de tu Facción." +
                    (pr != null ? $" Un Aeda itinerante detenido en tu plaza te la vende por {Miles(pr.Value)} de oro (salta lo de tu Facción, no el logro)." : "");
            }
            else if (est == "logro") t = "<b>El logro del mundo ya se cumplió.</b> Aparece en cuanto tu Facción cumpla lo suyo.";
            else if (est == "futura") t = $"<b>Bloqueada:</b> la Era {Romano[Math.Min(n.Era, Romano.Length - 1)]} aún no ha empezado en el mundo. Ya la ves porque tu Facción tiene adoptado el 30 % de la Era anterior.";
            else t = "<b>Aún nadie ha cumplido su logro</b> en el mundo.";
            var caja = Dialogo.Caja("recl-tip-estado tec-tip-estado " + est);
            caja.Add(Dialogo.Texto("En tu Facción", "recl-tip-meta"));
            caja.Add(Dialogo.Texto(t, "recl-tip-texto"));
            if (est == "adoptable") caja.Add(Renglon("", ChipsDeCoste(TarifaDe(T, n.EraId))));
            tip.Add(caja);

            var hito = (k["hito"] as JArray ?? new JArray()).OfType<JObject>().ToList();
            if (n.DeArranque)
            {
                tip.Add(Dialogo.Texto("CÓMO SE CONSIGUE", "recl-tip-seccion"));
                tip.Add(Dialogo.Texto("Toda Facción la tiene adoptada desde que nace.", "recl-tip-texto"));
            }
            else
            {
                // Requisitos (Víctor, 2026-11-06): «Servidor», el logro del mundo como barra x / y; «Facción», el hito.
                tip.Add(Dialogo.Texto("REQUISITOS", "recl-tip-seccion"));
                var partes = ArbolDeTecnologias.PartesLogro(k);
                if (partes.Count > 0)
                {
                    var todo = est == "revelada";
                    tip.Add(Dialogo.Texto("SERVIDOR", "tec-req"));
                    foreach (var x in partes)
                    {
                        tip.Add(BarraDeLogro(E, x, todo || ArbolDeTecnologias.ParteCumplida(E, x)));
                        var porPlaza = (long?)x["porPlaza"];
                        tip.Add(Dialogo.Texto($"{Mayuscula(TextoContador((string)x["contador"]))} por todas las Facciones" +
                            (porPlaza != null ? $" ({Miles(porPlaza.Value)} por cada una de las {Miles(Math.Max(1, E.Plazas))} plazas del mundo)" : ""), "tec-barra-pie"));
                    }
                    var ocultas = A.LogroDeTecnologia(id).Where(visibles.Contains).ToList();
                    if (ocultas.Count > 0) tip.Add(Dialogo.Texto($"Antes alguien del mundo necesita {string.Join(", ", ocultas.Select(TN))}: lo que cuenta este logro lo pide.", "tec-nota"));
                }
                tip.Add(Dialogo.Texto("FACCIÓN", "tec-req"));
                var cumplidas = E.Reveladas.TryGetValue(id, out var r) ? r : null;
                if (hito.Count == 0) tip.Add(Dialogo.Texto("nada más", "recl-tip-texto"));
                for (int i = 0; i < hito.Count; i++)
                {
                    var marca = cumplidas == null ? null : i < cumplidas.Count && cumplidas[i] ? "<color=#7fcf6a>✓</color>" : "<color=#e2675a>✗</color>";
                    tip.Add(HitoEnFicha(T, hito[i], marca));
                }
                var a = n.Acc;
                bool EnHito(string tipo, string campo, string valor) => hito.Any(q => (string)q["tipo"] == tipo && (string)q[campo] == valor);
                var otras = a.Orden.Where(x => visibles.Contains(x) && !EnHito("tecnologia", "id", x) && a.Por[x].Any(m => m != "logro")).ToList();
                var eds = a.Eds.Where(p => !EnHito("edificio", "edificio", p.Key)).ToList();
                var plazaDelHito = hito.Select(q => (string)q["tipo"] == "plazaEnNivel" ? (int?)q["nivel"] ?? 1 : (string)q["tipo"] == "edificioEnPlazaDeNivel" ? (int?)q["nivelPlaza"] ?? 1 : 1).DefaultIfEmpty(1).Max();
                var plazaEdificios = a.Plaza > plazaDelHito ? a.Plaza : 1;
                var piden = new List<string>();
                if (plazaEdificios > 1) piden.Add($"plaza a nivel {plazaEdificios}");
                piden.AddRange(eds.Select(p => $"{Pieza(p.Key)} {p.Value}"));
                if (otras.Count > 0) piden.Add((piden.Count > 0 ? "y " : "") + string.Join(", ", otras.Select(TN)));
                if (piden.Count > 0) tip.Add(Dialogo.Texto($"Esos edificios piden {string.Join(", ", piden)}.", "tec-nota"));
            }

            // Lo que abre.
            var abre = new List<VisualElement>();
            VisualElement Tropa(ArbolDeTropas.Tropa x) { var ic = IconosDelMundo.Icono("tropa", x.Id, 18, IconosDelMundo.OroClaro); var c0 = Dialogo.Caja("tec-rec tec-edi"); c0.tooltip = x.Nombre; if (ic != null) c0.Add(ic); return c0; }
            var tropas = A.Tropas.Where(x => x.Tecnologia == id).ToList();
            var pide = A.Tropas.Where(x => x.Tecnologia != id && A.Nodos.TryGetValue(x.Id, out var tn) && tn.Req.Por.ContainsKey(id)
                && tn.Req.Orden.All(q => !T.Nodos.ContainsKey(q) || visibles.Contains(q))).ToList();
            var recetas = A.RecetasQueAbre(id).ToList();
            var construye = A.EdificiosQueAbre(id).ToList();
            var mejora = A.NivelesQueAbre(id).ToList();
            if (tropas.Count > 0) abre.Add(Renglon("", "Recluta:", tropas.Select(Tropa)));
            if (recetas.Count > 0) abre.Add(Renglon("", "Fabrica:", recetas.Select(x => ChipDeTec(x))));
            if (construye.Count > 0) abre.Add(Renglon("", "Construye:", construye.Select(x => ChipDeEdificio(x))));
            if (mejora.Count > 0) abre.Add(Renglon("", "Mejora:", mejora.Select(x => ChipDeEdificio(x.tipo, x.nivel.ToString()))));
            if (k["bonusProduccion"] is JObject bonus) abre.Add(Renglon("", $"×{Num((double)bonus["factor"])}", ChipDeTec((string)bonus["recurso"])));
            if (id == "instituciones_civicas") abre.Add(Renglon("", "Con la", ChipDeEdificio("salaConsejo"), ": el nivel 4 de plaza (la pide) y +1 política del Gobernador"));
            if (id == "disciplina_formacion") abre.Add(Renglon("", "Las formaciones cerradas (muro de escudos, testudo, cuadro y cuña), para elegirlas en cada escuadra desde Tropas"));
            if (id == "ejercito_permanente") abre.Add(Renglon("", "Las escuadras de los héroes de tu Facción ganan un 10 % más de experiencia; el héroe, no"));
            if (id == "logistica_campana")
            {
                var l = BloqueDeDatos("LOGISTICA");
                var cap = (double?)l?["capacidadViveresPorHeroe"] ?? 350;
                var factor = (double?)l?["bonusViveres"]?["factor"] ?? 1.5;
                abre.Add(Renglon("", $"Los víveres de cada héroe de tu Facción ×{Num(factor)} (de {Num(cap)} a {Math.Round(cap * factor)})"));
            }
            if (MetalDe(id) is string metal) abre.Add(Renglon("", $"Las piezas de {metal} del héroe: comprarlas y forjarlas en la Armería de las plazas de tu Facción"));
            if (id == "carpinteria_militar") abre.Add(Renglon("", "<color=#e0a53a>CANON, SIN EFECTO AÚN</color> Escalas y defensas de campaña: equipo de la", ChipDeEdificio("carpinteria", "2"), ", sin efecto en el combate con números"));
            if (id == "trabajos_asedio") abre.Add(Renglon("", "<color=#e0a53a>CANON, SIN EFECTO AÚN</color> Equipo de asedio de la", ChipDeEdificio("carpinteria", "2"), ", sin efecto en el combate con números"));
            if (pide.Count > 0) abre.Add(Renglon("", "Hace falta para:", pide.Select(Tropa)));
            if (abre.Count > 0) { tip.Add(Dialogo.Texto("ABRE", "recl-tip-seccion")); foreach (var x in abre) tip.Add(x); }

            // La ficha fija: lo que quien mira puede hacer con esa tecnología (`fichaAcciones`). Lo que no puede hacer no sale.
            if (fijo) AccionesDeTecnologia(tip, T, h, f, id, est);
            tip.style.display = DisplayStyle.Flex;
        }

        void AccionesDeTecnologia(VisualElement tip, ArbolDeTecnologias T, Heroe h, Faccion f, string id, string est)
        {
            var soyRey = f.HasReyId && f.ReyId == h.Id;
            if (est != "adoptable" || !soyRey) return;
            var acciones = Dialogo.Caja("tec-ficha-acciones");
            acciones.name = "tec-ficha-acciones";
            var error = Dialogo.Texto("", "error oculto");
            error.name = "error-ficha-tecnologia";
            var capitalId = f.HasCapitalId ? f.CapitalId : null;
            var u = h.Ubicacion;
            var dondeEstoy = u != null && u.DondeCase == UbicacionHeroe.DondeOneofCase.AsentamientoId ? u.AsentamientoId : null;
            var capital = capitalId != null ? PlazaActual(capitalId) : null;
            var motivo = capitalId == null ? "tu Facción aún no tiene capital."
                : dondeEstoy != capitalId ? $"tienes que estar en la capital{(capital != null ? ", " + (capital.HasNombre ? capital.Nombre : capital.Id) : "")}." : null;
            if (motivo != null) acciones.Add(Dialogo.Texto("Para adoptarla: " + motivo, "tec-ficha-nota"));
            else
            {
                var tarifa = TarifaDe(T, T.Nodos[id].EraId);
                var falta = capital == null || tarifa.Any(p => Hay(capital, p.Key) < p.Value);
                Button adoptar = null;
                adoptar = Dialogo.Boton("", "boton-primario tec-ficha-boton", async () =>
                    await OrdenDelPanel(new Orden { AdoptarTecnologia = new OrdenAdoptarTecnologia { FaccionId = f.Id, TecnologiaId = id } }, m => Poner(error, m),
                        () => { firmaFaccion = null; }, adoptar));
                adoptar.name = "adoptar-" + id;
                adoptar.Add(Dialogo.Texto("Adoptar", "boton-texto"));
                foreach (var chip in ChipsDeCoste(tarifa, capital)) adoptar.Add(chip);
                adoptar.SetEnabled(!falta);
                if (falta) adoptar.tooltip = "El almacén de la capital no tiene bastante";
                acciones.Add(adoptar);
            }
            acciones.Add(error);
            tip.Add(acciones);
        }

        /// <summary>La ficha junto a su carta (a la derecha; si no cabe, a la izquierda), como `colocar` sin ratón.</summary>
        static void ColocarTipJunto(VisualElement tip, Rect carta)
        {
            VigilarTip(tip);
            tip.userData = carta;
            if (tip.parent == null || tip.style.display == DisplayStyle.None) return;
            var raiz = tip.parent.worldBound;
            var w = float.IsNaN(tip.layout.width) || tip.layout.width < 1 ? 380 : tip.layout.width;
            var hh = float.IsNaN(tip.layout.height) || tip.layout.height < 1 ? 260 : tip.layout.height;
            var x = carta.xMax - raiz.x + 8;
            var y = carta.yMin - raiz.y;
            if (x + w > raiz.width - 8) x = Mathf.Max(8, carta.xMin - raiz.x - w - 8);
            if (y + hh > raiz.height - 8) y = Mathf.Max(8, raiz.height - hh - 8);
            tip.style.left = x;
            tip.style.top = y;
        }
    }
}

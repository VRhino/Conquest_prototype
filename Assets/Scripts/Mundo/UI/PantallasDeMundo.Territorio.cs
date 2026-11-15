// La ventana Territorio del riel de la plaza (paso 9 del bloque 4, tanda 1), calcada de BronzeAgeClient (`ui/pueblos.ts`: `htmlPueblos`,
// `htmlZona`, `htmlMarcar`, `tarjetaPueblo`, `tarjetaObra`; `ui/panelFundacionDePlaza.ts`). Dos subpestañas: «Pueblos y aldeas» (la zona de
// influencia, el cupo, la fila de pueblos y obras con su detalle debajo, marcar una aldea eligiendo el sitio en el mapa 2D o un extractor sobre
// un nodo libre, mejorar, reponer el carro, destruir y cancelar) y «Fundación» (lanzar y desarmar la Caravana de Fundación). De visita en una
// plaza ajena, solo la primera y solo para mirar. Las avanzadas (la columna de la derecha) llegan en la tanda 2.
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
    public partial class PantallasDeMundo
    {
        /// <summary>Lo abierto bajo la fila de pueblos: el id de un pueblo o una obra, «marcar», o null. Sin tocar: «marcar» si no hay ninguno.</summary>
        string abiertoEnTerritorio;
        bool abiertoTocado;
        string plazaDeTerritorio, nodoElegido, cultivoElegido = "trigo";
        readonly Dictionary<string, RasterDeMundo> rastersDeFertilidad = new Dictionary<string, RasterDeMundo>();
        RasterDeMundo rasterTerreno;
        Func<double, double, bool> transitable;

        JToken ParamsPueblos => c.Datos.Bloque("Economia_Construccion", "PUEBLOS_EXTRACTORES");

        /// <summary>Los cultivos de una aldea, en el orden en que se ofrecen (`CULTIVOS` del prototipo); el nivel y lo que dan, de los datos.</summary>
        static readonly (string id, string nombre, string conArticulo, string aldea, string produce, string uso)[] Cultivos =
        {
            ("trigo", "Trigo", "el trigo", "Aldea de trigo", "trigo", "Trigo para la plaza. Sin corrales: el rebaño es de la aldea ganadera."),
            ("vid", "Vid", "la vid", "Aldea de viñas", "uva", "Uva para la bodega, que hace el vino. Sin corrales."),
            ("oliva", "Olivo", "el olivo", "Aldea de olivos", "aceituna", "Aceituna para la prensa, que hace el aceite. Sin corrales."),
            ("cebada", "Cebada", "la cebada", "Aldea de cebada", "cebada", "Cebada para la bodega, que hace la cerveza. Sin corrales."),
            ("sesamo", "Sésamo", "el sésamo", "Aldea de sésamo", "sesamo", "Sésamo para la prensa, que hace aceite. Sin corrales."),
            ("ganado", "Ganado", "el ganado", "Aldea ganadera", "livestock", "Corrales con un rebaño que crece solo y da ganado, carne y leche. Sin granjas: pasta en la tierra del trigo."),
        };

        static readonly HashSet<string> NodosExtraibles = new HashSet<string> { "piedra", "cobre", "estano", "hierro", "oro" };
        static readonly Dictionary<string, string> NombreDeNodo = new Dictionary<string, string>
            { ["piedra"] = "Piedra", ["cobre"] = "Cobre", ["estano"] = "Estaño", ["hierro"] = "Hierro", ["oro"] = "Oro", ["ganado"] = "Ganado", ["livestock"] = "Ganado" };

        static (double x, double y) Pt(Punto p) => p == null ? (0, 0) : (p.X / 1000.0, p.Y / 1000.0);
        static double Dist((double x, double y) a, (double x, double y) b) => Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y));
        static bool EsAldea(Pueblo x) => x.TipoExtractor == "aldeaAgricola";
        static int NivelDePueblo(Pueblo x) => x.HasNivelAldea ? (int)x.NivelAldea : 1;
        static string CultivoDe(Pueblo x) => Cultivos.Any(k => k.id == x.Cultivo) ? x.Cultivo : "trigo";
        int NivelMadreDeCultivo(string id) => (int?)ParamsPueblos?["aldea"]?["cultivos"]?[id]?["nivelMadre"] ?? (id == "vid" || id == "cebada" ? 3 : id == "oliva" || id == "sesamo" ? 4 : 1);
        string ProduceDe(string id) => (string)ParamsPueblos?["aldea"]?["cultivos"]?[id]?["produce"] ?? Cultivos.First(k => k.id == id).produce;
        bool ConGranjas(string id) => (bool?)ParamsPueblos?["aldea"]?["cultivos"]?[id]?["granjas"] ?? id != "ganado";
        bool ConCorrales(string id) => (bool?)ParamsPueblos?["aldea"]?["cultivos"]?[id]?["corrales"] ?? id == "ganado";
        string NombreSinNivel(Pueblo x) => EsAldea(x) ? Cultivos.First(k => k.id == CultivoDe(x)).aldea : Edificios.Nombre(x.TipoExtractor);

        /// <summary>Los pueblos activos y las obras de pueblo o aldea de esta plaza (las de avanzada van aparte).</summary>
        (List<Pueblo> pueblos, List<ObraDeMapa> obras) DeLaPlaza(Asentamiento a)
        {
            var al = c.Sesion.Almacen;
            var pueblos = al.Todas(Entidad.CuerpoOneofCase.Pueblo).Select(e => e.Pueblo).Where(x => x.AsentamientoMadreId == a.Id).OrderBy(x => x.Id).ToList();
            var obras = al.Todas(Entidad.CuerpoOneofCase.Obra).Select(e => e.Obra)
                .Where(o => o.AsentamientoMadreId == a.Id && (o.Tipo == TipoObra.ObraPueblo || o.Tipo == TipoObra.ObraAldea || o.Tipo == TipoObra.ObraMejoraAldea)).OrderBy(o => o.Id).ToList();
            return (pueblos, obras);
        }

        /// <summary>El cupo de pueblos (`PUEBLOS_EXTRACTORES.cupoPorNivel` del nivel operativo): los activos y las obras, sin las mejoras de aldea.</summary>
        (int usados, int? total) CupoDePueblos(Asentamiento a)
        {
            var (pueblos, obras) = DeLaPlaza(a);
            var total = (int?)ParamsPueblos?["cupoPorNivel"]?[(a.NivelActual > 0 ? a.NivelActual : a.Nivel).ToString()];
            return (pueblos.Count + obras.Count(o => o.Tipo != TipoObra.ObraMejoraAldea), total);
        }

        List<(double x, double y)> ZonaDe(Asentamiento a) => a.Zona.Select(Pt).ToList();

        /// <summary>Lo que ocupa un nodo o una parcela: los edificios que lo explotan y los pueblos y obras.</summary>
        HashSet<string> FuentesOcupadas()
        {
            var al = c.Sesion.Almacen;
            var r = new HashSet<string>();
            foreach (var e in al.Todas(Entidad.CuerpoOneofCase.Asentamiento)) foreach (var ed in e.Asentamiento.Edificios) if (ed.HasFuenteId) r.Add(ed.FuenteId);
            foreach (var e in al.Todas(Entidad.CuerpoOneofCase.Pueblo)) if (!string.IsNullOrEmpty(e.Pueblo.NodoId)) r.Add(e.Pueblo.NodoId);
            foreach (var e in al.Todas(Entidad.CuerpoOneofCase.Obra)) if (e.Obra.HasNodoId) r.Add(e.Obra.NodoId);
            return r;
        }

        /// <summary>Los nodos de piedra o mineral de la zona real sin pueblo ni obra, del más cercano al más lejano.</summary>
        List<(NodoRecurso n, double d)> NodosLibres(Asentamiento a)
        {
            var zona = ZonaDe(a);
            if (zona.Count < 3) return new List<(NodoRecurso, double)>();
            var ocupados = FuentesOcupadas();
            return c.Artefacto.Nodos.Where(n => NodosExtraibles.Contains(n.Tipo) && !ocupados.Contains(NodosDelMotor.Id(c.Artefacto, n)) && SitiosDeAldea.Dentro(Pt(n.Posicion).x, Pt(n.Posicion).y, zona))
                .Select(n => (n, Dist(Pt(n.Posicion), Pt(a.Posicion)))).OrderBy(x => x.Item2).ToList();
        }

        /// <summary>El nodo del artefacto de un id del motor (`nodo-N`).</summary>
        NodoRecurso NodoDelMotor(string id) => string.IsNullOrEmpty(id) ? null : c.Artefacto.Nodos.FirstOrDefault(n => NodosDelMotor.Id(c.Artefacto, n) == id);

        /// <summary>¿Gestiona los pueblos? El Gobernador o el Maestro de Obras, residente y dentro (`gestiona`).</summary>
        bool GestionaPueblos(Asentamiento a, Heroe yo) => CargoConstructor(a, yo.Id) != null && yo.HasResidenciaId && yo.ResidenciaId == a.Id;

        /// <summary>Lo que cambia la ventana: los pueblos, las obras y las caravanas de la plaza (cada uno con su versión).</summary>
        string FirmaDeTerritorio(Asentamiento a)
        {
            var al = c.Sesion.Almacen;
            var (pueblos, obras) = DeLaPlaza(a);
            var caravanas = al.Todas(Entidad.CuerpoOneofCase.Caravana).Select(e => e.Caravana).Where(k => k.OrigenAsentamientoId == a.Id);
            return string.Join(",", pueblos.Select(x => x.Id + ":" + x.Version + ":" + x.VersionInterior)) + "|" + string.Join(",", obras.Select(o => o.Id + ":" + o.Version))
                + "|" + string.Join(",", caravanas.Select(k => k.Id + ":" + k.Version)) + "|" + abiertoEnTerritorio + "|" + cultivoElegido + "|" + nodoElegido + "|" + FirmaDeAvanzadas(a);
        }

        /// <summary>Los cultivos que puede tener la siguiente aldea: los que ya desbloqueó su Facción (el mayor nivel ALCANZADO de sus plazas).</summary>
        List<(string id, string nombre, string conArticulo, string aldea, string produce, string uso)> CultivosPosibles(Asentamiento a)
        {
            var nivel = a.Nivel;
            foreach (var e in c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Asentamiento))
                if (e.Asentamiento.FaccionId == a.FaccionId) nivel = Math.Max(nivel, e.Asentamiento.Nivel);
            return Cultivos.Where(k => nivel >= NivelMadreDeCultivo(k.id)).ToList();
        }

        /// <summary>El anillo de la siguiente aldea (`anilloDeAldea`): la k-ésima va en el anillo que añade el primer nivel cuyo cupo llega a k.</summary>
        (double desde, double hasta) AnilloDeAldea(Asentamiento a)
        {
            var (pueblos, obras) = DeLaPlaza(a);
            var k = pueblos.Count(EsAldea) + obras.Count(o => o.Tipo == TipoObra.ObraAldea) + 1;
            var techos = (c.Datos.En("reglas.territorio.techos") as JArray)?.Select(t => (double)t / 1000).ToList() ?? new List<double> { 10, 15, 20, 25, 30 };
            var cupos = ParamsPueblos?["cupoPorNivel"] as JObject;
            var nivel = techos.Count;
            if (cupos != null)
                foreach (var n in cupos.Properties().Select(p => int.Parse(p.Name)).OrderBy(n => n))
                    if ((int)cupos[n.ToString()] >= k) { nivel = n; break; }
            nivel = Math.Min(techos.Count, Math.Max(1, nivel));
            return (nivel == 1 ? 0 : techos[nivel - 2], techos[nivel - 1]);
        }

        RasterDeMundo RasterDeFertilidad(string cultivo)
        {
            var capa = cultivo == "ganado" ? "trigo" : cultivo;
            if (rastersDeFertilidad.TryGetValue(capa, out var r)) return r;
            var a = c.Artefacto;
            Raster raster = capa switch { "vid" => a.FertilidadVid, "oliva" => a.FertilidadOliva, "cebada" => a.FertilidadCebada, "sesamo" => a.FertilidadSesamo, _ => a.Fertilidad };
            return rastersDeFertilidad[capa] = raster != null && raster.Encoding == EncodingRaster.EncodingU8 ? new RasterDeMundo(raster) : null;
        }

        /// <summary>Dónde cabe la aldea de ese cultivo en esta plaza (`sitiosDeLaPlaza`).</summary>
        SitiosDeAldea SitiosDeLaPlaza(Asentamiento a, string cultivo)
        {
            var f = RasterDeFertilidad(cultivo);
            if (f == null) return null;
            rasterTerreno ??= new RasterDeMundo(c.Artefacto.Terreno);
            transitable ??= SitiosDeAldea.Transitable(c.Artefacto.Transitable);
            var al = c.Sesion.Almacen;
            var ocupados = al.Todas(Entidad.CuerpoOneofCase.Asentamiento).Select(e => Pt(e.Asentamiento.Posicion))
                .Concat(al.Todas(Entidad.CuerpoOneofCase.FichaConocida).Select(e => Pt(e.FichaConocida.Posicion)))
                .Concat(al.Todas(Entidad.CuerpoOneofCase.Pueblo).Select(e => Pt(e.Pueblo.Posicion)))
                .Concat(al.Todas(Entidad.CuerpoOneofCase.Obra).Where(e => e.Obra.Tipo != TipoObra.ObraMejoraAldea).Select(e => Pt(e.Obra.Posicion)));
            return SitiosDeAldea.Buscar(f, rasterTerreno, ZonaDe(a), Pt(a.Posicion), AnilloDeAldea(a), ocupados,
                (double?)ParamsPueblos?["aldea"]?["fertilidadMinima"] ?? 0.3, (double?)ParamsPueblos?["aldea"]?["separacionMinima"] ?? 3, transitable);
        }

        /// <summary>El carro de acarreo con su buey (un carro básico de la flota), que toda obra de pueblo se lleva además de lo suyo.</summary>
        JObject CosteCarro() => Sumar(c.Datos.Bloque("Comercio_Caravanas", "CARRO_CATALOGO")?["basico"]?["costo"] as JObject,
            c.Datos.Bloque("Comercio_Caravanas", "ANIMAL_CATALOGO")?["buey"]?["costo"] as JObject);

        static JObject Sumar(params JObject[] xs)
        {
            var r = new JObject();
            foreach (var x in xs)
                if (x != null) foreach (var p in x.Properties()) r[p.Name] = ((double?)r[p.Name] ?? 0) + (double)p.Value;
            return r;
        }

        /// <summary>Un coste en chips con su icono, sin comparar con el almacén (lo que se lleva al sitio: `costeLlano`).</summary>
        static VisualElement CosteLlano(JObject coste)
        {
            var fila = Dialogo.Caja("plaza-chips");
            foreach (var p in coste?.Properties() ?? Enumerable.Empty<JProperty>())
            {
                var n = Math.Ceiling((double)p.Value);
                if (n <= 0) continue;
                var chip = Dialogo.Caja("plaza-coste");
                chip.tooltip = $"{n} {CosasDelMapa.NombreDeRecurso(p.Name)}";
                chip.Add(IconosDelMundo.Icono("recurso", p.Name, 14));
                chip.Add(Dialogo.Texto(n.ToString(), "plaza-coste-cifra"));
                fila.Add(chip);
            }
            return fila;
        }

        /// <summary>Unas casillas, `llenas` de `total` (el cupo).</summary>
        static VisualElement Casillas(int llenas, int total, string clase, string llena)
        {
            var fila = Dialogo.Caja(clase);
            for (int i = 0; i < total; i++) fila.Add(Dialogo.Caja(i < llenas ? llena : ""));
            return fila;
        }

        // ─────────────────────────── «Pueblos y aldeas» ───────────────────────────

        void Territorio(VisualElement cuerpo, Asentamiento a, Heroe yo, Action rehacer)
        {
            if (plazaDeTerritorio != a.Id) { plazaDeTerritorio = a.Id; abiertoTocado = false; abiertoEnTerritorio = null; }
            var (pueblos, obras) = DeLaPlaza(a);
            var gestiona = Propia(a, yo) && GestionaPueblos(a, yo);
            var (usados, total) = CupoDePueblos(a);
            var lleno = total != null && usados >= total;
            var puedeMarcar = gestiona && !lleno;
            var ids = new HashSet<string>(pueblos.Select(x => x.Id).Concat(obras.Select(o => o.Id)));
            var abierto = !abiertoTocado ? (ids.Count == 0 && puedeMarcar ? "marcar" : null) : abiertoEnTerritorio;
            if (abierto == "marcar" ? !puedeMarcar : abierto != null && !ids.Contains(abierto)) abierto = null;

            var col = Dialogo.Caja("territorio-col");
            col.name = "territorio";
            col.Add(TituloDeSeccion("Zona de influencia", "Lo que cae ahora dentro de la zona de la plaza (crece con su nivel y con las fronteras de las vecinas). Un nodo lo ocupa un pueblo o un edificio que lo explota; una parcela de bosque, una Leñera. Las casillas verdes son los sitios libres."));
            col.Add(Zona(a));
            var cupo = ChipConIcono(IconosDelMundo.Icono("glifo", "pueblos", 13), $"{usados} / {(total?.ToString() ?? "—")}", lleno ? "lleno" : "", "Cupo de pueblos por el nivel de la plaza");
            cupo.name = "cupo-pueblos";
            if (total > 0) cupo.Add(Casillas(usados, total.Value, "territorio-cupo-casillas", "usada"));
            col.Add(TituloDeSeccion("Pueblos", "Un pueblo es de la plaza: se marca aquí, una columna de tu Facción lleva sus materiales hasta la obra en el mapa y los entrega a su puerta (cualquier ciudadano de la columna), y con todo entregado se levanta. Su carro acarrea lo que produce a la plaza. Pulsa un pueblo para ver su detalle, y «+» para marcar uno nuevo.", cupo));

            var fila = Dialogo.Caja("territorio-iconos");
            void Abrir(string id) { abiertoTocado = true; abiertoEnTerritorio = abierto == id ? null : id; rehacer(); }
            if (puedeMarcar)
            {
                var mas = Dialogo.Boton("+", "territorio-icono-boton territorio-marcar" + (abierto == "marcar" ? " abierto" : ""), () => Abrir("marcar"));
                mas.name = "pueblo-abrir-marcar";
                mas.tooltip = "Marcar un pueblo nuevo";
                fila.Add(mas);
            }
            foreach (var x in pueblos)
            {
                var id = x.Id;
                var b = Dialogo.Boton("", "territorio-icono-boton" + (x.HasBatallaId ? " atacado" : "") + (abierto == id ? " abierto" : ""), () => Abrir(id));
                b.name = "pueblo-abrir-" + id;
                b.tooltip = NombreSinNivel(x) + (x.HasBatallaId ? " · ¡atacado!" : "");
                b.Add(EmblemaDePueblo(x, 22));
                b.Add(Dialogo.Texto(NivelDePueblo(x).ToString(), "territorio-icono-nivel"));
                if (x.HasBatallaId) b.Add(IconosDelMundo.Icono("glifo", "atacar", 11));
                fila.Add(b);
            }
            foreach (var o in obras)
            {
                var id = o.Id;
                var levantando = o.HasTerminaEnMs;
                var completa = !levantando && Faltan(o).Count == 0;
                var b = Dialogo.Boton("", "territorio-icono-boton obra " + (levantando ? "levantando" : completa ? "completa" : "pendiente") + (abierto == id ? " abierto" : ""), () => Abrir(id));
                b.name = "pueblo-abrir-" + id;
                b.tooltip = $"{NombreDeObra(o)} · {(levantando ? "levantándose" : completa ? "con todos los materiales" : "faltan materiales")}";
                b.Add(IconoDeObra(o, 22));
                fila.Add(b);
            }
            if (fila.childCount > 0) col.Add(fila);
            if (ids.Count == 0 && !puedeMarcar) col.Add(Dialogo.Texto("Esta plaza aún no tiene pueblos.", "plaza-nota"));
            var x0 = pueblos.FirstOrDefault(p => p.Id == abierto);
            var o0 = obras.FirstOrDefault(p => p.Id == abierto);
            var detalle = abierto == "marcar" ? Marcar(a, yo, rehacer) : x0 != null ? TarjetaPueblo(a, x0, gestiona, obras, yo) : o0 != null ? TarjetaObra(a, o0, gestiona, yo) : null;
            if (detalle != null)
            {
                var caja = Dialogo.Caja("territorio-detalle");
                caja.name = "territorio-detalle";
                caja.Add(detalle);
                col.Add(caja);
            }
            if (!gestiona) col.Add(Dialogo.Texto("Los pueblos los marca, mejora y destruye el Gobernador o el Maestro de Obras, residente y dentro de la plaza.", "plaza-nota"));
            else if (lleno && abierto == null) col.Add(Dialogo.Texto("Cupo lleno: para marcar otro pueblo, sube la plaza de nivel o destruye uno.", "plaza-nota"));
            var cols = Dialogo.Caja("territorio-cols");
            cols.Add(col);
            // A ventana completa: la zona, los pueblos y marcar uno a la izquierda; las avanzadas a la derecha (solo en una plaza de tu Facción).
            if (Propia(a, yo)) cols.Add(AvanzadasDeLaPlaza(a, yo, gestiona, rehacer));
            cuerpo.Add(cols);
        }

        /// <summary>La zona de influencia (`htmlZona`): una ficha por tipo de nodo y otra para el bosque, con los libres en grande y una casilla por sitio.</summary>
        VisualElement Zona(Asentamiento a)
        {
            var zona = ZonaDe(a);
            if (zona.Count < 3) return Dialogo.Texto("La zona de la plaza aún no ha llegado.", "plaza-nota");
            var ocupadas = FuentesOcupadas();
            var porTipo = new SortedDictionary<string, (int libres, int ocupados)>(StringComparer.Ordinal);
            foreach (var n in c.Artefacto.Nodos)
            {
                var (nx, ny) = Pt(n.Posicion);
                if (!SitiosDeAldea.Dentro(nx, ny, zona)) continue;
                porTipo.TryGetValue(n.Tipo, out var t);
                if (ocupadas.Contains(NodosDelMotor.Id(c.Artefacto, n))) t.ocupados++; else t.libres++;
                porTipo[n.Tipo] = t;
            }
            var parcelas = c.Artefacto.Parcelas.Where(p => SitiosDeAldea.Dentro(Pt(p.Centro).x, Pt(p.Centro).y, zona)).ToList();
            var usadas = parcelas.Count(p => ocupadas.Contains(p.Id));
            var fichas = Dialogo.Caja("territorio-zona");
            fichas.name = "zona-de-influencia";
            VisualElement Ficha(string id, VisualElement icono, string nombre, int libres, int ocupados, string ocupa)
            {
                var total = libres + ocupados;
                var f = Dialogo.Caja("territorio-zona-ficha" + (libres > 0 ? "" : " agotada"));
                f.name = "zona-" + id;
                f.tooltip = $"{nombre}: {libres} libre{(libres == 1 ? "" : "s")} de {total}. Lo ocupa {ocupa}.";
                if (icono != null) f.Add(icono);
                f.Add(Dialogo.Texto(nombre.ToUpperInvariant(), "territorio-zona-nombre"));
                var cifras = Dialogo.Caja("territorio-zona-libres");
                cifras.Add(Dialogo.Texto(libres.ToString(), "territorio-zona-n"));
                cifras.Add(Dialogo.Texto($"libre{(libres == 1 ? "" : "s")} de {total}", "territorio-zona-de"));
                f.Add(cifras);
                if (total <= 12)
                {
                    var casillas = Dialogo.Caja("territorio-zona-casillas");
                    for (int i = 0; i < total; i++) casillas.Add(Dialogo.Caja(i < libres ? "libre" : ""));
                    f.Add(casillas);
                }
                else f.Add(BarraAvance(total > 0 ? libres / (float)total : 0));
                return f;
            }
            foreach (var kv in porTipo)
            {
                var icono = kv.Key == "ganado" || kv.Key == "livestock" ? IconosDelMundo.Icono("recurso", "livestock", 22) : IconosDelMundo.Icono("recurso", kv.Key, 22);
                fichas.Add(Ficha(kv.Key, icono, NombreDeNodo.TryGetValue(kv.Key, out var n) ? n : kv.Key, kv.Value.libres, kv.Value.ocupados,
                    kv.Key == "ganado" || kv.Key == "livestock" ? "un edificio" : "un pueblo o un edificio"));
            }
            fichas.Add(Ficha("bosque", IconosDelMundo.Icono("glifo", "bosque", 22), "Bosque", parcelas.Count - usadas, usadas, "una Leñera"));
            return fichas;
        }

        VisualElement EmblemaDePueblo(Pueblo x, float lado) =>
            EsAldea(x) ? IconosDelMundo.Icono("recurso", ProduceDe(CultivoDe(x)), lado) : IconosDelMundo.Icono("edificio", x.TipoExtractor, lado, IconosDelMundo.OroClaro);

        /// <summary>El icono de una obra (`ICONO_OBRA`: ⛏ pueblo, 🌾 aldea, ⬆ mejora…): el del edificio, el del cultivo o el de las obras.</summary>
        VisualElement IconoDeObra(ObraDeMapa o, float lado)
        {
            switch (o.Tipo)
            {
                case TipoObra.ObraPueblo: return IconosDelMundo.Icono("glifo", "pico", lado);
                case TipoObra.ObraAldea: return IconosDelMundo.Icono("glifo", "viveres", lado);
                case TipoObra.ObraAvanzada: return IconosDelMundo.Icono("simbolo", "torre", lado, IconosDelMundo.OroClaro);
                case TipoObra.ObraReparacion: return IconosDelMundo.Icono("glifo", "mantenimiento", lado);
                default: return IconosDelMundo.Icono("glifo", "obra", lado);
            }
        }

        static Dictionary<string, long> Faltan(ObraDeMapa o) => o.Requerido.PorId
            .Select(kv => (kv.Key, n: kv.Value - (o.Entregado != null && o.Entregado.PorId.TryGetValue(kv.Key, out var e) ? e : 0)))
            .Where(x => x.n > 0).ToDictionary(x => x.Key, x => x.n);

        string NombreDeObra(ObraDeMapa o) => o == null ? "Obra" : CosasDelMapa.NombreDeObra(o.Tipo);

        /// <summary>Marcar un pueblo nuevo (`htmlMarcar`): dos cartas, la aldea (con su cultivo, coste, anillo y si hay tierra) y el extractor (los
        /// nodos libres para elegir).</summary>
        VisualElement Marcar(Asentamiento a, Heroe yo, Action rehacer)
        {
            var caja = Dialogo.Caja("");
            caja.name = "marcar";
            caja.Add(TituloDeSeccion("Marcar un pueblo nuevo", null));
            var nivel = a.NivelActual > 0 ? a.NivelActual : a.Nivel;
            var nivelAldea = (int?)ParamsPueblos?["nivelMadreMinimo"]?["aldea"] ?? 1;
            var nivelExtractor = (int?)ParamsPueblos?["nivelMadreMinimo"]?["extractor"] ?? 1;
            var libres = nivel >= nivelExtractor ? NodosLibres(a) : new List<(NodoRecurso n, double d)>();
            if (!libres.Any(n => NodosDelMotor.Id(c.Artefacto, n.n) == nodoElegido)) nodoElegido = libres.Count > 0 ? NodosDelMotor.Id(c.Artefacto, libres[0].n) : null;
            var sinAldea = nivel < nivelAldea;
            var posibles = CultivosPosibles(a);
            var cultivo = posibles.FirstOrDefault(k => k.id == cultivoElegido);
            if (cultivo.id == null) cultivo = Cultivos[0];
            var cartas = Dialogo.Caja("territorio-opciones");

            // La aldea.
            var aldea = Dialogo.Caja("territorio-opcion" + (sinAldea ? " bloqueada" : ""));
            aldea.name = "marcar-aldea";
            aldea.Add(IconosDelMundo.Icono("recurso", ProduceDe(cultivo.id), 30));
            aldea.Add(Dialogo.Texto(cultivo.id == "ganado" ? "Aldea ganadera" : "Aldea agrícola", "territorio-opcion-titulo"));
            if (!sinAldea && posibles.Count > 1)
            {
                var radios = Dialogo.Caja("territorio-cultivos");
                foreach (var k in posibles)
                {
                    var id = k.id;
                    var r = Dialogo.Boton("", "territorio-nodo territorio-cultivo" + (id == cultivo.id ? " elegido" : ""), () => { cultivoElegido = id; rehacer(); });
                    r.name = "cultivo-" + id;
                    r.Add(IconosDelMundo.Icono("recurso", ProduceDe(id), 16));
                    r.Add(Dialogo.Texto(k.nombre, "territorio-nodo-nombre"));
                    radios.Add(r);
                }
                aldea.Add(radios);
            }
            aldea.Add(Dialogo.Texto(cultivo.uso, "territorio-opcion-uso"));
            if (sinAldea) aldea.Add(Dialogo.Texto($"Desde el nivel {nivelAldea} de la plaza", "plaza-anillo-opcion-pide"));
            else
            {
                var coste = Sumar(ParamsPueblos?["aldea"]?["niveles"]?["1"]?["coste"] as JObject, ParamsPueblos?["costeAdicional"] as JObject, CosteCarro());
                var chips = CosteLlano(coste);
                chips.tooltip = "Con el carro de acarreo y su buey; se lleva al sitio";
                aldea.Add(chips);
                var (desde, hasta) = AnilloDeAldea(a);
                aldea.Add(IconosDelMundo.ConIcono("glifo", "sitio", Dialogo.Texto($"{(desde > 0 ? $"de {Math.Floor(desde)} a " : "hasta ")}{Math.Floor(hasta)} u de la plaza", "territorio-opcion-uso"), 13));
                var sitios = SitiosDeLaPlaza(a, cultivo.id);
                if (sitios != null)
                {
                    var minima = (double?)ParamsPueblos?["aldea"]?["fertilidadMinima"] ?? 0.3;
                    var separacion = (double?)ParamsPueblos?["aldea"]?["separacionMinima"] ?? 3;
                    var (texto, ok) = sitios.Validos.Count > 0 ? ($"Hay tierra buena para {cultivo.conArticulo} en su anillo (mejor fertilidad {sitios.Mejor.ToString("0.00", Es)}).", true)
                        : sitios.Cerca.Count > 0 ? ($"La tierra fértil de la zona está a menos de {separacion} de una plaza o un pueblo. Crecerá la zona con los edificios.", false)
                        : ($"El anillo de la siguiente aldea no tiene tierra para {cultivo.conArticulo} con fertilidad {minima.ToString("0.00", Es)} o más dentro de la zona: no cabe. La zona crece con cada edificio terminado.", false);
                    var gate = Dialogo.Texto(texto, "territorio-gate " + (ok ? "ok" : "falta"));
                    gate.name = "sitios-de-aldea";
                    aldea.Add(gate);
                }
            }
            var elegir = Dialogo.Boton("", "territorio-boton principal", () => ElegirSitioDeAldea(a, yo, cultivo));
            elegir.name = "elegir-aldea";
            elegir.Add(IconosDelMundo.Icono("glifo", "mapa", 16));
            elegir.Add(Dialogo.Texto("Elegir el sitio", "territorio-boton-texto"));
            elegir.SetEnabled(!sinAldea);
            aldea.Add(elegir);
            cartas.Add(aldea);

            // El extractor.
            var sinExtractor = nivel < nivelExtractor || libres.Count == 0;
            var ext = Dialogo.Caja("territorio-opcion" + (sinExtractor ? " bloqueada" : ""));
            ext.name = "marcar-extractor";
            ext.Add(IconosDelMundo.Icono("glifo", "pico", 30));
            ext.Add(Dialogo.Texto("Extractor", "territorio-opcion-titulo"));
            ext.Add(Dialogo.Texto("Cantera o mina sobre un nodo libre.", "territorio-opcion-uso"));
            if (nivel < nivelExtractor) ext.Add(Dialogo.Texto($"Desde el nivel {nivelExtractor} de la plaza", "plaza-anillo-opcion-pide"));
            else if (libres.Count == 0) ext.Add(Dialogo.Texto("No hay nodos de piedra ni de mineral libres en la zona", "plaza-anillo-opcion-pide"));
            else
            {
                var nodos = Dialogo.Caja("territorio-nodos");
                foreach (var (n, d) in libres)
                {
                    var id = NodosDelMotor.Id(c.Artefacto, n);
                    var r = Dialogo.Boton("", "territorio-nodo" + (id == nodoElegido ? " elegido" : ""), () => { nodoElegido = id; rehacer(); });
                    r.name = "nodo-" + id;
                    r.Add(IconosDelMundo.Icono("recurso", n.Tipo, 16));
                    r.Add(Dialogo.Texto(CosasDelMapa.NombreDeRecurso(n.Tipo), "territorio-nodo-nombre"));
                    r.Add(Dialogo.Texto($"a {Math.Floor(d)} u", "territorio-nodo-d"));
                    nodos.Add(r);
                }
                ext.Add(nodos);
            }
            Button marcar = null;
            marcar = Dialogo.Boton("", "territorio-boton principal", async () =>
            {
                if (nodoElegido == null) return;
                if (await OrdenDePlaza(new Orden { MarcarPueblo = new OrdenMarcarPueblo { AsentamientoId = a.Id, HeroeId = yo.Id, NodoId = nodoElegido } }, marcar))
                { abiertoTocado = true; abiertoEnTerritorio = null; rehacer(); }
            });
            marcar.name = "marcar-nodo";
            marcar.Add(IconosDelMundo.Icono("glifo", "pico", 14));
            marcar.Add(Dialogo.Texto("Marcar", "territorio-boton-texto"));
            marcar.SetEnabled(!sinExtractor);
            ext.Add(marcar);
            cartas.Add(ext);
            caja.Add(cartas);
            return caja;
        }

        /// <summary>«Elegir el sitio» de la aldea: el mapa 2D eligiendo un punto, con la zona de la plaza, los sitios de su cultivo y su capa de fertilidad.</summary>
        void ElegirSitioDeAldea(Asentamiento a, Heroe yo, (string id, string nombre, string conArticulo, string aldea, string produce, string uso) cultivo)
        {
            var sitios = SitiosDeLaPlaza(a, cultivo.id);
            var sinCapa = RasterDeFertilidad(cultivo.id) == null;
            var texto = sinCapa
                ? $"Pulsa en el mapa donde levantar la {cultivo.aldea.ToLowerInvariant()}."
                : $"Pulsa en el mapa donde levantar la {cultivo.aldea.ToLowerInvariant()}: en verde, la tierra buena para {cultivo.conArticulo} de la zona donde cabe (en gris, buena pero pegada a una plaza o un pueblo).";
            var plazaId = a.Id;
            var eleccion = new EleccionEnMapa
            {
                Texto = texto, Zona = ZonaDe(a), SitiosValidos = sitios?.Validos, SitiosCerca = sitios?.Cerca, PasoSitios = SitiosDeAldea.Paso,
                AlElegir = async (x, y) =>
                {
                    var orden = new Orden { MarcarAldea = new OrdenMarcarAldea { AsentamientoId = plazaId, HeroeId = yo.Id, Punto = new Punto { X = (long)Math.Round(x * 1000), Y = (long)Math.Round(y * 1000) } } };
                    if (cultivo.id != "trigo") orden.MarcarAldea.Cultivo = cultivo.id;
                    try
                    {
                        var r = await c.Sesion.Ordenar(orden);
                        if (r.Resultado == Resultado.OrdenAceptada) { abiertoTocado = true; abiertoEnTerritorio = null; return null; }
                        return Textos.Explicar(r.Rechazo);
                    }
                    catch (ErrorDeSesion e) { return e.Message; }
                },
            };
            AbrirMapa2D(Pt(a.Posicion), eleccion);
            // La capa de fertilidad de su cultivo, si está puesta (como el prototipo: `fijarCultivoDeLaCapa`).
            if (Mapa2D?.CultivoDeFertilidad != null && cultivo.id != "ganado") Mapa2D.CultivoDeFertilidad = cultivo.id;
        }

        /// <summary>Un pueblo como tarjeta (`tarjetaPueblo`): su emblema y nivel, sus chips, el almacén, el rebaño, el carro de acarreo y, para quien
        /// gestiona, «⬆ Nivel N», «Reponer carro» y «✕ Destruir».</summary>
        VisualElement TarjetaPueblo(Asentamiento a, Pueblo x, bool gestiona, List<ObraDeMapa> obras, Heroe yo)
        {
            var aldea = EsAldea(x);
            var cult = CultivoDe(x);
            var nivel = NivelDePueblo(x);
            var t = Dialogo.Caja("plaza-anillo territorio-tarjeta" + (x.HasBatallaId ? " atacado" : ""));
            t.name = "tarjeta-" + x.Id;
            var mejorando = obras.Any(o => o.Tipo == TipoObra.ObraMejoraAldea && o.DestinoId == x.Id);
            var cab = Dialogo.Caja("plaza-anillo-cab");
            var emblema = Dialogo.Caja("territorio-emblema");
            emblema.Add(EmblemaDePueblo(x, 22));
            cab.Add(emblema);
            var titulo = Dialogo.Caja("plaza-anillo-titulo");
            titulo.Add(Dialogo.Texto(NombreSinNivel(x), "plaza-anillo-nombre"));
            var nodo = NodoDelMotor(x.NodoId);
            titulo.Add(Dialogo.Texto($"{(nodo != null ? CosasDelMapa.NombreDeRecurso(nodo.Tipo) + " · " : "")}a {Math.Floor(Dist(Pt(x.Posicion), Pt(a.Posicion)))} u de la plaza", "plaza-obra-titulo-small"));
            cab.Add(titulo);
            cab.Add(Medallon(nivel, mejorando ? "en-obra" : "", 32, 36));
            t.Add(cab);

            var chips = Dialogo.Caja("plaza-anillo-chips");
            if (x.HasBatallaId) chips.Add(ChipConIcono(IconosDelMundo.Icono("glifo", "atacar", 12), "¡Atacado!", "baja"));
            if (mejorando) chips.Add(ChipConIcono(IconosDelMundo.Icono("glifo", "obra", 12), $"Subiendo a {nivel + 1}", "activa", "La mejora está en marcha: mira su obra en la lista"));
            if (x.HasFertilidad && ConGranjas(cult)) chips.Add(ChipConIcono(IconosDelMundo.Icono("glifo", "viveres", 12), (x.Fertilidad / 1000.0).ToString("0.00", Es), "", "Fertilidad del punto de la aldea"));
            if (!aldea)
            {
                var factor = (double?)ParamsPueblos?["extractor"]?["niveles"]?[nivel.ToString()]?["factor"] ?? nivel;
                var recurso = nodo?.Tipo ?? (x.TipoExtractor == "cantera" ? "piedra" : "cobre");
                chips.Add(ChipConIcono(IconosDelMundo.Icono("recurso", recurso, 12), $"{CosasDelMapa.NombreDeRecurso(recurso)} ×{factor}", "",
                    factor > 1 ? $"Por su nivel saca ×{factor} que en el nivel 1, y pide ×{factor} de trabajadores a la plaza" : "Lo que saca de serie: cada nivel más lo multiplica"));
            }
            var deAhora = aldea ? ParamsPueblos?["aldea"]?["niveles"]?[nivel.ToString()] : null;
            if (deAhora != null && ConGranjas(cult)) { var g = (int)deAhora["granjas"]; chips.Add(ChipConIcono(IconosDelMundo.Icono("recurso", ProduceDe(cult), 12), $"{g} granja{(g == 1 ? "" : "s")}", "", "Granjas de la aldea")); }
            if (deAhora != null && ConCorrales(cult)) { var k = (int)deAhora["corrales"]; chips.Add(ChipConIcono(IconosDelMundo.Icono("recurso", "livestock", 12), $"{k} corral{(k == 1 ? "" : "es")}", "", "Corrales de la aldea")); }
            if (chips.childCount > 0) t.Add(chips);

            // El almacén del pueblo, en barra; lo que guarda, en fichas.
            var guardado = x.AlmacenPorRecurso.Count > 0 ? x.AlmacenPorRecurso.Sum(kv => Valor(kv.Value.Cantidad)) : Valor(x.AlmacenLocal?.Cantidad);
            var capacidad = (x.AlmacenLocal?.Capacidad ?? 0) / 1000.0;
            var k0 = capacidad > 0 ? Math.Min(1, guardado / capacidad) : 0;
            var medida = Dialogo.Caja("territorio-medida" + (k0 >= 0.9 ? " lleno" : ""));
            medida.tooltip = "Almacén del pueblo: lo acarrea su carro a la plaza; lleno, deja de producir";
            medida.Add(IconosDelMundo.ConIcono("recurso", "_otro", Dialogo.Texto("Almacén", "territorio-medida-nombre"), 13));
            medida.Add(BarraAvance((float)k0));
            medida.Add(Dialogo.Texto($"{Math.Floor(guardado)} / {Math.Floor(capacidad)}", "territorio-medida-cifra"));
            t.Add(medida);
            var recursos = x.AlmacenPorRecurso.Where(kv => Valor(kv.Value.Cantidad) >= 1).ToList();
            if (recursos.Count > 0)
            {
                var fichas = Dialogo.Caja("plaza-chips");
                foreach (var kv in recursos.OrderBy(kv => kv.Key)) fichas.Add(IconosDelMundo.ChipDeRecurso(kv.Key, Math.Floor(Valor(kv.Value.Cantidad)).ToString(), CosasDelMapa.NombreDeRecurso(kv.Key)));
                t.Add(fichas);
            }
            if (x.HasRebanoCabezas)
            {
                var corrales = (int?)ParamsPueblos?["aldea"]?["niveles"]?[nivel.ToString()]?["corrales"];
                var tope = (double?)c.Datos.Bloque("Economia_Construccion", "ESPECIES")?[x.RebanoEspecie]?["topePorCorral"] * corrales;
                var cabezas = x.RebanoCabezas / 1000.0;
                var r = Dialogo.Caja("territorio-medida rebano");
                r.tooltip = $"Rebaño de {x.RebanoEspecie}: crece solo hasta llenar los corrales";
                r.Add(IconosDelMundo.ConIcono("recurso", "livestock", Dialogo.Texto("Rebaño", "territorio-medida-nombre"), 13));
                r.Add(tope > 0 ? BarraAvance((float)Math.Min(1, cabezas / tope.Value)) : new VisualElement());
                r.Add(Dialogo.Texto($"{Math.Floor(cabezas)}{(tope > 0 ? $" / {tope}" : "")}", "territorio-medida-cifra"));
                t.Add(r);
            }
            t.Add(CarroDeAcarreo(x));
            // La escolta del carro (`carroEnTarjeta`): desde la plaza, la ceden sus residentes con escuadras del campamento.
            if (x.HasCarroAcarreoId && c.Sesion.Almacen.Buscar(TipoEntidad.Caravana, x.CarroAcarreoId)?.Caravana is Caravana carro && Propia(a, yo)) Escolta(t, carro, a.Id, false);

            if (gestiona)
            {
                var acciones = Dialogo.Caja("plaza-anillo-acciones");
                var siguiente = aldea && x.HasNivelAldea ? ParamsPueblos?["aldea"]?["niveles"]?[(nivel + 1).ToString()] : null;
                var sigExt = !aldea ? ParamsPueblos?["extractor"]?["niveles"]?[(nivel + 1).ToString()] : null;
                if (sigExt != null && ((sigExt["coste"] == null) || (a.NivelActual > 0 ? a.NivelActual : a.Nivel) < ((int?)sigExt["nivelMadre"] ?? 1))) sigExt = null;
                var mejora = siguiente ?? sigExt;
                if (mejora != null && !mejorando)
                {
                    Button b = null;
                    b = Dialogo.Boton("", "plaza-anillo-mejorar", async () => await OrdenDePlaza(new Orden { MejorarAldea = new OrdenMejorarAldea { HeroeId = yo.Id, PuebloId = x.Id } }, b));
                    b.name = "mejorar-" + x.Id;
                    b.Add(Dialogo.Texto($"⬆ Nivel {nivel + 1}", "territorio-boton-texto"));
                    b.Add(CosteLlano(mejora["coste"] as JObject));
                    b.tooltip = aldea ? $"Al nivel {nivel + 1}. Lo que pide se lleva a la aldea como una obra."
                        : $"Al nivel {nivel + 1}: saca ×{(double?)mejora["factor"] ?? nivel + 1} que en el nivel 1 y pide lo mismo de trabajadores a la plaza. Lo que pide se lleva al pueblo como una obra.";
                    acciones.Add(b);
                }
                if (!x.HasCarroAcarreoId)
                {
                    Button b = null;
                    b = Dialogo.Boton("", "territorio-boton", async () => await OrdenDePlaza(new Orden { ReponerCarroDeAcarreo = new OrdenReponerCarroDeAcarreo { HeroeId = yo.Id, PuebloId = x.Id } }, b));
                    b.name = "reponer-carro-" + x.Id;
                    b.Add(IconosDelMundo.Icono("glifo", "carrito", 14));
                    b.Add(Dialogo.Texto("Reponer carro", "territorio-boton-texto"));
                    b.Add(ChipsDeCoste(a, CosteCarro()));
                    b.tooltip = "Compra en la plaza un carro con su buey para el acarreo (si no, la plaza lo repone sola en cuanto puede pagarlo).";
                    acciones.Add(b);
                }
                Button destruir = null;
                destruir = Dialogo.Boton("✕ Destruir", "plaza-anillo-abandonar", async () =>
                {
                    if (await dialogo.Abrir("No se recupera nada de lo que guarda ni de lo que costó.", "¿Destruir el pueblo?", "Destruir", "Volver", true))
                        await OrdenDePlaza(new Orden { DestruirPueblo = new OrdenDestruirPueblo { HeroeId = yo.Id, PuebloId = x.Id } }, destruir);
                });
                destruir.name = "destruir-" + x.Id;
                acciones.Add(destruir);
                t.Add(acciones);
            }
            return t;
        }

        static readonly Dictionary<EstadoCaravana, string> EstadoDelCarro = new Dictionary<EstadoCaravana, string>
        {
            [EstadoCaravana.CaravanaDisponible] = "parado en el pueblo", [EstadoCaravana.CaravanaEnTransito] = "camino de la plaza, cargado",
            [EstadoCaravana.CaravanaRetornando] = "volviendo vacío al pueblo", [EstadoCaravana.CaravanaPreparando] = "cargando",
            [EstadoCaravana.CaravanaAdjunta] = "enganchado a una columna", [EstadoCaravana.CaravanaAparcada] = "aparcado",
        };

        /// <summary>El carro de acarreo dentro de la tarjeta (`carroEnTarjeta`): dónde va y lo que lleva. Su escolta, en el interior del pueblo (tanda 3).</summary>
        VisualElement CarroDeAcarreo(Pueblo x)
        {
            var c0 = x.HasCarroAcarreoId ? c.Sesion.Almacen.Buscar(TipoEntidad.Caravana, x.CarroAcarreoId)?.Caravana : null;
            var caja = Dialogo.Caja("territorio-carro" + (x.HasCarroAcarreoId ? "" : " sin"));
            caja.name = "carro-" + x.Id;
            caja.Add(IconosDelMundo.Icono("simbolo", "carro", 24, x.HasCarroAcarreoId ? IconosDelMundo.Bronce : new Color(0.54f, 0.49f, 0.4f, 0.6f)));
            var texto = Dialogo.Caja("territorio-carro-texto");
            if (!x.HasCarroAcarreoId)
            {
                texto.Add(Dialogo.Texto("Sin carro de acarreo", "territorio-carro-titulo"));
                texto.Add(Dialogo.Texto("La plaza lo repone sola en cuanto puede pagarlo. Mientras, el pueblo acumula hasta llenarse.", "territorio-carro-small"));
            }
            else
            {
                texto.Add(Dialogo.Texto("Carro de acarreo", "territorio-carro-titulo"));
                if (c0 != null) texto.Add(Dialogo.Texto(EstadoDelCarro.TryGetValue(c0.Estado, out var e) ? e : c0.Estado.ToString(), "territorio-carro-small"));
            }
            caja.Add(texto);
            var carga = c0?.Contenido?.PorId.Where(kv => kv.Value >= 1000).ToList();
            if (carga?.Count > 0)
            {
                var fichas = Dialogo.Caja("plaza-chips");
                foreach (var kv in carga) fichas.Add(IconosDelMundo.ChipDeRecurso(kv.Key, (kv.Value / 1000).ToString(), CosasDelMapa.NombreDeRecurso(kv.Key)));
                caja.Add(fichas);
            }
            return caja;
        }

        /// <summary>Una obra de mapa como tarjeta (`tarjetaObra`): lo entregado de cada material con su barra o, levantándose, lo que le queda; y
        /// «✕ Cancelar» para quien gestiona (la de pueblo o aldea, con `destruirPueblo` del pueblo de su número).</summary>
        VisualElement TarjetaObra(Asentamiento a, ObraDeMapa o, bool gestiona, Heroe yo)
        {
            var levantando = o.HasTerminaEnMs;
            var completa = !levantando && Faltan(o).Count == 0;
            var t = Dialogo.Caja("plaza-anillo territorio-obra " + (levantando ? "mejora" : completa ? "cerrado" : ""));
            t.name = "tarjeta-" + o.Id;
            var cab = Dialogo.Caja("plaza-anillo-cab");
            var emblema = Dialogo.Caja("territorio-emblema");
            emblema.Add(IconoDeObra(o, 22));
            cab.Add(emblema);
            var titulo = Dialogo.Caja("plaza-anillo-titulo");
            titulo.Add(Dialogo.Texto(NombreDeObra(o), "plaza-anillo-nombre"));
            var nodo = o.HasNodoId ? NodoDelMotor(o.NodoId) : null;
            titulo.Add(Dialogo.Texto($"{(nodo != null ? CosasDelMapa.NombreDeRecurso(nodo.Tipo) + " · " : "")}a {Math.Floor(Dist(Pt(o.Posicion), Pt(a.Posicion)))} u de la plaza", "plaza-obra-titulo-small"));
            cab.Add(titulo);
            cab.Add(Dialogo.Texto(levantando ? "Levantándose" : completa ? "✓ Materiales" : "Faltan materiales", "plaza-anillo-estado"));
            t.Add(cab);
            if (levantando)
            {
                var avance = Dialogo.Caja("plaza-anillo-avance");
                avance.Add(BarraAvance(null));
                var queda = Dialogo.Texto(CuentaAtras(o.TerminaEnMs), "plaza-obra-titulo-small");
                avance.Add(IconosDelMundo.ConIcono("glifo", "espera", queda, 12));
                t.Add(avance);
                relojesDePlaza.Add(("ventana", () => queda.text = CuentaAtras(o.TerminaEnMs)));
            }
            else
            {
                var lineas = Dialogo.Caja("territorio-lineas");
                foreach (var kv in o.Requerido.PorId)
                {
                    var n = kv.Value / 1000.0;
                    var hay = Math.Min(n, (o.Entregado != null && o.Entregado.PorId.TryGetValue(kv.Key, out var e) ? e : 0) / 1000.0);
                    var l = Dialogo.Caja("territorio-linea" + (hay >= n ? " entregada" : ""));
                    l.name = "material-" + kv.Key;
                    l.tooltip = $"{CosasDelMapa.NombreDeRecurso(kv.Key)}: entregado {Math.Floor(hay)} de {n}";
                    var fila = Dialogo.Caja("territorio-linea-fila");
                    fila.Add(IconosDelMundo.Icono("recurso", kv.Key, 14));
                    fila.Add(Dialogo.Texto(Math.Floor(hay).ToString(), "territorio-linea-hay"));
                    fila.Add(Dialogo.Texto("/" + n, "territorio-linea-de"));
                    l.Add(fila);
                    l.Add(BarraAvance(n > 0 ? (float)(hay / n) : 1));
                    lineas.Add(l);
                }
                t.Add(lineas);
                if (!completa) t.Add(Dialogo.Texto("Una columna de tu Facción lo lleva y lo entrega a la puerta de la obra.", "plaza-nota"));
            }
            if (gestiona && o.Tipo != TipoObra.ObraMejoraAldea)
            {
                var acciones = Dialogo.Caja("plaza-anillo-acciones");
                Button b = null;
                b = Dialogo.Boton("✕ Cancelar", "plaza-anillo-abandonar", async () =>
                {
                    if (!await dialogo.Abrir("Lo entregado no se devuelve.", "¿Cancelar la obra?", "Cancelar la obra", "Volver", true)) return;
                    // Una obra de avanzada se cancela con `cancelarObra`; la de un pueblo o una aldea, destruyendo su pueblo (comparten número: obra-7 ↔ pueblo-7).
                    var deAvanzada = o.Tipo == TipoObra.ObraAvanzada || o.Tipo == TipoObra.ObraMejoraAvanzada || o.Tipo == TipoObra.ObraReparacion;
                    await OrdenDePlaza(deAvanzada ? new Orden { CancelarObra = new OrdenCancelarObra { HeroeId = yo.Id, ObraId = o.Id } }
                        : new Orden { DestruirPueblo = new OrdenDestruirPueblo { HeroeId = yo.Id, PuebloId = "pueblo-" + o.Id.Substring(o.Id.IndexOf('-') + 1) } }, b);
                });
                b.name = "cancelar-" + o.Id;
                b.tooltip = "Cancela la obra: no devuelve lo entregado.";
                acciones.Add(b);
                t.Add(acciones);
            }
            return t;
        }

        // ─────────────────────────── «Fundación» ───────────────────────────

        static readonly Dictionary<EstadoCaravana, string> EstadoDeFundacion = new Dictionary<EstadoCaravana, string>
        {
            [EstadoCaravana.CaravanaDisponible] = "esperando dentro", [EstadoCaravana.CaravanaAdjunta] = "enganchada a una columna",
            [EstadoCaravana.CaravanaAparcada] = "aparcada en una plaza", [EstadoCaravana.CaravanaEnTransito] = "en camino", [EstadoCaravana.CaravanaRetornando] = "de vuelta a su origen",
        };

        /// <summary>Los requisitos para lanzar una Caravana de Fundación: (cumplido, icono, texto, por qué no).</summary>
        List<(bool ok, VisualElement icono, string texto, string porque)> RequisitosDeFundacion(Asentamiento a, Heroe yo)
        {
            var reside = yo.HasResidenciaId && yo.ResidenciaId == a.Id;
            var presente = yo.Ubicacion?.DondeCase == UbicacionHeroe.DondeOneofCase.AsentamientoId && yo.Ubicacion.AsentamientoId == a.Id;
            var faccion = c.Sesion.Almacen.Buscar(TipoEntidad.Faccion, a.FaccionId)?.Faccion;
            var costo = c.Datos.CosteCaravanaFundacion;
            var tabla = c.Datos.Bloque("Diplomacia_Facciones", "CAP_FUNDACION_POR_NIVEL") as JArray;
            int? cap = tabla?.Count > 0 ? (int)tabla[Math.Min(tabla.Count, Math.Max(1, (int)(faccion?.Nivel ?? 1))) - 1] : (int?)null;
            var faltantes = costo.Where(kv => Hay(a, kv.Key) < kv.Value / 1000.0).ToList();
            var nivel = a.NivelActual > 0 ? a.NivelActual : a.Nivel;
            var cooldown = (double?)c.Datos.Bloque("Comercio_Caravanas", "CARAVANA_COOLDOWN")?["cooldownMinutos"] ?? 10;
            var msCooldown = a.HasUltimaCaravanaCreadaEnMs ? Math.Max(0, a.UltimaCaravanaCreadaEnMs + cooldown * 60_000 - AhoraMs) : 0;
            var al = c.Sesion.Almacen;
            var propias = al.Todas(Entidad.CuerpoOneofCase.Asentamiento).Count(e => e.Asentamiento.FaccionId == a.FaccionId);
            var vivas = al.Todas(Entidad.CuerpoOneofCase.Caravana).Count(e => e.Caravana.Tipo == TipoCaravana.CaravanaDeFundacion && (e.Caravana.HasFaccionId ? e.Caravana.FaccionId : a.FaccionId) == a.FaccionId);
            return new List<(bool, VisualElement, string, string)>
            {
                (reside && presente, IconosDelMundo.Icono("glifo", "casa", 14), "Residente presente", !reside ? "solo la lanza quien reside en esta plaza" : "tienes que estar en la plaza"),
                (nivel >= 2, IconosDelMundo.Icono("glifo", "templo", 14), "Plaza de nivel 2 o más", $"la plaza es de nivel {nivel}"),
                (msCooldown == 0, IconosDelMundo.Icono("glifo", "espera", 14), "Sin enfriamiento de caravanas", $"faltan ~{CuentaAtras((long)(AhoraMs + msCooldown))} (lo comparten las caravanas comerciales)"),
                (cap == null || propias + vivas < cap, IconosDelMundo.Icono("glifo", "campamento", 14), $"Cupo de Fundación{(cap != null ? $" {propias + vivas}/{cap}" : "")}",
                    $"{propias + vivas}/{cap} contando las caravanas ya lanzadas (nivel de Facción {faccion?.Nivel ?? 1})"),
                (faltantes.Count == 0, IconosDelMundo.Icono("recurso", "_otro", 14), "Coste completo en el almacén",
                    "faltan " + string.Join(", ", faltantes.Select(kv => $"{kv.Value / 1000 - Hay(a, kv.Key)} de {CosasDelMapa.NombreDeRecurso(kv.Key)}"))),
            };
        }

        void Fundacion(VisualElement cuerpo, Asentamiento a, Heroe yo, Action rehacer)
        {
            var caja = Dialogo.Caja("territorio-fundacion");
            caja.name = "fundacion";
            caja.Add(TituloDeSeccion("Caravana de Fundación", "Fundar un asentamiento nuevo exige una Caravana de Fundación. La plaza paga su coste entero del almacén; quien la lanza es su titular. Espera aquí dentro: cualquiera de tu Facción que salga como ejército puede llevársela (al partir, el Líder elige). Luego funda donde estés. Si nadie la lleva caduca y el coste vuelve al almacén; desarmarla a mano también lo devuelve."));
            var gates = RequisitosDeFundacion(a, yo);
            var lanzable = gates.All(g => g.ok);
            var pendientes = gates.Count(g => !g.ok);
            var tarjeta = Dialogo.Caja("plaza-anillo territorio-fundar" + (lanzable ? " cerrado" : ""));
            tarjeta.name = "nueva-caravana";
            var cab = Dialogo.Caja("plaza-anillo-cab");
            var emblema = Dialogo.Caja("territorio-emblema");
            emblema.Add(IconosDelMundo.Icono("simbolo", "carro", 22, IconosDelMundo.OroClaro));
            cab.Add(emblema);
            var titulo = Dialogo.Caja("plaza-anillo-titulo");
            titulo.Add(Dialogo.Texto("Nueva caravana", "plaza-anillo-nombre"));
            titulo.Add(Dialogo.Texto(lanzable ? "Todo listo: sale del almacén de la plaza" : $"Falta{(pendientes == 1 ? "" : "n")} {pendientes} requisito{(pendientes == 1 ? "" : "s")}", "plaza-obra-titulo-small"));
            cab.Add(titulo);
            tarjeta.Add(cab);
            var costes = Dialogo.Caja("territorio-lineas");
            foreach (var kv in c.Datos.CosteCaravanaFundacion)
            {
                var n = kv.Value / 1000.0;
                var hay = Hay(a, kv.Key);
                var l = Dialogo.Caja("territorio-linea" + (hay >= n ? " entregada" : " falta"));
                l.tooltip = $"{CosasDelMapa.NombreDeRecurso(kv.Key)}: {(hay < n ? $"faltan {n - hay}" : "en el almacén")}";
                var fila = Dialogo.Caja("territorio-linea-fila");
                fila.Add(IconosDelMundo.Icono("recurso", kv.Key, 14));
                fila.Add(Dialogo.Texto(Math.Min(hay, n).ToString(), "territorio-linea-hay"));
                fila.Add(Dialogo.Texto("/" + n, "territorio-linea-de"));
                l.Add(fila);
                l.Add(BarraAvance((float)Math.Min(1, hay / n)));
                costes.Add(l);
            }
            tarjeta.Add(costes);
            var reqs = Dialogo.Caja("territorio-reqs");
            foreach (var (ok, icono, texto, porque) in gates)
            {
                var r = Dialogo.Caja("plaza-req " + (ok ? "ok" : "falta"));
                r.Add(Dialogo.Texto(ok ? "✓" : "✗", "plaza-req-marca"));
                if (icono != null) r.Add(icono);
                var t = Dialogo.Caja("territorio-req-texto");
                t.Add(Dialogo.Texto(texto, "territorio-req-nombre"));
                if (!ok) t.Add(Dialogo.Texto(porque, "plaza-nota"));
                r.Add(t);
                reqs.Add(r);
            }
            tarjeta.Add(reqs);
            var pie = Dialogo.Caja("plaza-anillo-acciones");
            Button lanzar = null;
            lanzar = Dialogo.Boton("", "plaza-anillo-mejorar", async () =>
                await OrdenDePlaza(new Orden { LanzarCaravanaFundacion = new OrdenLanzarCaravanaFundacion { OrigenAsentamientoId = a.Id } }, lanzar));
            lanzar.name = "lanzar-fundacion";
            lanzar.Add(IconosDelMundo.Icono("glifo", "campamento", 14));
            lanzar.Add(Dialogo.Texto("Lanzar caravana", "territorio-boton-texto"));
            lanzar.SetEnabled(lanzable);
            pie.Add(lanzar);
            tarjeta.Add(pie);
            caja.Add(tarjeta);

            // Las caravanas de esta plaza.
            var radioPuerta = c.Datos.RadioPuerta;
            var caducidadMs = ((double?)c.Datos.Bloque("Economia_Construccion", "FUNDACION")?["caducidadCaravanaHoras"] ?? 0) * 3_600_000;
            var mias = c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Caravana).Select(e => e.Caravana)
                .Where(k => k.Tipo == TipoCaravana.CaravanaDeFundacion && k.OrigenAsentamientoId == a.Id && !k.HasOrigenCampamentoId).ToList();
            var n0 = Dialogo.Texto(mias.Count.ToString(), "plaza-obra-chip");
            caja.Add(TituloDeSeccion("De esta plaza", null, n0));
            if (mias.Count == 0) caja.Add(Dialogo.Texto("Esta plaza no tiene ninguna Caravana de Fundación en pie.", "plaza-nota"));
            foreach (var k in mias)
            {
                var suelta = k.Estado == EstadoCaravana.CaravanaDisponible || k.Estado == EstadoCaravana.CaravanaAparcada;
                var p = Instante.Posicion(k.Plan, k.Posicion, AhoraMs);
                var enLaPuerta = Dist((p.x / 1000, p.y / 1000), Pt(a.Posicion)) <= radioPuerta;
                var titular = k.HasTitularId && k.TitularId == yo.Id;
                var nota = !titular ? $"La lleva {(k.HasTitularId ? Textos.NombreDeHeroe(c.Sesion.Almacen, k.TitularId) : "—")}: solo su titular la desarma."
                    : k.Estado != EstadoCaravana.CaravanaDisponible ? "Para desarmarla tiene que estar suelta (desengánchala primero)."
                    : !enLaPuerta ? "Para desarmarla tiene que estar en la puerta de su plaza." : "";
                var t = Dialogo.Caja("plaza-anillo territorio-caravana");
                t.name = "caravana-" + k.Id;
                var kcab = Dialogo.Caja("plaza-anillo-cab");
                kcab.Add(IconosDelMundo.Icono("simbolo", "carro", 22, IconosDelMundo.OroClaro));
                var kt = Dialogo.Caja("plaza-anillo-titulo");
                kt.Add(Dialogo.Texto("Caravana " + k.Id, "plaza-anillo-nombre"));
                kt.Add(Dialogo.Texto((k.HasTitularId ? Textos.NombreDeHeroe(c.Sesion.Almacen, k.TitularId) : "—") + (titular ? " (tú)" : ""), "plaza-obra-titulo-small"));
                kcab.Add(kt);
                kcab.Add(Dialogo.Texto(EstadoDeFundacion.TryGetValue(k.Estado, out var e) ? e : k.Estado.ToString(), "plaza-anillo-estado"));
                t.Add(kcab);
                if (suelta && k.HasCaducaEnMs)
                {
                    var caduca = Dialogo.Caja("plaza-anillo-avance");
                    caduca.tooltip = "Si nadie la lleva, caduca y el coste vuelve al almacén";
                    caduca.Add(Dialogo.Texto("caduca en " + CuentaAtras(k.CaducaEnMs), "plaza-obra-titulo-small"));
                    if (caducidadMs > 0) caduca.Add(BarraAvance((float)Math.Max(0, Math.Min(1, (k.CaducaEnMs - AhoraMs) / caducidadMs))));
                    t.Add(caduca);
                }
                if (nota != "") t.Add(Dialogo.Texto(nota, "plaza-nota"));
                if (titular)
                {
                    var acc = Dialogo.Caja("plaza-anillo-acciones");
                    Button d = null;
                    var id = k.Id;
                    d = Dialogo.Boton("✕ Desarmar", "plaza-anillo-abandonar", async () =>
                    {
                        if (await dialogo.Abrir("El coste vuelve al almacén de la plaza.", "¿Desarmar la Caravana de Fundación?", "Desarmar", "Volver"))
                            await OrdenDePlaza(new Orden { DesarmarCaravanaFundacion = new OrdenDesarmarCaravanaFundacion { CaravanaId = id } }, d);
                    });
                    d.name = "desarmar-" + k.Id;
                    d.SetEnabled(nota == "");
                    d.tooltip = nota == "" ? "El coste vuelve al almacén de la plaza" : nota;
                    acc.Add(d);
                    t.Add(acc);
                }
                caja.Add(t);
            }
            cuerpo.Add(caja);
        }

        /// <summary>¿Se puede lanzar ya la Caravana de Fundación? Enciende el «!» de Territorio y de Fundación.</summary>
        bool FundacionLista(Asentamiento a, Heroe yo) => Propia(a, yo) && RequisitosDeFundacion(a, yo).All(g => g.ok);
    }
}

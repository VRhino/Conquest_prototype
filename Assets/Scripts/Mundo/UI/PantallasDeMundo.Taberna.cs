// La Taberna (paso 12 del bloque 4, tanda 1), calcada de BronzeAgeClient (`ui/panelIntel.ts`, `ui/rumores.ts` y `ui/tabernaRegion.ts`): la intel que se
// compra en la taberna de una plaza de tu Facción (el Rey, el Embajador o el Gobernador, dentro; paga el oro del almacén de la plaza) o en la de un
// campamento de mercenarios (cualquiera con Facción; paga tu almacén personal si resides, si no el carro de tu columna).
// - A la izquierda: la taberna con su oro y sus Miradas abiertas; «Mirada» (un ojo sobre un punto, con su precio por distancia), «Informe de plaza»
//   (la foto de una plaza ajena), el informe de la región (solo en plaza) y los rumores (las vetas de estaño de la región).
// - A la derecha: tus Miradas, tus informes de plaza (el abierto, con sus edificios, su guarnición y sus murallas) y tus informes de región.
// El motor valida todo; su motivo sale tal cual.
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
        /// <summary>El punto elegido para la próxima Mirada y el informe de plaza abierto (sobreviven a los repintados).</summary>
        (double x, double y)? centroMirada;
        string informeAbierto;

        JToken Intel => c.Datos.Bloque("Niebla_De_Guerra", "INTEL");

        RasterDeMundo rasterRegion;

        /// <summary>La región del relieve en (x, y) (u), del raster `region` del artefacto; null fuera de toda región o sin artefacto.</summary>
        int? RegionEn(double x, double y)
        {
            if (rasterRegion == null && c.Artefacto?.Region != null && c.Artefacto.Region.Encoding == EncodingRaster.EncodingU8) rasterRegion = new RasterDeMundo(c.Artefacto.Region);
            if (rasterRegion == null) return null;
            var id = rasterRegion.En(x, y);
            return id == 0 ? (int?)null : id;
        }

        string NombreDeRegion(int id) => c.Artefacto?.Regiones.FirstOrDefault(r => r.Id == id)?.Nombre ?? $"la región {id}";

        /// <summary>Lo que se sabe de una taberna para comprar: dónde está, con qué oro se paga y cuántas Miradas admite a la vez.</summary>
        sealed class TabernaDeIntel
        {
            public string Tipo, Id, Etiqueta;
            public double X, Y, Oro;
            public int Cupo;
        }

        TabernaDeIntel TabernaDePlaza(Asentamiento a)
        {
            var taberna = Internos(a).FirstOrDefault(e => e.Tipo == "taberna" && Activo(e));
            if (taberna == null) return null;
            var cupo = (int?)Catalogo?["taberna"]?["niveles"]?[NivelInterno(taberna).ToString()]?["cupoMiradas"] ?? 0;
            return new TabernaDeIntel { Tipo = "asentamiento", Id = a.Id, Etiqueta = $"Taberna de {(a.HasNombre ? a.Nombre : a.Id)}", X = a.Posicion.X / 1000.0, Y = a.Posicion.Y / 1000.0, Oro = Hay(a, "oro"), Cupo = cupo };
        }

        TabernaDeIntel TabernaDeCampamento(CampamentoMercenarios camp, Heroe h)
        {
            var oro = camp.ResidentesIds.Contains(h.Id)
                ? (h.AlmacenPersonal != null && h.AlmacenPersonal.PorId.TryGetValue("oro", out var o) ? o / 1000.0 : 0)
                : (ColumnaDelHeroe(h)?.Suministro is Cantidades s && s.PorId.TryGetValue("oro", out var oc) ? oc / 1000.0 : 0);
            return new TabernaDeIntel { Tipo = "campamento", Id = camp.Id, Etiqueta = "Taberna del campamento", X = camp.Posicion.X / 1000.0, Y = camp.Posicion.Y / 1000.0, Oro = oro,
                Cupo = (int?)Intel?["campamento"]?["cupoMiradas"] ?? 1 };
        }

        /// <summary>Las plazas ajenas que conoces (la que se ve gana a la recordada): las que se pueden mirar o de las que se pide un informe.</summary>
        List<(string id, string nombre, int nivel, double x, double y)> PlazasAjenas(Heroe h)
        {
            var a = c.Sesion.Almacen;
            var vistas = a.Todas(Entidad.CuerpoOneofCase.Asentamiento).Select(e => e.Asentamiento).Where(x => x.FaccionId != h.FaccionId && x.Posicion != null)
                .Select(x => (x.Id, x.HasNombre ? x.Nombre : x.Id, (int)x.Nivel, x.Posicion.X / 1000.0, x.Posicion.Y / 1000.0)).ToList();
            var ids = new HashSet<string>(vistas.Select(x => x.Item1));
            var recordadas = a.Todas(Entidad.CuerpoOneofCase.FichaConocida).Select(e => e.FichaConocida)
                .Where(f => !ids.Contains(f.AsentamientoId) && f.FaccionId != h.FaccionId && f.Posicion != null && (f.Tipo == TipoEntidad.NoEspecificado || f.Tipo == TipoEntidad.Asentamiento))
                .Select(f => (f.AsentamientoId, f.HasNombre ? f.Nombre : f.AsentamientoId, (int)f.Nivel, f.Posicion.X / 1000.0, f.Posicion.Y / 1000.0));
            return vistas.Concat(recordadas).OrderBy(x => x.Item2).ToList();
        }

        /// <summary>Lo que cuesta mirar `centro` desde la taberna `t` (`precioMirada` del motor): la base más un tanto por unidad de distancia a tus
        /// ojos más cercanos (la taberna, tus plazas y tus columnas).</summary>
        int PrecioMirada(TabernaDeIntel t, Heroe h, double x, double y)
        {
            var a = c.Sesion.Almacen;
            var ojos = new List<(double x, double y)> { (t.X, t.Y) };
            ojos.AddRange(a.Todas(Entidad.CuerpoOneofCase.Asentamiento).Select(e => e.Asentamiento).Where(p => p.FaccionId == h.FaccionId && p.Posicion != null).Select(p => (p.Posicion.X / 1000.0, p.Posicion.Y / 1000.0)));
            ojos.AddRange(a.Todas(Entidad.CuerpoOneofCase.Ejercito).Select(e => e.Ejercito).Where(e => e.FaccionId == h.FaccionId && (e.Plan != null || e.Posicion != null))
                .Select(e => { var p = Instante.Posicion(e.Plan, e.Posicion, AhoraMs); return (p.x / 1000, p.y / 1000); }));
            var lejos = ojos.Min(o => CosasDelMapa.Distancia(o.x, o.y, x, y));
            var m = Intel?["mirada"];
            return (int)Math.Ceiling(((double?)m?["oroBase"] ?? 0) + ((double?)m?["oroPorUnidad"] ?? 0) * lejos);
        }

        static string Hace(double ms)
        {
            var min = Math.Max(0, Math.Round(ms / 60_000));
            return min < 90 ? $"{min} min" : min < 60 * 36 ? $"{Math.Round(min / 60)} h" : $"{Math.Round(min / 1440)} d";
        }

        string FirmaDeIntel(Heroe h)
        {
            var a = c.Sesion.Almacen;
            return $"{string.Join(",", a.Todas(Entidad.CuerpoOneofCase.Mirada).Select(e => e.Mirada.Id + ":" + e.Mirada.Version))}|{string.Join(",", a.Todas(Entidad.CuerpoOneofCase.Informe).Select(e => e.Informe.AsentamientoId + ":" + e.Informe.Version))}"
                + $"|{MiFaccion(h)?.Version}|{h.Version}|{centroMirada}|{informeAbierto}|{Math.Floor(AhoraMs / 60_000)}";
        }

        /// <summary>El panel Intel (`renderPanelIntel`) de la taberna `t`, en dos columnas. `plaza`: la de la taberna, para el informe de región.</summary>
        void PanelDeIntel(VisualElement cuerpo, TabernaDeIntel t, Heroe h, Asentamiento plaza, Action rehacer)
        {
            var a = c.Sesion.Almacen;
            // Sus piezas (los chips, las filas de la región) son las de la plaza: también en el campamento y a su puerta.
            if (Resources.Load<StyleSheet>("MundoUI/Plaza") is StyleSheet hoja && !cuerpo.styleSheets.Contains(hoja)) cuerpo.styleSheets.Add(hoja);
            var cols = Dialogo.Caja("intel-cols");
            var izq = Dialogo.Caja("intel-col");
            var der = Dialogo.Caja("intel-col intel-col--der");
            cols.Add(izq);
            cols.Add(der);
            cuerpo.Add(cols);
            var miradas = a.Todas(Entidad.CuerpoOneofCase.Mirada).Select(e => e.Mirada).Where(m => m.FaccionId == h.FaccionId).OrderByDescending(m => m.CompradaEnMs).ToList();
            var abiertas = miradas.Count(m => m.OrigenId == t.Id && m.ExpiraEnMs > AhoraMs);

            // La cabecera: la taberna, su oro y sus Miradas abiertas en ojos.
            var cab = Dialogo.Caja("intel-cab");
            cab.Add(IconosDelMundo.Icono("edificio", "taberna", 34, IconosDelMundo.Bronce));
            var cabTexto = Dialogo.Caja("intel-cab-cuerpo");
            cabTexto.Add(Dialogo.Texto(t.Etiqueta, "pacto-plaza"));
            var chips = Dialogo.Caja("intel-cab-chips");
            chips.Add(IconosDelMundo.ChipDeRecurso("oro", Math.Floor(t.Oro).ToString(), t.Tipo == "asentamiento" ? "El oro del almacén de la plaza, con el que se paga" : "Tu oro, con el que se paga"));
            var ojos = Dialogo.Texto($"👁 {abiertas}/{t.Cupo}", "plaza-obra-chip");
            ojos.name = "intel-ojos";
            ojos.tooltip = $"Miradas abiertas: {abiertas} de {t.Cupo}";
            chips.Add(ojos);
            cabTexto.Add(chips);
            cab.Add(cabTexto);
            izq.Add(cab);
            // Mirada e informe, de dos en dos (`.intel-servicios`); la región y los rumores, debajo a lo ancho.
            var servicios = Dialogo.Caja("intel-servicios");
            izq.Add(servicios);

            VisualElement Servicio(string icono, string titulo, string sub, string ayuda)
            {
                var s = Dialogo.Caja("intel-servicio");
                var sc = Dialogo.Caja("intel-servicio-cab");
                sc.Add(Dialogo.Texto(icono, "intel-servicio-icono"));
                var st = Dialogo.Caja("intel-servicio-titulo");
                st.Add(Dialogo.Texto(titulo, "pacto-plaza"));
                if (sub != null) st.Add(Dialogo.Texto(sub, "plaza-sub"));
                sc.Add(st);
                s.Add(ConAyuda(sc, ayuda));
                servicios.Add(s);
                return s;
            }
            Button Comprar(string nombre, int? precio, Func<Orden> orden, Action alComprar = null)
            {
                Button b = null;
                b = Dialogo.Boton("", "boton-primario intel-comprar", async () =>
                {
                    if (await OrdenDePlaza(orden(), b)) { alComprar?.Invoke(); rehacer(); }
                });
                // El texto y el precio uno junto al otro (`Comprar 🪙 63`), no encima.
                b.Add(Dialogo.Texto("Comprar", "intel-comprar-texto"));
                if (precio != null) b.Add(IconosDelMundo.ChipDeRecurso("oro", precio.ToString(), "Lo que cuesta"));
                b.name = nombre;
                return b;
            }

            // Mirada: alrededor de una plaza ajena o un campamento, o un punto (x, y).
            var m0 = Intel?["mirada"];
            var radio = (double?)m0?["radio"] ?? 0;
            var horas = ((double?)m0?["duracionMinutos"] ?? 0) / 60;
            var mirada = Servicio("👁", "Mirada", $"radio {radio} · {horas} h",
                $"Un ojo de {radio} de radio durante {horas} h sobre cualquier punto: ves en vivo lo que verías con una columna ahí, sin interiores. Lo ven también tus aliados. Cuanto más lejos de lo que ya ves, más cuesta.");
            mirada.name = "intel-mirada";
            var ajenas = PlazasAjenas(h);
            var campamentos = a.Todas(Entidad.CuerpoOneofCase.CampamentoMercenarios).Select(e => e.CampamentoMercenarios).Where(x => x.Posicion != null).ToList();
            var opciones = ajenas.Select(x => (x.nombre, x.x, x.y)).Concat(campamentos.Select(x => ("Campamento " + x.Id, x.Posicion.X / 1000.0, x.Posicion.Y / 1000.0))).ToList();
            if (opciones.Count > 0)
            {
                var cerca = new DropdownField(new[] { "Mirar alrededor de…" }.Concat(opciones.Select(o => o.Item1)).ToList(), 0) { name = "intel-cerca" };
                cerca.AddToClassList("tropas-orden");
                cerca.RegisterValueChangedCallback(_ => { if (cerca.index > 0) { var o = opciones[cerca.index - 1]; centroMirada = (o.Item2, o.Item3); rehacer(); } });
                mirada.Add(cerca);
            }
            var coords = Dialogo.Caja("intel-coords");
            coords.Add(Dialogo.Texto("📍", "plaza-sub"));
            var cx = new IntegerField { name = "intel-x", value = centroMirada == null ? 0 : (int)Math.Round(centroMirada.Value.x) };
            var cy = new IntegerField { name = "intel-y", value = centroMirada == null ? 0 : (int)Math.Round(centroMirada.Value.y) };
            foreach (var f in new[] { cx, cy })
            {
                f.AddToClassList("plaza-carro-cantidad");
                f.isDelayed = true;
                f.RegisterValueChangedCallback(_ => { centroMirada = (cx.value, cy.value); rehacer(); });
                coords.Add(f);
            }
            mirada.Add(coords);
            if (abiertas >= t.Cupo) mirada.Add(Dialogo.Texto("Taberna al límite: espera a que caduque una Mirada.", "plaza-sub"));
            else if (centroMirada.HasValue)
            {
                var centro = centroMirada.Value;
                var precio = PrecioMirada(t, h, centro.x, centro.y);
                mirada.Add(Comprar("intel-comprar-mirada", precio, () => new Orden { ComprarMirada = new OrdenComprarMirada
                {
                    Origen = new OrdenComprarMirada.Types.Origen { Tipo = t.Tipo, Id = t.Id },
                    Centro = new Punto { X = (long)Math.Round(centro.x * 1000), Y = (long)Math.Round(centro.y * 1000) },
                } }));
            }
            else mirada.Add(Dialogo.Texto("Elige un sitio o pon sus coordenadas.", "plaza-sub"));

            // Informe de plaza: de una plaza ajena que conoces, sin uno reciente.
            var porNivel = (int?)Intel?["informe"]?["oroPorNivel"] ?? 0;
            var esperaInforme = ((double?)Intel?["informe"]?["cooldownMinutos"] ?? 0) * 60_000;
            var informe = Servicio("📜", "Informe de plaza", $"{porNivel} de oro por nivel", "La foto, con fecha, del layout y la defensa de una plaza ajena que conoces. Avisa, sin firma, a su Facción.");
            informe.name = "intel-informe";
            var informes = a.Todas(Entidad.CuerpoOneofCase.Informe).Select(e => e.Informe).ToList();
            var libres = ajenas.Where(x => !informes.Any(i => i.AsentamientoId == x.id && i.ConocidoEnMs + esperaInforme > AhoraMs)).ToList();
            if (ajenas.Count == 0) informe.Add(Dialogo.Texto("No conoces ninguna plaza ajena: mira una zona o explora.", "plaza-sub"));
            else if (libres.Count == 0) informe.Add(Dialogo.Texto("Ya tienes un informe reciente de todas las plazas que conoces.", "plaza-sub"));
            else
            {
                var plazas = new DropdownField(libres.Select(x => $"{x.nombre} · nivel {x.nivel} · {porNivel * x.nivel} oro").ToList(), 0) { name = "intel-plaza" };
                plazas.AddToClassList("tropas-orden");
                informe.Add(plazas);
                informe.Add(Comprar("intel-comprar-informe", null, () =>
                {
                    var elegida = libres[Math.Max(0, plazas.index)];
                    informeAbierto = elegida.id;
                    return new Orden { ComprarInformePlaza = new OrdenComprarInformePlaza { Origen = new OrdenComprarInformePlaza.Types.Origen { Tipo = t.Tipo, Id = t.Id }, AsentamientoId = elegida.id } };
                }));
            }

            // El informe de región: solo en la taberna de una plaza.
            if (plaza != null) InformeDeRegion(izq, plaza, h, rehacer);

            // Los rumores: las vetas de estaño de la región de la taberna, si queda alguna por explorar.
            Rumores(izq, t, h, Comprar);

            // A la derecha: tus Miradas, tus informes de plaza (el abierto, con su detalle) y tus informes de región.
            der.Add(TituloDeSeccion("Tus Miradas", null, Chip(miradas.Count.ToString())));
            if (miradas.Count == 0) der.Add(Dialogo.Texto("Ninguna.", "plaza-sub"));
            var listaM = Dialogo.Caja("intel-miradas");
            foreach (var mi in miradas)
            {
                var abierta = mi.ExpiraEnMs > AhoraMs;
                var b = Dialogo.Caja("intel-mirada" + (abierta ? " abierta" : ""));
                b.name = "mirada-" + mi.Id;
                b.Add(Dialogo.Texto(abierta ? "👁" : "⏳", "intel-servicio-icono"));
                var bt = Dialogo.Caja("intel-servicio-titulo");
                bt.Add(Dialogo.Texto($"({Math.Round(mi.Centro.X / 1000.0)}, {Math.Round(mi.Centro.Y / 1000.0)})", "pacto-plaza"));
                bt.Add(Dialogo.Texto(abierta ? $"{Hace(mi.ExpiraEnMs - AhoraMs)} restantes" : $"repetible en {Hace(mi.LibreEnMs - AhoraMs)}", "plaza-sub"));
                b.Add(bt);
                listaM.Add(b);
            }
            der.Add(listaM);
            der.Add(TituloDeSeccion("Tus informes", null, Chip(informes.Count.ToString())));
            if (informes.Count == 0) der.Add(Dialogo.Texto("Ninguno.", "plaza-sub"));
            var listaI = Dialogo.Caja("intel-informes");
            foreach (var inf in informes.OrderByDescending(i => i.ConocidoEnMs))
            {
                var id = inf.AsentamientoId;
                var b = Dialogo.Boton("", "intel-informe-btn" + (id == informeAbierto ? " abierto" : ""), () => { informeAbierto = informeAbierto == id ? null : id; rehacer(); });
                b.name = "informe-plaza-" + id;
                b.Add(Dialogo.Texto($"{(inf.HasNombre ? inf.Nombre : id)} · nivel {inf.Nivel}", "pacto-plaza"));
                b.Add(Dialogo.Texto($"hace {Hace(AhoraMs - inf.ConocidoEnMs)}", "plaza-sub"));
                listaI.Add(b);
            }
            der.Add(listaI);
            if (informes.FirstOrDefault(i => i.AsentamientoId == informeAbierto) is InformePlaza abiertoInf) der.Add(DetalleDeInforme(abiertoInf));
            var region = MiFaccion(h)?.InformesRegion.OrderByDescending(x => x.CompradoEnMs).ToList() ?? new List<InformeRegion>();
            if (region.Count > 0)
            {
                der.Add(TituloDeSeccion("Tus informes de región", null, Chip(region.Count.ToString())));
                foreach (var r in region)
                {
                    var fila = Dialogo.Caja("intel-region-fila");
                    fila.Add(Dialogo.Texto(r.Tipo == "plazas" ? "🏛" : "⚖", "intel-region-icono"));
                    var tx = Dialogo.Caja("intel-region-texto");
                    tx.Add(Dialogo.Texto($"{(r.Tipo == "plazas" ? "Sus plazas" : "Sus órdenes")} · {NombreDeRegion((int)r.Region)}", "pacto-plaza"));
                    tx.Add(Dialogo.Texto(r.HasCaducaEnMs && r.CaducaEnMs > AhoraMs ? $"a la vista {FaltaPara(r.CaducaEnMs)} más" : $"comprado hace {Hace(AhoraMs - r.CompradoEnMs)}", "plaza-sub"));
                    fila.Add(tx);
                    der.Add(fila);
                }
            }
        }

        /// <summary>Los rumores de la taberna (`htmlServicioRumores`): las vetas de estaño de su región que tu Facción aún no tiene exploradas. Sin
        /// región o sin vetas, nada; con todas sabidas, «Tu Facción ya lo sabe».</summary>
        void Rumores(VisualElement donde, TabernaDeIntel t, Heroe h, Func<string, int?, Func<Orden>, Action, Button> comprar)
        {
            var region = RegionEn(t.X, t.Y);
            if (region == null || c.Artefacto == null) return;
            var niebla = c.Sesion.Almacen.Ambitos.Values.Select(x => x.Niebla).FirstOrDefault(n => n != null);
            bool Explorado(double x, double y)
            {
                if (niebla == null || niebla.TamanoCelda == 0) return false;
                var celda = niebla.TamanoCelda / 1000.0;
                return Almacen.EstadoCelda(niebla, (int)(x / celda), (int)(y / celda)) > 0;
            }
            var estano = c.Artefacto.Nodos.Where(n => n.Tipo == "estano" && n.Posicion != null && RegionEn(n.Posicion.X / 1000.0, n.Posicion.Y / 1000.0) == region).ToList();
            if (estano.Count == 0) return;
            var nuevas = estano.Count(n => !Explorado(n.Posicion.X / 1000.0, n.Posicion.Y / 1000.0));
            var v = Intel?["rumores"]?["vetasEstano"];
            var precio = (int?)v?["oro"] ?? 0;
            var s = Dialogo.Caja("intel-servicio intel-servicio--ancho");
            s.name = "intel-rumores";
            var sc = Dialogo.Caja("intel-servicio-cab");
            sc.Add(Dialogo.Texto("🗣", "intel-servicio-icono"));
            var st = Dialogo.Caja("intel-servicio-titulo");
            st.Add(Dialogo.Texto("Rumores", "pacto-plaza"));
            st.Add(Dialogo.Texto(NombreDeRegion(region.Value), "plaza-sub"));
            sc.Add(st);
            s.Add(ConAyuda(sc, $"Lo que se cuenta en la taberna de la región del relieve donde está. Las vetas de estaño: el terreno a {(double?)v?["radio"] ?? 0} de cada yacimiento de estaño de la región queda explorado para tu Facción, como si lo hubiera andado una columna. Si tu Facción ya lo sabe todo, no se vende. Nadie se entera."));
            var fila = Dialogo.Caja("intel-region-fila");
            fila.Add(Dialogo.Texto("⛏", "intel-region-icono"));
            var tx = Dialogo.Caja("intel-region-texto");
            tx.Add(Dialogo.Texto("Vetas de estaño de la región", "pacto-plaza"));
            tx.Add(Dialogo.Texto(nuevas == 0 ? "Ya tiene exploradas todas las de esta región." : "Deja explorado el terreno de alrededor de cada una.", "plaza-sub"));
            fila.Add(tx);
            if (nuevas == 0) fila.Add(Dialogo.Texto("Tu Facción ya lo sabe", "plaza-sub"));
            else fila.Add(comprar("intel-comprar-rumor", precio, () => new Orden { ComprarRumor = new OrdenComprarRumor { Origen = new OrdenComprarRumor.Types.Origen { Tipo = t.Tipo, Id = t.Id }, Tipo = "vetas_estano" } }, null));
            s.Add(fila);
            donde.Add(s);
        }

        /// <summary>El informe de plaza abierto (`detalleInforme`): la plaza, su Facción y cuándo es la foto; sus edificios y su guarnición en iconos con
        /// su cuenta, y cada recinto de muralla con lo levantado.</summary>
        VisualElement DetalleDeInforme(InformePlaza inf)
        {
            var caja = Dialogo.Caja("intel-informe");
            caja.name = "informe-detalle";
            var cab = Dialogo.Caja("intel-informe-cab");
            cab.Add(Dialogo.Texto($"{(inf.HasNombre ? inf.Nombre : inf.AsentamientoId)} · nivel {inf.Nivel}", "pacto-plaza"));
            cab.Add(Chip($"📜 hace {Hace(AhoraMs - inf.ConocidoEnMs)}"));
            caja.Add(ConAyuda(cab, "Es una foto: no se actualiza. Su almacén no viene en el informe."));
            var hombres = inf.Guarnicion.Sum(g => (long)g.Cantidad);
            var chips = Dialogo.Caja("intel-informe-chips");
            chips.Add(Chip($"🧔 {inf.HeroesIds.Count}"));
            chips.Add(Chip($"🛡 {hombres}"));
            chips.Add(Chip($"🏛 {inf.Edificios.Count}"));
            caja.Add(chips);
            caja.Add(Kicker("Edificios"));
            var edificios = Dialogo.Caja("intel-iconos");
            foreach (var g in inf.Edificios.GroupBy(e => e.Tipo))
            {
                var ic = Dialogo.Caja("intel-icono");
                ic.tooltip = $"{Edificios.Nombre(g.Key)}: {g.Count()}";
                ic.Add(IconosDelMundo.Icono("edificio", g.Key, 24, IconosDelMundo.Bronce));
                ic.Add(Dialogo.Texto(g.Count().ToString(), "pacto-plaza"));
                edificios.Add(ic);
            }
            caja.Add(edificios);
            caja.Add(Kicker("Guarnición"));
            if (hombres == 0) caja.Add(Dialogo.Texto("Sin guarnición.", "plaza-sub"));
            else
            {
                var tropas = Dialogo.Caja("intel-iconos");
                foreach (var g in inf.Guarnicion.GroupBy(x => x.TropaId))
                {
                    var ic = Dialogo.Caja("intel-icono");
                    var n = g.Sum(x => (long)x.Cantidad);
                    if (PiezasDeTropa.Miniatura(IdDeTropa(g.Key), 26, $"{NombreDeTropa(IdDeTropa(g.Key))}: {n}") is VisualElement mini) ic.Add(mini);
                    ic.Add(Dialogo.Texto(n.ToString(), "pacto-plaza"));
                    tropas.Add(ic);
                }
                caja.Add(tropas);
            }
            caja.Add(Kicker("Murallas"));
            if (inf.Recintos.Count == 0) caja.Add(Dialogo.Texto("Sin recinto.", "plaza-sub"));
            foreach (var r in inf.Recintos)
            {
                var hechas = Math.Max(0, Math.Min(r.Celdas.Count, (int)r.Avance + 1));
                var fila = Dialogo.Caja("intel-recinto");
                fila.Add(Dialogo.Texto($"🧱 Nivel {r.Nivel}", "plaza-sub"));
                fila.Add(BarraAvance(r.Celdas.Count == 0 ? 0 : (float)hechas / r.Celdas.Count));
                fila.Add(Dialogo.Texto($"{hechas}/{r.Celdas.Count}", "pacto-plaza"));
                caja.Add(fila);
            }
            return caja;
        }
    }
}

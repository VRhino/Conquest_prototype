// Por dentro de un pueblo, una aldea o una avanzada (paso 9 del bloque 4, tanda 3), calcado de BronzeAgeClient (`main.ts`: `montarPueblo`,
// `montarAvanzada`; `ui/pueblos.ts`: el interior del pueblo; `ui/avanzadas.ts`: las partes de la avanzada; `ciudad3d/pueblo.ts` y
// `ciudad3d/avanzada.ts`). El mismo esqueleto que la plaza (el HUD de `Plaza.uxml`): el blasón del sitio, su almacén en el pie, el riel con sus
// partes y la ventana de cada una, y «Salir al mundo». La escena es FeudoScene: el pueblo, con la planta que manda el motor estando dentro; la
// avanzada, con la planta de su etapa hecha aquí, como el prototipo (el motor no la manda): atalaya, fuerte o ciudadela.
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
    public partial class PantallasDeMundo
    {
        /// <summary>La parte abierta del riel del sitio (resumen, guarnicion, acarreo, carga, almacen, tropa) o null.</summary>
        string parteDeSitio;
        readonly Dictionary<string, string> firmasDeSitio = new Dictionary<string, string>();

        Pueblo PuebloDondeEsta() => HeroeAhora()?.Ubicacion is UbicacionHeroe u && u.DondeCase == UbicacionHeroe.DondeOneofCase.PuebloId
            ? c.Sesion.Almacen.Buscar(TipoEntidad.Pueblo, u.PuebloId)?.Pueblo : null;
        Avanzada AvanzadaDondeEsta() => HeroeAhora()?.Ubicacion is UbicacionHeroe u && u.DondeCase == UbicacionHeroe.DondeOneofCase.AvanzadaId
            ? c.Sesion.Almacen.Buscar(TipoEntidad.Avanzada, u.AvanzadaId)?.Avanzada : null;

        Asentamiento MadreDe(string madreId) => c.Sesion.Almacen.Buscar(TipoEntidad.Asentamiento, madreId)?.Asentamiento;
        string NombreDeMadre(string madreId) => MadreDe(madreId) is Asentamiento a && a.HasNombre ? a.Nombre
            : c.Sesion.Almacen.Buscar(TipoEntidad.FichaConocida, madreId)?.FichaConocida?.Nombre ?? "su plaza madre";

        /// <summary>¿Gobierna o es Maestro de Obras de la madre? (`gestionaLaMadre`)</summary>
        bool GestionaLaMadre(string madreId, Heroe yo) => MadreDe(madreId) is Asentamiento a && CargoConstructor(a, yo.Id) != null;

        // ─────────────────────────── la planta ───────────────────────────

        /// <summary>La planta del pueblo (`puebloEnVista`): las piezas que manda el motor estando dentro, sus calles y caminos, y la puerta al final
        /// del camino que más se aleja.</summary>
        static void LlenarPlantaDePueblo(SelectorDePersonaje.InteriorParaVer interior, Pueblo x)
        {
            interior.Edificios.Clear(); interior.Calles.Clear(); interior.Muralla.Clear(); interior.Puerta.Clear();
            foreach (var e in x.Planta)
                interior.Edificios.Add(new SelectorDePersonaje.PiezaDePlanta
                {
                    Id = e.Id, Tipo = e.Tipo, X = (e.Posicion?.X ?? 0) / 1000.0, Y = (e.Posicion?.Y ?? 0) / 1000.0, Ancho = e.Ancho, Alto = e.Alto,
                    Nivel = (int)Math.Max(1, Math.Min(3, x.HasNivelMadre ? x.NivelMadre : 1)),
                });
            var trazado = x.Trazado ?? new TrazadoUrbano();
            foreach (var r in trazado.Calles) interior.Calles.Add(Pieza(r, "calle"));
            foreach (var r in trazado.Caminos) interior.Calles.Add(Pieza(r, "camino"));
            // La puerta, donde aparece el héroe: al sur de todo, fuera de las piezas y de las calles (el pueblo no tiene muralla ni puerta propia).
            double T = trazado.UnidadesPorCelda > 0 ? trazado.UnidadesPorCelda : 3;
            var todo = interior.Edificios.Concat(interior.Calles).ToList();
            double sur = todo.Count > 0 ? todo.Max(r => r.Y + r.Alto / 2) : Math.Max(trazado.RadioUl, 18);
            interior.Puerta.Add(new SelectorDePersonaje.PiezaDePlanta { Tipo = "puerta", X = 0, Y = sur + 2 * T, Ancho = T, Alto = T });
        }

        /// <summary>Lo que hay dentro de cada etapa de una avanzada, en ul (`ciudad3d/avanzada.ts`: PIEZAS): tipo, x, y, ancho, alto.</summary>
        static readonly Dictionary<int, (string tipo, double x, double y, double ancho, double alto)[]> PiezasDeAvanzada = new Dictionary<int, (string, double, double, double, double)[]>
        {
            [1] = new[] { ("torreVigia", -3.0, -6.0, 6.0, 6.0), ("pozo", 3, 0, 3, 3), ("lenera", -6, 0, 3, 3) },
            [2] = new[] { ("torreHomenaje", -3.0, -12.0, 6.0, 6.0), ("barracon", -12, -3, 9, 6), ("almacen", 3, -3, 9, 6), ("lenera", 6, -12, 6, 6), ("pozo", -12, -12, 3, 3) },
            [3] = new[] { ("torreHomenaje", -6.0, -21.0, 12.0, 12.0), ("barracon", -21, -6, 12, 9), ("almacen", 9, -6, 12, 9), ("granero", 9, 6, 9, 9), ("plazaDeArmas", -18, 6, 12, 9), ("pozo", 3, 15, 3, 3) },
        };

        /// <summary>Nivel de los edificios (su estilo) y de la muralla, por etapa (`NIVEL` del prototipo).</summary>
        static (int plaza, int muro) NivelDeAvanzada(int etapa) => etapa == 1 ? (1, 1) : etapa == 2 ? (3, 2) : (4, 3);

        /// <summary>Un anillo cuadrado de celdas, de `c0` a `c1`, en orden; torres en las esquinas y la puerta en las dos celdas del medio de abajo.</summary>
        static List<(int col, int row, string clase)> AnilloCuadrado(int c0, int c1)
        {
            var celdas = new List<(int, int, string)>();
            string Clase(int col, int row) => (col == c0 || col == c1) && (row == c0 || row == c1) ? "torre" : row == c1 && (col == -1 || col == 0) ? "puerta" : "muro";
            for (int col = c0; col < c1; col++) celdas.Add((col, c0, Clase(col, c0)));
            for (int row = c0; row < c1; row++) celdas.Add((c1, row, Clase(c1, row)));
            for (int col = c1; col > c0; col--) celdas.Add((col, c1, Clase(col, c1)));
            for (int row = c1; row > c0; row--) celdas.Add((c0, row, Clase(c0, row)));
            return celdas;
        }

        /// <summary>Un anillo redondo de radio `r` celdas, sin huecos; una torre cada seis celdas y la puerta abajo.</summary>
        static List<(int col, int row, string clase)> AnilloRedondo(int r)
        {
            var celdas = new List<(int col, int row, string clase)>();
            var vistas = new HashSet<(int, int)>();
            var pasos = (int)Math.Ceiling(2 * Math.PI * r * 4);
            for (int i = 0; i < pasos; i++)
            {
                var a = i / (double)pasos * Math.PI * 2;
                int col = (int)Math.Round(Math.Cos(a) * r - 0.5, MidpointRounding.AwayFromZero), row = (int)Math.Round(Math.Sin(a) * r - 0.5, MidpointRounding.AwayFromZero);
                if (vistas.Add((col, row))) celdas.Add((col, row, "muro"));
            }
            var abajo = celdas.Max(k => k.row);
            for (int i = 0; i < celdas.Count; i++)
            {
                var k = celdas[i];
                if (k.row == abajo && (k.col == -1 || k.col == 0)) celdas[i] = (k.col, k.row, "puerta");
                else if (i % 6 == 3) celdas[i] = (k.col, k.row, "torre");
            }
            return celdas;
        }

        /// <summary>La planta de la avanzada por su etapa (`avanzadaEnVista`): las piezas de dentro, el muro celda a celda (en ruinas, en pie solo el
        /// 45 %), la calle de la puerta al edificio de dentro y el camino hacia fuera.</summary>
        static void LlenarPlantaDeAvanzada(SelectorDePersonaje.InteriorParaVer interior, Avanzada x)
        {
            const double T = 3;
            interior.Edificios.Clear(); interior.Calles.Clear(); interior.Muralla.Clear(); interior.Puerta.Clear();
            var etapa = (int)Math.Max(1, Math.Min(3, x.Etapa));
            var ruinas = x.Estado == EstadoAvanzada.AvanzadaEnRuinas;
            var (nivelPlaza, nivelMuro) = NivelDeAvanzada(etapa);
            var piezas = PiezasDeAvanzada[etapa];
            for (int i = 0; i < piezas.Length; i++)
            {
                var (tipo, px, py, ancho, alto) = piezas[i];
                interior.Edificios.Add(new SelectorDePersonaje.PiezaDePlanta
                {
                    Id = $"{x.Id}-{tipo}-{i}", Tipo = tipo, X = px + ancho / 2, Y = py + alto / 2, Ancho = ancho, Alto = alto, Nivel = nivelPlaza, Estado = ruinas ? "cola" : null,
                });
            }
            var celdas = etapa == 1 ? AnilloCuadrado(-3, 2) : etapa == 2 ? AnilloCuadrado(-5, 4) : AnilloRedondo(9);
            var enPie = ruinas ? (int)Math.Floor(celdas.Count * 0.45) : celdas.Count;
            for (int i = 0; i < celdas.Count; i++)
            {
                var (col, row, clase) = celdas[i];
                var pieza = new SelectorDePersonaje.PiezaDePlanta { Tipo = clase, X = (col + 0.5) * T, Y = (row + 0.5) * T, Ancho = T, Alto = T, Nivel = nivelMuro, Estado = i < enPie ? null : "plan" };
                interior.Muralla.Add(pieza);
            }
            var abajo = celdas.Max(k => k.row);
            var ancla = piezas[0];
            var fondo = ancla.y + ancla.alto;
            interior.Calles.Add(new SelectorDePersonaje.PiezaDePlanta { Tipo = "calle", X = 0, Y = (fondo + abajo * T) / 2, Ancho = 2 * T, Alto = Math.Max(T, abajo * T - fondo) });
            interior.Calles.Add(new SelectorDePersonaje.PiezaDePlanta { Tipo = "camino", X = 0, Y = (abajo + 1) * T + 4 * T, Ancho = 2 * T, Alto = 8 * T });
            interior.Puerta.Add(new SelectorDePersonaje.PiezaDePlanta { Tipo = "puerta", X = 0, Y = (abajo + 0.5) * T, Ancho = 2 * T, Alto = T });
        }

        // ─────────────────────────── montar ───────────────────────────

        void InteriorDePueblo(Pueblo x) => InteriorDeSitio("pueblo", x.Id);
        void InteriorDeAvanzada(Avanzada x) => InteriorDeSitio("avanzada", x.Id);

        /// <summary>El interior de un pueblo o una avanzada: FeudoScene encima, con su planta y el héroe andando, y el HUD de la plaza con lo suyo.</summary>
        async void InteriorDeSitio(string tipo, string id)
        {
            if (SelectorDePersonaje.Interior != null) return;
            var v = Montar("Plaza");
            v.AddToClassList("sitio-" + tipo);
            plazaId = null;
            parteDeSitio = null;
            firmasDeSitio.Clear();
            relojesDePlaza.Clear();
            Pueblo P() => c.Sesion.Almacen.Buscar(TipoEntidad.Pueblo, id)?.Pueblo;
            Avanzada A() => c.Sesion.Almacen.Buscar(TipoEntidad.Avanzada, id)?.Avanzada;
            var botonAvisos = v.Q<Button>("avisos");
            botonAvisos.clicked += panelAvisos.Alternar;
            v.Q<Button>("cerrar-sesion").clicked += async () =>
            {
                if (await dialogo.Abrir("Tu héroe sale del mundo y se cierra la sesión en este equipo.", "¿Cerrar sesión?", "Cerrar sesión")) _ = CerrarSesion();
            };
            BarraDelJugador(v);
            v.Q<Button>("salir-al-mundo").clicked += () => _ = SalirDelSitio(tipo, id);

            var interior = new SelectorDePersonaje.InteriorParaVer { Heroe = HeroeParaVer(c.Datos) };
            string plantaPintada = null;
            void Planta()
            {
                string firma;
                if (tipo == "pueblo") { var x = P(); if (x == null) return; firma = $"{x.Version}|{x.Planta.Count}|{x.NivelAldea}"; if (firma != plantaPintada) LlenarPlantaDePueblo(interior, x); }
                else { var x = A(); if (x == null) return; firma = $"{x.Etapa}|{x.Estado}"; if (firma != plantaPintada) LlenarPlantaDeAvanzada(interior, x); }
                if (firma == plantaPintada) return;
                plantaPintada = firma;
                interior.Version++;
            }
            Planta();
            interior.AlInteractuar = zona => { if (zona == "puerta") _ = SalirDelSitio(tipo, id); };
            var cartel = v.Q<Label>("cartel");
            // Las mismas medidas que la plaza (el riel centrado, la ventana, el pie): el mismo esqueleto.
            v.RegisterCallback<GeometryChangedEvent>(_ => AjustarPlaza(v));
            v.Q("plaza-pie").RegisterCallback<GeometryChangedEvent>(_ => AjustarPlaza(v));
            v.schedule.Execute(() =>
            {
                PintarAvisos(botonAvisos);
                var yo = HeroeAhora();
                if (yo == null) return;
                Planta();
                if (!RatonPulsado() && PintarHudDeSitio(v, tipo, id, yo)) AjustarPlaza(v);
                foreach (var r in relojesDePlaza.ToList()) r.reloj();
                Poner(cartel, !interior.PanelAbierto && interior.ZonaCercana == "puerta" ? "F · Salir al mundo" : null);
            }).Every(250);
            v.schedule.Execute(() =>
            {
                var teclado = UnityEngine.InputSystem.Keyboard.current;
                if (teclado == null || !teclado.escapeKey.wasPressedThisFrame || capaEncima.Q(className: "dialogo-velo") != null || parteDeSitio == null) return;
                parteDeSitio = null;
                firmasDeSitio.Clear();
            }).Every(0);

            var volverCamaras = ApartarCamarasDelMundo();
            var mundo = EscenaDelMundo();
            async Task Cerrar()
            {
                if (SelectorDePersonaje.Interior == null) return;
                cerrarEscenaEncima = null;
                cerrarInterior = null;
                if (mundo.IsValid() && mundo.isLoaded) UnityEngine.SceneManagement.SceneManager.SetActiveScene(mundo);
                var descarga = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(Feudo);
                while (descarga != null && !descarga.isDone) await Task.Yield();
                SelectorDePersonaje.Interior = null;
                volverCamaras();
            }
            SelectorDePersonaje.Interior = interior;
            cerrarEscenaEncima = Cerrar;
            cerrarInterior = Cerrar;
            var carga = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(Feudo, UnityEngine.SceneManagement.LoadSceneMode.Additive);
            while (!carga.isDone) await Task.Yield();
            var escena = UnityEngine.SceneManagement.SceneManager.GetSceneByName(Feudo);
            if (escena.IsValid() && escena.isLoaded) UnityEngine.SceneManagement.SceneManager.SetActiveScene(escena);
        }

        /// <summary>«Salir al mundo» (`ventanaSalida.ts`): vuelves a la puerta con la columna con la que entraste y todo lo que trajiste; tu
        /// guarnición se queda.</summary>
        async Task SalirDelSitio(string tipo, string id)
        {
            var interior = SelectorDePersonaje.Interior;
            if (interior != null) interior.PanelAbierto = true;
            var donde = tipo == "pueblo" ? "el pueblo" : "la " + NombreDeEtapa((int)(c.Sesion.Almacen.Buscar(TipoEntidad.Avanzada, id)?.Avanzada?.Etapa ?? 1)).ToLowerInvariant();
            var si = await dialogo.Abrir($"Vuelves a la puerta de {donde} con todo lo que trajiste; tu guarnición se queda.", "Salir al mundo", "Salir al mapa", "Volver");
            if (interior != null) interior.PanelAbierto = false;
            if (!si) return;
            var h = HeroeAhora();
            if (h == null) return;
            var fuera = await OrdenDePlaza(tipo == "pueblo"
                ? new Orden { SalirDePueblo = new OrdenSalirDePueblo { PuebloId = id, HeroeId = h.Id } }
                : new Orden { SalirDeAvanzada = new OrdenSalirDeAvanzada { AvanzadaId = id, HeroeId = h.Id } });
            if (!fuera) return;
            // Como al salir de la plaza: se cierra FeudoScene y vuelve el mapa con la columna a la puerta.
            if (cerrarInterior != null) await cerrarInterior();
            if (this != null) Mapa();
        }

        bool CambiaSitio(string seccion, string firma)
        {
            if (firmasDeSitio.TryGetValue(seccion, out var antes) && antes == firma) return false;
            firmasDeSitio[seccion] = firma;
            relojesDePlaza.RemoveAll(r => r.seccion == seccion);
            return true;
        }

        // ─────────────────────────── el HUD ───────────────────────────

        /// <summary>Las partes del riel (`pestanasDePueblo`, `partesDeAvanzada`): (id, título, familia, icono).</summary>
        List<(string id, string titulo, string familia, string icono)> PartesDelSitio(string tipo, string id, Heroe yo)
        {
            var r = new List<(string, string, string, string)> { ("resumen", "Resumen", "riel", "gobierno") };
            if (tipo == "pueblo")
            {
                var x = c.Sesion.Almacen.Buscar(TipoEntidad.Pueblo, id)?.Pueblo;
                var propio = x != null && MadreDe(x.AsentamientoMadreId)?.FaccionId is string f && yo.HasFaccionId && f == yo.FaccionId;
                r.Add(("guarnicion", "Guarnición", "edificio", "muralla"));
                r.Add(("acarreo", "Acarreo y escolta", "simbolo", "carro"));
                if (propio && MiColumna() != null) r.Add(("carga", "Carga", "edificio", "almacen"));
            }
            else
            {
                var x = c.Sesion.Almacen.Buscar(TipoEntidad.Avanzada, id)?.Avanzada;
                var propia = x != null && MadreDe(x.AsentamientoMadreId)?.FaccionId is string f && yo.HasFaccionId && f == yo.FaccionId;
                if (propia) r.Add(("guarnicion", "Guarnición", "edificio", "muralla"));
                if (x != null && x.AlmacenPorRecurso.Values.Any(e => e.Capacidad > 0)) r.Add(("almacen", "Almacén", "edificio", "almacen"));
                if (propia) r.Add(("tropa", "Tropa de serie", "edificio", "barracon"));
            }
            return r;
        }

        /// <summary>Repinta lo que haya cambiado del HUD del sitio; true si se rehízo algo que mueve las medidas (pie, riel o ventana).</summary>
        bool PintarHudDeSitio(VisualElement v, string tipo, string id, Heroe yo)
        {
            var p = tipo == "pueblo" ? c.Sesion.Almacen.Buscar(TipoEntidad.Pueblo, id)?.Pueblo : null;
            var x = tipo == "avanzada" ? c.Sesion.Almacen.Buscar(TipoEntidad.Avanzada, id)?.Avanzada : null;
            if (p == null && x == null) return false;
            var otra = false;
            var madreId = p?.AsentamientoMadreId ?? x.AsentamientoMadreId;
            var version = p?.Version ?? x.Version;
            var col = MiColumna();
            var firmaBase = $"{version}|{p?.VersionInterior}|{yo.Version}|{col?.Version}|{string.Join(",", EscuadrasDelHeroe(yo).Select(s => s.Version))}";
            if (CambiaSitio("blason", $"{version}|{yo.FaccionId}")) PintarBlasonDeSitio(v.Q("blason"), p, x, yo);
            if (CambiaSitio("pie", $"{version}|{p?.VersionInterior}|{Math.Floor(AhoraMs / 60000)}")) { PintarPieDeSitio(v.Q("plaza-pie"), p, x); otra = true; }
            var letrero = v.Q<Label>("letrero");
            var batalla = p?.HasBatallaId == true || x?.HasBatallaId == true;
            letrero.text = batalla ? (p != null ? "⚔ Están atacando el pueblo: si sigues dentro, defiendes." : "⚔ Están atacando la avanzada: si sigues dentro, defiendes.") : "";
            Ver(letrero, batalla);
            var partes = PartesDelSitio(tipo, id, yo);
            if (parteDeSitio != null && partes.All(q => q.id != parteDeSitio)) parteDeSitio = null;
            if (CambiaSitio("riel", $"{parteDeSitio}|{string.Join(",", partes.Select(q => q.id))}"))
            {
                var riel = v.Q("riel");
                riel.Clear();
                foreach (var (pid, titulo, familia, icono) in partes)
                {
                    var parte = pid;
                    var b = new Button(() => { parteDeSitio = parteDeSitio == parte ? null : parte; firmasDeSitio.Clear(); }) { name = "riel-" + pid };
                    b.AddToClassList("plaza-riel-boton");
                    b.EnableInClassList("activo", parteDeSitio == pid);
                    b.Add(IconosDelMundo.Icono(familia, icono, 28, parteDeSitio == pid ? IconosDelMundo.OroClaro : IconosDelMundo.Bronce));
                    b.Add(Dialogo.Texto(titulo, "plaza-riel-etiqueta"));
                    riel.Add(b);
                }
                Ver(riel, riel.childCount > 0);
                otra = true;
            }
            if (!CambiaSitio("ventana", $"{parteDeSitio}|{firmaBase}|{(parteDeSitio == "acarreo" ? c.Sesion.Almacen.Buscar(TipoEntidad.Caravana, p?.CarroAcarreoId ?? "")?.Caravana?.Version : 0)}")) return otra;
            var ventana = v.Q("plaza-ventana");
            ventana.Clear();
            ventana.RemoveFromClassList("plaza-ventana-completa");
            if (parteDeSitio == null) { Ver(ventana, false); return true; }
            Ver(ventana, true);
            var (_, nombreParte, fam, ic) = partes.First(q => q.id == parteDeSitio);
            var cab = Dialogo.Caja("plaza-ventana-cab");
            var titulo2 = Dialogo.Caja("plaza-ventana-titulo");
            titulo2.Add(IconosDelMundo.Icono(fam, ic, 28, IconosDelMundo.Bronce));
            titulo2.Add(Dialogo.Texto(nombreParte, "plaza-ventana-nombre"));
            cab.Add(titulo2);
            var cerrar = Dialogo.Boton("✕", "plaza-ventana-cerrar", () => { parteDeSitio = null; firmasDeSitio.Clear(); });
            cerrar.name = "ventana-cerrar";
            cerrar.tooltip = "Cerrar (Esc)";
            cab.Add(cerrar);
            ventana.Add(cab);
            var cuerpo = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden, name = "sitio-" + parteDeSitio };
            cuerpo.AddToClassList("plaza-ventana-cuerpo");
            ventana.Add(cuerpo);
            switch (parteDeSitio)
            {
                case "resumen": if (p != null) ResumenDePueblo(cuerpo, p, yo); else ResumenDeAvanzada(cuerpo, x); break;
                case "guarnicion": if (p != null) GuarnicionDePueblo(cuerpo, p); else { SerieDeAvanzada(cuerpo, x); GuarnicionDeAvanzada(cuerpo, x); } break;
                case "acarreo": AcarreoYEscolta(cuerpo, p, true); break;
                case "carga": CargaDelPueblo(cuerpo, p); break;
                case "almacen": AlmacenDeAvanzada(cuerpo, x); break;
                case "tropa":
                    if (MadreDe(x.AsentamientoMadreId) is Asentamiento madre && madre.Cargos?.GobernadorId == yo.Id && madre.Cargos.HasGobernadorId && EleccionDeTropa(madre, x) is VisualElement t) cuerpo.Add(t);
                    else cuerpo.Add(Dialogo.Texto($"La tropa de serie la elige el Gobernador de {NombreDeMadre(x.AsentamientoMadreId)}.", "plaza-nota"));
                    break;
            }
            return true;
        }

        /// <summary>El blasón del sitio (`htmlBlasonInterior`): su icono, su nombre, «De {madre}» y si es de tu Facción o de visita, con el nivel.</summary>
        void PintarBlasonDeSitio(VisualElement blason, Pueblo p, Avanzada x, Heroe yo)
        {
            blason.Clear();
            var madreId = p?.AsentamientoMadreId ?? x.AsentamientoMadreId;
            var propio = MadreDe(madreId)?.FaccionId is string f && yo.HasFaccionId && f == yo.FaccionId;
            var emblema = Dialogo.Caja("territorio-emblema sitio-emblema");
            emblema.Add(p != null ? EmblemaDePueblo(p, 34) : IconosDelMundo.Icono("simbolo", "torre", 34, IconosDelMundo.OroClaro));
            blason.Add(emblema);
            var texto = Dialogo.Caja("plaza-blason-texto");
            var nombre = Dialogo.Caja("plaza-blason-nombre");
            var n = Dialogo.Texto(p != null ? NombreSinNivel(p) : NombreDeEtapa((int)x.Etapa), "plaza-nombre");
            n.name = "nombre-sitio";
            nombre.Add(n);
            texto.Add(nombre);
            texto.Add(Dialogo.Texto($"De {NombreDeMadre(madreId)}", "plaza-blason-small"));
            texto.Add(Dialogo.Texto(propio ? "De tu Facción" : "De visita", "plaza-blason-small"));
            blason.Add(texto);
            if (p != null) blason.Add(Medallon(NivelDePueblo(p), "", 28, 32));
        }

        /// <summary>El almacén en el pie (`almacenDelPie`): lo que guarda con su barra; la atalaya no tiene almacén.</summary>
        void PintarPieDeSitio(VisualElement pie, Pueblo p, Avanzada x)
        {
            pie.Clear();
            var recursos = p != null ? p.AlmacenPorRecurso.ToDictionary(kv => kv.Key, kv => kv.Value) : x.AlmacenPorRecurso.ToDictionary(kv => kv.Key, kv => kv.Value);
            var capacidad = p != null ? (p.AlmacenLocal?.Capacidad ?? 0) / 1000.0 : recursos.Values.Select(e => e.Capacidad / 1000.0).DefaultIfEmpty(0).Max();
            if (capacidad <= 0) return;
            var placa = Placa("almacen", "Almacén", "placa-almacen");
            var piezas = placa.Q(className: "plaza-placa-piezas");
            var total = recursos.Values.Sum(e => Valor(e.Cantidad));
            piezas.Add(Dialogo.Texto($"{Math.Floor(total)} / {Math.Floor(capacidad)}", "plaza-coste-cifra"));
            foreach (var kv in recursos.Where(kv => Valor(kv.Value.Cantidad) >= 1).OrderBy(kv => kv.Key))
                piezas.Add(IconosDelMundo.ChipDeRecurso(kv.Key, Math.Floor(Valor(kv.Value.Cantidad)).ToString(), CosasDelMapa.NombreDeRecurso(kv.Key)));
            pie.Add(placa);
        }

        // ─────────────────────────── las partes del pueblo ───────────────────────────

        /// <summary>«Resumen» del pueblo: fertilidad, nivel, lo que da su cultivo, el rebaño y «Mejorar a nivel N» para quien gestiona la madre.</summary>
        void ResumenDePueblo(VisualElement cuerpo, Pueblo x, Heroe yo)
        {
            var aldea = EsAldea(x);
            var nivel = NivelDePueblo(x);
            var cult = CultivoDe(x);
            cuerpo.Add(Dialogo.Texto($"{NombreSinNivel(x)} de nivel {nivel}, de {NombreDeMadre(x.AsentamientoMadreId)}.", "plaza-nota"));
            var chips = Dialogo.Caja("plaza-anillo-chips");
            if (x.HasFertilidad && ConGranjas(cult)) chips.Add(ChipConIcono(IconosDelMundo.Icono("glifo", "viveres", 12), (x.Fertilidad / 1000.0).ToString("0.00", Es), "", "Fertilidad del punto de la aldea"));
            if (!aldea && NodoDelMotor(x.NodoId) is NodoRecurso nodo) chips.Add(ChipConIcono(IconosDelMundo.Icono("recurso", nodo.Tipo, 12), CosasDelMapa.NombreDeRecurso(nodo.Tipo)));
            if (chips.childCount > 0) cuerpo.Add(chips);
            if (aldea) cuerpo.Add(Dialogo.Texto(Cultivos.First(k => k.id == cult).uso, "plaza-nota"));
            if (x.HasRebanoCabezas)
            {
                var corrales = (int?)ParamsPueblos?["aldea"]?["niveles"]?[nivel.ToString()]?["corrales"];
                var tope = (double?)c.Datos.Bloque("Economia_Construccion", "ESPECIES")?[x.RebanoEspecie]?["topePorCorral"] * corrales;
                var cabezas = x.RebanoCabezas / 1000.0;
                var r = Dialogo.Caja("territorio-medida rebano");
                r.Add(IconosDelMundo.ConIcono("recurso", "livestock", Dialogo.Texto("Rebaño", "territorio-medida-nombre"), 13));
                r.Add(tope > 0 ? BarraAvance((float)Math.Min(1, cabezas / tope.Value)) : new VisualElement());
                r.Add(Dialogo.Texto($"{Math.Floor(cabezas)}{(tope > 0 ? $" / {tope}" : "")}", "territorio-medida-cifra"));
                cuerpo.Add(r);
            }
            var siguiente = aldea ? ParamsPueblos?["aldea"]?["niveles"]?[(nivel + 1).ToString()] : ParamsPueblos?["extractor"]?["niveles"]?[(nivel + 1).ToString()];
            var mejorando = ObraSobre(x.Id) != null;
            if (siguiente?["coste"] is JObject coste && !mejorando && GestionaLaMadre(x.AsentamientoMadreId, yo))
            {
                var acciones = Dialogo.Caja("plaza-anillo-acciones");
                Button b = null;
                b = Dialogo.Boton("", "plaza-anillo-mejorar", async () => await OrdenDePlaza(new Orden { MejorarAldea = new OrdenMejorarAldea { HeroeId = yo.Id, PuebloId = x.Id } }, b));
                b.name = "mejorar-" + x.Id;
                b.Add(Dialogo.Texto($"Mejorar a nivel {nivel + 1}", "territorio-boton-texto"));
                b.Add(CosteLlano(coste));
                b.tooltip = "Lo que pide se lleva al pueblo como una obra.";
                acciones.Add(b);
                cuerpo.Add(acciones);
            }
            else if (mejorando) cuerpo.Add(Dialogo.Texto($"Subiendo a nivel {nivel + 1}: su obra espera los materiales a la puerta.", "plaza-nota"));
            cuerpo.Add(Dialogo.Texto("Dentro: tu columna está fuera del mapa, nadie la ve y sigue comiendo de sus víveres. Al salir vuelve a la puerta con todo lo que trajo.", "plaza-nota"));
        }

        /// <summary>El carro de acarreo y su escolta (`htmlEscoltaDentro`, `escolta`): el carro (su estado y lo que lleva) y las escuadras cedidas, con
        /// «Retirar» y las que se pueden ceder (de tu columna dentro del pueblo; del campamento desde Territorio, si resides en la madre).</summary>
        void AcarreoYEscolta(VisualElement cuerpo, Pueblo x, bool desdeColumna)
        {
            cuerpo.Add(CarroDeAcarreo(x));
            var carro = x.HasCarroAcarreoId ? c.Sesion.Almacen.Buscar(TipoEntidad.Caravana, x.CarroAcarreoId)?.Caravana : null;
            if (carro == null) return;
            Escolta(cuerpo, carro, x.AsentamientoMadreId, desdeColumna);
        }

        /// <summary>El cupo de escolta del carro: el del Mercado de la plaza madre por su nivel o, si no tiene, el del primer nivel.</summary>
        int CupoDeEscolta(string madreId)
        {
            var tabla = c.Datos.Bloque("Comercio_Caravanas", "CARAVANA_ESCOLTA")?["liderazgoPorNivelMercado"] as JArray;
            if (tabla == null || tabla.Count == 0) return 0;
            var mercado = MadreDe(madreId) is Asentamiento a ? Internos(a).Where(e => e.Tipo == "mercado" && Activo(e)).Select(NivelInterno).DefaultIfEmpty(0).Max() : 0;
            return (int)tabla[Math.Max(0, Math.Min(tabla.Count, Math.Max(1, mercado)) - 1)];
        }

        void Escolta(VisualElement cuerpo, Caravana carro, string madreId, bool desdeColumna)
        {
            var yo = HeroeAhora();
            if (yo == null) return;
            var parada = carro.Estado == EstadoCaravana.CaravanaDisponible;
            var cedidas = EscuadrasDelHeroe(yo).Where(s => s.Contenedor?.DondeCase == ContenedorEscuadron.DondeOneofCase.CaravanaId && s.Contenedor.CaravanaId == carro.Id).ToList();
            var cupo = CupoDeEscolta(madreId);
            var usado = carro.Escolta.Sum(e => LiderazgoDe(e.TropaId) ?? 0);
            if (carro.Escolta.Count == 0) usado = cedidas.Sum(s => LiderazgoDe(s.TropaId) ?? 0);
            var libre = cupo - usado;
            var caja = Dialogo.Caja("territorio-escolta");
            caja.name = "escolta-" + carro.Id;
            caja.Add(CupoEnBarra("Escolta del carro", usado, cupo));
            var otros = Math.Max(0, carro.EscoltaIds.Count - cedidas.Count);
            caja.Add(Dialogo.Texto((cedidas.Count + otros > 0 ? $"{cedidas.Count + otros} escuadra(s) de escolta{(otros > 0 ? $", {otros} de otros héroes" : "")}." : "Sin escolta.")
                + (parada ? "" : " Solo se cambia con el carro parado en el pueblo."), "plaza-nota"));
            foreach (var s in cedidas)
            {
                var sid = s.Id;
                caja.Add(FilaDeEscuadra(s, parada ? BotonDeOrden("Retirar", "quitar-escolta-" + sid, () => new Orden { QuitarEscolta = new OrdenQuitarEscolta { CaravanaId = carro.Id, HeroeId = yo.Id, EscuadronIds = { sid } } }) : null));
            }
            var reside = yo.HasResidenciaId && yo.ResidenciaId == madreId;
            var candidatas = !parada ? new List<Escuadron>() : desdeColumna ? EscuadrasDeLaColumna()
                : reside ? EscuadrasDelHeroe(yo).Where(s => s.Contenedor?.DondeCase == ContenedorEscuadron.DondeOneofCase.Campamento && !s.EnGuarnicion && s.Cantidad > 0).ToList() : new List<Escuadron>();
            foreach (var s in candidatas)
            {
                var sid = s.Id;
                var cabe = (LiderazgoDe(s.TropaId) ?? 0) <= libre;
                caja.Add(FilaDeEscuadra(s, BotonDeOrden(desdeColumna ? "Escoltar" : "Ceder", "escoltar-" + sid, () => new Orden { AsignarEscolta = new OrdenAsignarEscolta { CaravanaId = carro.Id, HeroeId = yo.Id, EscuadronIds = { sid } } },
                    cabe, cabe ? null : $"No cabe: quedan {Math.Max(0, libre)} de Liderazgo")));
            }
            if (parada && !desdeColumna && !reside) caja.Add(Dialogo.Texto("Solo quien reside en la plaza cede escolta.", "plaza-nota"));
            cuerpo.Add(caja);
        }

        /// <summary>Lo que produce el pueblo, lo único que se le puede dejar (`produce`): la aldea, lo de su cultivo y, con corrales, ganado, carne y
        /// leche; un extractor, lo que guarda.</summary>
        List<string> LoQueProduce(Pueblo x)
        {
            if (!EsAldea(x)) return x.AlmacenPorRecurso.Keys.ToList();
            var cult = CultivoDe(x);
            var r = new List<string>();
            if (ConGranjas(cult)) r.Add(ProduceDe(cult));
            if (ConCorrales(cult)) r.AddRange(new[] { "livestock", "carne", "leche" });
            return r.Distinct().ToList();
        }

        /// <summary>«Carga» (`htmlCargaDelPueblo`): del almacén del pueblo al carro de tu columna («Al carro») y del carro al pueblo lo que produce («Dejar»).</summary>
        void CargaDelPueblo(VisualElement cuerpo, Pueblo x)
        {
            var col = MiColumna();
            if (col == null) return;
            cuerpo.Add(Dialogo.Texto("Carga lo que guarda el pueblo hasta donde quepa en el carro, o déjale lo que produce hasta su capacidad.", "plaza-nota"));
            void Fila(string r, double n, bool cargar)
            {
                var fila = Dialogo.Caja("puerta-carga");
                fila.name = (cargar ? "cargar-" : "dejar-") + r;
                fila.Add(IconosDelMundo.ConIcono("recurso", r, Dialogo.Texto(CosasDelMapa.NombreDeRecurso(r), "puerta-carga-nombre"), 18));
                fila.Add(Dialogo.Texto(Math.Floor(n).ToString(), "puerta-carga-lleva"));
                var cantidad = new IntegerField { name = "cantidad-" + (cargar ? "cargar-" : "dejar-") + r, value = (int)Math.Floor(n) };
                cantidad.AddToClassList("puerta-carga-cantidad");
                fila.Add(cantidad);
                var recurso = r;
                fila.Add(BotonDeOrden(cargar ? "Al carro" : "Dejar", (cargar ? "boton-cargar-" : "boton-dejar-") + r, () => cargar
                    ? new Orden { CargarDePueblo = new OrdenCargarDePueblo { PuebloId = x.Id, Recurso = recurso, Cantidad = Math.Max(1, cantidad.value) * 1000L } }
                    : new Orden { DejarEnPueblo = new OrdenDejarEnPueblo { PuebloId = x.Id, Recurso = recurso, Cantidad = Math.Max(1, cantidad.value) * 1000L } }));
                cuerpo.Add(fila);
            }
            var cargar = x.AlmacenPorRecurso.Where(kv => Valor(kv.Value.Cantidad) >= 1).OrderBy(kv => kv.Key).ToList();
            cuerpo.Add(Dialogo.Texto("DEL ALMACÉN DEL PUEBLO AL CARRO", "plaza-sub puerta-sub"));
            if (cargar.Count == 0) cuerpo.Add(Dialogo.Texto("El almacén del pueblo está vacío.", "plaza-nota"));
            foreach (var kv in cargar) Fila(kv.Key, Valor(kv.Value.Cantidad), true);
            var dejar = LoQueProduce(x).Select(r => (r, n: (col.Suministro != null && col.Suministro.PorId.TryGetValue(r, out var v) ? v : 0) / 1000.0)).Where(t => t.n >= 1).ToList();
            if (dejar.Count > 0) cuerpo.Add(Dialogo.Texto("DEL CARRO AL ALMACÉN DEL PUEBLO", "plaza-sub puerta-sub"));
            foreach (var (r, n) in dejar) Fila(r, n, false);
        }

        // ─────────────────────────── las partes de la avanzada ───────────────────────────

        JObject SerieDeEtapa(Avanzada x) => CombateAvanzada?["guarnicionDeSerie"]?[ClaveDeEtapa((int)x.Etapa)] as JObject;

        /// <summary>«Resumen» de la avanzada (`htmlResumen`): qué es, de qué plaza, si está en ruinas o la atacan, y sus cifras.</summary>
        void ResumenDeAvanzada(VisualElement cuerpo, Avanzada x)
        {
            cuerpo.Add(Dialogo.Texto($"{NombreDeEtapa((int)x.Etapa)} de {NombreDeMadre(x.AsentamientoMadreId)}.", "plaza-nota"));
            if (x.Estado == EstadoAvanzada.AvanzadaEnRuinas)
                cuerpo.Add(Dialogo.Texto("En ruinas: no funciona nada." + (x.HasInvulnerableHastaMs && x.InvulnerableHastaMs > AhoraMs ? $" No se la puede atacar en {CuentaAtras(x.InvulnerableHastaMs)}." : ""), "plaza-nota falta"));
            else if (x.HasBatallaId) cuerpo.Add(Dialogo.Texto("La están atacando: si sigues dentro, defiendes.", "plaza-nota falta"));
            var chips = Dialogo.Caja("plaza-anillo-chips");
            var escuadras = SerieDeEtapa(x)?.Properties().Sum(p => (int)p.Value) ?? 0;
            if (escuadras > 0) chips.Add(ChipConIcono(IconosDelMundo.Icono("glifo", "atacar", 12), $"{escuadras} de serie", "", "Escuadras de serie, fuera de cupo"));
            var cupo = (int?)CombateAvanzada?["cupoGuarnicion"]?[ClaveDeEtapa((int)x.Etapa)];
            if (cupo != null) chips.Add(ChipConIcono(IconosDelMundo.Icono("glifo", "escudo", 12), $"{cupo} por héroe", "", "Cupo de guarnición por héroe, en Liderazgo"));
            var capacidad = x.AlmacenPorRecurso.Values.Select(e => e.Capacidad / 1000.0).DefaultIfEmpty(0).Max();
            if (capacidad > 0) chips.Add(ChipConIcono(IconosDelMundo.Icono("recurso", "_otro", 12), $"{Math.Floor(capacidad)} por recurso", "", "Lo que guarda de cada recurso"));
            cuerpo.Add(chips);
            cuerpo.Add(Dialogo.Texto("Dentro: tu columna está fuera del mapa, nadie la ve y sigue comiendo de sus víveres. Al salir vuelve a la puerta con todo lo que trajo; tu guarnición se queda.", "plaza-nota"));
        }

        /// <summary>La guarnición de serie de la avanzada (`htmlGuarnicion`): las escuadras de cada escalón con su tropa, y si está mermada.</summary>
        void SerieDeAvanzada(VisualElement cuerpo, Avanzada x)
        {
            var serie = SerieDeEtapa(x);
            if (serie == null) return;
            var porDefecto = (string)CombateAvanzada?["tropaPorDefecto"] ?? "";
            cuerpo.Add(Dialogo.Texto("DE SERIE", "plaza-sub puerta-sub"));
            var total = 0;
            foreach (var p in serie.Properties().OrderBy(p => int.Parse(p.Name)))
            {
                var escalon = uint.Parse(p.Name);
                var tropa = x.TropaElegidaPorEscalon.TryGetValue(escalon, out var t) ? t : porDefecto;
                total += (int)p.Value;
                var fila = Dialogo.Caja("puerta-escuadra");
                if (PiezasDeTropa.Miniatura(tropa, 30) is VisualElement mini) fila.Add(mini);
                var textos = Dialogo.Caja("puerta-escuadra-texto");
                textos.Add(Dialogo.Texto(NombreDeTropa(tropa), "aviso-titulo"));
                textos.Add(Dialogo.Texto($"{(int)p.Value} escuadra{((int)p.Value == 1 ? "" : "s")} · escalón {escalon}", "campo-ayuda"));
                fila.Add(textos);
                cuerpo.Add(fila);
            }
            var minutos = (double?)CombateAvanzada?["reposicionMinutos"];
            if (x.GuarnicionSerieIds.Count < total)
                cuerpo.Add(Dialogo.Texto($"Mermada: {x.GuarnicionSerieIds.Count} de {total}" + (minutos != null ? $"; cada escuadra caída vuelve en {(minutos >= 60 ? $"{Math.Floor(minutos.Value / 60)} h" : $"{minutos} min")}." : "."), "plaza-nota"));
            cuerpo.Add(Dialogo.Texto("TUYA", "plaza-sub puerta-sub"));
        }

        /// <summary>«Almacén» de la avanzada (`htmlAlmacen`): lo que guarda de cada recurso con su barra, dejar lo del carro y llenar los víveres.</summary>
        void AlmacenDeAvanzada(VisualElement cuerpo, Avanzada x)
        {
            foreach (var kv in x.AlmacenPorRecurso.Where(kv => kv.Value.Capacidad > 0).OrderBy(kv => kv.Key))
            {
                var n = Valor(kv.Value.Cantidad);
                var cap = kv.Value.Capacidad / 1000.0;
                var fila = Dialogo.Caja("territorio-medida");
                fila.name = "almacen-" + kv.Key;
                fila.Add(IconosDelMundo.ConIcono("recurso", kv.Key, Dialogo.Texto(CosasDelMapa.NombreDeRecurso(kv.Key), "territorio-medida-nombre"), 14));
                fila.Add(BarraAvance(cap > 0 ? (float)Math.Min(1, n / cap) : 0));
                fila.Add(Dialogo.Texto($"{Math.Floor(n)} / {Math.Floor(cap)}", "territorio-medida-cifra"));
                cuerpo.Add(fila);
            }
            CargaAlAlmacen(cuerpo, x);
            var col = MiColumna();
            var yo = HeroeAhora();
            var lider = col != null && yo != null && (col.Participantes.Count <= 1 || col.LiderId == yo.Id);
            var trigo = x.AlmacenPorRecurso.TryGetValue("trigo", out var tr) ? Valor(tr.Cantidad) : 0;
            if (lider && trigo >= 1)
            {
                var acciones = Dialogo.Caja("plaza-anillo-acciones");
                acciones.Add(BotonDeOrden("Llenar víveres", "repostar-" + x.Id, () => new Orden { Repostar = new OrdenRepostar { HeroeId = yo.Id, LugarId = x.Id } }, true,
                    $"Llena los víveres de tu columna con el trigo de su almacén (hay {Math.Floor(trigo)})."));
                cuerpo.Add(acciones);
            }
        }
    }
}

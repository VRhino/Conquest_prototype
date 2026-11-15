// La plaza por dentro (paso 7 del bloque 4, tanda 1; BronzeAgeClient: `main.ts` «PANTALLA ASENTAMIENTO», `ui/hudPlaza.ts`, `ui/vistaCiudad.ts`,
// `ui/panelCentro.ts`, `ui/panelResidencia.ts`). FeudoScene monta la plaza desde el motor (edificios por tipo, nivel y estado, calles y muralla
// celda a celda) y el héroe anda por ella; aquí, el HUD calcado del prototipo: arriba a la izquierda el blasón (escudo, nivel, nombre con su lápiz,
// casa y corona con sus tarjetas), la subida en curso y las obras con la cola; arriba al centro, las placas de Población, Almacén y Estado; arriba
// a la derecha, la barra del jugador; abajo, «Salir al mundo» y el riel (sus ventanas llegan en las tandas 2 y 3; el minimapa, más adelante). Con
// F junto a un edificio, su ficha al borde derecho (recetas, mejorar, la cola, cancelar la obra); el medallón abre «Subir de nivel». Lo que no se
// puede hacer no sale. Las cifras, de la plaza (ámbito ASENTAMIENTO) y de `/v1/datos`; los rechazos, con su frase, flotando.
using System;
using System.Collections.Generic;
using System.Globalization;
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
        // ─────────────────────────── qué plaza y sus datos ───────────────────────────

        Asentamiento PlazaActual(string id) => c.Sesion.Almacen.Buscar(TipoEntidad.Asentamiento, id)?.Asentamiento;

        /// <summary>La plaza donde está dentro el héroe, ya con su interior (trazado y edificios): la de tu Facción o una ajena de visita (tanda 3, con
        /// lo que se ve desde dentro); si no, null.</summary>
        Asentamiento PlazaDondeEsta()
        {
            var h = HeroeAhora();
            var u = h?.Ubicacion;
            if (u == null || u.DondeCase != UbicacionHeroe.DondeOneofCase.AsentamientoId) return null;
            var a = PlazaActual(u.AsentamientoId);
            // La propia, cuando llega su interior; la ajena, con lo que se ve de ella (el motor no manda su interior: ni trazado ni edificios).
            if (a == null) return null;
            var propia = h.HasFaccionId && a.FaccionId == h.FaccionId;
            return !propia || a.Trazado != null && a.Edificios.Count > 0 ? a : null;
        }

        double AhoraMs => c.Sesion.AhoraMundo;
        
        static string Miles(double n) => Math.Floor(n).ToString("#,0", Es);
        static string Cifra(double n) => n == Math.Floor(n) || Math.Abs(n) >= 10 ? Math.Round(n).ToString(Es) : n.ToString("0.0", Es);

        double Valor(Ritmo r) => r == null ? 0 : Instante.Valor(r, AhoraMs) / 1000.0;
        double Hay(Asentamiento a, string r) => a.Almacen.TryGetValue(r, out var x) ? Math.Floor(Valor(x.Cantidad)) : 0;

        /// <summary>«3 d 4 h», «2 h 15 min», «40 min» (`juegoVisual.ts`: `duracion`).</summary>
        static string Duracion(double minutos)
        {
            if (minutos >= 1440)
            {
                var d = (int)Math.Floor(minutos / 1440);
                var hh = (int)Math.Round(minutos % 1440 / 60);
                return hh == 0 ? $"{d} d" : hh == 24 ? $"{d + 1} d" : $"{d} d {hh} h";
            }
            var h = (int)Math.Floor(minutos / 60);
            var min = (int)Math.Round(minutos % 60);
            return h == 0 ? $"{min} min" : min == 0 ? $"{h} h" : $"{h} h {min} min";
        }

        /// <summary>Lo que falta para un instante de mundo: «3 min», «45 s», «listo» (`cuentaAtras`).</summary>
        string CuentaAtras(long fin)
        {
            var ms = fin - AhoraMs;
            if (ms <= 0) return "listo";
            return ms >= 60_000 ? Duracion(Math.Round(ms / 60_000)) : $"{Math.Round(ms / 1000)} s";
        }

        JObject Catalogo => c.Datos.Bloque("Economia_Construccion", "EDIFICIO_CATALOGO") as JObject;
        JObject NivelDe(string tipo, int nivel) => Catalogo?[tipo]?["niveles"]?[nivel.ToString()] as JObject;
        static int NivelInterno(Edificio e) => e.HasNivelInterno ? (int)e.NivelInterno : 1;
        static bool Activo(Edificio e) => e.Estado == EstadoEdificio.EdificioActivo;
        static IEnumerable<Edificio> Internos(Asentamiento a) => a.Edificios.Where(e => e.Ambito != AmbitoEdificio.Mapa);

        /// <summary>El cargo de construcción del héroe en la plaza: «gobernador», «maestroObras» o null.</summary>
        static string CargoConstructor(Asentamiento a, string heroeId) =>
            a.Cargos?.GobernadorId == heroeId && a.Cargos.HasGobernadorId ? "gobernador"
            : a.Cargos != null && a.Cargos.HasMaestroObrasId && a.Cargos.MaestroObrasId == heroeId ? "maestroObras" : null;

        /// <summary>Lo que dura la obra o la mejora en marcha de un edificio (`panelObras.ts`: `minutosDeObra`, sin el factor de las políticas).</summary>
        double? MinutosDeObra(Edificio e)
        {
            var def = Catalogo?[e.Tipo] as JObject;
            if (def == null) return null;
            var baseMin = (double?)def["tiempoConstruccionMinutos"] ?? 0;
            if (e.Mejora != null)
            {
                var n = (int)e.Mejora.NivelObjetivo;
                var mult = (double?)c.Datos.Bloque("Economia_Construccion", "MEJORA_EDIFICIO")?["multiplicadorPorNivel"] ?? 2;
                return Math.Max(1, Math.Round((double?)def["niveles"]?[n.ToString()]?["obraMinutos"] ?? baseMin * Math.Pow(mult, n - 1)));
            }
            return Math.Max(1, Math.Round(baseMin));
        }

        static long? FinDeObra(Edificio e) => e.Mejora != null ? e.Mejora.CompletaEnMs : e.Estado == EstadoEdificio.EdificioEnConstruccion && e.HasCompletaEnMs ? e.CompletaEnMs : (long?)null;

        /// <summary>El avance de la obra en marcha (0 a 1), o null si no se sabe (`avanceDeObra`).</summary>
        // ponytail: sin `factorTiempoConstruccion` de las políticas activas; con una que acorte las obras, la barra arranca algo avanzada.
        float? AvanceDeObra(Edificio e)
        {
            var fin = FinDeObra(e);
            var minutos = MinutosDeObra(e);
            if (fin == null || minutos == null) return null;
            return Mathf.Clamp01((float)(1 - (fin.Value - AhoraMs) / (minutos.Value * 60_000)));
        }

        // ─────────────────────────── las piezas del prototipo ───────────────────────────

        /// <summary>El escudo de bronce con la cifra (`medallonNivel`).</summary>
        sealed class Escudo : VisualElement
        {
            public Color Relleno = new Color32(0x5a, 0x3f, 0x22, 0xff), Trazo = new Color32(0xd8, 0xb8, 0x78, 0xff);
            public bool Discontinuo;
            public Escudo(float ancho, float alto, string cifra = null, string clase = null)
            {
                pickingMode = PickingMode.Ignore;
                style.width = ancho;
                style.height = alto;
                style.flexShrink = 0;
                style.alignItems = Align.Center;
                style.justifyContent = Justify.Center;
                if (clase != null) AddToClassList(clase);
                if (cifra != null) { var b = new Label(cifra) { pickingMode = PickingMode.Ignore }; b.AddToClassList("plaza-medallon-cifra"); Add(b); }
                generateVisualContent += Pintar;
            }

            void Pintar(MeshGenerationContext m)
            {
                var p = m.painter2D;
                float sx = contentRect.width / 40f, sy = contentRect.height / 46f;
                Vector2 P(float x, float y) => new Vector2(x * sx, y * sy);
                p.BeginPath();
                p.MoveTo(P(20, 2)); p.LineTo(P(37, 10)); p.LineTo(P(37, 24));
                p.BezierCurveTo(P(37, 34), P(29.6f, 41.4f), P(20, 44));
                p.BezierCurveTo(P(10.4f, 41.4f), P(3, 34), P(3, 24));
                p.LineTo(P(3, 10)); p.ClosePath();
                p.fillColor = Relleno;
                p.Fill();
                p.strokeColor = Trazo;
                p.lineWidth = 2 * sx;
                if (Discontinuo) { p.lineWidth = 1.5f * sx; p.strokeColor = new Color(Trazo.r, Trazo.g, Trazo.b, 0.6f); }
                p.Stroke();
            }
        }

        static Escudo Medallon(int nivel, string clase = "", float ancho = 40, float alto = 46)
        {
            var e = new Escudo(ancho, alto, nivel.ToString(), "plaza-medallon");
            if (clase == "apagado") { e.Relleno = new Color32(0x1d, 0x17, 0x10, 0xff); e.Trazo = new Color32(0x6b, 0x4a, 0x29, 0xff); e.Discontinuo = true; e.AddToClassList("apagado"); }
            else if (clase == "listo") { e.Relleno = new Color32(0x6b, 0x4f, 0x20, 0xff); e.Trazo = new Color32(0xff, 0xd2, 0x4a, 0xff); }
            else if (clase == "en-obra") { e.Relleno = new Color32(0x3a, 0x2c, 0x18, 0xff); e.Trazo = new Color32(0xe0, 0xa5, 0x3a, 0xff); e.Discontinuo = true; }
            return e;
        }

        /// <summary>Una barra de avance (`barraAvance`): de 0 a 1; null, sin saber cuánto (rayada).</summary>
        static VisualElement BarraAvance(float? x)
        {
            var b = Dialogo.Caja("plaza-barra-avance");
            var i = Dialogo.Caja("plaza-barra-avance-i");
            if (x == null) b.AddToClassList("indefinida");
            i.style.width = Length.Percent((x ?? 1) * 100);
            b.Add(i);
            return b;
        }

        static void Avance(VisualElement barra, float x) => barra.Q(className: "plaza-barra-avance-i").style.width = Length.Percent(x * 100);

        /// <summary>Un coste en chips con su icono; en rojo lo que el almacén no cubre (`chipsDeCoste`).</summary>
        VisualElement ChipsDeCoste(Asentamiento a, JObject coste, int maximo = int.MaxValue)
        {
            var fila = Dialogo.Caja("plaza-chips");
            var lista = coste?.Properties().ToList() ?? new List<JProperty>();
            // Con más de `maximo`, los primeros y un «+N» con el resto (en rojo si falta algo de él).
            var resto = lista.Count > maximo ? lista.Count - maximo + 1 : 0;
            foreach (var p in resto > 0 ? lista.Take(maximo - 1) : lista)
            {
                var n = (double)p.Value;
                var hay = Hay(a, p.Name);
                var chip = Dialogo.Caja("plaza-coste");
                chip.EnableInClassList("falta", hay < n);
                chip.tooltip = $"{Cifra(n)} {CosasDelMapa.NombreDeRecurso(p.Name)}{(hay < n ? $" (hay {Miles(hay)})" : "")}";
                chip.Add(IconosDelMundo.Icono("recurso", p.Name, 14));
                chip.Add(Dialogo.Texto(Cifra(n), "plaza-coste-cifra"));
                fila.Add(chip);
            }
            if (resto > 0)
            {
                var chip = Dialogo.Caja("plaza-coste");
                chip.EnableInClassList("falta", lista.Skip(maximo - 1).Any(p => Hay(a, p.Name) < (double)p.Value));
                chip.Add(Dialogo.Texto($"+{resto}", "plaza-coste-cifra"));
                fila.Add(chip);
            }
            return fila;
        }

        /// <summary>El letrero de una sección (`.faction-kicker`): la raya de bronce y el texto en mayúsculas espaciadas.</summary>
        static VisualElement Kicker(string texto)
        {
            var fila = Dialogo.Caja("plaza-kicker");
            fila.Add(Dialogo.Caja("plaza-kicker-raya"));
            fila.Add(Dialogo.Texto(texto.ToUpperInvariant(), "plaza-kicker-texto"));
            return fila;
        }

        /// <summary>Un icono del prototipo que es un emoji o un símbolo: el de la familia que lo tenga.</summary>
        static VisualElement Glifo(string id, float lado = 16) => id switch
        {
            "gente" => IconosDelMundo.Icono("glifo", "gente", lado),
            "pesants" => IconosDelMundo.Icono("glifo", "viveres", lado),
            "artesanos" => IconosDelMundo.Icono("glifo", "obra", lado),
            "nobleza" => IconosDelMundo.Icono("glifo", "nobleza", lado),
            "mantenimiento" => IconosDelMundo.Icono("glifo", "mantenimiento", lado),
            "nutricion" => IconosDelMundo.Icono("recurso", "pan", lado),
            "reloj" => IconosDelMundo.Icono("glifo", "espera", lado),
            "casa" => IconosDelMundo.Icono("glifo", "casa", lado),
            "campamento" => IconosDelMundo.Icono("glifo", "campamento", lado),
            _ => null,
        };

        // ─────────────────────────── la pantalla ───────────────────────────

        /// <summary>La plaza abierta: su id, la ficha elegida, la ventana abierta, lo que cuelga del blasón y si se está cambiando el nombre.</summary>
        string plazaId, fichaId, popBlason;
        bool editandoNombre, bloqueandoPlaza;
        /// <summary>Lo que se refresca cada segundo sin rehacer nada: las cuentas atrás y las barras, por parte del HUD.</summary>
        readonly List<(string seccion, Action reloj)> relojesDePlaza = new List<(string, Action)>();
        /// <summary>Lo último que se pintó de cada parte del HUD: cada una se rehace solo si cambia lo suyo (el nombre a medio escribir o la cola
        /// con el ratón encima no se pierden en cada paso de minuto).</summary>
        readonly Dictionary<string, string> firmasDePlaza = new Dictionary<string, string>();

        /// <summary>El interior de tu plaza (paso 7): FeudoScene encima de esta escena, con la plaza montada desde el motor y el héroe andando, y el HUD.
        /// Como el del campamento, la sesión sigue viva en este objeto y «Salir al mundo» vuelve al mapa.</summary>
        async void InteriorDePlaza(Asentamiento plaza)
        {
            if (SelectorDePersonaje.Interior != null) return;
            var v = Montar("Plaza");
            plazaId = plaza.Id;
            fichaId = popBlason = ventanaPlaza = null;
            editandoNombre = bloqueandoPlaza = construidosAbiertos = false;
            cargosEditando.Clear();
            borradorReserva.Clear();
            reservaAnadidos.Clear();
            var nombre = plaza.HasNombre ? plaza.Nombre : plaza.Id;
            var botonAvisos = v.Q<Button>("avisos");
            botonAvisos.clicked += panelAvisos.Alternar;
            v.Q<Button>("cerrar-sesion").clicked += async () =>
            {
                if (await dialogo.Abrir("Tu héroe sale del mundo y se cierra la sesión en este equipo.", "¿Cerrar sesión?", "Cerrar sesión")) _ = CerrarSesion();
            };
            BarraDelJugador(v);
            v.Q<Button>("salir-al-mundo").clicked += () => VentanaDeSalida(null, PlazaActual(plaza.Id)?.Nombre ?? nombre, plaza.Id);

            var interior = new SelectorDePersonaje.InteriorParaVer { Heroe = HeroeParaVer(c.Datos) };
            string plantaPintada = null, vestidoCon = null;
            firmasDePlaza.Clear();
            relojesDePlaza.Clear();
            void Planta(Asentamiento a)
            {
                var firma = FirmaDePlanta(a);
                if (firma == plantaPintada) return;
                plantaPintada = firma;
                LlenarPlanta(interior, a);
                interior.Version++;
            }
            Planta(plaza);
            interior.AlInteractuar = zona =>
            {
                if (zona == "puerta") VentanaDeSalida(null, PlazaActual(plaza.Id)?.Nombre ?? nombre, plaza.Id);
                else if (zona.StartsWith("edificio:")) { fichaId = zona.Substring(9); firmasDePlaza.Remove("ficha"); }
            };

            var cartel = v.Q<Label>("cartel");
            letreroDePlaza = v.Q<Label>("letrero");
            var carteles = v.Q("carteles-obra");
            v.RegisterCallback<GeometryChangedEvent>(_ => AjustarPlaza(v));
            v.Q("plaza-pie").RegisterCallback<GeometryChangedEvent>(_ => AjustarPlaza(v));
            v.schedule.Execute(() =>
            {
                PintarAvisos(botonAvisos);
                var a = PlazaActual(plaza.Id);
                var yo = HeroeAhora();
                if (a == null || yo == null) return;
                Planta(a);
                // Si cambia lo que lleva puesto (equipar en la Base), el héroe se vuelve a vestir.
                var puesto = string.Join("|", (PiezasPuestas(yo) ?? new Dictionary<string, string>()).OrderBy(kv => kv.Key).Select(kv => kv.Key + "=" + kv.Value));
                if (puesto != vestidoCon)
                {
                    vestidoCon = puesto;
                    interior.Heroe = HeroeParaVer(c.Datos);
                    var local = UnityEngine.Object.FindObjectsByType<ConquestTactics.Visual.EntityVisualSync>(FindObjectsSortMode.None).FirstOrDefault(x => x.IsLocalHero);
                    if (local != null) VestidoDelMotor.Vestir(local.transform, interior.Heroe);
                }
                // Cada parte del HUD se rehace cuando cambia lo suyo; las cuentas atrás, en cada vuelta. Con el botón del ratón pulsado, se espera: si
                // se rehiciera entre pulsar y soltar, el clic se perdería (el botón pulsado ya no estaría).
                if (!RatonPulsado()) PintarHudDePlaza(v, a, yo, () => firmasDePlaza.Clear());
                foreach (var r in relojesDePlaza.ToList()) r.reloj();
                BloqueoDePlaza();
                interior.Elegido = fichaId;
                var zona = interior.PanelAbierto ? null : interior.ZonaCercana;
                Poner(cartel, zona == null ? null : zona == "puerta" ? "F · Salir al mundo"
                    : zona.StartsWith("edificio:") && a.Edificios.FirstOrDefault(e => e.Id == zona.Substring(9)) is Edificio ed ? $"F · {Edificios.Nombre(ed.Tipo)}" : null);
            }).Every(250);
            // Los carteles de obra, encima de su edificio en cada fotograma (la cámara se mueve con el héroe).
            v.schedule.Execute(() => PintarCartelesDeObra(carteles, interior)).Every(0);
            // Esc recoge lo que cuelga del blasón, luego la ventana y luego la ficha (el prototipo: primero lo de arriba).
            v.schedule.Execute(() =>
            {
                var teclado = UnityEngine.InputSystem.Keyboard.current;
                if (teclado == null || !teclado.escapeKey.wasPressedThisFrame || capaEncima.Q("ventana-salida") != null || capaEncima.Q(className: "dialogo-velo") != null) return;
                if (editandoNombre) editandoNombre = false;
                else if (popBlason != null) popBlason = null;
                else if (construidosAbiertos) construidosAbiertos = false;
                else if (ventanaPlaza != null) ventanaPlaza = null;
                else if (fichaId != null) fichaId = null;
                else return;
                firmasDePlaza.Clear();
            }).Every(0);

            // Como el campamento: la cámara del mundo se aparta y FeudoScene se carga encima.
            var volverCamaras = ApartarCamarasDelMundo();
            var mundo = EscenaDelMundo();
            async Task Cerrar()
            {
                if (SelectorDePersonaje.Interior == null) return;
                cerrarEscenaEncima = null;
                cerrarInterior = null;
                plazaId = null;
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

        /// <summary>Lo que el prototipo mide con clamp() y con la ventana (`.plaza-3d` y `vigilarPieArriba`): la columna izquierda, la barra de arriba
        /// entre el blasón y los botones del jugador (en dos filas el Almacén si no cabe; por debajo si aún menos), la ficha, el riel centrado en lo que
        /// deja el minimapa, «Salir al mundo» encima del riel por debajo de 1240 px y la ventana bajo la barra.</summary>
        static void AjustarPlaza(VisualElement v)
        {
            var plaza = v.Q("plaza");
            float w = plaza.layout.width, h = plaza.layout.height;
            if (float.IsNaN(w) || w <= 0) return;
            float vmin = Mathf.Min(w, h);
            float colIzq = Mathf.Clamp(0.28f * vmin, 240, 300), minimapa = Mathf.Clamp(0.25f * vmin, 170, 260), der = Mathf.Clamp(0.22f * w, 230, 320);
            var construyendo = plaza.ClassListContains("construyendo");
            float rielAlto = construyendo ? (h <= 820 ? 184 : 212) : h <= 820 ? 44 : 50, pieAlto = rielAlto + 16;
            var izq = v.Q("plaza-izq");
            izq.style.width = colIzq;
            izq.style.maxHeight = h - 8 - pieAlto - (w < 1240 ? 90 : 50);
            var barra = v.Q(className: "plaza-barra");
            var botones = float.IsNaN(barra.layout.width) ? 400 : barra.layout.width;
            var pie = v.Q("plaza-pie");
            pie.style.left = 16 + colIzq + 12;
            pie.style.right = botones + 12;
            var anchoPie = w - (16 + colIzq + 12) - (botones + 12);
            plaza.EnableInClassList("estrecha", anchoPie < 980);
            plaza.EnableInClassList("muy-estrecha", anchoPie < 640);
            OrdenarPlacas(pie);
            var finPie = float.IsNaN(pie.layout.yMax) ? 100 : pie.layout.yMax + 10;
            var derecha = v.Q("plaza-der");
            derecha.style.width = der;
            derecha.style.maxHeight = h - 60 - minimapa - 12;
            var riel = v.Q("riel");
            if (construyendo)
            {
                // El cajón, entre «Salir al mundo» y el minimapa, hasta 900 px y centrado en ese hueco. Con «Salir al mundo» en su misma fila (desde
                // 1240 px), empieza después de él: el botón de Conquest es más ancho que los 210 px del prototipo y tapaba el primer edificio.
                var anchoSalir = float.IsNaN(v.Q("salir-al-mundo").layout.width) ? 210 : v.Q("salir-al-mundo").layout.width;
                float desde = w < 1240 ? 16 + 210 : 16 + Mathf.Max(210, anchoSalir) + 12, hasta = w - minimapa - 28, ancho = Mathf.Min(900, hasta - desde);
                riel.style.left = desde + (hasta - desde - ancho) / 2;
                riel.style.width = ancho;
                riel.style.height = rielAlto - 4;
                riel.style.translate = new Translate(0, 0);
            }
            else
            {
                riel.style.left = 16 + (w - minimapa - 32) / 2;
                riel.style.width = StyleKeyword.Null;
                riel.style.height = StyleKeyword.Null;
                riel.style.translate = new Translate(Length.Percent(-50), 0);
            }
            var salir = v.Q("salir-al-mundo");
            salir.style.bottom = w < 1240 ? rielAlto + 24 : 12;
            v.Q("ayuda").style.bottom = (w < 1240 ? rielAlto + 24 : 12) + 70;
            var ventana = v.Q("plaza-ventana");
            ventana.style.top = finPie + 12;
            if (ventana.ClassListContains("plaza-ventana-completa"))
            {
                // A pantalla completa: de la barra de arriba al riel, que se queda para cambiar de ventana.
                ventana.style.left = 16;
                ventana.style.right = 16;
                ventana.style.width = StyleKeyword.Auto;
                ventana.style.bottom = rielAlto + 22;
                ventana.style.maxHeight = StyleKeyword.None;
            }
            else
            {
                ventana.style.left = 16 + colIzq + 12;
                ventana.style.right = StyleKeyword.Null;
                ventana.style.bottom = StyleKeyword.Null;
                ventana.style.width = Mathf.Min(ventana.ClassListContains("plaza-ventana-estrecha") ? 460 : 600, w - colIzq - 60);
                ventana.style.maxHeight = h - finPie - pieAlto - 34;
            }
        }

        /// <summary>Con la barra muy estrecha, el Almacén va detrás de Estado (`@container (max-width: 640px)`); si no, en medio.</summary>
        static void OrdenarPlacas(VisualElement pie)
        {
            var almacen = pie.Q("placa-almacen");
            var estado = pie.Q("placa-estado");
            if (almacen == null) return;
            // Estrecha: los recursos, en dos filas iguales (`--n2`) si son más de cuatro.
            var piezas = almacen.Q(className: "plaza-placa-piezas");
            var n = piezas.Query(className: "plaza-recurso").ToList().Count;
            var dos = pie.parent.ClassListContains("estrecha") && n > 4;
            foreach (var hijo in piezas.Children()) hijo.style.width = dos ? Length.Percent(100f / (float)Math.Ceiling(n / 2.0) - 0.01f) : new StyleLength(StyleKeyword.Null);
            if (pie.parent.ClassListContains("muy-estrecha")) almacen.BringToFront();
            else if (estado != null && pie.IndexOf(almacen) > pie.IndexOf(estado)) almacen.PlaceBehind(estado);
        }

        /// <summary>Con la ficha, la ventana o el nombre abiertos, el héroe no anda y el ratón queda libre (como un panel del campamento).</summary>
        void BloqueoDePlaza()
        {
            var interior = SelectorDePersonaje.Interior;
            var mio = fichaId != null || ventanaPlaza != null || editandoNombre || capaEncima.Q("panel-tropa") != null;
            if (interior == null || mio == bloqueandoPlaza) return;
            bloqueandoPlaza = mio;
            interior.PanelAbierto = mio;
        }

        /// <summary>Lo que cambia el dibujo de la plaza: edificios (estado y nivel), muralla y calles.</summary>
        static string FirmaDePlanta(Asentamiento a) =>
            string.Join(";", a.Edificios.Select(e => $"{e.Id}:{(int)e.Estado}:{NivelInterno(e)}:{e.Mejora?.NivelObjetivo}"))
            + "|" + string.Join(";", a.Recintos.Select(r => $"{r.Id}:{r.Nivel}:{r.Avance}:{r.Celdas.Count}:{r.Mejoradas}"))
            + "|" + (a.Trazado?.Calles.Count ?? 0) + ":" + (a.Trazado?.Caminos.Count ?? 0);

        /// <summary>La planta de la plaza para FeudoScene: los edificios de dentro (centro y huella en unidades locales), las calles y los caminos, y la
        /// muralla celda a celda (`Recinto.celdas`, en celdas de `unidades_por_celda`: lo levantado son las `avance` primeras). La puerta: la de la
        /// muralla más de fuera; sin muralla, el extremo del camino que más se aleja del centro.</summary>
        static void LlenarPlanta(SelectorDePersonaje.InteriorParaVer interior, Asentamiento a)
        {
            interior.Edificios.Clear(); interior.Calles.Clear(); interior.Muralla.Clear(); interior.Puerta.Clear();
            foreach (var e in Internos(a))
                interior.Edificios.Add(new SelectorDePersonaje.PiezaDePlanta
                {
                    Id = e.Id, Tipo = e.Tipo, Zona = "edificio:" + e.Id, X = (e.Posicion?.X ?? 0) / 1000.0, Y = (e.Posicion?.Y ?? 0) / 1000.0, Ancho = e.Ancho, Alto = e.Alto,
                    Estado = e.Estado == EstadoEdificio.EdificioEnCola ? "cola" : e.Estado == EstadoEdificio.EdificioEnConstruccion ? "obra" : null, Nivel = NivelInterno(e),
                });
            var trazado = a.Trazado ?? new TrazadoUrbano();
            foreach (var r in trazado.Calles) interior.Calles.Add(Pieza(r, "calle"));
            foreach (var r in trazado.Caminos) interior.Calles.Add(Pieza(r, "camino"));
            double T = trazado.UnidadesPorCelda > 0 ? trazado.UnidadesPorCelda : 3;
            SelectorDePersonaje.PiezaDePlanta? puerta = null;
            double masLejos = -1;
            foreach (var r in a.Recintos)
                for (int i = 0; i < r.Celdas.Count; i++)
                {
                    var k = r.Celdas[i];
                    var nivel = r.HasMejorandoA && i < r.Mejoradas ? (int)r.MejorandoA : (int)r.Nivel;
                    var pieza = new SelectorDePersonaje.PiezaDePlanta
                    {
                        Tipo = k.Clase == ClaseCelda.CeldaTorre ? "torre" : k.Clase == ClaseCelda.CeldaPuerta ? "puerta" : "muro",
                        X = (k.Col + 0.5) * T, Y = (k.Fila + 0.5) * T, Ancho = T, Alto = T, Estado = i < r.Avance ? null : "plan", Nivel = nivel,
                    };
                    interior.Muralla.Add(pieza);
                    var d = Math.Sqrt(pieza.X * pieza.X + pieza.Y * pieza.Y);
                    if (pieza.Tipo == "puerta" && d > masLejos) { masLejos = d; puerta = pieza; }
                }
            if (puerta == null)
            {
                // Sin muralla: el extremo del camino que más se aleja del centro (el centro urbano está en el origen del espacio local).
                foreach (var r in trazado.Caminos)
                    foreach (var (x, y) in new[] { (r.X + 1.5, r.Y + 1.5), (r.X + r.Ancho - 1.5, r.Y + r.Alto - 1.5) })
                    {
                        var d = Math.Sqrt(x * x + y * y);
                        if (d > masLejos) { masLejos = d; puerta = new SelectorDePersonaje.PiezaDePlanta { Tipo = "puerta", X = x, Y = y, Ancho = T, Alto = T }; }
                    }
            }
            interior.Puerta.Add(puerta ?? new SelectorDePersonaje.PiezaDePlanta { Tipo = "puerta", X = 0, Y = Math.Max(trazado.RadioUl, 30), Ancho = T, Alto = T });
        }

        // ─────────────────────────── el HUD ───────────────────────────

        void PintarHudDePlaza(VisualElement v, Asentamiento a, Heroe yo, Action rehacer)
        {
            var cargo = CargoConstructor(a, yo.Id);
            bool Cambia(string seccion, string firma)
            {
                if (firmasDePlaza.TryGetValue(seccion, out var antes) && antes == firma) return false;
                firmasDePlaza[seccion] = firma;
                relojesDePlaza.RemoveAll(r => r.seccion == seccion);
                return true;
            }
            var interiores = a.VersionInterior;
            if (Cambia("blason", $"{a.Version}|{yo.Version}|{editandoNombre}|{popBlason}|{AscensoPosible(a)}"))
            {
                PintarBlason(v.Q("blason"), a, yo, rehacer);
                PintarPopBlason(v.Q("blason-pop"), a, yo, rehacer);
            }
            if (Cambia("obras", $"{FirmaDePlanta(a)}|{string.Join(",", a.Edificios.Select(e => e.Prioridad))}|{a.Ascenso?.CompletaEnMs}|{cargo}|{FirmaDeObrasDeMapa(a)}"))
            {
                // De visita en una plaza ajena, sus obras no se ven.
                if (Propia(a, yo)) { PintarAscensoHud(v.Q("ascenso-hud"), a); PintarObrasHud(v.Q("obras-hud"), a, cargo); }
                else { Ver(v.Q("ascenso-hud"), false); Ver(v.Q("obras-hud"), false); }
            }
            if (Cambia("placas", $"{a.Version}|{interiores}|{cargo}"))
            {
                PintarPlacas(v.Q("plaza-pie"), v.Q<Label>("letrero"), a);
                // Un clic en el Almacén abre su ventana; el «+» de la Vivienda, con cargo, abre Construcción en su pestaña con la Vivienda marcada.
                if (v.Q("placa-almacen") is VisualElement almacen)
                    almacen.RegisterCallback<ClickEvent>(_ => { ventanaPlaza = ventanaPlaza == "almacen" ? null : "almacen"; rehacer(); });
                if (cargo != null)
                    v.Q("plaza-pie").Query(className: "plaza-pob-vivienda").ForEach(mas =>
                    {
                        mas.AddToClassList("pulsable");
                        mas.pickingMode = PickingMode.Position;
                        mas.RegisterCallback<ClickEvent>(ev => { ev.StopPropagation(); grupoConstruir = "ciudad"; ventanaPlaza = "construccion"; rehacer(); });
                    });
            }
            if (Cambia("ficha", $"{fichaId}|{a.Version}|{interiores}|{cargo}")) PintarFicha(v.Q("plaza-der"), a, yo, cargo, rehacer);
            // La ventana abierta deja de estarlo si ya no se puede (sin cargo, sin almacén).
            if (ventanaPlaza != null && !VentanaDisponible(ventanaPlaza, a, yo)) ventanaPlaza = null;
            var faccion = c.Sesion.Almacen.Buscar(TipoEntidad.Faccion, a.FaccionId)?.Faccion;
            var otra = false;
            // El riel: solo lo que se puede abrir (Construcción con cargo y Gobierno; Defensa, Reclutamiento y la Armería llegan en la tanda 3). Con
            // Construcción abierta, su cajón.
            // Cada una se rehace solo si cambia lo que enseña: rehacerla en cada paso de minuto se tragaba el clic que caía en ese momento (el
            // botón pulsado ya no estaba) y devolvía la lista arriba.
            if (Cambia("riel", FirmaDelRiel(a, yo))) { PintarRiel(v, a, yo, rehacer); otra = true; }
            var sub = ventanaPlaza != null && subDePlaza.TryGetValue(ventanaPlaza, out var s) ? s : "";
            // Gobierno no enseña nada del paso de minuto (sus cuentas atrás van con el reloj); el Almacén, sí (las cifras).
            if (Cambia("ventana", $"{ventanaPlaza}|{sub}|{a.Version}|{(ventanaPlaza == "almacen" ? interiores : 0)}|{yo.Version}|{faccion?.Version}|{(ventanaPlaza == "territorio" ? FirmaDeTerritorio(a) : "")}")) { PintarVentanaDePlaza(v.Q("plaza-ventana"), a, yo, rehacer); otra = true; }
            if (otra) AjustarPlaza(v);
        }

        /// <summary>Manda una orden de la plaza; el rechazo, con su frase, sale flotando (`errorDePlaza`). El botón se apaga mientras.</summary>
        async Task<bool> OrdenDePlaza(Orden orden, VisualElement boton = null)
        {
            boton?.SetEnabled(false);
            ResultadoOrden r = null;
            try { r = await c.Sesion.Ordenar(orden); }
            catch (ErrorDeSesion e) { panelAvisos.Mostrar(e.Message, true); }
            // «Sin cambios» (p. ej. ▶ entre dos obras de la misma prioridad) no es un rechazo: no se avisa.
            if (r?.Resultado == Resultado.OrdenRechazada) panelAvisos.Mostrar(Textos.Explicar(r.Rechazo), true);
            boton?.SetEnabled(true);
            return r?.Resultado == Resultado.OrdenAceptada;
        }

        // ── el blasón ──

        /// <summary>El blasón (`htmlBlasonHud`): el escudo de la Facción con el medallón del nivel (abre la subida; brilla si el Gobernador ya puede
        /// pedirla), el nombre con su lápiz si resides, la Facción y «rinde como N»; a la derecha, la casa (resides o no) y la corona (capital).</summary>
        void PintarBlason(VisualElement blason, Asentamiento a, Heroe yo, Action rehacer)
        {
            blason.Clear();
            var propia = yo.HasFaccionId && a.FaccionId == yo.FaccionId;
            var reside = yo.HasResidenciaId && yo.ResidenciaId == a.Id;
            if (!reside) editandoNombre = false;
            var faccion = c.Sesion.Almacen.Buscar(TipoEntidad.Faccion, a.FaccionId)?.Faccion;
            // El sigilo de la Facción dueña, de 64 px y con la corona si es la del Gran Rey (`svgSigilo(f.sigilo, 64, {granRey})`).
            var (sigilo, granRey) = SigiloDeFaccion(a.FaccionId);
            blason.Add(SigiloUI(sigilo, 64, granRey, "plaza-blason-escudo"));

            var texto = Dialogo.Caja("plaza-blason-texto");
            if (editandoNombre)
            {
                var fila = Dialogo.Caja("plaza-blason-renombrar");
                var campo = new TextField { value = a.HasNombre ? a.Nombre : a.Id, maxLength = 32, name = "nombre-plaza" };
                fila.Add(campo);
                async void Guardar()
                {
                    if (await OrdenDePlaza(new Orden { RenombrarAsentamiento = new OrdenRenombrarAsentamiento { AsentamientoId = a.Id, Nombre = campo.value } })) { editandoNombre = false; rehacer(); }
                }
                var ok = Dialogo.Boton("✓", "plaza-blason-ok", Guardar);
                ok.name = "guardar-nombre";
                ok.tooltip = "Guardar el nombre (Intro)";
                var no = Dialogo.Boton("✕", "plaza-blason-no", () => { editandoNombre = false; rehacer(); });
                no.name = "cancelar-nombre";
                no.tooltip = "Cancelar (Esc)";
                campo.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) Guardar(); }, TrickleDown.TrickleDown);
                fila.Add(ok);
                fila.Add(no);
                texto.Add(fila);
                campo.schedule.Execute(() => { campo.Focus(); campo.SelectAll(); });
            }
            else
            {
                var fila = Dialogo.Caja("plaza-blason-nombre");
                var nombre = Dialogo.Texto(a.HasNombre ? a.Nombre : a.Id, "plaza-nombre");
                nombre.name = "plaza-nombre";
                fila.Add(nombre);
                if (reside)
                {
                    var lapiz = Dialogo.Boton("✎", "plaza-blason-lapiz", () => { editandoNombre = true; rehacer(); });
                    lapiz.name = "renombrar";
                    lapiz.tooltip = "Cambiar el nombre de la ciudad";
                    fila.Add(lapiz);
                }
                texto.Add(fila);
            }
            texto.Add(Dialogo.Texto(faccion?.Nombre ?? NombreDeFaccion(c.Sesion.Almacen, a.FaccionId) ?? "Sin Facción", "plaza-blason-small"));
            if (a.NivelActual != a.Nivel)
            {
                var opera = Dialogo.Texto($"rinde como {a.NivelActual}", "plaza-blason-small plaza-blason-opera");
                opera.tooltip = $"Por el mantenimiento, funciona como una plaza de nivel {a.NivelActual}";
                texto.Add(opera);
            }
            blason.Add(texto);

            // El medallón, prendido en la esquina del escudo: abre la subida; el Gobernador lo ve brillar con su «!» cuando ya la puede pedir.
            var listo = propia && a.Cargos?.GobernadorId == yo.Id && AscensoPosible(a);
            var medallon = new Button(() => { ventanaAscenso = !ventanaAscenso; rehacer(); }) { name = "medallon" };
            medallon.AddToClassList("plaza-blason-medallon");
            medallon.EnableInClassList("listo", listo);
            medallon.tooltip = listo ? $"Plaza de nivel {a.Nivel}: ya puede subir a nivel {a.Nivel + 1}" : $"Plaza de nivel {a.Nivel}: subir de nivel";
            medallon.Add(Medallon((int)a.Nivel, listo ? "listo" : "", 28, 32));
            if (listo) medallon.Add(Dialogo.Texto("!", "plaza-blason-atencion"));
            // De visita en una plaza ajena, el nivel solo se ve.
            if (!propia) { medallon.pickingMode = PickingMode.Ignore; medallon.tooltip = $"Plaza de nivel {a.Nivel}"; }
            blason.Add(medallon);

            // La casa y la corona.
            var capital = a.HasCapitalDeFaccionId && a.CapitalDeFaccionId == a.FaccionId;
            var corona = propia && capital ? "si" : propia && faccion != null && faccion.HasReyId && faccion.ReyId == yo.Id ? "puede" : null;
            var iconos = Dialogo.Caja("plaza-blason-iconos");
            Button Icono(string nombreBoton, string clase, string titulo, string simbolo)
            {
                var b = new Button(() => { popBlason = popBlason == nombreBoton ? null : nombreBoton; rehacer(); }) { name = nombreBoton, tooltip = titulo };
                b.AddToClassList("plaza-blason-icono");
                b.AddToClassList(clase);
                b.EnableInClassList("abierto", popBlason == nombreBoton);
                b.Add(IconosDelMundo.Icono("simbolo", simbolo, 16, clase == "dentro" ? IconosDelMundo.OroClaro : (Color?)new Color32(0xb8, 0xab, 0x92, 0xff)));
                iconos.Add(b);
                return b;
            }
            if (propia) Icono("residencia", reside ? "dentro" : "fuera", reside ? "Resides aquí: es tu base" : "No resides aquí", "residencia");
            if (corona != null) Icono("capital", corona == "si" ? "dentro" : "fuera", corona == "si" ? "Capital de la Facción" : "No es la capital: como Rey, puedes designarla", "capital");
            if (popBlason == "capital" && corona == null || popBlason == "residencia" && !propia) popBlason = null;
            if (iconos.childCount > 0 && !editandoNombre) blason.Add(iconos);
        }

        /// <summary>La tarjeta que cuelga de la casa o de la corona (`panelResidencia.ts`).</summary>
        void PintarPopBlason(VisualElement pop, Asentamiento a, Heroe yo, Action rehacer)
        {
            pop.Clear();
            Ver(pop, popBlason != null);
            if (popBlason == null) return;
            var reside = yo.HasResidenciaId && yo.ResidenciaId == a.Id;
            VisualElement Tarjeta(string clase, VisualElement icono, string titulo, string linea, Button boton = null)
            {
                var t = Dialogo.Caja("plaza-tarjeta " + clase);
                var cab = Dialogo.Caja("plaza-tarjeta-cab");
                if (icono != null) { icono.AddToClassList("plaza-tarjeta-icono"); cab.Add(icono); }
                var cuerpo = Dialogo.Caja("plaza-tarjeta-cuerpo");
                cuerpo.Add(Dialogo.Texto(titulo, "plaza-tarjeta-titulo"));
                cuerpo.Add(Dialogo.Texto(linea, "plaza-tarjeta-linea"));
                cab.Add(cuerpo);
                t.Add(cab);
                if (boton != null) t.Add(boton);
                pop.Add(t);
                return t;
            }
            if (popBlason == "residencia")
            {
                if (!reside)
                {
                    Button mudarme = null;
                    mudarme = Dialogo.Boton("Hacerla mi base", "plaza-tarjeta-boton", async () =>
                    {
                        var prestadas = EscuadrasDelHeroe(yo).Where(s => s.HasPrestadaPorCampamentoId).ToList();
                        var aviso = $"Vas a hacer de {a.Nombre} tu base: dejas de residir donde vives ahora y tu campamento se muda contigo."
                            + (prestadas.Count > 0 ? $" Se te retira la tropa prestada por tu campamento anterior ({string.Join(", ", prestadas.Select(s => $"{s.Nombre}: {s.Cantidad}"))})." : "")
                            + " Después hay que esperar un tiempo para volver a mudarte. ¿Seguro?";
                        if (!await dialogo.Abrir(aviso, "¿Hacer de esta plaza tu base?", "Hacerla mi base")) return;
                        await OrdenDePlaza(new Orden { CambiarResidencia = new OrdenCambiarResidencia { DestinoId = a.Id, HeroeId = yo.Id } }, mudarme);
                    });
                    mudarme.name = "mudarme";
                    mudarme.tooltip = "Hacer de esta plaza tu base: tu campamento se muda contigo";
                    Tarjeta("fuera", Glifo("campamento", 24), "No resides aquí", "Puedes hacer de esta plaza tu base: tu campamento se muda contigo.", mudarme);
                    return;
                }
                var nombresCargo = new (string id, string nombre)[]
                {
                    (a.Cargos?.GobernadorId, "Gobernador"), (a.Cargos?.MaestroObrasId, "Maestro de Obras"), (a.Cargos?.MaestroCaravanasId, "Maestro de Caravanas"),
                    (a.Cargos?.TesoreroId, "Tesorero"), (a.Cargos?.GeneralId, "General"), (a.Cargos?.SacerdoteId, "Sacerdote"),
                };
                var mios = nombresCargo.Where(x => x.id == yo.Id).Select(x => x.nombre).ToList();
                Button dejar = null;
                dejar = Dialogo.Boton("Dejar", "plaza-tarjeta-boton peligro", async () =>
                {
                    if (!await dialogo.Abrir($"Pierdes tu vivienda en {a.Nombre}, tus cargos locales y tu guarnición aquí, y pasas a un campamento de mercenarios. Sigues en tu Facción.",
                            "¿Dejar tu residencia?", "Dejar la residencia", peligro: true)) return;
                    await OrdenDePlaza(new Orden { DejarResidencia = new OrdenDejarResidencia { HeroeId = yo.Id } }, dejar);
                });
                dejar.name = "dejar-residencia";
                dejar.tooltip = "Dejar tu residencia aquí (sigues en tu Facción)";
                var titulo = ConAyuda(Dialogo.Texto("Resides aquí", "plaza-tarjeta-titulo"),
                    "Dejar la residencia libera tu vivienda y vacía tus cargos locales, suelta tu guarnición y te lleva al campamento de mercenarios más cercano. Sigues en tu Facción. No hay reembolso y hay un enfriamiento antes de poder mudarte de nuevo.");
                var t = Tarjeta("dentro", Glifo("casa", 24), "", mios.Count > 0 ? string.Join(" · ", mios) : "Sin cargo en la plaza", dejar);
                var cuerpoT = t.Q(className: "plaza-tarjeta-cuerpo");
                cuerpoT.RemoveAt(0);
                cuerpoT.Insert(0, titulo);
                return;
            }
            // La capital.
            var faccion = c.Sesion.Almacen.Buscar(TipoEntidad.Faccion, a.FaccionId)?.Faccion;
            if (a.HasCapitalDeFaccionId && a.CapitalDeFaccionId == a.FaccionId)
            {
                Tarjeta("capital", Glifo("nobleza", 24), "Capital de la Facción", "Aquí se adopta la tecnología; es el centro del mantenimiento por distancia.");
                return;
            }
            var bloqueo = !Internos(a).Any(e => e.Tipo == "palacio" && Activo(e)) ? "Necesita un Palacio activo." : null;
            var dias = 14.0;
            try { dias = c.Datos.Numero("documentos.Diplomacia_Facciones.CAPITAL.cooldownDias"); } catch (FaltaDato) { }
            if (bloqueo == null && faccion != null && faccion.HasCapitalDesignadaEnMs && AhoraMs < faccion.CapitalDesignadaEnMs + dias * 86_400_000)
                bloqueo = $"La capital se trasladó hace poco: faltan unos {Math.Ceiling((faccion.CapitalDesignadaEnMs + dias * 86_400_000 - AhoraMs) / 86_400_000)} días de mundo.";
            Button designar = null;
            designar = Dialogo.Boton("Designar", "plaza-tarjeta-boton", async () =>
                await OrdenDePlaza(new Orden { DesignarCapital = new OrdenDesignarCapital { FaccionId = a.FaccionId, AsentamientoId = a.Id } }, designar));
            designar.name = "designar-capital";
            designar.SetEnabled(bloqueo == null);
            if (bloqueo != null) designar.tooltip = bloqueo;
            Tarjeta(bloqueo != null ? "apagada" : "rey", Glifo("nobleza", 24), "Puedes hacerla capital", bloqueo ?? "Como Rey, sin coste; luego hay un enfriamiento para moverla.", designar);
        }

        // ── la subida en curso y las obras ──

        /// <summary>La subida de nivel en curso, bajo el blasón (`pintarAscensoHud`): a qué nivel, cuánto lleva, lo que falta y la barra.</summary>
        void PintarAscensoHud(VisualElement hud, Asentamiento a)
        {
            hud.Clear();
            var x = a.Ascenso;
            Ver(hud, x != null);
            if (x == null) return;
            var cab = Dialogo.Caja("plaza-ascenso-hud-cab");
            cab.Add(Dialogo.Texto($"Subiendo a nivel {x.NivelObjetivo}", "plaza-ascenso-hud-texto"));
            var pct = Dialogo.Texto("", "plaza-ascenso-hud-pct");
            cab.Add(pct);
            hud.Add(cab);
            var barra = BarraAvance(0);
            hud.Add(barra);
            var falta = Dialogo.Texto("", "plaza-ascenso-hud-falta");
            hud.Add(falta);
            void Reloj()
            {
                double total = x.CompletaEnMs - x.IniciadoEnMs;
                var hecho = total > 0 ? Mathf.Clamp01((float)((AhoraMs - x.IniciadoEnMs) / total)) : 1f;
                pct.text = $"{Math.Floor(hecho * 100)} %";
                Avance(barra, hecho);
                falta.text = x.CompletaEnMs > AhoraMs ? $"faltan {CuentaAtras(x.CompletaEnMs)}" : "terminando";
            }
            Reloj();
            relojesDePlaza.Add(("obras", Reloj));
        }

        /// <summary>Las obras bajo el blasón (`htmlObrasHud`): las 4 que antes acaban, con su barra, lo que les falta y ✕ para quien tiene cargo; debajo,
        /// la cola en casillas numeradas con ◀ ✕ ▶ al pasar por encima (Gobernador o Maestro de Obras).</summary>
        void PintarObrasHud(VisualElement hud, Asentamiento a, string cargo)
        {
            hud.Clear();
            var enMarcha = Internos(a).Select(e => (e, fin: FinDeObra(e))).Where(x => x.fin != null).OrderBy(x => x.fin).ToList();
            var esperan = Internos(a).Where(e => e.Estado == EstadoEdificio.EdificioEnCola).OrderByDescending(e => e.HasPrioridad ? e.Prioridad : 0).ToList();
            var (deMapa, deAvanzada) = ObrasDeMapaDelHud(a);
            Ver(hud, enMarcha.Count + esperan.Count + deMapa.Count + deAvanzada.Count > 0);
            if (enMarcha.Count + esperan.Count + deMapa.Count + deAvanzada.Count == 0) return;
            if (enMarcha.Count + esperan.Count > 0)
            {
                if (enMarcha.Count > 0) hud.Add(Dialogo.Texto($"OBRAS · {enMarcha.Count} EN MARCHA", "plaza-obras-cab"));
                else hud.Add(Dialogo.Texto("OBRAS", "plaza-obras-cab"));
            }
            foreach (var (e, fin) in enMarcha.Take(4))
            {
                var fila = Dialogo.Caja("plaza-obras-fila");
                fila.name = "obra-" + e.Id;
                fila.Add(IconosDelMundo.Icono("edificio", e.Tipo, 22, new Color32(0xe0, 0xa5, 0x3a, 0xff)));
                var texto = Dialogo.Caja("plaza-obras-texto");
                var linea = Dialogo.Caja("plaza-obras-linea");
                linea.Add(Dialogo.Texto(Edificios.Nombre(e.Tipo), "plaza-obras-nombre"));
                var falta = Dialogo.Texto(CuentaAtras(fin.Value), "plaza-obras-falta");
                linea.Add(falta);
                if (cargo != null)
                {
                    var edificio = e;
                    Button cancelar = null;
                    cancelar = Dialogo.Boton("✕", "plaza-obras-cancelar", async () =>
                    {
                        if (await ConfirmarCancelarObra(edificio))
                            await OrdenDePlaza(new Orden { CancelarConstruccion = new OrdenCancelarConstruccion { AsentamientoId = a.Id, Cargo = cargo, EdificioId = edificio.Id } }, cancelar);
                    });
                    cancelar.name = "cancelar-obra-" + e.Id;
                    cancelar.tooltip = $"{(e.Mejora != null ? "Cancelar la mejora" : "Cancelar la obra")} (no se devuelve nada)";
                    linea.Add(cancelar);
                }
                texto.Add(linea);
                texto.Add(Dialogo.Texto(e.Mejora != null ? $"Mejora a nivel {e.Mejora.NivelObjetivo}" : e.Danado ? "Reparación" : "Obra nueva", "plaza-obras-que"));
                var barra = BarraAvance(AvanceDeObra(e));
                texto.Add(barra);
                fila.Add(texto);
                hud.Add(fila);
                var ed = e;
                relojesDePlaza.Add(("obras", () => { falta.text = CuentaAtras(fin.Value); if (AvanceDeObra(ed) is float x) Avance(barra, x); }));
            }
            if (enMarcha.Count > 4) hud.Add(Dialogo.Texto($"{enMarcha.Count - 4} más en marcha", "plaza-obras-mas"));
            // Las obras de mapa de la plaza, debajo: pueblos y aldeas, y avanzadas (`filaObraMapa`).
            void DeMapa(string cab, List<ObraDeMapa> lista)
            {
                if (lista.Count == 0) return;
                hud.Add(Dialogo.Texto($"{cab} · {lista.Count}", "plaza-obras-cab plaza-obras-cab-cola"));
                foreach (var o in lista) hud.Add(FilaObraDeMapa(o));
            }
            if (esperan.Count == 0) { DeMapa("PUEBLOS Y ALDEAS", deMapa); DeMapa("AVANZADAS", deAvanzada); return; }
            hud.Add(Dialogo.Texto($"EN COLA · {esperan.Count}", "plaza-obras-cab plaza-obras-cab-cola"));
            var cola = Dialogo.Caja("plaza-obras-cola");
            cola.name = "cola";
            for (int i = 0; i < esperan.Count; i++)
            {
                var e = esperan[i];
                var casilla = Dialogo.Caja("plaza-obras-casilla");
                casilla.name = "cola-" + e.Id;
                casilla.EnableInClassList("danado", e.Danado);
                casilla.tooltip = $"{i + 1}. {Edificios.Nombre(e.Tipo)}{(e.Danado ? " · reconstrucción" : "")}";
                casilla.Add(IconosDelMundo.Icono("edificio", e.Tipo, 22, new Color32(0xf1, 0xdc, 0xae, 0xcc)));
                casilla.Add(Dialogo.Texto((i + 1).ToString(), "plaza-obras-n"));
                if (cargo != null)
                {
                    var ctl = Dialogo.Caja("plaza-obras-ctl");
                    var id = e.Id;
                    Button Control(string texto, string nombre, string titulo, Func<Orden> orden, bool activo)
                    {
                        Button b = null;
                        b = Dialogo.Boton(texto, "plaza-obras-ctl-boton", async () => await OrdenDePlaza(orden(), b));
                        b.name = nombre;
                        b.tooltip = titulo;
                        b.SetEnabled(activo);
                        ctl.Add(b);
                        return b;
                    }
                    Control("◀", "antes-" + id, "Antes", () => new Orden { MoverEnCola = new OrdenMoverEnCola { AsentamientoId = a.Id, Cargo = cargo, EdificioId = id, Direccion = "arriba" } }, i > 0);
                    Control("✕", "quitar-" + id, "Quitar de la cola", () => new Orden { QuitarDeCola = new OrdenQuitarDeCola { AsentamientoId = a.Id, Cargo = cargo, EdificioId = id } }, true);
                    Control("▶", "despues-" + id, "Después", () => new Orden { MoverEnCola = new OrdenMoverEnCola { AsentamientoId = a.Id, Cargo = cargo, EdificioId = id, Direccion = "abajo" } }, i < esperan.Count - 1);
                    casilla.Add(ctl);
                    casilla.RegisterCallback<PointerEnterEvent>(_ => ctl.AddToClassList("visible"));
                    casilla.RegisterCallback<PointerLeaveEvent>(_ => ctl.RemoveFromClassList("visible"));
                }
                cola.Add(casilla);
            }
            hud.Add(cola);
            DeMapa("PUEBLOS Y ALDEAS", deMapa);
            DeMapa("AVANZADAS", deAvanzada);
        }

        /// <summary>Las obras de mapa de la plaza para el HUD: las de pueblo y aldea, y las de avanzada, de la que antes acaba a la última.</summary>
        (List<ObraDeMapa> deMapa, List<ObraDeMapa> deAvanzada) ObrasDeMapaDelHud(Asentamiento a)
        {
            var todas = c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Obra).Select(e => e.Obra).Where(o => o.AsentamientoMadreId == a.Id)
                .OrderBy(o => o.HasTerminaEnMs ? o.TerminaEnMs : long.MaxValue).ToList();
            return (todas.Where(o => o.Tipo == TipoObra.ObraPueblo || o.Tipo == TipoObra.ObraAldea || o.Tipo == TipoObra.ObraMejoraAldea).ToList(),
                todas.Where(o => o.Tipo == TipoObra.ObraAvanzada || o.Tipo == TipoObra.ObraMejoraAvanzada || o.Tipo == TipoObra.ObraReparacion).ToList());
        }

        string FirmaDeObrasDeMapa(Asentamiento a)
        {
            var (m, v) = ObrasDeMapaDelHud(a);
            return string.Join(",", m.Concat(v).Select(o => o.Id + ":" + o.Version));
        }

        /// <summary>Una obra de mapa en el HUD (`filaObraMapa`): su nombre, el % entregado o, levantándose, lo que le queda, y su barra.</summary>
        VisualElement FilaObraDeMapa(ObraDeMapa o)
        {
            var fila = Dialogo.Caja("plaza-obras-fila");
            fila.name = "obra-mapa-" + o.Id;
            fila.Add(IconoDeObra(o, 22));
            var texto = Dialogo.Caja("plaza-obras-texto");
            var linea = Dialogo.Caja("plaza-obras-linea");
            var nombre = o.Tipo switch
            {
                TipoObra.ObraPueblo => "Pueblo", TipoObra.ObraAldea => "Aldea agrícola", TipoObra.ObraMejoraAldea => "Mejora de aldea",
                TipoObra.ObraAvanzada => "Atalaya", TipoObra.ObraMejoraAvanzada => "Mejora de avanzada", TipoObra.ObraReparacion => "Reparación de avanzada", _ => "Obra",
            };
            linea.Add(Dialogo.Texto(nombre, "plaza-obras-nombre"));
            double pedido = o.Requerido.PorId.Values.Sum(), hecho = o.Requerido.PorId.Sum(kv => Math.Min(kv.Value, o.Entregado != null && o.Entregado.PorId.TryGetValue(kv.Key, out var e) ? e : 0));
            var avance = pedido > 0 ? hecho / pedido : 1;
            var levantando = o.HasTerminaEnMs;
            var falta = Dialogo.Texto(levantando ? CuentaAtras(o.TerminaEnMs) : $"{Math.Floor(avance * 100)} %", "plaza-obras-falta");
            linea.Add(falta);
            texto.Add(linea);
            texto.Add(Dialogo.Texto(levantando ? "Levantándose en el mapa" : "Esperando materiales en el mapa", "plaza-obras-que"));
            texto.Add(BarraAvance(levantando ? (float?)null : (float)avance));
            fila.Add(texto);
            if (levantando) relojesDePlaza.Add(("obras", () => falta.text = CuentaAtras(o.TerminaEnMs)));
            return fila;
        }

        /// <summary>La confirmación de cancelar una obra o una mejora en curso: no se devuelve nada (`confirmarCancelarObra`).</summary>
        Task<bool> ConfirmarCancelarObra(Edificio e)
        {
            var nombre = Edificios.Nombre(e.Tipo);
            return e.Mejora != null
                ? dialogo.Abrir($"{nombre} se queda en su nivel y no se devuelve nada de lo pagado. La siguiente obra de la cola empieza ya.", "¿Cancelar la mejora?", "Cancelar la mejora", "Seguir", true)
                : dialogo.Abrir($"{nombre} desaparece con su sitio y no se devuelve nada de lo pagado. La siguiente obra de la cola empieza ya.", "¿Cancelar la obra?", "Cancelar la obra", "Seguir", true);
        }

        /// <summary>Los carteles «Obra · 5 h 19 min» encima de cada edificio en obra o en mejora, como la vista 3D del prototipo (`rotulo`).</summary>
        void PintarCartelesDeObra(VisualElement capa, SelectorDePersonaje.InteriorParaVer interior)
        {
            var a = plazaId == null ? null : PlazaActual(plazaId);
            if (a == null || capa.panel == null || interior.EnPantalla == null) { capa.Clear(); return; }
            var vistos = new HashSet<string>();
            foreach (var e in Internos(a))
            {
                var fin = FinDeObra(e);
                var donde = fin == null ? null : interior.EnPantalla(e.Id);
                if (donde == null) continue;
                var nombre = "cartel-obra-" + e.Id;
                vistos.Add(nombre);
                if (!(capa.Q<Label>(nombre) is Label l)) { l = Dialogo.Texto("", "plaza-cartel-obra"); l.name = nombre; l.pickingMode = PickingMode.Ignore; capa.Add(l); }
                l.text = $"{(e.Mejora != null ? "Mejora" : e.Danado ? "Reparación" : "Obra")} · {CuentaAtras(fin.Value)}";
                var p = new Vector2(donde.Value.x * capa.layout.width, donde.Value.y * capa.layout.height);
                l.style.left = p.x;
                l.style.top = p.y;
            }
            foreach (var hijo in capa.Children().ToList()) if (!vistos.Contains(hijo.name)) hijo.RemoveFromHierarchy();
        }

        // ── las placas de arriba ──

        /// <summary>Una pieza de una placa (`pieza`): icono, cifra y barrita; al pasar, el letrero con el nombre y el detalle.</summary>
        VisualElement Pieza(VisualElement icono, string cifra, float llenado, string nombre, string[] detalle, string clase, Label letrero)
        {
            var p = Dialogo.Caja("plaza-recurso" + (string.IsNullOrEmpty(clase) ? "" : " " + clase));
            p.name = "pieza-" + nombre;
            if (icono != null) { icono.AddToClassList("plaza-recurso-icono"); p.Add(icono); }
            p.Add(Dialogo.Texto(cifra, "plaza-recurso-cifra"));
            var barra = Dialogo.Caja("plaza-recurso-barra");
            var i = Dialogo.Caja("plaza-recurso-barra-i");
            i.style.width = Length.Percent(Mathf.Clamp(llenado, 0, 100));
            barra.Add(i);
            p.Add(barra);
            p.RegisterCallback<PointerEnterEvent>(_ =>
            {
                letrero.text = nombre + "\n" + string.Join("\n", detalle);
                Ver(letrero, true);
                var r = p.worldBound;
                letrero.style.left = r.center.x;
                letrero.style.top = r.yMax + 7;
                letrero.style.translate = new Translate(Length.Percent(-50), 0);
            });
            p.RegisterCallback<PointerLeaveEvent>(_ => Ver(letrero, false));
            return p;
        }

        static VisualElement Placa(string clase, string titulo, string nombre)
        {
            var g = Dialogo.Caja("plaza-placa plaza-placa-" + clase);
            g.name = nombre;
            g.Add(Dialogo.Caja("plaza-placa-piezas"));
            var t = Dialogo.Texto(titulo.ToUpperInvariant(), "plaza-placa-titulo");
            g.Add(t);
            return g;
        }

        /// <summary>Lo último que se vio de cada clase de población, para la flecha (`flechaPob`): sube, baja o parada por falta de sitio.</summary>
        readonly Dictionary<string, (double valor, int dir, double en)> vistoPob = new Dictionary<string, (double, int, double)>();

        VisualElement FlechaPob(string clave, double valor, bool parado)
        {
            var ahora = Time.realtimeSinceStartupAsDouble;
            if (!vistoPob.TryGetValue(clave, out var antes)) vistoPob[clave] = (valor, 0, ahora);
            else if (valor != antes.valor) vistoPob[clave] = (valor, valor > antes.valor ? 1 : -1, ahora);
            var v = vistoPob[clave];
            const double Dura = 5 * 60;
            if (v.dir == -1 && ahora - v.en < Dura) return Dialogo.Texto("▼", "plaza-pob-flecha baja");
            if (parado) return Dialogo.Caja("plaza-pob-flecha parada");
            if (v.dir == 1 && ahora - v.en < Dura) return Dialogo.Texto("▲", "plaza-pob-flecha sube");
            return null;
        }

        /// <summary>La barrita de una pieza de población (`barraPob`): lo que hay, la raya de lo que cabe, el rombo de lo que pide el nivel siguiente,
        /// el tramo rojo entre los dos y, en el total, lo cerrado del límite al techo.</summary>
        static void BarraPob(VisualElement pieza, double tiene, double escala, double? sitio, double? meta, bool cerrado = false)
        {
            float Pct(double x) => Mathf.Clamp((float)(escala > 0 ? x / escala * 100 : 0), 0, 100);
            var barra = pieza.Q(className: "plaza-recurso-barra");
            barra.AddToClassList("plaza-pob-barra");
            barra.Q(className: "plaza-recurso-barra-i").style.width = Length.Percent(Pct(tiene));
            void Marca(string clase, float izq, float? ancho = null)
            {
                var m = Dialogo.Caja(clase);
                m.style.left = Length.Percent(izq);
                if (ancho != null) m.style.width = Length.Percent(ancho.Value);
                barra.Add(m);
            }
            if (sitio != null && cerrado && Pct(sitio.Value) < 100) Marca("plaza-pob-cerrado", Pct(sitio.Value), 100 - Pct(sitio.Value));
            if (sitio != null && meta != null && sitio < meta) Marca("plaza-pob-falta", Pct(sitio.Value), Pct(meta.Value) - Pct(sitio.Value));
            if (sitio != null) Marca("plaza-pob-sitio", Pct(sitio.Value));
            if (meta != null) Marca("plaza-pob-meta", Pct(meta.Value));
        }

        /// <summary>Población, Almacén y Estado, arriba al centro (`htmlPieDePlaza`).</summary>
        void PintarPlacas(VisualElement pie, Label letrero, Asentamiento a)
        {
            pie.Clear();
            Ver(letrero, false);
            // Población: el total con el techo del nivel y, tras una raya, cada clase contra su sitio y lo que pide el nivel siguiente.
            if (a.Poblacion != null)
            {
                var placa = Placa("poblacion", "Población", "placa-poblacion");
                var piezas = placa.Q(className: "plaza-placa-piezas");
                var reqs = c.Datos.Bloque("Economia_Construccion", "NIVEL_ASENTAMIENTO");
                var cat = Catalogo;
                var viviendas = Internos(a).Count(e => e.Tipo == "vivienda" && Activo(e));
                var palacio = Internos(a).FirstOrDefault(e => e.Tipo == "palacio" && Activo(e));
                var meta = reqs?["requisitos"]?[(a.Nivel + 1).ToString()];
                double pes = Math.Floor(Valor(a.Poblacion.Pesants)), art = Math.Floor(Valor(a.Poblacion.Artesanos)), nob = Math.Floor(Valor(a.Poblacion.Nobleza));
                var clases = new List<(string icono, string nombre, double tiene, double? sitio, double? meta, bool vivienda)>
                {
                    ("pesants", "Pesants", pes, (double?)cat?["vivienda"]?["capacidadPesants"] * viviendas, (double?)meta?["pesants"] is double m1 && m1 > 0 ? m1 : (double?)null, true),
                    ("artesanos", "Artesanos", art, (double?)cat?["vivienda"]?["capacidadArtesanos"] * viviendas, (double?)meta?["artesanos"] is double m2 && m2 > 0 ? m2 : (double?)null, true),
                };
                if (nob > 0 || palacio != null)
                    clases.Add(("nobleza", "Nobleza", nob, palacio != null ? (double?)NivelDe("palacio", NivelInterno(palacio))?["capacidadNobles"] ?? 0 : 0, null, false));
                var techo = (double?)reqs?["techoPoblacion"]?[a.Nivel.ToString()];
                var total = pes + art + nob;
                double? sitioTotal = clases.All(x => x.sitio != null) ? clases.Sum(x => x.sitio.Value) : (double?)null;
                var limite = sitioTotal == null ? techo : techo != null ? Math.Min(techo.Value, sitioTotal.Value) : sitioTotal;
                var lleno = limite != null && total >= limite * 0.95;
                var escala = Math.Max(Math.Max(techo ?? 0, limite ?? 0), Math.Max(total, 1));
                var detalle = new List<string> { limite != null ? $"{Miles(total)} de {Miles(limite.Value)} que caben ahora" : $"{Miles(total)} en total" };
                if (techo != null) detalle.Add($"Techo del nivel {a.Nivel}: {Miles(techo.Value)}");
                var pTotal = Pieza(Glifo("gente"), Miles(total), 0, "Población", detalle.ToArray(), "plaza-pob-pieza" + (lleno ? " lleno" : ""), letrero);
                BarraPob(pTotal, total, escala, limite, null, true);
                piezas.Add(pTotal);
                piezas.Add(Dialogo.Caja("plaza-placa-raya"));
                foreach (var k in clases)
                {
                    var sinSitio = k.sitio != null && (k.vivienda ? k.tiene >= k.sitio * 0.95 : k.sitio > 0 && k.tiene >= k.sitio * 0.95);
                    var escalaC = Math.Max(Math.Max(k.tiene, k.sitio ?? 0), Math.Max(k.meta ?? 0, 1));
                    var lineas = new List<string> { k.sitio != null ? $"{Miles(k.tiene)} de {Miles(k.sitio.Value)} que caben" : Miles(k.tiene) };
                    if (k.meta != null) lineas.Add($"El nivel {a.Nivel + 1} pide {Miles(k.meta.Value)}");
                    var p = Pieza(Glifo(k.icono), Miles(k.tiene), 0, k.nombre, lineas.ToArray(), "plaza-pob-pieza" + (sinSitio ? " lleno" : ""), letrero);
                    var cifra = p.Q(className: "plaza-recurso-cifra");
                    var idx = p.IndexOf(cifra) + 1;
                    if (FlechaPob($"{a.Id}:{k.nombre}", k.tiene, sinSitio) is VisualElement flecha) p.Insert(idx++, flecha);
                    if (sinSitio && k.vivienda)
                    {
                        // Sin sitio: la Vivienda con su «+» late junto a la cifra. Pulsarla abre Construcción (tanda 2); hasta entonces, solo avisa.
                        var mas = Dialogo.Caja("plaza-pob-vivienda");
                        mas.Add(IconosDelMundo.Icono("edificio", "vivienda", 16, IconosDelMundo.OroClaro));
                        mas.Add(Dialogo.Texto("+", "plaza-pob-vivienda-mas"));
                        p.Insert(idx, mas);
                    }
                    BarraPob(p, k.tiene, escalaC, k.sitio, k.meta);
                    piezas.Add(p);
                }
                pie.Add(placa);
            }
            // Almacén: lo que tiene la plaza, hasta 8, en el orden de los iconos (la barra automática: elegir cuáles va en la ventana Almacén, tanda 2).
            if (a.Almacen.Count > 0)
            {
                var placa = Placa("almacen", "Almacén", "placa-almacen");
                var piezas = placa.Q(className: "plaza-placa-piezas");
                var lista = RecursosDeLaBarra(a);
                foreach (var r in lista)
                {
                    var cantidad = Hay(a, r);
                    var capacidad = a.Almacen.TryGetValue(r, out var x) ? x.Capacidad / 1000.0 : 0;
                    var llenado = capacidad > 0 ? cantidad / capacidad * 100 : 0;
                    piezas.Add(Pieza(IconosDelMundo.Icono("recurso", r, 15), Miles(cantidad), (float)llenado, CosasDelMapa.NombreDeRecurso(r),
                        new[] { $"{Miles(cantidad)}{(capacidad > 0 ? $" de {Miles(capacidad)}" : "")}" }, llenado >= 90 ? "lleno" : "", letrero));
                }
                if (lista.Count == 0) piezas.Add(Dialogo.Texto("Almacén vacío", "plaza-placa-vacio"));
                pie.Add(placa);
            }
            // Estado: mantenimiento y nutrición de 0 a 100, en verde, ámbar o rojo.
            {
                var placa = Placa("estado", "Estado", "placa-estado");
                var piezas = placa.Q(className: "plaza-placa-piezas");
                string Tono(double x) => x >= 66 ? "bien" : x >= 33 ? "medio" : "mal";
                void Medida(string icono, string nombre, double valor, Dictionary<string, string> palabras, string[] ayuda)
                {
                    var tono = Tono(valor);
                    piezas.Add(Pieza(Glifo(icono), Math.Round(valor).ToString(Es), (float)valor, nombre,
                        new[] { $"{palabras[tono]}: {Math.Round(valor)} de 100" }.Concat(ayuda).ToArray(), "tono-" + tono, letrero));
                }
                if (a.MedidorMantenimiento != null)
                    Medida("mantenimiento", "Mantenimiento", Valor(a.MedidorMantenimiento), new Dictionary<string, string> { ["bien"] = "Al día", ["medio"] = "Con retrasos", ["mal"] = "En ruina" },
                        new[] { "Baja si los ingresos no cubren el mantenimiento.", "Si se hunde, la plaza rinde como una de nivel menor." }
                            .Concat(a.NivelActual != a.Nivel ? new[] { $"Ahora rinde como una de nivel {a.NivelActual}." } : new string[0]).ToArray());
                if (a.Nutricion != null)
                    Medida("nutricion", "Nutrición", Valor(a.Nutricion), new Dictionary<string, string> { ["bien"] = "Bien comidos", ["medio"] = "Escasea", ["mal"] = "Hambre" },
                        new[]
                        {
                            "Sube con comida en el almacén y baja sin ella.",
                            "Cada tipo de población come primero lo suyo (carne todos; pan los pesants, queso los artesanos, vino, cerveza y los dos aceites la nobleza) y el trigo pone lo que falte.",
                            "Cada bien que comen hace crecer más a su tipo de población.",
                            "Con poca, la gente crece despacio; a cero, muere.",
                        });
                if (piezas.childCount > 0) pie.Add(placa);
            }
            OrdenarPlacas(pie);
        }

        /// <summary>Lo que la barra automática enseña en cada plaza, en el orden en que llegó (`barraAutomatica`): quita lo que se acabó y añade lo nuevo
        /// mientras quepa (8).</summary>
        readonly Dictionary<string, List<string>> barraDePlaza = new Dictionary<string, List<string>>();

        List<string> BarraAutomatica(Asentamiento a, List<string> conAlgo)
        {
            var antes = (barraDePlaza.TryGetValue(a.Id, out var l) ? l : new List<string>()).Where(conAlgo.Contains).ToList();
            var lista = antes.Concat(conAlgo.Where(r => !antes.Contains(r))).Take(8).ToList();
            barraDePlaza[a.Id] = lista;
            return lista;
        }

        // ── la ficha del edificio ──

        /// <summary>La ficha del edificio elegido, al borde derecho (`htmlFicha`): nivel y estado, sus notas, la obra en marcha, las recetas de su
        /// nivel, la receta de la bodega o la prensa, el buey del molino y, con cargo, mejorar, quitar de la cola o cancelar la obra.</summary>
        void PintarFicha(VisualElement der, Asentamiento a, Heroe yo, string cargo, Action rehacer)
        {
            der.Clear();
            var e = fichaId == null ? null : a.Edificios.FirstOrDefault(x => x.Id == fichaId);
            if (e == null) { fichaId = null; return; }
            var ficha = Dialogo.Caja("plaza-ficha");
            ficha.name = "ficha-edificio";
            var nivel = NivelInterno(e);
            var cab = Dialogo.Caja("plaza-ficha-cab");
            var titulo = Kicker(Edificios.Nombre(e.Tipo));
            titulo.AddToClassList("plaza-ficha-titulo");
            cab.Add(titulo);
            var cerrar = Dialogo.Boton("✕", "plaza-ficha-cerrar", () => { fichaId = null; rehacer(); });
            cerrar.name = "ficha-cerrar";
            cerrar.tooltip = "Cerrar";
            cab.Add(cerrar);
            ficha.Add(cab);

            var cuerpo = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            cuerpo.AddToClassList("plaza-ficha-cuerpo");
            ficha.Add(cuerpo);
            var grid = Dialogo.Caja("plaza-ficha-grid");
            foreach (var (etiqueta, valor) in new[] { ("NIVEL", nivel.ToString()), ("ESTADO", Activo(e) ? "Activo" : e.Estado == EstadoEdificio.EdificioEnConstruccion ? "En construcción" : "En cola") })
            {
                var celda = Dialogo.Caja("plaza-ficha-dato");
                celda.Add(Dialogo.Texto(etiqueta, "plaza-ficha-etiqueta"));
                celda.Add(Dialogo.Texto(valor, "plaza-ficha-valor"));
                grid.Add(celda);
            }
            cuerpo.Add(grid);
            var notas = new[] { e.Danado ? "dañado (reconstrucción)" : null, e.PausadoPorAlmacenLleno ? "parado — almacén lleno" : null }.Where(x => x != null).ToList();
            if (notas.Count > 0) cuerpo.Add(Dialogo.Texto(string.Join(" · ", notas), "plaza-nota"));
            // Si el edificio tiene ventana (Barracón, Armería…), su botón (duda 5).
            BotonDeVentanaEnFicha(cuerpo, a, yo, e, rehacer);
            var niveles = Catalogo?[e.Tipo]?["niveles"] as JObject;

            // Lo que el Albergue de caravanas suma a la flota del Mercado a su nivel.
            if (e.Tipo == "albergueCaravanas" && NivelDe(e.Tipo, nivel) is JObject cupo && ((int?)cupo["cupoCaravanas"] > 0 || (int?)cupo["cupoCarros"] > 0))
            {
                var conMercado = Internos(a).Any(x => x.Tipo == "mercado" && Activo(x));
                cuerpo.Add(Dialogo.Texto($"{(conMercado ? "Suma" : "Sin un Mercado activo no suma nada; con él, suma")} a la flota {(int?)cupo["cupoCaravanas"] ?? 0} caravanas y {(int?)cupo["cupoCarros"] ?? 0} carros.", "plaza-nota"));
            }

            // La obra en marcha, con su barra y lo que le queda.
            if (FinDeObra(e) is long fin)
            {
                var obra = Dialogo.Caja("plaza-ficha-obra");
                var linea = Dialogo.Caja("plaza-ficha-obra-linea");
                linea.Add(Dialogo.Texto(e.Mejora != null ? $"Mejorándose a N{e.Mejora.NivelObjetivo}" : "En construcción", "plaza-ficha-obra-que"));
                var queda = Dialogo.Texto($"quedan {CuentaAtras(fin)}", "plaza-ficha-obra-queda");
                linea.Add(queda);
                obra.Add(linea);
                var barra = BarraAvance(AvanceDeObra(e));
                obra.Add(barra);
                cuerpo.Add(obra);
                relojesDePlaza.Add(("ficha", () => { queda.text = $"quedan {CuentaAtras(fin)}"; if (AvanceDeObra(e) is float x) Avance(barra, x); }));
            }

            // Las recetas de su nivel (la bodega y la prensa, la suya en su bloque).
            var deUnaReceta = DeUnaReceta(e.Tipo);
            var recetas = deUnaReceta ? new List<JToken>() : (NivelDe(e.Tipo, nivel)?["recetas"] as JArray)?.ToList() ?? new List<JToken>();
            if (recetas.Count > 0)
            {
                cuerpo.Add(ConAyuda(Kicker("Recetas de este nivel"), "Consumo por edificio: no lo publica el servidor."));
                foreach (var r in recetas)
                {
                    var fila = Dialogo.Caja("plaza-ficha-receta");
                    fila.name = "receta-" + (string)r["produce"];
                    fila.Add(IconosDelMundo.Icono("recurso", (string)r["produce"], 15));
                    fila.Add(Dialogo.Texto(CosasDelMapa.NombreDeRecurso((string)r["produce"]) + ":", "plaza-ficha-receta-nombre"));
                    var insumos = (r["consumePorUnidad"] as JObject)?.Properties().ToList() ?? new List<JProperty>();
                    if (insumos.Count == 0) fila.Add(Dialogo.Texto("sin insumos", "plaza-ficha-receta-texto"));
                    foreach (var (p, i) in insumos.Select((p, i) => (p, i)))
                    {
                        if (i > 0) fila.Add(Dialogo.Texto("·", "plaza-ficha-receta-texto"));
                        fila.Add(IconosDelMundo.Icono("recurso", p.Name, 14));
                        fila.Add(Dialogo.Texto($"{Cifra((double)p.Value)} {CosasDelMapa.NombreDeRecurso(p.Name)}", "plaza-ficha-receta-texto"));
                    }
                    fila.Add(Dialogo.Texto("por unidad", "plaza-ficha-receta-texto"));
                    cuerpo.Add(fila);
                }
            }
            if (deUnaReceta) RecetaUnica(cuerpo, a, e, cargo);
            Buey(cuerpo, a, e, cargo);

            // Mejorar (con cargo, en pie y sin mejora, si el cliente no sabe ya que no se puede: nivel de plaza, edificio o tecnología que pide).
            if (cargo != null && e.Tipo != "centroUrbano" && Activo(e) && e.Mejora == null && niveles?[(nivel + 1).ToString()] is JObject siguiente)
            {
                var plazaNivel = a.NivelActual;
                var pideEdificio = (string)siguiente["requiereEdificio"];
                var pideTec = (string)siguiente["requiereTecnologia"];
                var adoptadas = TecnologiasDeMiFaccion();
                var sePuede = plazaNivel >= ((int?)siguiente["requisitoNivelAsentamiento"] ?? 0)
                    && (pideEdificio == null || Internos(a).Any(x => x.Tipo == pideEdificio && Activo(x) && NivelInterno(x) >= ((int?)siguiente["requiereEdificioNivel"] ?? 1)))
                    && (pideTec == null || adoptadas == null || adoptadas.Contains(pideTec));
                if (sePuede)
                {
                    var coste = siguiente["costoMejora"] as JObject ?? new JObject();
                    var caro = coste.Properties().Any(p => Hay(a, p.Name) < (double)p.Value);
                    var bloque = Dialogo.Caja("plaza-ficha-mejora" + (caro ? " caro" : ""));
                    bloque.Add(Kicker($"Mejorar a N{nivel + 1}"));
                    var costeFila = Dialogo.Caja("plaza-ficha-mejora-coste");
                    if (coste.Count > 0) costeFila.Add(ChipsDeCoste(a, coste)); else costeFila.Add(Dialogo.Texto("sin coste", "plaza-nota"));
                    if ((double?)siguiente["obraMinutos"] is double mins)
                    {
                        var tiempo = Dialogo.Caja("plaza-ficha-mejora-tiempo");
                        tiempo.Add(Glifo("reloj", 12));
                        tiempo.Add(Dialogo.Texto(Duracion(mins), "plaza-nota"));
                        costeFila.Add(tiempo);
                    }
                    bloque.Add(costeFila);
                    Button mejorar = null;
                    mejorar = Dialogo.Boton($"↑ Mejorar a N{nivel + 1}", "boton-primario plaza-ficha-boton", async () =>
                        await OrdenDePlaza(new Orden { MejorarEdificioAhora = new OrdenMejorarEdificioAhora { AsentamientoId = a.Id, Cargo = cargo, EdificioId = e.Id } }, mejorar));
                    mejorar.name = "mejorar";
                    if (caro) mejorar.tooltip = "No hay bastante en el almacén todavía";
                    bloque.Add(mejorar);
                    cuerpo.Add(bloque);
                }
            }
            else if (cargo != null && e.Estado == EstadoEdificio.EdificioEnCola)
            {
                Button quitar = null;
                quitar = Dialogo.Boton("Quitar de la cola", "boton-secundario plaza-ficha-boton", async () =>
                    await OrdenDePlaza(new Orden { QuitarDeCola = new OrdenQuitarDeCola { AsentamientoId = a.Id, Cargo = cargo, EdificioId = e.Id } }, quitar));
                quitar.name = "quitar-de-cola";
                cuerpo.Add(quitar);
            }
            if (cargo != null && (e.Estado == EstadoEdificio.EdificioEnConstruccion || e.Mejora != null))
            {
                Button cancelar = null;
                cancelar = Dialogo.Boton($"✕ {(e.Mejora != null ? "Cancelar la mejora" : "Cancelar la obra")}", "boton-secundario plaza-ficha-boton plaza-ficha-cancelar", async () =>
                {
                    if (await ConfirmarCancelarObra(e))
                        await OrdenDePlaza(new Orden { CancelarConstruccion = new OrdenCancelarConstruccion { AsentamientoId = a.Id, Cargo = cargo, EdificioId = e.Id } }, cancelar);
                });
                cancelar.name = "cancelar-obra";
                cancelar.tooltip = "No se devuelve nada de lo pagado";
                cuerpo.Add(cancelar);
            }
            der.Add(ficha);
        }

        /// <summary>Las tecnologías adoptadas por tu Facción (`TecnologiasFaccion`, ámbito FACCION), o null si aún no han llegado.</summary>
        HashSet<string> TecnologiasDeMiFaccion()
        {
            var h = HeroeAhora();
            var t = h != null && h.HasFaccionId ? c.Sesion.Almacen.Buscar(TipoEntidad.TecnologiaFaccion, h.FaccionId)?.Tecnologias : null;
            return t == null ? null : new HashSet<string>(t.Adoptadas);
        }

        // ── la bodega, la prensa y el molino (`recetasUnicas.ts`, `htmlRecetaUnica`, `htmlBuey`) ──

        static readonly Dictionary<string, (string taller, string produce, string nombre)> RecetaDeInsumo = new Dictionary<string, (string, string, string)>
        {
            ["uva"] = ("bodega", "vino", "Vino"), ["cebada"] = ("bodega", "cerveza", "Cerveza"),
            ["aceituna"] = ("prensa", "aceite", "Aceite de oliva"), ["sesamo"] = ("prensa", "aceiteSesamo", "Aceite de sésamo"),
        };

        JObject TalleresDeUnaReceta => c.Datos.Bloque("Economia_Construccion", "TALLERES_DE_UNA_RECETA") as JObject;
        bool DeUnaReceta(string tipo) => TalleresDeUnaReceta?[tipo] != null;
        List<string> InsumosDe(Edificio e) => (NivelDe(e.Tipo, NivelInterno(e))?["recetas"] as JArray)?
            .Select(r => (r["consumePorUnidad"] as JObject)?.Properties().FirstOrDefault()?.Name).Where(x => x != null).ToList() ?? new List<string>();
        string InsumoActivo(Edificio e) => !DeUnaReceta(e.Tipo) ? null : e.HasReceta ? e.Receta : InsumosDe(e).FirstOrDefault();

        int RecetasDistintas(string tipo, uint nivel)
        {
            var tabla = (TalleresDeUnaReceta?[tipo]?["recetasDistintasPorNivel"] as JObject)?.Properties().Select(p => (n: int.Parse(p.Name), v: (int)p.Value)).OrderBy(x => x.n).ToList();
            if (tabla == null || tabla.Count == 0) return 1;
            return tabla.LastOrDefault(x => x.n <= nivel).v is int v && tabla.Any(x => x.n <= nivel) ? v : tabla[0].v;
        }

        List<string> RecetasElegibles(Asentamiento a, Edificio e)
        {
            if (!DeUnaReceta(e.Tipo)) return new List<string>();
            var activo = InsumoActivo(e);
            var caben = RecetasDistintas(e.Tipo, a.NivelActual);
            return InsumosDe(e).Where(i => i != activo && Internos(a).Where(x => x.Tipo == e.Tipo).Select(x => x.Id == e.Id ? i : InsumoActivo(x)).Where(x => x != null).Distinct().Count() <= caben).ToList();
        }

        void RecetaUnica(VisualElement cuerpo, Asentamiento a, Edificio e, string cargo)
        {
            var activo = InsumoActivo(e);
            if (activo == null) return;
            var bloque = Dialogo.Caja("plaza-ficha-mejora");
            var regla = e.Tipo == "bodega"
                ? "Cada bodega hace vino o cerveza, no las dos. Caben tres en una plaza y, en los niveles 3 y 4, todas hacen la misma bebida; en el 5 puede haber de las dos."
                : "Cada prensa hace aceite de aceituna o de sésamo, no los dos. Caben tres en una plaza y todas sacan el aceite de la misma fuente.";
            bloque.Add(ConAyuda(Kicker("Receta"), regla));
            // «🍷 Vino, con 2 🍇 uva por unidad.» (`receta` de vistaCiudad.ts): el icono del producto y el del insumo.
            var activa = Dialogo.Caja("plaza-ficha-receta");
            var ra = RecetaDeInsumo.TryGetValue(activo, out var xa) ? xa : (null, null, CosasDelMapa.NombreDeRecurso(activo));
            var porUnidad = (NivelDe(e.Tipo, NivelInterno(e))?["recetas"] as JArray)?.FirstOrDefault(y => y["consumePorUnidad"]?[activo] != null)?["consumePorUnidad"]?[activo];
            if (ra.produce != null) activa.Add(IconosDelMundo.Icono("recurso", ra.produce, 15));
            activa.Add(Dialogo.Texto(ra.nombre + (porUnidad != null ? $", con {Cifra((double)porUnidad)}" : "."), "plaza-ficha-receta-texto"));
            if (porUnidad != null)
            {
                activa.Add(IconosDelMundo.Icono("recurso", activo, 15));
                activa.Add(Dialogo.Texto($"{CosasDelMapa.NombreDeRecurso(activo).ToLowerInvariant()} por unidad.", "plaza-ficha-receta-texto"));
            }
            bloque.Add(activa);
            var plural = e.Tipo == "bodega" ? "bodegas" : "prensas";
            if (e.RecetaParada)
                bloque.Add(Dialogo.Texto($"Parada: la plaza ha bajado de nivel y ya no le caben tantas recetas distintas en sus {plural}. Vuelve a producir con el nivel o eligiéndole otra receta.", "plaza-nota falta"));
            var elegibles = cargo != null ? RecetasElegibles(a, e) : new List<string>();
            if (elegibles.Count > 0)
            {
                var fila = Dialogo.Caja("plaza-chips");
                foreach (var i in elegibles)
                {
                    var insumo = i;
                    var nombre = RecetaDeInsumo.TryGetValue(i, out var x) ? x.nombre : i;
                    Button b = null;
                    b = Dialogo.Boton($"Hacer {nombre.ToLowerInvariant()}", "boton-secundario", async () =>
                        await OrdenDePlaza(new Orden { ElegirReceta = new OrdenElegirReceta { AsentamientoId = a.Id, Cargo = cargo, EdificioId = e.Id, Insumo = insumo } }, b));
                    b.name = "receta-elegir-" + i;
                    fila.Add(b);
                }
                bloque.Add(fila);
            }
            else if (cargo != null && InsumosDe(e).Any(i => i != activo) && !e.RecetaParada && RecetasDistintas(e.Tipo, a.NivelActual) == 1)
                bloque.Add(Dialogo.Texto($"Todas las {plural} de la plaza {(e.Tipo == "bodega" ? "hacen la misma bebida" : "sacan el aceite de la misma fuente")}: una {Edificios.Nombre(e.Tipo).ToLowerInvariant()} solo cambia de receta si es la única{(e.Tipo == "bodega" ? ", o con la plaza en el nivel 5" : "")}.", "plaza-nota"));
            cuerpo.Add(bloque);
        }

        void Buey(VisualElement cuerpo, Asentamiento a, Edificio e, string cargo)
        {
            var factor = (double?)Catalogo?[e.Tipo]?["factorBuey"];
            if (factor == null) return;
            var veces = factor == 2 ? "el doble" : $"{Cifra(factor.Value)} veces más";
            if (e.Buey) { cuerpo.Add(Dialogo.Texto($"Con su buey muele {veces}.", "plaza-nota")); return; }
            JObject coste = null;
            try { coste = c.Datos.Bloque("Comercio_Caravanas", "ANIMAL_CATALOGO")?["buey"]?["costo"] as JObject; } catch (FaltaDato) { }
            if (cargo == null || coste == null) return;
            var caro = coste.Properties().Any(p => Hay(a, p.Name) < (double)p.Value);
            var bloque = Dialogo.Caja("plaza-ficha-mejora" + (caro ? " caro" : ""));
            bloque.Add(Kicker("Buey del molino"));
            bloque.Add(Dialogo.Texto($"Con un buey muele {veces} gastando el mismo trigo por saco. Se queda en el molino y se pierde con él.", "plaza-nota"));
            bloque.Add(ChipsDeCoste(a, coste));
            Button comprar = null;
            comprar = Dialogo.Boton("Comprar el buey", "boton-primario plaza-ficha-boton", async () =>
                await OrdenDePlaza(new Orden { AsignarBuey = new OrdenAsignarBuey { AsentamientoId = a.Id, Cargo = cargo, EdificioId = e.Id } }, comprar));
            comprar.name = "comprar-buey";
            if (caro) comprar.tooltip = "No hay bastante en el almacén todavía";
            bloque.Add(comprar);
            cuerpo.Add(bloque);
        }

        // ── subir de nivel (`panelCentro.ts`: `htmlAscensoPlaza`) ──

        /// <summary>Un requisito del nivel siguiente: cumplido, no cumplido o null (lo comprueba el servidor al pedirla).</summary>
        sealed class Requisito
        {
            public bool? Ok;
            public VisualElement Icono;
            public string Texto;
            public (double, double)? Barra;
            public VisualElement Extra;
        }

        /// <summary>Los requisitos del nivel siguiente, como el motor (`NIVEL_ASENTAMIENTO` y `ASCENSO_ASENTAMIENTO`).</summary>
        List<Requisito> RequisitosDeNivel(Asentamiento a, JToken r, JObject coste)
        {
            r ??= new JObject();
            bool ActivoA(string tipo, int nivel) => Internos(a).Any(x => x.Tipo == tipo && Activo(x) && NivelInterno(x) >= nivel);
            var pedidos = (r["edificios"] as JArray)?.Select(x => (string)x).ToList() ?? new List<string>();
            var hechos = pedidos.Where(t => ActivoA(t, (int?)r["nivelInternoMinimo"]?[t] ?? 1)).ToList();
            var minimo = (int?)r["edificiosMinimo"] ?? pedidos.Count;
            var pueblos = a.PueblosIds.Select(id => c.Sesion.Almacen.Buscar(TipoEntidad.Pueblo, id)?.Pueblo).Where(p => p != null).ToList();
            var aldeas = pueblos.Count(p => p.TipoExtractor == "aldeaAgricola");
            var faltan = coste?.Properties().Where(p => Hay(a, p.Name) < (double)p.Value).ToList() ?? new List<JProperty>();
            var lista = new List<Requisito>();
            double pes = Math.Floor(Valor(a.Poblacion?.Pesants)), art = Math.Floor(Valor(a.Poblacion?.Artesanos));
            lista.Add(new Requisito { Ok = pes >= ((double?)r["pesants"] ?? 0), Icono = Glifo("pesants", 16), Texto = "Pesants", Barra = (pes, (double?)r["pesants"] ?? 0) });
            if (((double?)r["artesanos"] ?? 0) > 0)
                lista.Add(new Requisito { Ok = art >= (double)r["artesanos"], Icono = Glifo("artesanos", 16), Texto = "Artesanos", Barra = (art, (double)r["artesanos"]) });
            if (pedidos.Count > 0)
            {
                var extra = Dialogo.Caja("plaza-req-edificios");
                foreach (var t in pedidos)
                {
                    var hecho = hechos.Contains(t);
                    var caja = Dialogo.Caja("plaza-req-edificio" + (hecho ? " hecho" : ""));
                    caja.tooltip = $"{Edificios.Nombre(t)}{(hecho ? ": en marcha" : ": falta")}";
                    caja.Add(IconosDelMundo.Icono("edificio", t, 22, hecho ? IconosDelMundo.OroClaro : (Color?)new Color(0.63f, 0.55f, 0.43f, 0.45f)));
                    extra.Add(caja);
                }
                lista.Add(new Requisito
                {
                    Ok = hechos.Count >= minimo, Icono = IconosDelMundo.Icono("edificio", "centroUrbano", 16, IconosDelMundo.OroClaro),
                    Texto = $"{(minimo == pedidos.Count ? "Edificios en marcha" : $"{minimo} de estos edificios")} ({hechos.Count}/{minimo})", Extra = extra,
                });
            }
            foreach (var p in (r["edificiosConNivel"] as JObject)?.Properties() ?? Enumerable.Empty<JProperty>())
            {
                var mejor = Internos(a).Where(x => x.Tipo == p.Name && Activo(x)).Select(NivelInterno).DefaultIfEmpty(0).Max();
                lista.Add(new Requisito { Ok = mejor >= (int)p.Value, Icono = IconosDelMundo.Icono("edificio", p.Name, 16, IconosDelMundo.OroClaro), Texto = $"{Edificios.Nombre(p.Name)} a nivel {p.Value}" });
            }
            if (((int?)r["aldeasAgricolas"] ?? 0) > 0)
                lista.Add(new Requisito { Ok = aldeas >= (int)r["aldeasAgricolas"], Icono = Glifo("pesants", 16), Texto = "Una aldea agrícola activa (pestaña Territorio)" });
            if (((int?)r["pueblosActivos"] ?? 0) > 0)
            {
                var n = (int)r["pueblosActivos"];
                lista.Add(new Requisito { Ok = pueblos.Count >= n, Icono = IconosDelMundo.Icono("simbolo", "casa", 16, IconosDelMundo.OroClaro),
                    Texto = $"{(n == 1 ? "Un pueblo activo" : $"{n} pueblos activos")}, aldea o extractor (pestaña Territorio)", Barra = (pueblos.Count, n) });
            }
            if ((int?)r["recintoCompletoNivelMinimo"] is int muro && muro > 0)
                lista.Add(new Requisito { Ok = null, Icono = IconosDelMundo.Icono("edificio", "muralla", 16, IconosDelMundo.OroClaro), Texto = $"Un recinto de muralla completo de nivel {muro} o más (lo comprueba el servidor)" });
            var chips = coste != null && coste.Count > 0 ? ChipsDeCoste(a, coste) : Dialogo.Texto("sin coste", "plaza-nota");
            lista.Add(new Requisito { Ok = faltan.Count == 0, Icono = IconosDelMundo.Icono("recurso", "_otro", 16), Texto = faltan.Count > 0 ? "En el almacén (en rojo, lo que falta)" : "En el almacén", Extra = chips });
            return lista;
        }

        /// <summary>¿Se puede pedir ya la subida? Sin obra en marcha, bajo el techo y con todo lo que se comprueba aquí cumplido (`ascensoPosible`).</summary>
        bool AscensoPosible(Asentamiento a)
        {
            if (a.Ascenso != null) return false;
            var reqs = c.Datos.Bloque("Economia_Construccion", "NIVEL_ASENTAMIENTO");
            var objetivo = a.Nivel + 1;
            if (objetivo > ((int?)reqs?["nivelMaximo"] ?? 5)) return false;
            var coste = c.Datos.Bloque("Economia_Construccion", "ASCENSO_ASENTAMIENTO")?["porNivelObjetivo"]?[objetivo.ToString()]?["costo"] as JObject;
            return RequisitosDeNivel(a, reqs?["requisitos"]?[objetivo.ToString()], coste).All(x => x.Ok != false);
        }

        /// <summary>La ventana «Subir de nivel» (estrecha, junto a la columna izquierda): la tarjeta con el escudo de ahora y el del nivel siguiente, los
        /// pilotos de los requisitos, el tiempo de obra y el botón; el aviso de solvencia; y los requisitos desplegados, con sus barras.</summary>
        void PintarVentanaAscenso(VisualElement ventana, Asentamiento a, Heroe yo, Action rehacer)
        {
            ventana.Clear();
            Ver(ventana, ventanaAscenso);
            if (!ventanaAscenso) return;
            var cab = Dialogo.Caja("plaza-ventana-cab");
            var titulo = Dialogo.Caja("plaza-ventana-titulo");
            titulo.Add(Medallon((int)a.Nivel, "", 26, 30));
            titulo.Add(Dialogo.Texto("Subir de nivel", "plaza-ventana-nombre"));
            cab.Add(titulo);
            var cerrar = Dialogo.Boton("✕", "plaza-ventana-cerrar", () => { ventanaAscenso = false; rehacer(); });
            cerrar.name = "ventana-cerrar";
            cerrar.tooltip = "Cerrar (Esc)";
            cab.Add(cerrar);
            ventana.Add(cab);
            var cuerpo = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            cuerpo.AddToClassList("plaza-ventana-cuerpo");
            ventana.Add(cuerpo);
            var soyGobernador = a.Cargos?.GobernadorId == yo.Id;
            var info = "La subida de nivel no es automática: la pide el Gobernador, se paga entera del almacén y tarda una obra. Si conquistan la plaza durante la obra, se pierde. El servidor comprueba además el cupo de tu Facción. Si lo que llega a la plaza no cubre el mantenimiento del nivel nuevo, lo verás aquí como aviso, pero puedes subir igual.";
            var tarjeta = Dialogo.Caja("plaza-ascenso-tarjeta");
            tarjeta.name = "ascenso-tarjeta";
            var niveles = Dialogo.Caja("plaza-ascenso-niveles");
            var infoCaja = Dialogo.Caja("plaza-ascenso-info");
            tarjeta.Add(niveles);
            tarjeta.Add(infoCaja);
            cuerpo.Add(tarjeta);
            niveles.Add(Medallon((int)a.Nivel));
            niveles.Add(IconosDelMundo.Icono("simbolo", "flecha", 14, new Color32(0xd8, 0xb8, 0x78, 0xff)));
            if (a.Ascenso != null)
            {
                tarjeta.AddToClassList("en-obra");
                niveles.Add(Medallon((int)a.Ascenso.NivelObjetivo, "en-obra"));
                infoCaja.Add(ConAyuda(Dialogo.Texto($"Subiendo a nivel {a.Ascenso.NivelObjetivo}", "plaza-ascenso-titulo"), info));
                var barra = BarraAvance(0);
                infoCaja.Add(barra);
                var falta = Dialogo.Texto("", "plaza-nota plaza-ascenso-obra");
                infoCaja.Add(falta);
                var x = a.Ascenso;
                void Reloj()
                {
                    double total = x.CompletaEnMs - x.IniciadoEnMs;
                    Avance(barra, total > 0 ? Mathf.Clamp01((float)((AhoraMs - x.IniciadoEnMs) / total)) : 1);
                    falta.text = $"faltan {CuentaAtras(x.CompletaEnMs)}";
                }
                Reloj();
                relojesDePlaza.Add(("ventana", Reloj));
                return;
            }
            var reqs = c.Datos.Bloque("Economia_Construccion", "NIVEL_ASENTAMIENTO");
            var objetivo = (int)a.Nivel + 1;
            if (objetivo > ((int?)reqs?["nivelMaximo"] ?? 5))
            {
                niveles.Clear();
                niveles.Add(Medallon((int)a.Nivel, "listo"));
                infoCaja.Add(ConAyuda(Dialogo.Texto("Nivel más alto de hoy", "plaza-ascenso-titulo"), info));
                infoCaja.Add(Dialogo.Texto("La plaza no puede subir más por ahora.", "plaza-nota"));
                return;
            }
            var asc = c.Datos.Bloque("Economia_Construccion", "ASCENSO_ASENTAMIENTO")?["porNivelObjetivo"]?[objetivo.ToString()];
            var lista = RequisitosDeNivel(a, reqs?["requisitos"]?[objetivo.ToString()], asc?["costo"] as JObject);
            var cumplidos = lista.Count(x => x.Ok != false);
            var puede = lista.All(x => x.Ok != false);
            tarjeta.EnableInClassList("lista", puede);
            niveles.Add(Medallon(objetivo, puede ? "listo" : "apagado"));
            infoCaja.Add(ConAyuda(Dialogo.Texto($"Subir a nivel {objetivo}", "plaza-ascenso-titulo"), info));
            var pilotos = Dialogo.Caja("plaza-ascenso-pilotos");
            pilotos.tooltip = $"{cumplidos} de {lista.Count} requisitos";
            foreach (var x in lista) { var piloto = Dialogo.Caja("plaza-piloto " + (x.Ok == null ? "servidor" : x.Ok.Value ? "ok" : "falta")); piloto.tooltip = x.Texto; pilotos.Add(piloto); }
            pilotos.Add(Dialogo.Texto($"{cumplidos}/{lista.Count}", "plaza-nota"));
            infoCaja.Add(pilotos);
            if ((double?)asc?["obraMinutos"] is double mins)
            {
                var obra = Dialogo.Caja("plaza-ascenso-obra-fila");
                obra.Add(Glifo("reloj", 12));
                obra.Add(Dialogo.Texto($"obra de {Duracion(mins)}", "plaza-nota plaza-ascenso-obra"));
                infoCaja.Add(obra);
            }
            var motivo = !soyGobernador ? "Solo el Gobernador puede pedir la subida." : puede ? $"Subir a nivel {objetivo}" : "Aún no cumple todos los requisitos (despliégalos debajo).";
            Button subir = null;
            subir = Dialogo.Boton("⬆", "plaza-ascenso-boton", async () =>
                await OrdenDePlaza(new Orden { SolicitarAscenso = new OrdenSolicitarAscenso { AsentamientoId = a.Id } }, subir));
            subir.name = "subir-nivel";
            subir.tooltip = motivo;
            subir.SetEnabled(puede && soyGobernador);
            tarjeta.Add(subir);

            // El aviso de solvencia (`insolvenciaAscenso`): no bloquea.
            if (a.InsolvenciaAscenso.Count > 0)
            {
                var aviso = Dialogo.Caja("plaza-ascenso-aviso");
                aviso.name = "aviso-solvencia";
                var signo = IconosDelMundo.Icono("glifo", "aviso", 18);
                if (signo != null) { signo.AddToClassList("plaza-ascenso-aviso-icono"); aviso.Add(signo); }
                var cuerpoAviso = Dialogo.Caja("plaza-ascenso-aviso-cuerpo");
                aviso.Add(cuerpoAviso);
                cuerpoAviso.Add(Dialogo.Texto($"Con lo que llega hoy, el nivel {objetivo} no se pagaría solo", "plaza-ascenso-aviso-titulo"));
                cuerpoAviso.Add(Dialogo.Texto("No impide subir, pero el mantenimiento iría a la baja si no cambia.", "plaza-nota"));
                foreach (var x in a.InsolvenciaAscenso)
                {
                    var fila = Dialogo.Caja("plaza-ascenso-insolvencia");
                    fila.Add(IconosDelMundo.Icono("recurso", x.Recurso, 14));
                    fila.Add(Dialogo.Texto(CosasDelMapa.NombreDeRecurso(x.Recurso), "plaza-ascenso-insolvencia-nombre"));
                    fila.Add(Dialogo.Texto($"llega {Cifra(x.Llega / 1000.0)} y pediría {Cifra(x.Pide / 1000.0)} por minuto", "plaza-nota"));
                    cuerpoAviso.Add(fila);
                }
                cuerpo.Add(aviso);
            }

            // Los requisitos, desplegados (el prototipo los abre en esta ventana).
            var plegable = new Foldout { text = $"Requisitos del nivel {objetivo}", value = true, name = "requisitos" };
            plegable.AddToClassList("plaza-plegable");
            var meta = Dialogo.Texto($"{cumplidos} de {lista.Count}", "plaza-plegable-meta" + (puede ? "" : " falta"));
            plegable.Q<Toggle>().Add(meta);
            foreach (var x in lista)
            {
                var fila = Dialogo.Caja("plaza-req " + (x.Ok == null ? "servidor" : x.Ok.Value ? "ok" : "falta"));
                fila.Add(Dialogo.Texto(x.Ok == null ? "?" : x.Ok.Value ? "✓" : "✗", "plaza-req-marca"));
                var icono = Dialogo.Caja("plaza-req-icono");
                if (x.Icono != null) icono.Add(x.Icono);
                fila.Add(icono);
                var cuerpoR = Dialogo.Caja("plaza-req-cuerpo");
                cuerpoR.Add(Dialogo.Texto(x.Texto, "plaza-req-texto"));
                if (x.Barra.HasValue)
                {
                    var (tiene, pide) = x.Barra.Value;
                    var medida = Dialogo.Caja("plaza-req-medida");
                    var barra = Dialogo.Caja("plaza-req-barra");
                    var i = Dialogo.Caja("plaza-req-barra-i");
                    i.style.width = Length.Percent(Mathf.Clamp01((float)(pide > 0 ? tiene / pide : 1)) * 100);
                    barra.Add(i);
                    medida.Add(barra);
                    medida.Add(Dialogo.Texto($"{Miles(tiene)} / {Miles(pide)}", "plaza-nota"));
                    cuerpoR.Add(medida);
                }
                if (x.Extra != null) cuerpoR.Add(x.Extra);
                fila.Add(cuerpoR);
                plegable.Add(fila);
            }
            if (!soyGobernador) plegable.Add(Dialogo.Texto("Solo el Gobernador puede pedir la subida.", "plaza-nota"));
            cuerpo.Add(plegable);
        }
    }
}

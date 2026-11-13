// Lo que hay en el mapa de mundo, sacado del almacén de la sesión (paso 2 del bloque 4, tanda 2B), con los tamaños y colores de BronzeAgeClient
// (`src/mundo3d/huellas.ts`, `config.ts`, `maquetas.ts`, `estado.ts`), qué hay bajo un clic (`objetosBajoElClic` y `ui/agarre.ts`) y qué acciones
// a la puerta se pueden hacer con cada cosa (`ui/accionesPuerta.ts`, `ui/interaccionAjena.ts`, `ui/pueblos.ts`). Regla de interfaz del autor: se
// enseña solo lo que el jugador puede hacer. Los radios de las reglas salen de `/v1/datos`. C# puro.
using System;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;

namespace ConquestTactics.Mundo
{
    /// <summary>Clases de cosas del mapa, en el orden en que salen en la lista del clic (`ORDEN_DEL_CLIC` de BronzeAgeClient).</summary>
    public enum ClaseDeCosa { Batalla, Ejercito, Formacion, EjercitoAjeno, CaravanaAjena, Caravana, Pueblo, Aldea, Avanzada, Obra, Aeda, Alijo, Mercenarios, Bandidos, Plaza, Nodo }

    public sealed class CosaDelMapa
    {
        public ClaseDeCosa Clase;
        public string Id, Nombre, Detalle, FaccionId;
        /// <summary>Centro, en unidades del mapa.</summary>
        public double X, Y;
        /// <summary>Lo que ocupa en el suelo (radio, u) y su alto (u): la caja que la pinta.</summary>
        public double Radio, Alto;
        /// <summary>Radio de la zona de clic (u), antes del mínimo en píxeles.</summary>
        public double RadioClic;
        /// <summary>Color en sRGB, 0xRRGGBB.</summary>
        public uint Color;
        /// <summary>Lo que solo se recuerda va bajo la niebla; lo que se ve ahora, encima.</summary>
        public bool BajoNiebla;
        public Entidad Entidad;
        public bool SeElige => Clase != ClaseDeCosa.Nodo;
    }

    /// <summary>Una raya del mapa (camino, frontera, anillo): sus puntos (u), su ancho (u), su color y si va bajo la niebla.</summary>
    public sealed class RayaDelMapa
    {
        public List<(double x, double y)> Puntos = new List<(double, double)>();
        public double Ancho;
        public uint Color;
        public bool BajoNiebla, Cerrada;
    }

    public sealed class AccionDelMapa
    {
        public string Texto;
        public Orden Orden;
        public AccionDelMapa(string texto, Orden orden) { Texto = texto; Orden = orden; }
    }

    public static class CosasDelMapa
    {
        // Factores de BronzeAgeClient (`config.ts`): maqueta 0,8; campamentos y alijos ×2; pueblos y avanzadas ×1,6; nodos ×0,5.
        const double Maqueta = 0.8, Pueblo = 1.6, Campamentos = 2, Holgura = 0.12;
        /// <summary>Colores de Facción sin sigilo (`paletas.ts`: `FACCION_COLORES`) y lo que no es de nadie (`COLOR_AJENO`).</summary>
        static readonly uint[] ColoresDeFaccion = { 0xc0392b, 0x2980b9, 0x27ae60, 0x8e44ad, 0xd35400, 0x16a085 };
        public const uint Ajeno = 0x9a927f, Mercenarios = 0x6fa3d6, Bandidos = 0xc0392b, Oro = 0xe0b422, BatallaColor = 0x8a3b12,
            Camino = 0x8b5a2b, AnilloMercenarios = 0x2e5e8c, Mirada = 0x7cc4ea, Piedra = 0x8d8d8d;

        /// <summary>El color de una Facción (`f-N`). El sigilo, con sus colores, entra con el paso 8; hasta entonces, la paleta de reserva por N.</summary>
        public static uint ColorDeFaccion(string faccionId)
        {
            if (string.IsNullOrEmpty(faccionId)) return Ajeno;
            var guion = faccionId.LastIndexOf('-');
            return guion >= 0 && int.TryParse(faccionId.Substring(guion + 1), out var n) ? ColoresDeFaccion[Math.Abs(n) % ColoresDeFaccion.Length] : Ajeno;
        }

        static (double x, double y) P(Punto p) => p == null ? (0, 0) : (p.X / 1000.0, p.Y / 1000.0);

        /// <summary>Todo lo que hay en el almacén para pintar, salvo tu propia columna (la pinta el mapa con su ruta). `radioZonaInicial` (u):
        /// `reglas.territorio.radio_inicial` de `/v1/datos`.</summary>
        public static List<CosaDelMapa> Reunir(Almacen a, string heroeId, double ahora, IEnumerable<NodoRecurso> nodos, double radioZonaInicial)
        {
            var r = new List<CosaDelMapa>();
            var heroe = heroeId == null ? null : a.Buscar(TipoEntidad.Heroe, heroeId)?.Heroe;
            var miFaccion = heroe != null && heroe.HasFaccionId ? heroe.FaccionId : null;
            var miColumna = heroe?.Ubicacion?.EjercitoId;
            var plazas = new Dictionary<string, Asentamiento>();
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.Asentamiento)) plazas[e.Asentamiento.Id] = e.Asentamiento;
            uint ColorDeMadre(string madreId) => plazas.TryGetValue(madreId ?? "", out var m) ? ColorDeFaccion(m.FaccionId) : Ajeno;

            foreach (var p in plazas.Values)
            {
                var (x, y) = P(p.Posicion);
                double radioZona = radioZonaInicial;
                if (p.Zona.Count >= 3) radioZona = p.Zona.Max(z => Math.Sqrt(Math.Pow(z.X / 1000.0 - x, 2) + Math.Pow(z.Y / 1000.0 - y, 2)));
                double rc = radioZona * 0.1, huella = rc * Maqueta * (p.Nivel <= 1 ? 0.62 : p.Nivel == 2 ? 0.8 : 1);
                r.Add(new CosaDelMapa
                {
                    Clase = ClaseDeCosa.Plaza, Id = p.Id, Nombre = p.HasNombre ? p.Nombre : p.Id, Detalle = $"Nivel {p.Nivel}", FaccionId = p.FaccionId,
                    X = x, Y = y, Radio = huella, Alto = Math.Max(0.4, huella * 0.9), RadioClic = huella + Holgura, Color = ColorDeFaccion(p.FaccionId), Entidad = a.Buscar(TipoEntidad.Asentamiento, p.Id),
                });
            }
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.FichaConocida))
            {
                var f = e.FichaConocida;
                if (plazas.ContainsKey(f.AsentamientoId) || (f.Tipo != TipoEntidad.NoEspecificado && f.Tipo != TipoEntidad.Asentamiento)) continue;
                var (x, y) = P(f.Posicion);
                double rc = (f.HasRadioPotencial ? f.RadioPotencial / 1000.0 : radioZonaInicial) * 0.1, huella = rc * Maqueta * (f.Nivel <= 1 ? 0.62 : f.Nivel == 2 ? 0.8 : 1);
                r.Add(new CosaDelMapa
                {
                    Clase = ClaseDeCosa.Plaza, Id = f.AsentamientoId, Nombre = f.HasNombre ? f.Nombre : f.AsentamientoId, Detalle = $"Nivel {f.Nivel} · recordada", FaccionId = f.FaccionId,
                    X = x, Y = y, Radio = huella, Alto = Math.Max(0.4, huella * 0.9), RadioClic = huella + Holgura, Color = ColorDeFaccion(f.FaccionId), BajoNiebla = true, Entidad = e,
                });
            }
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.Pueblo))
            {
                var q = e.Pueblo;
                var (x, y) = P(q.Posicion);
                bool aldea = q.TipoExtractor == "aldeaAgricola";
                double huella = (aldea ? 1.4 : 0.58) * Pueblo;
                r.Add(new CosaDelMapa
                {
                    Clase = aldea ? ClaseDeCosa.Aldea : ClaseDeCosa.Pueblo, Id = q.Id, Nombre = aldea ? "Aldea" : "Pueblo", Detalle = "De " + NombreDePlaza(plazas, q.AsentamientoMadreId),
                    FaccionId = plazas.TryGetValue(q.AsentamientoMadreId, out var m) ? m.FaccionId : null,
                    X = x, Y = y, Radio = aldea ? 0.58 * Pueblo : huella, Alto = 0.35, RadioClic = huella + Holgura, Color = ColorDeMadre(q.AsentamientoMadreId), Entidad = e,
                });
            }
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.Avanzada))
            {
                var v = e.Avanzada;
                var (x, y) = P(v.Posicion);
                bool ruinas = v.Estado == EstadoAvanzada.AvanzadaEnRuinas;
                double huella = 0.5 * Pueblo;
                r.Add(new CosaDelMapa
                {
                    Clase = ClaseDeCosa.Avanzada, Id = v.Id, Nombre = "Avanzada", Detalle = "De " + NombreDePlaza(plazas, v.AsentamientoMadreId) + (ruinas ? " · en ruinas" : ""),
                    FaccionId = plazas.TryGetValue(v.AsentamientoMadreId, out var m) ? m.FaccionId : null,
                    X = x, Y = y, Radio = huella, Alto = ruinas ? 0.25 : 0.65, RadioClic = huella + Holgura, Color = ruinas ? 0x7d7466u : ColorDeMadre(v.AsentamientoMadreId), Entidad = e,
                });
            }
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.Obra))
            {
                var o = e.Obra;
                var (x, y) = P(o.Posicion);
                r.Add(new CosaDelMapa
                {
                    Clase = ClaseDeCosa.Obra, Id = o.Id, Nombre = "Obra", Detalle = "De " + NombreDePlaza(plazas, o.AsentamientoMadreId),
                    X = x, Y = y, Radio = 0.4 * Maqueta, Alto = 0.3, RadioClic = 0.4 * Maqueta + Holgura, Color = ColorDeMadre(o.AsentamientoMadreId), Entidad = e,
                });
            }
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.CampamentoMercenarios))
            {
                var (x, y) = P(e.CampamentoMercenarios.Posicion);
                double huella = 0.63 * Maqueta * Campamentos;
                r.Add(new CosaDelMapa { Clase = ClaseDeCosa.Mercenarios, Id = e.CampamentoMercenarios.Id, Nombre = "Mercenarios", Detalle = "Campamento de mercenarios",
                    X = x, Y = y, Radio = huella, Alto = 0.45, RadioClic = huella + Holgura, Color = Mercenarios, Entidad = e });
            }
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.CampamentoBandidos))
            {
                var b = e.CampamentoBandidos;
                var (x, y) = P(b.Posicion);
                double huella = 0.48 * Maqueta * Campamentos;
                r.Add(new CosaDelMapa { Clase = ClaseDeCosa.Bandidos, Id = b.Id, Nombre = "Bandidos", Detalle = $"Nivel {b.Nivel}",
                    X = x, Y = y, Radio = huella, Alto = 0.35, RadioClic = huella + Holgura, Color = Bandidos, Entidad = e });
            }
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.Alijo))
            {
                var (x, y) = P(e.Alijo.Posicion);
                double huella = 0.14 * Maqueta * Campamentos;
                r.Add(new CosaDelMapa { Clase = ClaseDeCosa.Alijo, Id = e.Alijo.Id, Nombre = "Alijo", Detalle = "Recursos sueltos en el suelo",
                    X = x, Y = y, Radio = huella, Alto = 0.2, RadioClic = huella + Holgura, Color = Oro, Entidad = e });
            }
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.Batalla))
            {
                var (x, y) = P(e.Batalla.Punto);
                r.Add(new CosaDelMapa { Clase = ClaseDeCosa.Batalla, Id = e.Batalla.Id, Nombre = "Batalla", Detalle = "En curso",
                    X = x, Y = y, Radio = 0.65, Alto = 0.55, RadioClic = 0.65 + Holgura, Color = BatallaColor, Entidad = e });
            }
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.Ejercito))
            {
                var c = e.Ejercito;
                if (c.Id == miColumna || c.Plan == null && c.Posicion == null) continue;
                var p = Instante.Posicion(c.Plan, c.Posicion, ahora);
                bool mia = miFaccion != null && c.FaccionId == miFaccion, ejercito = c.Tipo == TipoColumna.ColumnaEjercito, formacion = c.HasFormacionExpiraEnMs;
                r.Add(new CosaDelMapa
                {
                    Clase = mia ? (formacion ? ClaseDeCosa.Formacion : ClaseDeCosa.Ejercito) : ClaseDeCosa.EjercitoAjeno, Id = c.Id,
                    Nombre = (ejercito ? "Ejército" : "Columna") + (mia ? " de tu Facción" : " ajena"),
                    Detalle = c.Participantes.Count == 1 ? Textos.NombreDeHeroe(a, c.Participantes[0].HeroeId) : $"{c.Participantes.Count} héroes", FaccionId = c.FaccionId,
                    X = p.x / 1000, Y = p.y / 1000, Radio = 0.3, Alto = 0.5, RadioClic = 0.8, Color = c.HasFaccionId() ? ColorDeFaccion(c.FaccionId) : 0xc0392b, Entidad = e,
                });
            }
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.Caravana))
            {
                var c = e.Caravana;
                if (c.Plan == null && c.Posicion == null) continue;
                var p = Instante.Posicion(c.Plan, c.Posicion, ahora);
                bool mia = miFaccion != null && c.HasFaccionId && c.FaccionId == miFaccion || c.HasTitularId && c.TitularId == heroeId;
                r.Add(new CosaDelMapa
                {
                    Clase = mia ? ClaseDeCosa.Caravana : ClaseDeCosa.CaravanaAjena, Id = c.Id, Nombre = mia ? "Caravana de tu Facción" : "Caravana ajena",
                    Detalle = c.Escoltada ? "Escoltada" : "", FaccionId = c.HasFaccionId ? c.FaccionId : null,
                    X = p.x / 1000, Y = p.y / 1000, Radio = 0.3, Alto = 0.35, RadioClic = 0.7, Color = c.HasFaccionId ? ColorDeFaccion(c.FaccionId) : Ajeno, Entidad = e,
                });
            }
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.AedaItinerante))
            {
                var v = e.AedaItinerante;
                if (v.HasEnAsentamientoId) continue; // el parado va en su plaza
                var p = Instante.Posicion(v.Plan, v.Posicion, ahora);
                r.Add(new CosaDelMapa { Clase = ClaseDeCosa.Aeda, Id = v.Id, Nombre = "Aeda", Detalle = "De camino",
                    X = p.x / 1000, Y = p.y / 1000, Radio = 0.2, Alto = 0.4, RadioClic = 0.5, Color = 0xe9dcc0, Entidad = e });
            }
            if (nodos != null)
                foreach (var n in nodos)
                {
                    var (x, y) = P(n.Posicion);
                    r.Add(new CosaDelMapa { Clase = ClaseDeCosa.Nodo, Id = n.Id, Nombre = NombreDeRecurso(n.Tipo), X = x, Y = y, Radio = 0.5 * 0.5, Alto = 0.25, Color = ColorDeRecurso(n.Tipo), BajoNiebla = true });
                }
            return r;
        }

        static bool HasFaccionId(this Ejercito c) => !string.IsNullOrEmpty(c.FaccionId);

        static string NombreDePlaza(Dictionary<string, Asentamiento> plazas, string id) =>
            plazas.TryGetValue(id ?? "", out var p) && p.HasNombre ? p.Nombre : "una plaza";

        public static string NombreDeRecurso(string tipo) => string.IsNullOrEmpty(tipo) ? "Recurso" : char.ToUpper(tipo[0]) + tipo.Substring(1).Replace('_', ' ');

        static uint ColorDeRecurso(string tipo)
        {
            switch (tipo)
            {
                case "cobre": return 0xb87333;
                case "estano": case "estaño": return 0xa8a9ad;
                case "hierro": return 0x6b6b6b;
                case "oro": return 0xd4af37;
                case "plata": return 0xc8c8c8;
                default: return Piedra;
            }
        }

        /// <summary>Caminos (sendero 0,4 u; camino y calzada 0,65), zonas de Facción tal cual llegan, las fronteras de cada plaza a la vista, el
        /// anillo de protección de los mercenarios y las Miradas. No se fusiona nada en el cliente.</summary>
        public static List<RayaDelMapa> Rayas(Almacen a, string heroeId, double radioProteccionMercenarios, double ahora)
        {
            var r = new List<RayaDelMapa>();
            var heroe = heroeId == null ? null : a.Buscar(TipoEntidad.Heroe, heroeId)?.Heroe;
            var miFaccion = heroe != null && heroe.HasFaccionId ? heroe.FaccionId : null;
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.RedCaminos))
                foreach (var t in e.RedCaminos.Tramos)
                {
                    var raya = new RayaDelMapa { Ancho = t.Escalon == 0 ? 0.4 : 0.65, Color = Camino, BajoNiebla = true };
                    foreach (var p in t.Puntos) raya.Puntos.Add(P(p));
                    if (raya.Puntos.Count > 1) r.Add(raya);
                }
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.ZonaFaccion))
                foreach (var c in e.ZonaFaccion.Contornos)
                {
                    var raya = new RayaDelMapa { Ancho = 0.18, Color = ColorDeFaccion(e.ZonaFaccion.FaccionId), BajoNiebla = e.ZonaFaccion.FaccionId != miFaccion, Cerrada = true };
                    foreach (var p in c.Puntos) raya.Puntos.Add(P(p));
                    if (raya.Puntos.Count > 2) r.Add(raya);
                }
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.Asentamiento))
            {
                var p = e.Asentamiento;
                if (p.Zona.Count < 3) continue;
                var raya = new RayaDelMapa { Ancho = 0.12, Color = ColorDeFaccion(p.FaccionId), BajoNiebla = true, Cerrada = true };
                foreach (var z in p.Zona) raya.Puntos.Add(P(z));
                r.Add(raya);
            }
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.CampamentoMercenarios))
                r.Add(Circulo(P(e.CampamentoMercenarios.Posicion), radioProteccionMercenarios, 0.1, AnilloMercenarios));
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.Mirada))
            {
                var m = e.Mirada;
                r.Add(Circulo(P(m.Centro), m.Radio / 1000.0, ahora < m.ExpiraEnMs ? 0.15 : 0.1, ahora < m.ExpiraEnMs ? Mirada : 0xb4b4b4));
            }
            return r;
        }

        public static RayaDelMapa Circulo((double x, double y) c, double radio, double ancho, uint color, int lados = 48)
        {
            var raya = new RayaDelMapa { Ancho = ancho, Color = color, Cerrada = true };
            for (int i = 0; i < lados; i++)
            {
                var t = i * 2 * Math.PI / lados;
                raya.Puntos.Add((c.x + radio * Math.Cos(t), c.y + radio * Math.Sin(t)));
            }
            return raya;
        }

        /// <summary>Lo que hay bajo un clic en (x, y): lo que tiene ese punto en su zona de clic, que mide como poco `minimoPx` píxeles de radio
        /// a la escala de la cámara. En el orden de la lista (`ClaseDeCosa`) y, dentro de cada clase, de cerca a lejos.</summary>
        public static List<CosaDelMapa> BajoElClic(IEnumerable<CosaDelMapa> cosas, double x, double y, double pxPorUnidad, double minimoPx = 13)
        {
            var minimo = pxPorUnidad > 0 ? minimoPx / pxPorUnidad : 0;
            return cosas.Where(c => c.SeElige && Distancia(c.X, c.Y, x, y) <= Math.Max(c.RadioClic, minimo))
                .OrderBy(c => c.Clase).ThenBy(c => Distancia(c.X, c.Y, x, y)).ToList();
        }

        public static double Distancia(double ax, double ay, double bx, double by) => Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));

        /// <summary>Todo lo que hace falta saber del jugador para decidir sus acciones.</summary>
        public sealed class Contexto
        {
            public string HeroeId, FaccionId, ResidenciaId;
            /// <summary>Tu columna en el mapa (null si estás dentro de algo) y dónde está ahora (u).</summary>
            public Ejercito Columna;
            public double X, Y;
            public bool Herido, ConSoldados, Lider;
            /// <summary>La columna marcha o se está formando: entonces no hay acciones a la puerta (el motor mide la puerta con la columna
            /// donde él la tiene; solo sale lo que se puede hacer, como BronzeAgeClient 0.195.1).</summary>
            public bool EnMarcha;
            public string Persiguiendo;
            /// <summary>Radios de `/v1/datos` (u): puerta, encuentro (ataque), inspección y protección de los mercenarios.</summary>
            public double RadioPuerta, RadioEncuentro, RadioInspeccion, RadioProteccion;
            public List<(double x, double y)> CampamentosDeMercenarios = new List<(double, double)>();
        }

        /// <summary>Las acciones que el jugador puede hacer ahora con `c`, cada una con su orden. Solo lo que se puede (si falta algo, no sale).</summary>
        public static List<AccionDelMapa> Acciones(CosaDelMapa c, Contexto j)
        {
            var r = new List<AccionDelMapa>();
            if (j.Columna == null || c.Clase == ClaseDeCosa.Nodo) return r;
            var d = Distancia(j.X, j.Y, c.X, c.Y);
            bool propia = c.FaccionId != null && c.FaccionId == j.FaccionId;
            bool aLaPuerta = d <= j.RadioPuerta && !j.EnMarcha, lider = j.Lider;
            bool protegido = j.CampamentosDeMercenarios.Any(m => Distancia(m.x, m.y, j.X, j.Y) <= j.RadioProteccion || Distancia(m.x, m.y, c.X, c.Y) <= j.RadioProteccion);
            bool puedeAtacar = !j.Herido && d <= j.RadioEncuentro && !protegido;
            Orden Atacar(OrdenAtacar.Types.Objetivo o) => new Orden { Atacar = new OrdenAtacar { HeroeId = j.HeroeId, Objetivo = o } };
            switch (c.Clase)
            {
                case ClaseDeCosa.Plaza:
                    if (c.BajoNiebla) break; // recordada: solo se marcha
                    if (aLaPuerta && lider && (propia || c.Id == j.ResidenciaId))
                        r.Add(new AccionDelMapa("Entrar", new Orden { EntrarEnAsentamiento = new OrdenEntrarEnAsentamiento { AsentamientoId = c.Id, HeroeId = j.HeroeId } }));
                    // Un asedio solo lo abre un ejército con soldados.
                    if (!propia && puedeAtacar && j.ConSoldados && j.Columna.Tipo == TipoColumna.ColumnaEjercito)
                        r.Add(new AccionDelMapa("Atacar", Atacar(new OrdenAtacar.Types.Objetivo { AsentamientoId = c.Id })));
                    if (!propia && d <= j.RadioInspeccion)
                        r.Add(new AccionDelMapa("Inspeccionar", new Orden { Inspeccionar = new OrdenInspeccionar { HeroeId = j.HeroeId, Objetivo = new OrdenInspeccionar.Types.Objetivo { AsentamientoId = c.Id } } }));
                    break;
                case ClaseDeCosa.Mercenarios:
                    if (aLaPuerta && lider) r.Add(new AccionDelMapa("Entrar", new Orden { EntrarEnCampamento = new OrdenEntrarEnCampamento { CampamentoId = c.Id, HeroeId = j.HeroeId } }));
                    break;
                case ClaseDeCosa.Bandidos:
                    if (puedeAtacar) r.Add(new AccionDelMapa("Atacar", Atacar(new OrdenAtacar.Types.Objetivo { CampamentoId = c.Id })));
                    break;
                case ClaseDeCosa.Alijo:
                    if (aLaPuerta) r.Add(new AccionDelMapa("Abrir", new Orden { AbrirAlijo = new OrdenAbrirAlijo { AlijoId = c.Id } }));
                    break;
                case ClaseDeCosa.Pueblo:
                case ClaseDeCosa.Aldea:
                    if (aLaPuerta && lider && propia) r.Add(new AccionDelMapa("Entrar", new Orden { EntrarEnPueblo = new OrdenEntrarEnPueblo { PuebloId = c.Id, HeroeId = j.HeroeId } }));
                    if (!propia && puedeAtacar && j.ConSoldados) r.Add(new AccionDelMapa("Atacar", Atacar(new OrdenAtacar.Types.Objetivo { PuebloId = c.Id })));
                    break;
                case ClaseDeCosa.Avanzada:
                    var ruinas = c.Entidad?.Avanzada?.Estado == EstadoAvanzada.AvanzadaEnRuinas;
                    if (!ruinas && aLaPuerta && lider && propia) r.Add(new AccionDelMapa("Entrar", new Orden { EntrarEnAvanzada = new OrdenEntrarEnAvanzada { AvanzadaId = c.Id, HeroeId = j.HeroeId } }));
                    if (!propia && puedeAtacar && j.ConSoldados) r.Add(new AccionDelMapa("Atacar", Atacar(new OrdenAtacar.Types.Objetivo { AvanzadaId = c.Id })));
                    break;
                case ClaseDeCosa.EjercitoAjeno:
                case ClaseDeCosa.CaravanaAjena:
                    bool esColumna = c.Clase == ClaseDeCosa.EjercitoAjeno;
                    if (d <= j.RadioInspeccion)
                        r.Add(new AccionDelMapa("Inspeccionar", new Orden { Inspeccionar = new OrdenInspeccionar { HeroeId = j.HeroeId, Objetivo = esColumna
                            ? new OrdenInspeccionar.Types.Objetivo { EjercitoId = c.Id } : new OrdenInspeccionar.Types.Objetivo { CaravanaId = c.Id } } }));
                    if (j.Herido || propia) break;
                    if (j.Persiguiendo == c.Id) r.Add(new AccionDelMapa("Dejar de perseguir", new Orden { DejarDePerseguir = new OrdenDejarDePerseguir { HeroeId = j.HeroeId } }));
                    else if (!j.Columna.HasFormacionExpiraEnMs)
                        r.Add(new AccionDelMapa("Perseguir", new Orden { Perseguir = new OrdenPerseguir { HeroeId = j.HeroeId, Objetivo = esColumna
                            ? new OrdenPerseguir.Types.Objetivo { EjercitoId = c.Id } : new OrdenPerseguir.Types.Objetivo { CaravanaId = c.Id } } }));
                    if (puedeAtacar)
                        r.Add(new AccionDelMapa(esColumna ? "Atacar" : "Interceptar", Atacar(esColumna ? new OrdenAtacar.Types.Objetivo { EjercitoId = c.Id } : new OrdenAtacar.Types.Objetivo { CaravanaId = c.Id })));
                    break;
                case ClaseDeCosa.Caravana:
                    // La Caravana de Fundación que vuelve sola a su origen: se toma desde el mapa (BronzeAgeClient no lo tiene; inventario §4).
                    var k = c.Entidad?.Caravana;
                    if (k != null && k.Tipo == TipoCaravana.CaravanaDeFundacion && k.Estado == EstadoCaravana.CaravanaRetornando && d <= j.RadioEncuentro)
                        r.Add(new AccionDelMapa("Reclamar la caravana", new Orden { ReclamarCaravana = new OrdenReclamarCaravana { HeroeId = j.HeroeId, CaravanaId = c.Id } }));
                    break;
            }
            return r;
        }
    }
}

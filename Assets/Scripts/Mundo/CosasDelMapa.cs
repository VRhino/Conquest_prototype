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
        /// <summary>El recurso de un yacimiento (para su icono).</summary>
        public string Recurso;
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
        /// <summary>Con `CosasDelMapa.ConFiguras`, las figuras de una columna, una caravana suelta o un Aeda (tema 7) y el camino de su cabeza
        /// hacia atrás (u) por el que se ponen; null en lo demás.</summary>
        public List<SitioDeFigura> Figuras;
        public List<(double x, double y)> Camino;
        /// <summary>Con figuras: a qué velocidad va su cabeza ahora (u/s; 0 parada), para el paso de las figuras.</summary>
        public double Velocidad;
        /// <summary>Una caravana enganchada a una columna: con figuras, sus carros van en la columna y ella no se pinta.</summary>
        public bool EnColumna;
        /// <summary>La columna de quien mira (su color de bando, el propio).</summary>
        public bool Propia;
    }

    /// <summary>Una raya del mapa (camino, frontera, anillo): sus puntos (u), su ancho (u), su color y si va bajo la niebla.</summary>
    public enum ClaseDeRaya { Anillo, Camino, ZonaDeFaccion, ZonaDePlaza }

    public sealed class RayaDelMapa
    {
        public ClaseDeRaya Clase;
        /// <summary>La Facción de una zona (para saber si es la tuya en el mapa 2D).</summary>
        public string FaccionId;
        public List<(double x, double y)> Puntos = new List<(double, double)>();
        public double Ancho;
        public uint Color;
        public bool BajoNiebla, Cerrada;
        /// <summary>De un camino: 0 sendero, 1 camino, 2 calzada.</summary>
        public int Escalon;
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

        /// <summary>El color de una Facción (`faccionColor`): el primario de su sigilo; si no se conoce, la paleta de reserva por N de `f-N`.</summary>
        public static uint ColorDeFaccion(Almacen a, string faccionId)
        {
            if (string.IsNullOrEmpty(faccionId)) return Ajeno;
            if (Sigilos.Primario(Sigilos.DeFaccion(a, faccionId)) is uint primario) return primario;
            var guion = faccionId.LastIndexOf('-');
            return guion >= 0 && int.TryParse(faccionId.Substring(guion + 1), out var n) ? ColoresDeFaccion[Math.Abs(n) % ColoresDeFaccion.Length] : Ajeno;
        }

        static (double x, double y) P(Punto p) => p == null ? (0, 0) : (p.X / 1000.0, p.Y / 1000.0);

        /// <summary>Todo lo que hay en el almacén para pintar, salvo tu propia columna (la pinta el mapa con su ruta). `radioZonaInicial` (u):
        /// `reglas.territorio.radio_inicial` de `/v1/datos`.</summary>
        /// <summary>El nombre de una obra de mapa por su tipo (`NOMBRE_OBRA` del prototipo).</summary>
        public static string NombreDeObra(TipoObra t) => t switch
        {
            TipoObra.ObraPueblo => "Obra de pueblo", TipoObra.ObraAldea => "Obra de aldea agrícola", TipoObra.ObraMejoraAldea => "Mejora de aldea",
            TipoObra.ObraAvanzada => "Obra de avanzada", TipoObra.ObraMejoraAvanzada => "Mejora de avanzada", TipoObra.ObraReparacion => "Reparación de avanzada", _ => "Obra",
        };

        /// <summary>«Batalla contra bandidos» o «Batalla».</summary>
        public static string NombreDeBatalla(Batalla b) =>
            b.Ticket?.ContextoEstrategico?.CampamentoBandidos != null || b.HasBloqueoCampamentoId ? "Batalla contra bandidos" : "Batalla";

        /// <summary>Su estado y quién combate: «En curso · Bat123 y Ana contra los bandidos».</summary>
        public static string DetalleDeBatalla(Almacen a, Batalla b)
        {
            var estado = b.Estado == EstadoBatalla.BatallaConvocando ? "Convocándose" : b.Estado == EstadoBatalla.BatallaAsignada ? "A punto de empezar" : "En curso";
            string Lado(BattleSide l, string sinHeroes)
            {
                if (l == null) return sinHeroes;
                var nombres = l.Participantes.Select(p => a.NombresHeroes.TryGetValue(p.HeroeId, out var n) ? n : p.Heroe?.DisplayName ?? p.HeroeId).ToList();
                return nombres.Count == 0 ? sinHeroes : nombres.Count == 1 ? nombres[0] : string.Join(", ", nombres.Take(nombres.Count - 1)) + " y " + nombres.Last();
            }
            return b.Ticket == null ? estado : $"{estado} · {Lado(b.Ticket.Atacante, "atacantes")} contra {Lado(b.Ticket.Defensor, "los bandidos")}";
        }

        /// <summary>¿Se pintan las plazas con las miniaturas («Mejorado» de `MaquetasDelMapa`, que lo pone)? Entonces su huella tiene un mínimo.</summary>
        public static bool HuellaMejorada;
        /// <summary>El radio mínimo de la huella de una plaza con las miniaturas (u): el caserío recién fundado, vez y media del prototipo.</summary>
        public const double HuellaMinima = 0.9;

        /// <summary>El radio de la huella de una plaza (u), con el de su ciudad `rc` (la décima parte del de su zona) y su nivel: la del prototipo
        /// (`MaquetasDelPrototipo.RadioDePlaza`) y, con las miniaturas, nunca menos de `HuellaMinima` (así no encoge al subir de nivel). La
        /// usan lo dibujado, su suelo, su claro, el aro y el clic.</summary>
        public static double HuellaDePlaza(double rc, double nivel)
        {
            double r = MaquetasDelPrototipo.RadioDePlaza(rc, nivel);
            return HuellaMejorada ? Math.Max(HuellaMinima, r) : r;
        }

        /// <summary>«Mejorado»: cuánto crece la huella de una plaza con muralla, por su nivel y su sitio (x, y), que dan su modelo: el radio de su
        /// muralla, con tope (lo pone `MaquetasDelMapa`; sin él, 1).</summary>
        public static Func<double, double, double, double> FactorDeMuralla;

        /// <summary>«Mejorado»: la huella (u) de un pueblo extractor por su tipo y su nivel, la del radio de su modelo (lo pone `MaquetasDelMapa`;
        /// NaN si no hay modelo: la del prototipo).</summary>
        public static Func<string, uint, double> HuellaDePueblo;

        /// <summary>«Mejorado»: la huella (u) de una avanzada por su etapa, la del mayor radio de los modelos de esa etapa en todos sus estados
        /// (lo pone `MaquetasDelMapa`; NaN si no hay modelo: la del prototipo).</summary>
        public static Func<uint, double> HuellaDeAvanzada;

        /// <summary>«Mejorado»: la huella (u) de los modelos de los campamentos y los alijos (el mayor radio de sus variantes, a la escala del
        /// mapa; lo pone `MaquetasDelMapa`; NaN si no hay modelo). La huella es la mayor entre la del prototipo y esta.</summary>
        public static Func<ClaseDeCosa, double> HuellaDeMenudo;

        /// <summary>La huella de lo menudo: la mayor entre la del prototipo y la de su modelo («Mejorado»).</summary>
        static double ConModelo(ClaseDeCosa clase, double huella)
        {
            double h = HuellaMejorada && HuellaDeMenudo != null ? HuellaDeMenudo(clase) : double.NaN;
            return double.IsNaN(h) ? huella : Math.Max(huella, h);
        }

        /// <summary>La huella (u) de una avanzada de esa etapa: «Mejorado», la de sus modelos (con su mejora en marcha, la de la etapa siguiente,
        /// que es la que se dibuja); si no, la del prototipo.</summary>
        public static double HuellaDeAvanzadaEn(uint etapa, bool mejoraEnMarcha)
        {
            uint e = Math.Max(1, Math.Min(3, etapa));
            if (mejoraEnMarcha && e < 3) e++;
            double h = HuellaMejorada && HuellaDeAvanzada != null ? HuellaDeAvanzada(e) : double.NaN;
            return double.IsNaN(h) ? 0.5 * Pueblo : h;
        }

        /// <summary>La huella de una plaza contando su muralla (u): la del aro, el clic y el claro. Solo en «Mejorado» y si la tiene.</summary>
        public static double HuellaConMuralla(double huella, double nivel, double x, double y, bool muralla) =>
            HuellaMejorada && muralla && FactorDeMuralla != null ? huella * FactorDeMuralla(nivel, x, y) : huella;

        /// <summary>Los tramos de la red de caminos tal como llegan (su id y sus puntos, u).</summary>
        public static List<(string id, IReadOnlyList<(double x, double y)> puntos)> TramosDeCamino(Almacen a) =>
            a.Todas(Entidad.CuerpoOneofCase.RedCaminos).SelectMany(e => e.RedCaminos.Tramos)
                .Select(t => (t.Id, (IReadOnlyList<(double x, double y)>)t.Puntos.Select(P).ToList())).ToList();

        public static List<CosaDelMapa> Reunir(Almacen a, string heroeId, double ahora, IEnumerable<NodoRecurso> nodos, double radioZonaInicial)
        {
            var r = new List<CosaDelMapa>();
            var heroe = heroeId == null ? null : a.Buscar(TipoEntidad.Heroe, heroeId)?.Heroe;
            var miFaccion = heroe != null && heroe.HasFaccionId ? heroe.FaccionId : null;
            var miColumna = heroe?.Ubicacion?.EjercitoId;
            MsFiguras = 0;
            var escuadrones = ConFiguras ? a.Todas(Entidad.CuerpoOneofCase.Escuadron).Select(x => x.Escuadron).ToList() : null;
            var plazas = new Dictionary<string, Asentamiento>();
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.Asentamiento)) plazas[e.Asentamiento.Id] = e.Asentamiento;
            uint ColorDeMadre(string madreId) => plazas.TryGetValue(madreId ?? "", out var m) ? ColorDeFaccion(a, m.FaccionId) : Ajeno;

            foreach (var p in plazas.Values)
            {
                var (x, y) = P(p.Posicion);
                double radioZona = radioZonaInicial;
                if (p.Zona.Count >= 3) radioZona = p.Zona.Max(z => Math.Sqrt(Math.Pow(z.X / 1000.0 - x, 2) + Math.Pow(z.Y / 1000.0 - y, 2)));
                double rc = radioZona * 0.1, nivel = p.Nivel == 0 ? 1 : p.Nivel, huella = HuellaDePlaza(rc, nivel);
                double conMuralla = HuellaConMuralla(huella, nivel, x, y, p.TieneMuralla);
                r.Add(new CosaDelMapa
                {
                    Clase = ClaseDeCosa.Plaza, Id = p.Id, Nombre = p.HasNombre ? p.Nombre : p.Id, Detalle = $"Nivel {p.Nivel}", FaccionId = p.FaccionId,
                    X = x, Y = y, Radio = conMuralla, Alto = Math.Max(0.4, huella * 0.9), RadioClic = conMuralla + Holgura, Color = ColorDeFaccion(a, p.FaccionId), Entidad = a.Buscar(TipoEntidad.Asentamiento, p.Id),
                });
            }
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.FichaConocida))
            {
                var f = e.FichaConocida;
                if (plazas.ContainsKey(f.AsentamientoId) || (f.Tipo != TipoEntidad.NoEspecificado && f.Tipo != TipoEntidad.Asentamiento)) continue;
                var (x, y) = P(f.Posicion);
                double rc = (f.HasRadioPotencial ? f.RadioPotencial / 1000.0 : radioZonaInicial) * 0.1, huella = HuellaDePlaza(rc, f.Nivel == 0 ? 1 : f.Nivel);
                r.Add(new CosaDelMapa
                {
                    Clase = ClaseDeCosa.Plaza, Id = f.AsentamientoId, Nombre = f.HasNombre ? f.Nombre : f.AsentamientoId, Detalle = $"Nivel {f.Nivel} · recordada", FaccionId = f.FaccionId,
                    X = x, Y = y, Radio = huella, Alto = Math.Max(0.4, huella * 0.9), RadioClic = huella + Holgura, Color = ColorDeFaccion(a, f.FaccionId), BajoNiebla = true, Entidad = e,
                });
            }
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.Pueblo))
            {
                var q = e.Pueblo;
                var (x, y) = P(q.Posicion);
                bool aldea = q.TipoExtractor == "aldeaAgricola";
                double huella = (aldea ? 1.4 : 0.58) * Pueblo;
                // («Mejorado»: el pueblo, la huella de su modelo; el clic, un 10 % más)
                // (la aldea, solo su núcleo: el aro de 0,5 u y el clic de 0,62 u; un clic en sus campos manda la columna allí)
                double hp = HuellaMejorada && HuellaDePueblo != null ? HuellaDePueblo(q.TipoExtractor, q.NivelAldea) : double.NaN;
                if (aldea && !double.IsNaN(hp)) hp = 0.5;
                r.Add(new CosaDelMapa
                {
                    Clase = aldea ? ClaseDeCosa.Aldea : ClaseDeCosa.Pueblo, Id = q.Id, Nombre = aldea ? "Aldea" : "Pueblo", Detalle = "De " + NombreDePlaza(plazas, q.AsentamientoMadreId),
                    FaccionId = plazas.TryGetValue(q.AsentamientoMadreId, out var m) ? m.FaccionId : null,
                    X = x, Y = y, Radio = !double.IsNaN(hp) ? hp : aldea ? 0.58 * Pueblo : huella, Alto = 0.35, RadioClic = !double.IsNaN(hp) ? (aldea ? 0.62 : hp * 1.1) : huella + Holgura, Color = ColorDeMadre(q.AsentamientoMadreId), Entidad = e,
                });
            }
            // (las avanzadas con su mejora en marcha: se dibujan, y se tocan, con la huella de la etapa siguiente)
            var mejorandose = new HashSet<string>(a.Todas(Entidad.CuerpoOneofCase.Obra).Select(k => k.Obra)
                .Where(o => o.Tipo == TipoObra.ObraMejoraAvanzada && o.HasTerminaEnMs && o.HasDestinoId).Select(o => o.DestinoId));
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.Avanzada))
            {
                var v = e.Avanzada;
                var (x, y) = P(v.Posicion);
                bool ruinas = v.Estado == EstadoAvanzada.AvanzadaEnRuinas;
                double huella = HuellaDeAvanzadaEn(v.Etapa, !ruinas && mejorandose.Contains(v.Id));
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
                    Clase = ClaseDeCosa.Obra, Id = o.Id, Nombre = NombreDeObra(o.Tipo), Detalle = "De " + NombreDePlaza(plazas, o.AsentamientoMadreId),
                    X = x, Y = y, Radio = 0.4 * Maqueta, Alto = 0.3, RadioClic = 0.4 * Maqueta + Holgura, Color = ColorDeMadre(o.AsentamientoMadreId), Entidad = e,
                });
            }
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.CampamentoMercenarios))
            {
                var (x, y) = P(e.CampamentoMercenarios.Posicion);
                double huella = ConModelo(ClaseDeCosa.Mercenarios, 0.63 * Maqueta * Campamentos);
                r.Add(new CosaDelMapa { Clase = ClaseDeCosa.Mercenarios, Id = e.CampamentoMercenarios.Id, Nombre = "Mercenarios", Detalle = "Campamento de mercenarios",
                    X = x, Y = y, Radio = huella, Alto = 0.45, RadioClic = huella + Holgura, Color = Mercenarios, Entidad = e });
            }
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.CampamentoBandidos))
            {
                var b = e.CampamentoBandidos;
                var (x, y) = P(b.Posicion);
                double huella = ConModelo(ClaseDeCosa.Bandidos, 0.48 * Maqueta * Campamentos);
                r.Add(new CosaDelMapa { Clase = ClaseDeCosa.Bandidos, Id = b.Id, Nombre = "Bandidos", Detalle = $"Nivel {b.Nivel}",
                    X = x, Y = y, Radio = huella, Alto = 0.35, RadioClic = huella + Holgura, Color = Bandidos, Entidad = e });
            }
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.Alijo))
            {
                var (x, y) = P(e.Alijo.Posicion);
                double huella = ConModelo(ClaseDeCosa.Alijo, 0.14 * Maqueta * Campamentos);
                r.Add(new CosaDelMapa { Clase = ClaseDeCosa.Alijo, Id = e.Alijo.Id, Nombre = "Alijo", Detalle = "Recursos sueltos en el suelo",
                    X = x, Y = y, Radio = huella, Alto = 0.2, RadioClic = huella + Holgura, Color = Oro, Entidad = e });
            }
            // Las batallas mientras duran (el motor guarda también las cerradas, una hora): en su punto, para todos los que la ven.
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.Batalla))
            {
                var bt = e.Batalla;
                if (bt.Estado != EstadoBatalla.BatallaConvocando && bt.Estado != EstadoBatalla.BatallaAsignada && bt.Estado != EstadoBatalla.BatallaEnCurso) continue;
                var (x, y) = P(bt.Punto);
                r.Add(new CosaDelMapa { Clase = ClaseDeCosa.Batalla, Id = bt.Id, Nombre = NombreDeBatalla(bt), Detalle = DetalleDeBatalla(a, bt),
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
                    X = p.x / 1000, Y = p.y / 1000, Radio = 0.3, Alto = 0.5, RadioClic = 0.8, Color = c.HasFaccionId() ? ColorDeFaccion(a, c.FaccionId) : 0xc0392b, Entidad = e,
                });
                if (ConFiguras) ConSusFiguras(r[r.Count - 1], a, c, escuadrones, ahora);
            }
            // Las caravanas enganchadas a una columna (las comerciales y la de Fundación) van detrás de ella, en fila y en su rumbo (paso 11): su carro
            // 3D la sigue y la caja de donde se enganchó desaparece.
            var detras = new Dictionary<string, (double x, double y)>();
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.Ejercito))
            {
                var c = e.Ejercito;
                var ids = c.CaravanasAdjuntasIds.ToList();
                if (!string.IsNullOrEmpty(c.CaravanaFundacionId) && !ids.Contains(c.CaravanaFundacionId)) ids.Insert(0, c.CaravanaFundacionId);
                if (ids.Count == 0 || c.Plan == null && c.Posicion == null || c.HasDentroDe) continue;
                var p = Instante.Posicion(c.Plan, c.Posicion, ahora);
                var antes = Instante.Posicion(c.Plan, c.Posicion, ahora - 3000);
                double dx = p.x - antes.x, dy = p.y - antes.y, n = Math.Sqrt(dx * dx + dy * dy);
                if (n > 1) Rumbos[c.Id] = (dx / n, dy / n);
                var (rx, ry) = Rumbos.TryGetValue(c.Id, out var rumbo) ? rumbo : (1.0, 0.0);
                for (int k = 0; k < ids.Count; k++)
                    detras[ids[k]] = ((p.x - rx * SeparacionCarro * (k + 1)) / 1000, (p.y - ry * SeparacionCarro * (k + 1)) / 1000);
            }
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.Caravana))
            {
                var c = e.Caravana;
                var enFila = detras.TryGetValue(c.Id, out var tras);
                if (!enFila && c.Plan == null && c.Posicion == null) continue;
                var p = enFila ? (x: tras.x * 1000, y: tras.y * 1000, enMarcha: false) : Instante.Posicion(c.Plan, c.Posicion, ahora);
                bool mia = miFaccion != null && c.HasFaccionId && c.FaccionId == miFaccion || c.HasTitularId && c.TitularId == heroeId;
                r.Add(new CosaDelMapa
                {
                    Clase = mia ? ClaseDeCosa.Caravana : ClaseDeCosa.CaravanaAjena, Id = c.Id,
                    Nombre = mia ? (c.HasNombre && c.Nombre != "" ? c.Nombre : "Caravana de tu Facción") : "Caravana ajena",
                    // La propia comercial: sus carros y lo que lleva (lo ajeno no se sabe).
                    Detalle = string.Join(" · ", new[]
                    {
                        mia && c.Tipo == TipoCaravana.CaravanaComercial ? $"🛞 {c.Carros.Count} · 📦 {Math.Floor(c.Contenido?.PorId.Values.Sum(v => v / 1000.0) ?? 0)}" : null,
                        c.Escoltada ? "Escoltada" : null,
                    }.Where(x => x != null)), FaccionId = c.HasFaccionId ? c.FaccionId : null,
                    X = p.x / 1000, Y = p.y / 1000, Radio = 0.3, Alto = 0.35, RadioClic = 0.7, Color = c.HasFaccionId ? ColorDeFaccion(a, c.FaccionId) : Ajeno, Entidad = e,
                });
                if (ConFiguras && enFila) r[r.Count - 1].EnColumna = true;
                else if (ConFiguras)
                {
                    var (tropa, carros) = ColumnasDelMapa.DeCaravana(c);
                    Figuras(r[r.Count - 1], ColumnasDelMapa.Disposicion(0, tropa, carros, c.Id), c.Plan, c.Posicion, ahora);
                }
            }
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.AedaItinerante))
            {
                var v = e.AedaItinerante;
                // El parado, un cuadro pequeño junto a su plaza (Víctor, paso 13: 2a; la lira pequeña del prototipo); el que va de camino, donde está.
                var parado = v.HasEnAsentamientoId;
                var p = parado && plazas.TryGetValue(v.EnAsentamientoId, out var en) && en.Posicion != null
                    ? (x: en.Posicion.X + 1500.0, y: en.Posicion.Y - 1500.0, enMarcha: false) : Instante.Posicion(v.Plan, v.Posicion, ahora);
                r.Add(new CosaDelMapa { Clase = ClaseDeCosa.Aeda, Id = v.Id, Nombre = "Aeda " + NombreDeAeda(v.Id),
                    Detalle = parado ? "Parado en " + NombreDePlaza(plazas, v.EnAsentamientoId) : "De camino a otra plaza",
                    X = p.x / 1000, Y = p.y / 1000, Radio = parado ? 0.15 : 0.2, Alto = 0.4, RadioClic = 0.5, Color = 0xe9dcc0, Entidad = e });
                if (ConFiguras)
                {
                    var aeda = r[r.Count - 1];
                    aeda.Figuras = ColumnasDelMapa.DeAeda();
                    aeda.Camino = parado ? new List<(double, double)> { (aeda.X, aeda.Y) } : RastroDe(v.Id).Camino(v.Plan, v.Posicion, ahora, 1);
                }
            }
            if (nodos != null)
                foreach (var n in nodos)
                {
                    var (x, y) = P(n.Posicion);
                    r.Add(new CosaDelMapa { Clase = ClaseDeCosa.Nodo, Id = n.Id, Recurso = n.Tipo, Nombre = NombreDeRecurso(n.Tipo), X = x, Y = y, Radio = 0.5 * 0.5, Alto = 0.25, Color = ColorDeRecurso(n.Tipo), BajoNiebla = true });
                }
            return r;
        }

        static bool HasFaccionId(this Ejercito c) => !string.IsNullOrEmpty(c.FaccionId);

        /// <summary>Con figuras (el estilo «Mejorado» de las maquetas, tema 7): las columnas, las caravanas sueltas y los Aedas llevan sus figuras
        /// y el camino por el que se ponen, y las caravanas enganchadas van dentro de su columna. Lo pone el mapa.</summary>
        public static bool ConFiguras;

        /// <summary>El rastro de la cabeza de cada columna, caravana o Aeda, por id (lo que ha andado cuando su plan no llega).</summary>
        // ponytail: no se vacía; son unas decenas de ids por sesión. Quitar los que no salen en `Reunir` si crece.
        static readonly Dictionary<string, RastroDeColumna> Rastros = new Dictionary<string, RastroDeColumna>();

        static RastroDeColumna RastroDe(string id)
        {
            if (!Rastros.TryGetValue(id, out var r)) Rastros[id] = r = new RastroDeColumna();
            return r;
        }

        static void Figuras(CosaDelMapa cosa, List<SitioDeFigura> figuras, Plan plan, Punto posicion, double ahora)
        {
            cosa.Figuras = figuras;
            var antes = Instante.Posicion(plan, posicion, ahora - 1000);
            var ya = Instante.Posicion(plan, posicion, ahora);
            cosa.Velocidad = Math.Sqrt((ya.x - antes.x) * (ya.x - antes.x) + (ya.y - antes.y) * (ya.y - antes.y)) / 1000;
            cosa.Camino = RastroDe(cosa.Id).Camino(plan, posicion, ahora, ColumnasDelMapa.Largo(figuras) + 0.5);
        }

        /// <summary>Las figuras de cada columna, por id, con cuándo se sacaron (ms del mundo): lo que lleva cambia poco, así que se rehace como
        /// mucho cada `RehacerFigurasMs` y no en cada fotograma.</summary>
        static readonly Dictionary<string, (double cuando, List<SitioDeFigura> figuras)> FigurasDeColumnas = new Dictionary<string, (double, List<SitioDeFigura>)>();
        const double RehacerFigurasMs = 2000;
        /// <summary>Lo que tardó lo de las figuras en el último `Reunir` (ms).</summary>
        public static double MsFiguras;
        static readonly System.Diagnostics.Stopwatch reloj = new System.Diagnostics.Stopwatch();

        static void ConSusFiguras(CosaDelMapa cosa, Almacen a, Ejercito c, List<Escuadron> escuadrones, double ahora)
        {
            reloj.Restart();
            if (!FigurasDeColumnas.TryGetValue(c.Id, out var f) || Math.Abs(ahora - f.cuando) > RehacerFigurasMs)
                FigurasDeColumnas[c.Id] = f = (ahora, ColumnasDelMapa.Disposicion(ColumnasDelMapa.Jinetes(c),
                    ColumnasDelMapa.TropaDe(c, escuadrones, id => a.Buscar(TipoEntidad.Heroe, id)?.Heroe),
                    ColumnasDelMapa.CarrosDe(c, id => a.Buscar(TipoEntidad.Caravana, id)?.Caravana), c.Id, c.Participantes.Select(p => p.HeroeId).ToList()));
            Figuras(cosa, f.figuras, c.Plan, c.Posicion, ahora);
            MsFiguras += reloj.Elapsed.TotalMilliseconds;
        }

        /// <summary>Tu columna con sus figuras (el mapa la pinta aparte, con su ruta; con figuras, en vez de su caja). Sin clic.</summary>
        public static CosaDelMapa MiColumnaConFiguras(Almacen a, Ejercito c, uint color, double ahora)
        {
            var p = Instante.Posicion(c.Plan, c.Posicion, ahora);
            var cosa = new CosaDelMapa { Clase = ClaseDeCosa.Ejercito, Id = c.Id, X = p.x / 1000, Y = p.y / 1000, Color = color, Entidad = null, Propia = true, FaccionId = c.HasFaccionId() ? c.FaccionId : null };
            ConSusFiguras(cosa, a, c, a.Todas(Entidad.CuerpoOneofCase.Escuadron).Select(x => x.Escuadron).ToList(), ahora);
            return cosa;
        }

        /// <summary>Lo que separa, en milésimas de u, cada carro enganchado del anterior (o de la columna): medio u, unos 6 m a la escala del mapa.</summary>
        const double SeparacionCarro = 500;

        /// <summary>El último rumbo (unitario) de cada columna en marcha: parada, sus carros se quedan detrás según por dónde llegó.</summary>
        static readonly Dictionary<string, (double x, double y)> Rumbos = new Dictionary<string, (double, double)>();

        /// <summary>Los nombres de los Aedas itinerantes, por su N (el motor no los nombra; `mundo/aedas.ts`): bardos y cantores de la tradición griega.</summary>
        static readonly string[] NombresDeAedas = { "Demódoco", "Femio", "Tamiris", "Lino", "Museo", "Arión", "Terpandro", "Eumolpo", "Anfión", "Olén", "Taletas", "Alcmán",
            "Melampo", "Crisótemis", "Filamón", "Estesícoro", "Panfo", "Íbico", "Corina", "Telesila", "Praxila", "Lasos", "Polimnesto", "Sácadas" };

        /// <summary>`aeda-3` → «Tamiris». Sin número, el id.</summary>
        public static string NombreDeAeda(string id)
        {
            var i = id.LastIndexOf('-');
            return i >= 0 && int.TryParse(id.Substring(i + 1), out var n) && n >= 1 ? NombresDeAedas[(n - 1) % NombresDeAedas.Length] : id;
        }

        static string NombreDePlaza(Dictionary<string, Asentamiento> plazas, string id) =>
            plazas.TryGetValue(id ?? "", out var p) && p.HasNombre ? p.Nombre : "una plaza";

        /// <summary>El nombre de un recurso (BronzeAgeClient: `RECURSO_NOMBRE` de `paletas.ts`; el motor no los manda); los demás, su id con mayúscula.</summary>
        public static string NombreDeRecurso(string tipo) => string.IsNullOrEmpty(tipo) ? "Recurso"
            : NombresDeRecurso.TryGetValue(tipo, out var n) ? n : char.ToUpper(tipo[0]) + tipo.Substring(1).Replace('_', ' ');

        static readonly Dictionary<string, string> NombresDeRecurso = new Dictionary<string, string>
        {
            ["madera"] = "Madera", ["piedra"] = "Piedra", ["trigo"] = "Trigo", ["cobre"] = "Cobre", ["estano"] = "Estaño", ["oro"] = "Oro",
            ["livestock"] = "Ganado", ["harina"] = "Harina", ["pan"] = "Pan", ["carne"] = "Carne", ["leche"] = "Leche", ["queso"] = "Queso",
            ["uva"] = "Uva", ["vino"] = "Vino", ["aceituna"] = "Aceituna", ["aceite"] = "Aceite de oliva", ["cebada"] = "Cebada",
            ["cerveza"] = "Cerveza", ["sesamo"] = "Sésamo", ["aceiteSesamo"] = "Aceite de sésamo", ["lingoteCobre"] = "Lingote de cobre",
            ["lingoteEstano"] = "Lingote de estaño", ["lingoteBronce"] = "Lingote de bronce", ["cuero"] = "Cuero", ["cueroCurtido"] = "Cuero curtido",
            ["hierro"] = "Hierro", ["lingoteHierro"] = "Lingote de hierro", ["cueroCalidad"] = "Cuero de calidad",
            ["armaMadera"] = "Arma de madera", ["armaCobre"] = "Arma de cobre", ["armaBronce"] = "Arma de bronce", ["armaBronceCalidad"] = "Arma de bronce de calidad",
            ["armaduraBasica"] = "Armadura básica", ["armaduraIntermedia"] = "Armadura intermedia", ["armaduraBronce"] = "Armadura de bronce",
            ["armaduraBronceCalidad"] = "Armadura de bronce de calidad", ["armaHierro"] = "Arma de hierro", ["armaHierroCalidad"] = "Arma de hierro de calidad",
            ["carroGuerra"] = "Carro de guerra", ["chatarraCobre"] = "Chatarra de cobre", ["chatarraBronce"] = "Chatarra de bronce", ["chatarraHierro"] = "Chatarra de hierro",
        };

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
                    var raya = new RayaDelMapa { Clase = ClaseDeRaya.Camino, Escalon = (int)t.Escalon, Ancho = t.Escalon == 0 ? 0.4 : 0.65, Color = Camino, BajoNiebla = true };
                    foreach (var p in t.Puntos) raya.Puntos.Add(P(p));
                    if (raya.Puntos.Count > 1) r.Add(raya);
                }
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.ZonaFaccion))
                foreach (var c in e.ZonaFaccion.Contornos)
                {
                    var raya = new RayaDelMapa { Clase = ClaseDeRaya.ZonaDeFaccion, FaccionId = e.ZonaFaccion.FaccionId, Ancho = 0.18, Color = ColorDeFaccion(a, e.ZonaFaccion.FaccionId), BajoNiebla = e.ZonaFaccion.FaccionId != miFaccion, Cerrada = true };
                    foreach (var p in c.Puntos) raya.Puntos.Add(P(p));
                    if (raya.Puntos.Count > 2) r.Add(raya);
                }
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.Asentamiento))
            {
                var p = e.Asentamiento;
                if (p.Zona.Count < 3) continue;
                var raya = new RayaDelMapa { Clase = ClaseDeRaya.ZonaDePlaza, FaccionId = p.FaccionId, Ancho = 0.12, Color = ColorDeFaccion(a, p.FaccionId), BajoNiebla = true, Cerrada = true };
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
            /// <summary>La columna está apartada en una batalla de Unity (`en_batalla`) o se está formando: entonces no ataca (`columna.en_batalla`,
            /// `columna.en_formacion`).</summary>
            public bool EnBatalla, EnFormacion;
            public string Persiguiendo;
            /// <summary>Vas sola en tu columna personal, sin formarla: lo único que se une en campo a un ejército o a una persecución.</summary>
            public bool Personal;
            /// <summary>La batalla de ese id, si se ve (la de una plaza, un pueblo o una avanzada asediados).</summary>
            public Func<string, Batalla> Batalla = _ => null;
            /// <summary>El instante del mundo (ms), para lo que corre con el reloj (el trigo del almacén de una avanzada).</summary>
            public double Ahora;
            /// <summary>Radios de `/v1/datos` (u): puerta, encuentro (ataque), inspección y protección de los mercenarios.</summary>
            public double RadioPuerta, RadioEncuentro, RadioInspeccion, RadioProteccion;
            public List<(double x, double y)> CampamentosDeMercenarios = new List<(double, double)>();
        }

        /// <summary>Las acciones que el jugador puede hacer ahora con `c`, cada una con su orden. Solo lo que se puede (si falta algo, no sale).</summary>
        public static List<AccionDelMapa> Acciones(CosaDelMapa c, Contexto j)
        {
            var r = new List<AccionDelMapa>();
            // En una batalla la columna no recibe órdenes (`batalla.bloqueado`): nada que hacer hasta que acabe.
            if (j.Columna == null || j.EnBatalla || c.Clase == ClaseDeCosa.Nodo) return r;
            var d = Distancia(j.X, j.Y, c.X, c.Y);
            bool propia = c.FaccionId != null && c.FaccionId == j.FaccionId;
            bool aLaPuerta = d <= j.RadioPuerta && !j.EnMarcha, lider = j.Lider;
            bool protegido = j.CampamentosDeMercenarios.Any(m => Distancia(m.x, m.y, j.X, j.Y) <= j.RadioProteccion || Distancia(m.x, m.y, c.X, c.Y) <= j.RadioProteccion);
            bool puedeAtacar = !j.Herido && !j.EnBatalla && !j.EnFormacion && d <= j.RadioEncuentro && !protegido;
            Orden Atacar(OrdenAtacar.Types.Objetivo o) => new Orden { Atacar = new OrdenAtacar { HeroeId = j.HeroeId, Objetivo = o } };
            switch (c.Clase)
            {
                case ClaseDeCosa.Plaza:
                    if (c.BajoNiebla) break; // recordada: solo se marcha
                    // Un ejército entra entero y se desarma (`guarnecer`), solo en una plaza de su Facción y por su Líder; una columna personal, por la
                    // puerta de siempre (BronzeAgeClient: `entraEjercito`).
                    var entraEjercito = j.Columna.Tipo == TipoColumna.ColumnaEjercito || j.Columna.Participantes.Count > 1;
                    if (aLaPuerta && entraEjercito && propia && j.Columna.LiderId == j.HeroeId)
                        r.Add(new AccionDelMapa("Entrar con el ejército", new Orden { Guarnecer = new OrdenGuarnecer { AsentamientoId = c.Id, HeroeId = j.HeroeId } }));
                    else if (aLaPuerta && !entraEjercito && lider && (propia || c.Id == j.ResidenciaId))
                        r.Add(new AccionDelMapa("Entrar", new Orden { EntrarEnAsentamiento = new OrdenEntrarEnAsentamiento { AsentamientoId = c.Id, HeroeId = j.HeroeId } }));
                    // Un asedio solo lo abre un ejército con soldados.
                    if (!propia && puedeAtacar && j.ConSoldados && j.Columna.Tipo == TipoColumna.ColumnaEjercito)
                        r.Add(new AccionDelMapa("Atacar", Atacar(new OrdenAtacar.Types.Objetivo { AsentamientoId = c.Id })));
                    if (!propia && d <= j.RadioInspeccion)
                        r.Add(new AccionDelMapa("Inspeccionar", new Orden { Inspeccionar = new OrdenInspeccionar { HeroeId = j.HeroeId, Objetivo = new OrdenInspeccionar.Types.Objetivo { AsentamientoId = c.Id } } }));
                    if (c.Entidad?.Asentamiento is Asentamiento asediada && asediada.HasBatallaId) r.AddRange(Unirse(j.Batalla(asediada.BatallaId), j, "Unirse a la batalla"));
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
                    if (c.Entidad?.Pueblo is Pueblo saqueado && saqueado.HasBatallaId) r.AddRange(Unirse(j.Batalla(saqueado.BatallaId), j, "Unirse a la batalla"));
                    break;
                case ClaseDeCosa.Avanzada:
                    var ruinas = c.Entidad?.Avanzada?.Estado == EstadoAvanzada.AvanzadaEnRuinas;
                    if (!ruinas && aLaPuerta && lider && propia) r.Add(new AccionDelMapa("Entrar", new Orden { EntrarEnAvanzada = new OrdenEntrarEnAvanzada { AvanzadaId = c.Id, HeroeId = j.HeroeId } }));
                    // Llenar los víveres de tu columna con el trigo de su almacén (el Líder, a la puerta; fuerte y ciudadela).
                    var trigo = c.Entidad?.Avanzada?.AlmacenPorRecurso.TryGetValue("trigo", out var t) == true ? Instante.Valor(t.Cantidad, j.Ahora) / 1000.0 : 0;
                    if (!ruinas && aLaPuerta && lider && propia && trigo >= 1) r.Add(new AccionDelMapa("Llenar víveres", new Orden { Repostar = new OrdenRepostar { HeroeId = j.HeroeId, LugarId = c.Id } }));
                    if (!propia && puedeAtacar && j.ConSoldados) r.Add(new AccionDelMapa("Atacar", Atacar(new OrdenAtacar.Types.Objetivo { AvanzadaId = c.Id })));
                    if (c.Entidad?.Avanzada is Avanzada atacada && atacada.HasBatallaId) r.AddRange(Unirse(j.Batalla(atacada.BatallaId), j, "Unirse a la batalla"));
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
                case ClaseDeCosa.Ejercito:
                case ClaseDeCosa.Formacion:
                    // Unirse en campo (`unirseEnCampo`): tu columna personal sola, junto a él, si admite a alguien y no le has pedido ya.
                    var ej = c.Entidad?.Ejercito;
                    if (ej == null || !j.Personal || ej.PoliticaDeUnion == PoliticaUnion.UnionRechazar || d > j.RadioEncuentro) break;
                    if (ej.PeticionesDeUnion.Any(x => x.HeroeId == j.HeroeId && x.ExpiraEnMs > j.Ahora)) break;
                    r.Add(new AccionDelMapa(ej.PoliticaDeUnion == PoliticaUnion.UnionPreguntar ? "Pedir unirme" : "Unirme",
                        new Orden { UnirseEnCampo = new OrdenUnirseEnCampo { EjercitoId = ej.Id, HeroeId = j.HeroeId } }));
                    break;
                case ClaseDeCosa.Batalla:
                    r.AddRange(Unirse(c.Entidad?.Batalla, j));
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

        /// <summary>Unirse a una batalla (`unirseABatalla`), como la ficha de BronzeAgeClient (`panelBatalla.ts`): con el lado que el motor te da
        /// (`lado_para_unirse`); en una persecución, que no lo da, eliges «Con quien persigue» o «Con el perseguido» si vas sola en tu columna
        /// personal. Nada si ya combates en ella o es una batalla campal.</summary>
        static IEnumerable<AccionDelMapa> Unirse(Batalla b, Contexto j, string texto = "Unirse")
        {
            if (b == null || b.HasLadoPropio || j.Herido) yield break;
            Orden O(string lado) { var o = new OrdenUnirseABatalla { HeroeId = j.HeroeId, BattleId = b.Id }; if (lado != null) o.Lado = lado; return new Orden { UnirseABatalla = o }; }
            if (b.HasLadoParaUnirse) { yield return new AccionDelMapa(texto, O(null)); yield break; }
            if (b.Ticket?.ContextoEstrategico?.CampoAbierto?.Solitarios == true && j.Personal && !j.EnBatalla)
            {
                yield return new AccionDelMapa("Con quien persigue", O("atacante"));
                yield return new AccionDelMapa("Con el perseguido", O("defensor"));
            }
        }
    }
}

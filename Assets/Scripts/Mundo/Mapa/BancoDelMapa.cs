// Un mapa de mundo en local, sin servidor, para ver lo dibujado: el mundo que hay en disco (el que bajó la última partida, en
// `persistentDataPath/mundo`), sitios de tierra firme, llana y despejada donde ponerlo (`Buscar`), lo que se pone en ellos (plazas, pueblos,
// aldeas, avanzadas, obras, campamentos, alijos, batallas; cada uno con su helper), y el mapa con un héroe propio y su columna a los que sigue
// la cámara (`Montar`). Lo usan el banco de asentamientos del editor (`MenuDeAsentamientos`, con `ElBanco`) y la previa del mapa
// (`PreviaDelMapa`, con lo suyo).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BronzeAge.Contract.V1;
using UnityEngine;
using AmbitoPb = BronzeAge.Contract.V1.Ambito;

namespace ConquestTactics.Mundo.Mapa
{
    public sealed class BancoDelMapa
    {
        public ArtefactoDeMundo Artefacto { get; private set; }
        public DatosDeJuego Datos { get; private set; }
        public Relieve Relieve { get; private set; }
        public CaucesDelMapa Cauces { get; private set; }
        public RasterDeMundo Bosque { get; private set; }
        /// <summary>Lo que se pone: la VISTA de la Facción f-1.</summary>
        public readonly Foto Foto = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoVista, Id = "f-1" }, Version = 2 };
        /// <summary>Cada cosa puesta, con su nombre y dónde (u).</summary>
        public readonly List<(string nombre, double x, double y)> Sitios = new List<(string, double, double)>();
        readonly List<(double x, double y, double r)> puestos = new List<(double, double, double)>();
        readonly string prefijo;
        int id;
        /// <summary>De dónde empieza a buscar sitio `Buscar` mientras no hay nada puesto (u); por defecto, el centro del mundo.</summary>
        public (double x, double y) Origen;

        BancoDelMapa(string prefijo) { this.prefijo = prefijo; }

        public static Punto Pu(double x, double y) => new Punto { X = (long)(x * 1000), Y = (long)(y * 1000) };
        public string NuevoId() => $"{prefijo}-{id++}";

        static string Primero(string carpeta, string patron = "*.bin") =>
            Directory.Exists(carpeta) ? Directory.GetFiles(carpeta, patron).OrderBy(f => f).FirstOrDefault() : null;

        /// <summary>El hash del mundo cargado (los 8 primeros, del nombre de su fichero) y de dónde salió.</summary>
        public string Hash { get; private set; } = "";
        public string OrigenDelMundo { get; private set; } = "";

        /// <summary>El mundo en disco: el que sale del generador del motor (`BronzeAgeFase0/generador/salida/mundo`, hermana de Conquest), que es el
        /// más nuevo (para ver un mundo antes de que llegue a la partida; 26-11) y, si no lo hay, la caché del cliente (`persistentDataPath/mundo`,
        /// el que bajó la última partida; su fecha no vale para elegir, porque se reescribe al entrar). Los datos de juego, siempre de la caché.
        /// `MUNDO_BANCO=cache` fuerza la caché. Solo lectura: la caché no se toca. El panel de la mesa y de la previa dice el hash y el origen. Si
        /// no hay ninguno, falla.</summary>
        public static BancoDelMapa DelDisco(string prefijo)
        {
            var carpeta = Path.Combine(Application.persistentDataPath, "mundo");
            string fArtefacto = Primero(Path.Combine(carpeta, "artefacto")), fRelieve = Primero(Path.Combine(carpeta, "relieve"));
            string fDatos = Primero(Path.Combine(carpeta, "datos")), origen = "la caché del cliente";
            var generador = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "BronzeAgeFase0", "generador", "salida", "mundo"));
            string gArtefacto = Primero(generador, "mundo-*.bin"), gRelieve = Primero(generador, "relieve-*.bin"), forzado = Environment.GetEnvironmentVariable("MUNDO_BANCO");
            bool delGenerador = gArtefacto != null && (forzado != "cache" || fArtefacto == null);
            if (delGenerador)
            {
                fArtefacto = gArtefacto;
                fRelieve = gRelieve;
                origen = "el generador del motor";
            }
            if (fArtefacto == null || fDatos == null)
                throw new InvalidOperationException($"no hay mundo en disco en {carpeta}: entra antes una vez al mundo con el servidor (MundoScene)");
            var b = new BancoDelMapa(prefijo) { Artefacto = ArtefactoDeMundo.Parser.ParseFrom(File.ReadAllBytes(fArtefacto)), Datos = DatosDeJuego.Leer(File.ReadAllBytes(fDatos)) };
            var nombre = Path.GetFileNameWithoutExtension(fArtefacto);
            b.Hash = (nombre.StartsWith("mundo-") ? nombre.Substring(6) : nombre).Substring(0, 8);
            b.OrigenDelMundo = origen;
            b.Relieve = new Relieve(fRelieve != null ? PaqueteRelieve.Parser.ParseFrom(File.ReadAllBytes(fRelieve)) : null);
            b.Cauces = new CaucesDelMapa(b.Artefacto, b.Relieve);
            b.Bosque = new RasterDeMundo(b.Artefacto.DensidadBosque);
            b.Origen = (b.Relieve.Columnas * b.Relieve.Paso / 2, b.Relieve.Filas * b.Relieve.Paso / 2);
            return b;
        }

        /// <summary>Lo más de bosque (0-255) que admite `Libre` bajo lo que se pone (el banco, 20: despejado; la previa, más: lo suyo abre claro).</summary>
        public int BosqueMaximo = 20;

        /// <summary>¿Tierra firme, llana y despejada en (x, y) con radio `r` (sin agua, ni río, ni bosque espeso), y lejos de lo puesto?</summary>
        public bool Libre(double x, double y, double r)
        {
            double hMin = double.MaxValue, hMax = double.MinValue, paso = Math.Max(1, r / 5);
            if (Relieve.AguaEn(x, y) || !puestos.All(q => Math.Sqrt((q.x - x) * (q.x - x) + (q.y - y) * (q.y - y)) > q.r + r + 6)) return false;
            for (double dy = -r; dy <= r; dy += paso)
                for (double dx = -r; dx <= r; dx += paso)
                {
                    if (dx * dx + dy * dy > r * r) continue;
                    if (Relieve.AguaEn(x + dx, y + dy) || Bosque.En(x + dx, y + dy) > BosqueMaximo || Cauces.ACauce(x + dx, y + dy, 3) < 2) return false;
                    double h = Relieve.AlturaEn(x + dx, y + dy);
                    hMin = Math.Min(hMin, h);
                    hMax = Math.Max(hMax, h);
                    if (hMax - hMin > 30) return false;
                }
            return true;
        }

        /// <summary>El sitio, siempre el mismo para un mundo, el que mejor cumple (con su nota): hierba (fértil, ni arena ni seco) y llano en
        /// 25 u, sin bosque; un solo río o un tramo de costa a la vista a 30-40 u; un borde de bosque a 40-55 u; y como mucho un 5 % de agua en
        /// las `radio` u de alrededor (la escena). `nota`: lo que cumple el elegido.</summary>
        public (double x, double y) SitioDeHierba(double radio, out string nota)
        {
            var b = this;
            nota = "";
            var fer = new RasterDeMundo(b.Artefacto.Fertilidad);
            double ancho = b.Relieve.Columnas * b.Relieve.Paso, largo = b.Relieve.Filas * b.Relieve.Paso;
            var rios = b.Artefacto.Rios.Select(r => r.Puntos.Select(q => (x: q.X / 1000.0, y: q.Y / 1000.0)).ToList()).ToList();
            bool Agua(double x, double y) => b.Relieve.AguaEn(x, y) || b.Cauces.ACauce(x, y, 1) < 0.3;
            double Pendiente(double x, double y) =>
                Math.Max(Math.Abs(b.Relieve.AlturaEn(x + 2, y) - b.Relieve.AlturaEn(x - 2, y)), Math.Abs(b.Relieve.AlturaEn(x, y + 2) - b.Relieve.AlturaEn(x, y - 2))) / (4 * EscalaDelMapa.MetrosPorUnidad);
            double Fertil(double x, double y) => fer.En(x, y) / 255.0;
            double Bosque(double x, double y) => b.Bosque.En(x, y) / 255.0;
            var candidatos = new List<(double nota, double x, double y, double hierba, int agua, int rios, int bosque)>();
            for (double y = 150; y < largo - 150; y += 24)
                for (double x = 150; x < ancho - 150; x += 24)
                {
                    if (Agua(x, y) || Fertil(x, y) < 0.45 || Bosque(x, y) > 0.3 || Pendiente(x, y) > 0.08) continue;
                    // hierba y llano en 25 u
                    int hierba = 0;
                    for (int k = 0; k < 16; k++)
                        foreach (var r in new[] { 12.0, 25.0 })
                        {
                            double px = x + r * Math.Cos(k * Math.PI / 8), py = y + r * Math.Sin(k * Math.PI / 8);
                            if (!Agua(px, py) && Fertil(px, py) >= 0.45 && Bosque(px, py) < 0.3 && Pendiente(px, py) < 0.08) hierba++;
                        }
                    if (hierba < 24) continue;
                    // agua a la vista a 30-40 u, de un solo río (o costa)
                    int agua = 0;
                    for (int k = 0; k < 24; k++)
                        foreach (var r in new[] { 30.0, 35.0, 40.0 })
                            if (Agua(x + r * Math.Cos(k * Math.PI / 12), y + r * Math.Sin(k * Math.PI / 12))) agua++;
                    int nRios = rios.Count(rio => rio.Any(q => (q.x - x) * (q.x - x) + (q.y - y) * (q.y - y) < 65 * 65));
                    // un borde de bosque a 40-55 u
                    int bosque = 0;
                    for (int k = 0; k < 24; k++)
                        foreach (var r in new[] { 40.0, 48.0, 55.0 })
                            if (Bosque(x + r * Math.Cos(k * Math.PI / 12), y + r * Math.Sin(k * Math.PI / 12)) > 0.5) bosque++;
                    double puntos = hierba / 32.0 * 3 + (agua >= 2 && agua <= 18 ? 2 : agua > 18 ? -1 : 0) - Math.Max(0, nRios - 1) * 1.5 + Math.Min(bosque, 8) / 8.0;
                    candidatos.Add((puntos, x, y, hierba / 32.0, agua, nRios, bosque));
                }
            // el agua en las 60 u, solo en los mejores (es lo caro)
            (double x, double y)? mejor = null;
            double mejorNota = double.MinValue;
            var (cx, cy) = b.Origen;
            foreach (var c in candidatos.OrderByDescending(c => c.nota).ThenBy(c => (c.x - cx) * (c.x - cx) + (c.y - cy) * (c.y - cy)).Take(300))
            {
                int n = 0, mojado = 0;
                for (double dy = -radio; dy <= radio; dy += 6)
                    for (double dx = -radio; dx <= radio; dx += 6)
                    {
                        if (dx * dx + dy * dy > radio * radio) continue;
                        n++;
                        if (Agua(c.x + dx, c.y + dy)) mojado++;
                    }
                double fraccion = mojado / (double)n, puntos = c.nota - Math.Max(0, fraccion - 0.05) * 40;
                if (puntos > mejorNota)
                {
                    mejorNota = puntos;
                    mejor = (c.x, c.y);
                    nota = $"hierba {c.hierba:P0} en 25 u · agua en el anillo de 30-40 u: {c.agua}/72 · ríos a menos de 65 u: {c.rios} · bosque a 40-55 u: {c.bosque}/72 · agua en {radio:0} u: {fraccion:P0}";
                }
            }
            if (mejor == null) Debug.LogWarning("[Banco del mapa] ningún sitio con hierba y llano: el centro del mundo");
            return mejor ?? b.Origen;
        }

        /// <summary>Lo que ocupa algo puesto a mano (u): `Buscar` no pone nada encima.</summary>
        public void Ocupar(double x, double y, double r) => puestos.Add((x, y, r));

        /// <summary>Si `Buscar` empieza siempre en `Origen` (todo junto alrededor) o en lo último puesto (el banco: en fila).</summary>
        public bool DesdeElOrigen;

        /// <summary>El sitio libre de radio `r` más cerca de lo último puesto (o de `Origen`), en espiral; null si no hay.</summary>
        public (double x, double y)? Buscar(double r)
        {
            var (ox, oy) = puestos.Count > 0 && !DesdeElOrigen ? (puestos[puestos.Count - 1].x, puestos[puestos.Count - 1].y) : Origen;
            double cx = Relieve.Columnas * Relieve.Paso / 2, cy = Relieve.Filas * Relieve.Paso / 2;
            for (double d = 0; d < 1200; d += d < 80 ? 2 : 6)
            {
                int n = Math.Max(1, (int)(d * 1.5));
                for (int k = 0; k < n; k++)
                {
                    double a = k * Math.PI * 2 / n, x = ox + d * Math.Cos(a), y = oy + d * Math.Sin(a);
                    if (x < 30 || y < 30 || x > cx * 2 - 30 || y > cy * 2 - 30) continue;
                    if (Libre(x, y, r)) { puestos.Add((x, y, r)); return (x, y); }
                }
            }
            return null;
        }

        /// <summary>`Buscar`, y que la semilla del sitio cumpla `vale` (corriéndolo un poco).</summary>
        public (double x, double y)? Sitio(double r, Func<double, bool> vale = null)
        {
            for (int intento = 0; intento < 6; intento++)
            {
                var s = Buscar(r);
                if (s == null) return null;
                if (vale == null) return s;
                for (int t = 0; t < 40; t++)
                    if (vale(MaquetasDelPrototipo.Semilla(s.Value.x + t * 0.071, s.Value.y))) return (s.Value.x + t * 0.071, s.Value.y);
            }
            return null;
        }

        /// <summary>La huella de una plaza para buscarle sitio (u): la del nivel y la zona, al factor de lo fijo.</summary>
        public static double HuellaDePlaza(uint nivel, double zona) =>
            Math.Max(0.9, zona * 0.1 * 0.8 * (nivel <= 1 ? 0.62 : nivel == 2 ? 0.8 : 1)) * CosasDelMapa.FactorDeFijos * CosasDelMapa.FactorPlazas;

        /// <summary>Lo que se holga alrededor de lo que se pone (u), además de su radio real: que nada se monte.</summary>
        public const double HolguraDeSitio = 0.5;

        /// <summary>El radio (u) que reserva una plaza al buscarle sitio: el real de su modelo (la mayor variante, con su muralla) más la holgura;
        /// sin modelo a la medida de la choza, la huella vieja × 1,8 + 1.</summary>
        public static double RadioParaPlaza(uint nivel, double zona, bool muralla)
        {
            double h = CosasDelMapa.HuellaMayorDePlaza?.Invoke(nivel, muralla) ?? double.NaN;
            return double.IsNaN(h) ? HuellaDePlaza(nivel, zona) * 1.8 + 1 : h + HolguraDeSitio;
        }

        /// <summary>El radio (u) que reserva una avanzada de esa etapa: el real de sus modelos (el mayor de sus estados y ciudadelas) más la
        /// holgura; sin modelo, `antes` (lo de siempre).</summary>
        public static double RadioParaAvanzada(uint etapa, double antes)
        {
            double h = CosasDelMapa.HuellaDeAvanzada?.Invoke(Math.Max(1, Math.Min(3, etapa))) ?? double.NaN;
            return double.IsNaN(h) ? antes : h + HolguraDeSitio;
        }

        // ---------- lo que se pone ----------

        public Asentamiento Plaza(string nombre, uint nivel, double zona, bool muralla, string faccion, double x, double y)
        {
            var a = new Asentamiento { Id = NuevoId(), Nombre = nombre, Nivel = nivel, FaccionId = faccion, Posicion = Pu(x, y), TieneMuralla = muralla };
            for (int i = 0; i < 24; i++) a.Zona.Add(Pu(x + zona * Math.Cos(i * Math.PI / 12), y + zona * Math.Sin(i * Math.PI / 12)));
            Foto.Entidades.Add(new Entidad { Asentamiento = a });
            Sitios.Add((nombre, x, y));
            return a;
        }

        /// <summary>Un pueblo extractor de `recurso` (piedra, cobre, estano, hierro, oro) sobre su nodo, que se añade al artefacto.</summary>
        public Pueblo PuebloExtractor(string recurso, uint nivel, string madre, double x, double y)
        {
            string nodo = $"{prefijo}-nodo-{id}";
            Artefacto.Nodos.Add(new NodoRecurso { Id = nodo, Tipo = recurso, Rareza = "comun", Posicion = Pu(x, y) });
            var q = new Pueblo { Id = NuevoId(), AsentamientoMadreId = madre, TipoExtractor = recurso == "piedra" ? "cantera" : "mina", NivelAldea = nivel,
                NodoId = nodo, Posicion = Pu(x, y) };
            Foto.Entidades.Add(new Entidad { Pueblo = q });
            Sitios.Add(($"{q.TipoExtractor} de {recurso} · nivel {nivel}", x, y));
            return q;
        }

        /// <summary>Un nodo suelto de `recurso` (sin pueblo).</summary>
        public void Nodo(string recurso, double x, double y)
        {
            Artefacto.Nodos.Add(new NodoRecurso { Id = NuevoId(), Tipo = recurso, Rareza = "comun", Posicion = Pu(x, y) });
            Sitios.Add(($"nodo de {recurso}", x, y));
        }

        public Pueblo Aldea(string cultivo, uint nivel, string madre, double x, double y)
        {
            var q = new Pueblo { Id = NuevoId(), AsentamientoMadreId = madre, TipoExtractor = "aldeaAgricola", NivelAldea = nivel, Cultivo = cultivo, Posicion = Pu(x, y) };
            // (la ganadera, con su rebaño al pie de cría: 20 cabezas por corral; el motor solo tiene vacuno)
            if (cultivo == "ganado") { q.RebanoEspecie = "vacuno"; q.RebanoCabezas = 20_000 * nivel; }
            Foto.Entidades.Add(new Entidad { Pueblo = q });
            Sitios.Add(($"aldea de {cultivo} · nivel {nivel}", x, y));
            return q;
        }

        public void Avanzada(string nombre, uint etapa, bool ruinas, TipoObra obra, bool enMarcha, string madre, double x, double y)
        {
            string av = NuevoId();
            Foto.Entidades.Add(new Entidad { Avanzada = new Avanzada { Id = av, AsentamientoMadreId = madre, Etapa = etapa, Posicion = Pu(x, y),
                Estado = ruinas ? EstadoAvanzada.AvanzadaEnRuinas : EstadoAvanzada.AvanzadaActiva } });
            if (obra != TipoObra.NoEspecificado)
            {
                var o = new ObraDeMapa { Id = NuevoId(), AsentamientoMadreId = madre, Tipo = obra, Posicion = Pu(x, y), DestinoId = av };
                if (enMarcha) { o.IniciaEnMs = 1; o.TerminaEnMs = 9_999_999_999_999; }
                Foto.Entidades.Add(new Entidad { Obra = o });
            }
            Sitios.Add((nombre, x, y));
        }

        public void Obra(string nombre, TipoObra tipo, bool enMarcha, string madre, double x, double y)
        {
            var o = new ObraDeMapa { Id = NuevoId(), AsentamientoMadreId = madre, Tipo = tipo, Posicion = Pu(x, y) };
            if (enMarcha) { o.IniciaEnMs = 1; o.TerminaEnMs = 9_999_999_999_999; }
            Foto.Entidades.Add(new Entidad { Obra = o });
            Sitios.Add((nombre, x, y));
        }

        public void Mercenarios(uint origen, string militar, double x, double y)
        {
            var k = new CampamentoMercenarios { Id = NuevoId(), Posicion = Pu(x, y), Origen = origen };
            k.Edificios.AddRange(new[] { "taberna", "vivienda", "mercado", militar });
            Foto.Entidades.Add(new Entidad { CampamentoMercenarios = k });
            Sitios.Add(($"mercenarios (origen {origen}, {militar})", x, y));
        }

        public void Bandidos(uint nivel, bool veta, double x, double y)
        {
            var b = new CampamentoBandidos { Id = NuevoId(), Posicion = Pu(x, y), Nivel = nivel, BosqueId = veta ? "" : "bosque-banco" };
            if (veta) b.VetaId = "veta-banco";
            Foto.Entidades.Add(new Entidad { CampamentoBandidos = b });
            Sitios.Add(($"bandidos {(veta ? "de peña" : "del claro")} · nivel {nivel}", x, y));
        }

        public void Alijo(string nombre, string recurso, double x, double y)
        {
            Foto.Entidades.Add(new Entidad { Alijo = new Alijo { Id = NuevoId(), Posicion = Pu(x, y), Recurso = recurso, Cantidad = 1000 } });
            Sitios.Add((nombre, x, y));
        }

        /// <summary>Una batalla en curso contra bandidos: la tuya («heroe-banco», de f-1, ataca) o la de otro (de f-2).</summary>
        public void Batalla(double x, double y, bool propia)
        {
            Foto.Entidades.Add(UnaBatalla(NuevoId(), x, y, propia));
            Sitios.Add((propia ? "batalla propia" : "batalla ajena", x, y));
        }

        /// <summary>Las Facciones de prueba de la mesa y la previa, con nombre y blasón del catálogo del juego: f-1, «Los Atridas» (la del héroe
        /// del banco), y f-2, «Los de Tebas».</summary>
        public static IEnumerable<Entidad> FaccionesDePrueba() => new[]
        {
            new Entidad { Faccion = new Faccion { Id = "f-1", Nombre = "Los Atridas", Sigilo = new Sigilo { FormaId = "aspis", CampoId = "liso", EmblemaId = "leon",
                ColorPrimarioId = "azul", ColorSecundarioId = "oro", ColorEmblemaId = "oro", OrlaId = "greca", ColorOrlaId = "oro" } } },
            new Entidad { Faccion = new Faccion { Id = "f-2", Nombre = "Los de Tebas", Sigilo = new Sigilo { FormaId = "clasico", CampoId = "partido", EmblemaId = "toro",
                ColorPrimarioId = "rojo", ColorSecundarioId = "negro", ColorEmblemaId = "blanco", OrlaId = "lisa", ColorOrlaId = "blanco" } } },
        };

        public static Entidad UnaBatalla(string id, double x, double y, bool propia)
        {
            var ticket = new BattleTicket { Atacante = new BattleSide(), Defensor = new BattleSide() };
            ticket.Atacante.Participantes.Add(new BattleParticipantSnapshot { HeroeId = propia ? "heroe-banco" : "heroe-otro", FaccionId = propia ? "f-1" : "f-2" });
            return new Entidad { Batalla = new Batalla { Id = id, Punto = Pu(x, y), Estado = EstadoBatalla.BatallaEnCurso, Ticket = ticket } };
        }

        /// <summary>El banco de asentamientos: plazas de nivel 1 a 5 (con y sin muralla), los pueblos extractores de cada recurso y nivel, las
        /// aldeas (cada cultivo, con sus campos), y la parte 5 y lo menudo: las avanzadas por etapa y estado (las dos ciudadelas, en obras con su
        /// mejora en marcha, la mejora pendiente y la reparación de una ruina), las dos obras genéricas, los tres campamentos de mercenarios (cada
        /// tela con su edificio militar), los seis de bandidos, los ocho alijos y una batalla.</summary>
        public void ElBanco()
        {
            foreach (var (nivel, zona, muralla, faccion) in new[] { (1u, 12.0, false, "f-1"), (1u, 20.0, false, "f-2"), (2u, 25.0, false, "f-1"), (2u, 25.0, true, "f-2"),
                                                                     (3u, 35.0, false, "f-1"), (3u, 35.0, true, "f-2"), (4u, 45.0, false, "f-2"), (4u, 45.0, true, "f-1"),
                                                                     (5u, 55.0, false, "f-1"), (5u, 55.0, true, "f-2") })
            {
                // (la huella con su suelo, × 1,78, y algo de margen)
                var sitio = Buscar(HuellaDePlaza(nivel, zona) * 1.9 + 1);
                if (sitio == null) { Debug.LogWarning($"[Banco de asentamientos] sin sitio para el nivel {nivel} (zona {zona} u)"); continue; }
                Plaza($"Nivel {nivel} · zona {zona:0} u{(muralla ? " · muralla" : "")}", nivel, zona, muralla, faccion, sitio.Value.x, sitio.Value.y);
            }
            if (Sitios.Count == 0) throw new InvalidOperationException("no se encontró ningún sitio despejado");
            string madre = Foto.Entidades[0].Asentamiento.Id;
            double f = CosasDelMapa.FactorDeFijos;
            foreach (var tipo in new[] { "piedra", "cobre", "estano", "hierro", "oro" })
                for (uint nivel = 1; nivel <= 3; nivel++)
                {
                    var sitio = Buscar(3.5 * f);
                    if (sitio == null) { Debug.LogWarning($"[Banco de asentamientos] sin sitio para el pueblo de {tipo} {nivel}"); continue; }
                    PuebloExtractor(tipo, nivel, madre, sitio.Value.x, sitio.Value.y);
                }
            foreach (var (cultivo, nivel) in new[] { ("trigo", 1u), ("trigo", 2u), ("trigo", 3u), ("cebada", 2u), ("vid", 2u), ("oliva", 2u), ("sesamo", 2u), ("ganado", 2u) })
            {
                var sitio = Buscar(4.5 * f);
                if (sitio == null) { Debug.LogWarning($"[Banco de asentamientos] sin sitio para la aldea de {cultivo} {nivel}"); continue; }
                Aldea(cultivo, nivel, madre, sitio.Value.x, sitio.Value.y);
            }
            void UnaAvanzada(string nombre, uint etapa, bool ruinas, TipoObra obra, bool enMarcha, int ciudadela = 0)
            {
                var s = Sitio(3 * f, ciudadela == 0 ? null : (Func<double, bool>)(sem => MaquetasDelMapa.VarianteDeCiudadela(sem) == ciudadela));
                if (s == null) { Debug.LogWarning($"[Banco de asentamientos] sin sitio para {nombre}"); return; }
                Avanzada(nombre, etapa, ruinas, obra, enMarcha, madre, s.Value.x, s.Value.y);
            }
            UnaAvanzada("atalaya", 1, false, TipoObra.NoEspecificado, false);
            UnaAvanzada("fuerte", 2, false, TipoObra.NoEspecificado, false);
            UnaAvanzada("ciudadela 1", 3, false, TipoObra.NoEspecificado, false, 1);
            UnaAvanzada("ciudadela 2", 3, false, TipoObra.NoEspecificado, false, 2);
            UnaAvanzada("fuerte en obras (mejora de la atalaya)", 1, false, TipoObra.ObraMejoraAvanzada, true);
            UnaAvanzada("ciudadela 1 en obras", 2, false, TipoObra.ObraMejoraAvanzada, true, 1);
            UnaAvanzada("ciudadela 2 en obras", 2, false, TipoObra.ObraMejoraAvanzada, true, 2);
            UnaAvanzada("fuerte en ruina", 2, true, TipoObra.NoEspecificado, false);
            UnaAvanzada("ciudadela 1 en ruina", 3, true, TipoObra.NoEspecificado, false, 1);
            UnaAvanzada("ciudadela 2 en ruina", 3, true, TipoObra.NoEspecificado, false, 2);
            UnaAvanzada("fuerte con la mejora pendiente", 2, false, TipoObra.ObraMejoraAvanzada, false);
            UnaAvanzada("reparación del fuerte en ruina", 2, true, TipoObra.ObraReparacion, true);
            foreach (var (nombre, tipo, enMarcha) in new[] { ("obra pendiente", TipoObra.ObraAvanzada, false), ("obra arrancada", TipoObra.ObraPueblo, true) })
                if (Sitio(2.5 * f) is (double x, double y)) Obra(nombre, tipo, enMarcha, madre, x, y);
            foreach (var (origen, militar) in new[] { (0u, "barracon"), (1u, "galeriaDeTiro"), (2u, "caballerizas") })
                if (Sitio(4 * f) is (double x, double y)) Mercenarios(origen, militar, x, y);
            foreach (var veta in new[] { false, true })
                for (uint nv = 1; nv <= 3; nv++)
                    if (Sitio(3 * f) is (double x, double y)) Bandidos(nv, veta, x, y);
            foreach (var m in new[] { "alijo_1", "alijo_2" })
                foreach (var rec in new[] { "trigo", "madera", "piedra", "oro" })
                    if (Sitio(1.5 * f, sem => MaquetasDelMapa.ModeloDeAlijo(sem) == m) is (double x, double y)) Alijo($"{m.Replace('_', ' ')} de {rec}", rec, x, y);
            foreach (var propia in new[] { true, false })
                if (Sitio(3 * f) is (double bx, double by)) Batalla(bx, by, propia);
        }

        /// <summary>El mapa: un héroe propio («heroe-banco», de f-1) con su columna («ej-banco», parada en `donde`; la cámara la sigue), lo
        /// puesto y `mas` (otra foto de la VISTA, o null), en una sesión sin servidor con el reloj en marcha.</summary>
        public (Almacen almacen, Sesion sesion, MapaDeMundo mapa) Montar(string nombre, (double x, double y) donde, Foto mas = null)
        {
            var almacen = new Almacen();
            var heroe = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoHeroe, Id = "heroe-banco" }, Version = 1 };
            heroe.Entidades.Add(new Entidad { Heroe = new Heroe { Id = "heroe-banco", DisplayName = "Banco", FaccionId = "f-1", Ubicacion = new UbicacionHeroe { EjercitoId = "ej-banco" } } });
            var columna = new Ejercito { Id = "ej-banco", FaccionId = "f-1", Tipo = TipoColumna.ColumnaEjercito, Posicion = Pu(donde.x, donde.y), LiderId = "heroe-banco" };
            columna.Participantes.Add(new Participante { HeroeId = "heroe-banco" });
            heroe.Entidades.Add(new Entidad { Ejercito = columna });
            almacen.Aplicar(new MensajeDeServidor { Foto = heroe });
            var vista = Foto.Clone();
            if (mas != null) vista.Entidades.AddRange(mas.Entidades);
            almacen.Aplicar(new MensajeDeServidor { Foto = vista });
            var catalogo = Resources.Load<TextAsset>("MundoUI/Sigilo/catalogoSigilos");
            if (catalogo != null) Sigilos.Cargar(catalogo.text, "{}", "{}");
            // (una sesión sin servidor: nunca se abre)
            var sesion = new Sesion("http://127.0.0.1:1", "", "conquest-banco", almacen);
            sesion.RelojSinServidor(0);
            var mapa = new GameObject(nombre).AddComponent<MapaDeMundo>();
            mapa.RadioVisionFijo = 30;
            mapa.Iniciar(sesion, Relieve, Artefacto, Datos, () => "heroe-banco", _ => false, (t, p) => { }, null);
            return (almacen, sesion, mapa);
        }
    }
}

// Lo que hace falta para crear el héroe (paso 3 del bloque 4; BronzeAgeClient: `ui/clasesHeroe.ts`, `mundo/analisisCampamento.ts`,
// `ui/eleccionCampamento.ts`): las clases de `/v1/datos` con su equipo inicial, y lo que rodea a cada campamento donde se puede nacer, sacado del
// artefacto de mundo (el motor no lo manda). C# puro.
using System;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;
using Newtonsoft.Json.Linq;

namespace ConquestTactics.Mundo
{
    public sealed class ClaseDeHeroe
    {
        public string Id, Nombre, ArmaduraInicial;
        public List<string> Armaduras = new List<string>();
        /// <summary>fuerza, destreza, armadura y vitalidad.</summary>
        public Dictionary<string, int> Atributos = new Dictionary<string, int>();
        /// <summary>Los nombres de lo que lleva al nacer: su arma, las cuatro piezas de su armadura inicial (sin casco) y la montura de tiro.</summary>
        public List<string> EquipoInicial = new List<string>();

        static readonly string[] Huecos = { "torso", "guantes", "pantalones", "botas" };

        /// <summary>Las clases de `Objetos_Del_Heroe.CLASES_HEROE`, con su equipo inicial de `OBJETOS` (el de tier 1).</summary>
        public static List<ClaseDeHeroe> Leer(DatosDeJuego datos)
        {
            var objetos = datos.Bloque("Objetos_Del_Heroe", "OBJETOS")["objetos"].OfType<JObject>().ToList();
            var r = new List<ClaseDeHeroe>();
            foreach (JObject c in datos.Bloque("Objetos_Del_Heroe", "CLASES_HEROE")["clases"])
            {
                var clase = new ClaseDeHeroe { Id = (string)c["id"], Nombre = (string)c["nombre"], ArmaduraInicial = (string)c["armaduraInicial"] };
                clase.Armaduras.AddRange(c["armaduras"].Select(x => (string)x));
                foreach (var kv in (JObject)c["atributosBase"]) clase.Atributos[kv.Key] = (int)kv.Value;
                bool Tier1(JObject o) => (int?)o["tier"] == 1;
                var arma = objetos.FirstOrDefault(o => (string)o["tipo"] == "arma" && (string)o["clase"] == clase.Id && Tier1(o));
                if (arma != null) clase.EquipoInicial.Add((string)arma["nombre"]);
                foreach (var hueco in Huecos)
                {
                    var pieza = objetos.FirstOrDefault(o => (string)o["tipo"] == "armadura" && (string)o["hueco"] == hueco && (string)o["tipoArmadura"] == clase.ArmaduraInicial && Tier1(o));
                    if (pieza != null) clase.EquipoInicial.Add((string)pieza["nombre"]);
                }
                var montura = objetos.FirstOrDefault(o => (string)o["id"] == "montura_tiro");
                if (montura != null) clase.EquipoInicial.Add((string)montura["nombre"]);
                r.Add(clase);
            }
            return r;
        }

        /// <summary>«Armadura ligera y media; empieza con la ligera».</summary>
        public string TextoDeArmaduras => $"Armadura {string.Join(" y ", Armaduras)}; empieza con la {ArmaduraInicial}";
    }

    /// <summary>Lo que rodea a un campamento donde se puede nacer (BronzeAgeClient: `analizarCampamentos`), sacado del artefacto.</summary>
    public sealed class AnalisisDeCampamento
    {
        /// <summary>Las dos distancias de BronzeAgeClient: la zona cercana y la frontera (u).</summary>
        public const double RadioCercano = 150, RadioFrontera = 250;
        /// <summary>Densidad de bosque (0-255) desde la que cuenta como bosque.</summary>
        const int Bosque = 38;

        public string Id;
        public double X, Y;
        /// <summary>Residentes más los que nacieron aquí.</summary>
        public int Gente;
        /// <summary>Por clase de suelo (agua, costa, llano, colina, montaña, cima), la fracción a menos del radio cercano.</summary>
        public Dictionary<string, double> Suelo = new Dictionary<string, double>();
        public double FertilidadMedia, FraccionDeBosque;
        /// <summary>Por tipo de nodo: cuántos en la zona cercana, cuántos hasta la frontera y el más cercano (u).</summary>
        public Dictionary<string, (int cerca, int frontera, double masCerca)> Nodos = new Dictionary<string, (int, int, double)>();
        public double? Rio, Agua;
        public bool RioNavegable;
        /// <summary>Bandidos hasta la frontera: distancia y nivel.</summary>
        public List<(double distancia, uint nivel)> Bandidos = new List<(double, uint)>();
        public string Vecino;
        public double DistanciaAlVecino;
        /// <summary>El nombre que se enseña: el de su región del generador (y hacia dónde queda si dos caen en la misma); sin región, «Campamento N».</summary>
        public string Lugar;
        /// <summary>0..1 comparada con los demás campamentos, y las razones a favor que más pesan (ayuda del cliente: el motor no valora nada).</summary>
        public double Nota;
        public List<string> Razones = new List<string>();

        static readonly string[] NombresDeSuelo = { "", "agua", "costa", "llano", "colina", "montaña", "cima" };

        public static List<AnalisisDeCampamento> Analizar(IEnumerable<CampamentoParaElegir> campamentos, ArtefactoDeMundo a, Relieve relieve)
        {
            var terreno = new RasterDeMundo(a.Terreno);
            var fertilidad = a.Fertilidad != null && a.Fertilidad.Encoding == EncodingRaster.EncodingU8 ? new RasterDeMundo(a.Fertilidad) : null;
            var bosque = a.DensidadBosque != null && a.DensidadBosque.Encoding == EncodingRaster.EncodingU8 ? new RasterDeMundo(a.DensidadBosque) : null;
            var lista = campamentos.ToList();
            var r = new List<AnalisisDeCampamento>();
            foreach (var c in lista)
            {
                var x = new AnalisisDeCampamento { Id = c.CampamentoId, X = c.Posicion.X / 1000.0, Y = c.Posicion.Y / 1000.0, Gente = (int)(c.Residentes + c.EligieronComoInicial) };
                int muestras = 0, tierra = 0, conBosque = 0;
                double fert = 0, agua = double.MaxValue;
                var cuenta = new Dictionary<string, int>();
                for (double dy = -RadioFrontera; dy <= RadioFrontera; dy += 4)
                    for (double dx = -RadioFrontera; dx <= RadioFrontera; dx += 4)
                    {
                        double d = Math.Sqrt(dx * dx + dy * dy), px = x.X + dx, py = x.Y + dy;
                        if (d > RadioFrontera) continue;
                        if (relieve.AguaEn(px, py)) agua = Math.Min(agua, d);
                        if (d > RadioCercano) continue;
                        muestras++;
                        var clase = terreno.En(px, py);
                        var nombre = clase < NombresDeSuelo.Length ? NombresDeSuelo[clase] : "otro";
                        cuenta.TryGetValue(nombre, out var n);
                        cuenta[nombre] = n + 1;
                        if (clase == (byte)ClaseTerreno.TerrenoAgua || relieve.AguaEn(px, py)) continue;
                        tierra++;
                        if (fertilidad != null) fert += fertilidad.En(px, py) / 255.0;
                        if (bosque != null && bosque.En(px, py) >= Bosque) conBosque++;
                    }
                foreach (var kv in cuenta) x.Suelo[kv.Key] = (double)kv.Value / Math.Max(1, muestras);
                x.FertilidadMedia = tierra > 0 ? fert / tierra : 0;
                x.FraccionDeBosque = tierra > 0 ? (double)conBosque / tierra : 0;
                x.Agua = agua == double.MaxValue ? (double?)null : agua;
                foreach (var nodo in a.Nodos)
                {
                    var d = CosasDelMapa.Distancia(nodo.Posicion.X / 1000.0, nodo.Posicion.Y / 1000.0, x.X, x.Y);
                    if (d > RadioFrontera) continue;
                    x.Nodos.TryGetValue(nodo.Tipo, out var t);
                    x.Nodos[nodo.Tipo] = (t.cerca + (d <= RadioCercano ? 1 : 0), t.frontera + 1, t.frontera == 0 ? d : Math.Min(t.masCerca, d));
                }
                foreach (var rio in a.Rios)
                    for (int i = 0; i + 1 < rio.Puntos.Count; i++)
                    {
                        var d = DistanciaASegmento(x.X, x.Y, rio.Puntos[i], rio.Puntos[i + 1]);
                        if (x.Rio == null || d < x.Rio) { x.Rio = d; x.RioNavegable = rio.Navegable; }
                    }
                foreach (var b in a.BandidosIniciales)
                {
                    var d = CosasDelMapa.Distancia(b.Posicion.X / 1000.0, b.Posicion.Y / 1000.0, x.X, x.Y);
                    if (d <= RadioFrontera) x.Bandidos.Add((d, b.Nivel));
                }
                x.Bandidos.Sort((p, q) => p.distancia.CompareTo(q.distancia));
                foreach (var otro in lista)
                {
                    if (otro.CampamentoId == c.CampamentoId) continue;
                    var d = CosasDelMapa.Distancia(otro.Posicion.X / 1000.0, otro.Posicion.Y / 1000.0, x.X, x.Y);
                    if (x.Vecino == null || d < x.DistanciaAlVecino) { x.Vecino = otro.CampamentoId; x.DistanciaAlVecino = d; }
                }
                x.Lugar = Nombre(x.Id);
                r.Add(x);
            }
            if (a.Manifiesto?.Tamano != null) PonerLugares(r, a.Manifiesto.Tamano.X / 1000.0, a.Manifiesto.Tamano.Y / 1000.0);
            Valorar(r);
            return r;
        }

        /// <summary>Las áreas del relieve regional del generador (BronzeAgeClient `AREAS`): centro en fracción del mapa, radio para un mundo de 24.000
        /// de lado corto, y nombre. La semilla las mueve un poco y el artefacto no la trae: cerca de una linde el nombre puede ser el del vecino.</summary>
        static readonly (double x, double y, double radio, string nombre)[] Areas =
        {
            (0.12, 0.08, 2500, "Costa boscosa"), (0.55, 0.10, 3800, "Altiplano interior"), (0.84, 0.12, 4500, "Tierras altas secas"),
            (0.755, 0.51, 2300, "Corredor levantino"), (0.53, 0.27, 2900, "Cuenca fértil"), (0.89, 0.46, 4400, "Gran llanura fluvial"),
            (0.95, 0.28, 2100, "Piedemonte oriental"), (0.22, 0.25, 2100, "Colinas boscosas"), (0.25, 0.945, 3900, "Llanura costera meridional"),
            (0.54, 0.94, 3000, "Valle meridional"), (0.91, 0.84, 4200, "Estepa abierta"), (0.22, 0.425, 1500, "Península de los golfos"),
            (0.35, 0.105, 2100, "Tierras del estrecho"), (0.755, 0.78, 2500, "Meseta del istmo"), (0.54, 0.76, 1900, "Delta interior"),
            (0.88, 0.65, 2500, "Valles orientales"),
        };

        /// <summary>La región donde cae (x, y): la de más peso, 1 / (0,03 + d⁶) con d la distancia relativa a su radio (`regionDe`).</summary>
        public static (string nombre, double cx, double cy) Region(double x, double y, double ancho, double alto)
        {
            var escala = Math.Min(ancho, alto) / 24000;
            var mejor = Areas[0];
            double maximo = -1;
            foreach (var a in Areas)
            {
                double ex = x - a.x * ancho, ey = y - a.y * alto, d2 = (ex * ex + ey * ey) / Math.Pow(a.radio * escala, 2);
                var peso = 1 / (0.03 + d2 * d2 * d2);
                if (peso > maximo) { maximo = peso; mejor = a; }
            }
            return (mejor.nombre, mejor.x * ancho, mejor.y * alto);
        }

        static readonly string[] Rumbos = { "este", "nordeste", "norte", "noroeste", "oeste", "suroeste", "sur", "sudeste" };

        /// <summary>Un nombre por campamento (`nombresDeCampamentos`): el de su región y, si dos caen en la misma, hacia dónde queda cada uno dentro de
        /// ella (y un número romano si aún chocan).</summary>
        public static void PonerLugares(List<AnalisisDeCampamento> lista, double ancho, double alto)
        {
            var regiones = lista.Select(c => Region(c.X, c.Y, ancho, alto)).ToList();
            var nombres = regiones.Select((r, i) =>
            {
                if (!regiones.Where((o, j) => j != i).Any(o => o.nombre == r.nombre)) return r.nombre;
                var ang = Math.Atan2(r.cy - lista[i].Y, lista[i].X - r.cx); // la y crece hacia el sur
                return $"{r.nombre} ({Rumbos[(((int)Math.Round(ang / (Math.PI / 4)) % 8) + 8) % 8]})";
            }).ToList();
            var vistos = new Dictionary<string, int>();
            string[] romanos = { "I", "II", "III", "IV", "V" };
            for (int i = 0; i < lista.Count; i++)
            {
                var n = nombres[i];
                vistos.TryGetValue(n, out var k);
                vistos[n] = ++k;
                lista[i].Lugar = nombres.Count(x => x == n) > 1 ? $"{n} {(k <= 5 ? romanos[k - 1] : k.ToString())}" : n;
            }
        }

        /// <summary>Peso de cada yacimiento al valorar un sitio: los metales del bronce y el oro más que la piedra y el ganado.</summary>
        static readonly Dictionary<string, double> PesoDeNodo = new Dictionary<string, double> { ["cobre"] = 2, ["estano"] = 3, ["hierro"] = 1.5, ["oro"] = 2, ["piedra"] = 1, ["livestock"] = 1 };

        /// <summary>La nota de cada campamento para «Recomendado» y «Elegir por mí» (`valorarCampamentos`): poca gente 0,3, tierra fértil 0,25,
        /// yacimientos a 150 u 0,25, pocos bandidos a 150 u 0,1 y agua cerca 0,1; cada cosa comparada con los demás (0 el peor, 1 el mejor).</summary>
        public static void Valorar(List<AnalisisDeCampamento> lista)
        {
            double[] pesos = { 0.3, 0.25, 0.25, 0.1, 0.1 };
            string[] texto = { "Poca gente", "Tierra fértil", "Yacimientos cerca", "Pocos bandidos", "Agua cerca" };
            var f = lista.Select(a => new[]
            {
                -a.Gente,
                a.FertilidadMedia,
                PesoDeNodo.Sum(kv => kv.Value * Math.Min(3, a.Nodos.TryGetValue(kv.Key, out var n) ? n.cerca : 0) / 3.0),
                -a.Bandidos.Where(b => b.distancia <= RadioCercano).Sum(b => (double)b.nivel),
                (a.Rio != null ? 1 - a.Rio.Value / RadioFrontera : 0) + (a.Agua != null ? 0.5 * (1 - a.Agua.Value / RadioFrontera) : 0),
            }).ToList();
            for (int i = 0; i < lista.Count; i++)
            {
                var n = new double[pesos.Length];
                var con = new bool[pesos.Length];
                var nota = 0.0;
                for (int k = 0; k < pesos.Length; k++)
                {
                    double min = f.Min(x => x[k]), max = f.Max(x => x[k]);
                    con[k] = max > min;
                    n[k] = con[k] ? (f[i][k] - min) / (max - min) : 0.5;
                    nota += n[k] * pesos[k];
                }
                lista[i].Nota = nota;
                lista[i].Razones = Enumerable.Range(0, pesos.Length).Where(k => n[k] >= 0.6 && con[k])
                    .OrderByDescending(k => n[k] * pesos[k]).Take(2).Select(k => texto[k]).ToList();
            }
        }

        /// <summary>El recomendado: el de nota más alta.</summary>
        public static AnalisisDeCampamento Recomendado(List<AnalisisDeCampamento> lista) => lista.OrderByDescending(a => a.Nota).FirstOrDefault();

        /// <summary>«Elegir por mí»: al azar entre los tres mejor valorados, con más peso cuanto mejor (`r` en [0, 1)).</summary>
        public static AnalisisDeCampamento ElegirPorMi(List<AnalisisDeCampamento> lista, double r)
        {
            var mejores = lista.OrderByDescending(a => a.Nota).Take(3).ToList();
            var resto = r * mejores.Sum(m => m.Nota + 0.05);
            foreach (var m in mejores) if ((resto -= m.Nota + 0.05) <= 0) return m;
            return mejores.FirstOrDefault();
        }

        static double DistanciaASegmento(double px, double py, Punto a, Punto b)
        {
            double ax = a.X / 1000.0, ay = a.Y / 1000.0, bx = b.X / 1000.0, by = b.Y / 1000.0, vx = bx - ax, vy = by - ay;
            var l2 = vx * vx + vy * vy;
            var t = l2 > 0 ? Math.Max(0, Math.Min(1, ((px - ax) * vx + (py - ay) * vy) / l2)) : 0;
            return CosasDelMapa.Distancia(px, py, ax + t * vx, ay + t * vy);
        }

        /// <summary>El nombre que se enseña: «Campamento N».</summary>
        public static string Nombre(string id)
        {
            var guion = id.LastIndexOf('-');
            return guion >= 0 ? "Campamento " + id.Substring(guion + 1) : id;
        }
    }
}

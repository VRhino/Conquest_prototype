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
                r.Add(x);
            }
            return r;
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

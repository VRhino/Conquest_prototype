// El trazado de caminos y fronteras del mapa, portado tal cual del prototipo (BronzeAgeClient): `suavizarCamino`, `simplificar`, `chaikin`,
// `recorridos`, `tramosEnCaja`, `sentidoDe` y `circulo` (`src/mundo3d/lineas.ts`); las anclas de los caminos (`anclasDe`, `caminoSuave` en
// `mundo3d/estado.ts`); `partirFronteras` (`src/mundo/fronteras.ts`); `zonasDeLoRecordado` y `poligonoDeZona` (`src/mundo/zonasRecordadas.ts`);
// y el refuerzo del relleno de los colores oscuros (`refuerzo`, `mundo3d/territorio.ts`). C# puro: se prueba sin Unity contra lo que dan las
// funciones del prototipo (`tasks/referencias/trazado.mts`). Puntos en u del mapa.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ConquestTactics.Mundo
{
    public static class TrazadoDelMapa
    {
        // ------------------------------------------------------------ caminos ------------------------------------------------------------

        /// <summary>Ramer-Douglas-Peucker: quita los vértices que se apartan menos de `tolerancia` u de la recta entre los que se quedan.</summary>
        public static List<(double x, double y)> Simplificar(IReadOnlyList<(double x, double y)> puntos, double tolerancia)
        {
            if (puntos.Count < 3) return puntos.ToList();
            var queda = new bool[puntos.Count];
            queda[0] = queda[puntos.Count - 1] = true;
            var pila = new Stack<(int, int)>();
            pila.Push((0, puntos.Count - 1));
            while (pila.Count > 0)
            {
                var (i, j) = pila.Pop();
                var a = puntos[i];
                var b = puntos[j];
                double dx = b.x - a.x, dy = b.y - a.y, l = Hypot(dx, dy);
                int peor = -1;
                double max = tolerancia;
                for (int k = i + 1; k < j; k++)
                {
                    var q = puntos[k];
                    double d = l != 0 ? Math.Abs((q.x - a.x) * dy - (q.y - a.y) * dx) / l : Hypot(q.x - a.x, q.y - a.y);
                    if (d > max) { max = d; peor = k; }
                }
                if (peor >= 0)
                {
                    queda[peor] = true;
                    pila.Push((i, peor));
                    pila.Push((peor, j));
                }
            }
            var r = new List<(double, double)>();
            for (int k = 0; k < puntos.Count; k++) if (queda[k]) r.Add(puntos[k]);
            return r;
        }

        /// <summary>Chaikin: en cada vuelta, cada esquina se corta por un cuarto de sus dos tramos (como mucho `corteMax` u). Los extremos, quietos.</summary>
        public static List<(double x, double y)> Chaikin(IReadOnlyList<(double x, double y)> puntos, int vueltas, double corteMax)
        {
            var ps = puntos.ToList();
            for (int v = 0; v < vueltas && ps.Count >= 3; v++)
            {
                var o = new List<(double x, double y)> { ps[0] };
                for (int k = 0; k < ps.Count - 1; k++)
                {
                    var a = ps[k];
                    var b = ps[k + 1];
                    double l = Hypot(b.x - a.x, b.y - a.y), t = l != 0 ? Math.Min(0.25, corteMax / l) : 0;
                    if (k > 0) o.Add((a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t));
                    if (k < ps.Count - 2) o.Add((b.x - (b.x - a.x) * t, b.y - (b.y - a.y) * t));
                }
                o.Add(ps[ps.Count - 1]);
                ps = o;
            }
            return ps;
        }

        /// <summary>El trazado de un camino tal como se pinta: por el punto medio de cada tramo de la red, sin los vértices que no doblan (0,3 u) y con
        /// las curvas redondeadas (Chaikin, 3 vueltas, corte de 1,5 u como mucho). Los extremos no se mueven, ni los vértices de `fijos` (índices,
        /// en orden: las plazas y pueblos que el camino cruza), donde se parte y cada trozo se suaviza aparte.</summary>
        public static List<(double x, double y)> SuavizarCamino(IReadOnlyList<(double x, double y)> puntos, IReadOnlyList<int> fijos = null)
        {
            if (fijos != null && fijos.Count > 0)
            {
                var o = new List<(double x, double y)>();
                int desde = 0;
                foreach (var k in fijos.Concat(new[] { puntos.Count - 1 }))
                {
                    if (k <= desde || k > puntos.Count - 1) continue;
                    var trozo = SuavizarCamino(puntos.Skip(desde).Take(k + 1 - desde).ToList());
                    o.AddRange(o.Count > 0 ? trozo.Skip(1) : trozo);
                    desde = k;
                }
                return o;
            }
            if (puntos.Count < 3) return puntos.ToList();
            var medios = new List<(double x, double y)> { puntos[0] };
            for (int k = 1; k < puntos.Count; k++) medios.Add(((puntos[k - 1].x + puntos[k].x) / 2, (puntos[k - 1].y + puntos[k].y) / 2));
            medios.Add(puntos[puntos.Count - 1]);
            return Chaikin(Simplificar(medios, 0.3), 3, 1.5);
        }

        /// <summary>Los sitios por los que un camino pasa tal cual (plazas, pueblos, campamentos de mercenarios): su punto redondeado a la u y los de
        /// alrededor (el servidor redondea a la u los extremos de cada tramo de su red).</summary>
        public static HashSet<(long, long)> Anclas(IEnumerable<(double x, double y)> sitios)
        {
            var o = new HashSet<(long, long)>();
            foreach (var p in sitios)
            {
                long x = Redondear(p.x), y = Redondear(p.y);
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++) o.Add((x + dx, y + dy));
            }
            return o;
        }

        /// <summary>El camino suavizado con sus vértices interiores que caen en un ancla fijos (`caminoSuave`).</summary>
        public static List<(double x, double y)> CaminoSuave(IReadOnlyList<(double x, double y)> puntos, ISet<(long, long)> anclas)
        {
            var fijos = new List<int>();
            for (int k = 1; k < puntos.Count - 1; k++) if (anclas.Contains((Redondear(puntos[k].x), Redondear(puntos[k].y)))) fijos.Add(k);
            return SuavizarCamino(puntos, fijos);
        }

        /// <summary>El paso de un camino por un vado (el vado es un punto: el motor cruza por un nodo en él y lo pisa a `radio` = 0,75 u): en el
        /// camino suavizado `suave` (que lleva el punto del vado fijo, como ancla), por cada vértice de `crudo` que es un vado (a menos de 0,01 u),
        /// el trazo cruza recto a `radio` u a cada lado del punto, en la dirección del camino sin suavizar por él. Sin vados, lo deja igual.</summary>
        public static List<(double x, double y)> PorLosVados(List<(double x, double y)> suave, IReadOnlyList<(double x, double y)> crudo,
            IEnumerable<(double x, double y)> vados, double radio = 0.75)
        {
            if (vados == null) return suave;
            var r = suave;
            foreach (var v in vados)
            {
                int k = -1;
                for (int i = 0; i < crudo.Count; i++) if (Hypot(crudo[i].x - v.x, crudo[i].y - v.y) < 0.01) { k = i; break; }
                if (k < 0) continue;
                var a = crudo[Math.Max(0, k - 1)];
                var b = crudo[Math.Min(crudo.Count - 1, k + 1)];
                double dx = b.x - a.x, dy = b.y - a.y, l = Hypot(dx, dy);
                if (l < 1e-9) continue;
                dx /= l; dy /= l;
                // dónde está el vado en el suavizado: el vértice más cercano (es fijo: está justo en él)
                int j = 0;
                for (int i = 1; i < r.Count; i++) if (Hypot(r[i].x - v.x, r[i].y - v.y) < Hypot(r[j].x - v.x, r[j].y - v.y)) j = i;
                var antes = new List<(double x, double y)>();
                var despues = new List<(double x, double y)>();
                for (int i = 0; i < j; i++) if (Hypot(r[i].x - v.x, r[i].y - v.y) > radio) antes.Add(r[i]);
                for (int i = j + 1; i < r.Count; i++) if (Hypot(r[i].x - v.x, r[i].y - v.y) > radio) despues.Add(r[i]);
                var nuevo = new List<(double x, double y)>(antes);
                if (j > 0) nuevo.Add((v.x - dx * radio, v.y - dy * radio));
                nuevo.Add(v);
                if (j < r.Count - 1) nuevo.Add((v.x + dx * radio, v.y + dy * radio));
                nuevo.AddRange(despues);
                r = nuevo;
            }
            return r;
        }

        /// <summary>Lo largo de una polilínea hasta cada vértice.</summary>
        public static double[] Recorridos(IReadOnlyList<(double x, double y)> puntos)
        {
            var o = new double[puntos.Count];
            for (int k = 1; k < puntos.Count; k++) o[k] = o[k - 1] + Hypot(puntos[k].x - puntos[k - 1].x, puntos[k].y - puntos[k - 1].y);
            return o;
        }

        /// <summary>Los tramos de una polilínea que caen en la caja [x0, x1] × [y0, y1]: los segmentos con algún extremo dentro, seguidos; cada tramo llega
        /// hasta el vértice de fuera. Devuelve índices de vértices (desde, hasta), para que se sepa lo andado hasta el primero.</summary>
        public static List<(int desde, int hasta)> TramosEnCaja(IReadOnlyList<(double x, double y)> puntos, double x0, double y0, double x1, double y1)
        {
            bool Dentro((double x, double y) p) => p.x >= x0 && p.x <= x1 && p.y >= y0 && p.y <= y1;
            var o = new List<(int, int)>();
            int inicio = -1, fin = -1;
            for (int k = 1; k < puntos.Count; k++)
            {
                if (Dentro(puntos[k - 1]) || Dentro(puntos[k]))
                {
                    if (inicio < 0) inicio = k - 1;
                    fin = k;
                }
                else if (inicio >= 0)
                {
                    o.Add((inicio, fin));
                    inicio = -1;
                }
            }
            if (inicio >= 0) o.Add((inicio, fin));
            return o;
        }

        /// <summary>1 si el contorno va en sentido antihorario (su interior a la izquierda), -1 si no.</summary>
        public static int Sentido(IReadOnlyList<(double x, double y)> puntos)
        {
            double a = 0;
            for (int k = 0; k < puntos.Count; k++)
            {
                var p = puntos[k];
                var q = puntos[(k + 1) % puntos.Count];
                a += p.x * q.y - q.x * p.y;
            }
            return a >= 0 ? 1 : -1;
        }

        /// <summary>Los puntos de un círculo, cerrado (el último repite el primero).</summary>
        public static List<(double x, double y)> Circulo((double x, double y) c, double r, int lados = 0)
        {
            if (lados <= 0) lados = Math.Max(24, (int)Math.Ceiling(r * 6));
            var o = new List<(double, double)>();
            for (int k = 0; k <= lados; k++) o.Add((c.x + r * Math.Cos((double)k / lados * Math.PI * 2), c.y + r * Math.Sin((double)k / lados * Math.PI * 2)));
            return o;
        }

        // ------------------------------------------------------------ fronteras ------------------------------------------------------------

        /// <summary>Lo que se tienen que acercar dos bordes para contar como el mismo (u), y cada cuánto se mira un lado (u).</summary>
        const double Tolerancia = 0.35, PasoFrontera = 0.5;

        public enum TipoDeFrontera { Exterior, Interior }

        public struct TramoDeFrontera
        {
            public int Zona;
            public TipoDeFrontera Tipo;
            public List<(double x, double y)> Puntos;
        }

        /// <summary>Parte los contornos de las zonas en tramos exteriores (la frontera) e interiores (entre provincias de la misma Facción, una sola vez);
        /// lo que cae dentro de una zona hermana no sale. Una zona sin hermanas que la toquen sale entera, cerrada.</summary>
        public static List<TramoDeFrontera> PartirFronteras(IReadOnlyList<(string faccionId, IReadOnlyList<(double x, double y)> contorno)> zonas)
        {
            var contornos = zonas.Select(z => Abierto(z.contorno)).ToList();
            var cajas = contornos.Select(CajaDe).ToList();
            var o = new List<TramoDeFrontera>();
            for (int i = 0; i < contornos.Count; i++)
            {
                var c = contornos[i];
                if (c.Count < 3) continue;
                var hermanas = Enumerable.Range(0, contornos.Count)
                    .Where(j => j != i && zonas[j].faccionId == zonas[i].faccionId && contornos[j].Count >= 3 && Tocan(cajas[i], cajas[j])).ToList();
                if (hermanas.Count == 0)
                {
                    var cerrado = c.ToList();
                    cerrado.Add(c[0]);
                    o.Add(new TramoDeFrontera { Zona = i, Tipo = TipoDeFrontera.Exterior, Puntos = cerrado });
                    continue;
                }
                var tramos = new List<(int clase, List<(double x, double y)> puntos)>(); // 0 exterior, 1 interior, 2 oculto
                double area = 0;
                for (int k = 0; k < c.Count; k++) { var p = c[k]; var q = c[(k + 1) % c.Count]; area += p.x * q.y - q.x * p.y; }
                int sentido = area >= 0 ? 1 : -1;
                for (int k = 0; k < c.Count; k++)
                {
                    var a = c[k];
                    var b = c[(k + 1) % c.Count];
                    int n = Math.Max(1, (int)Math.Ceiling(Hypot(b.x - a.x, b.y - a.y) / PasoFrontera));
                    for (int s = 0; s < n; s++)
                    {
                        (double x, double y) p0 = (a.x + (b.x - a.x) * s / n, a.y + (b.y - a.y) * s / n);
                        (double x, double y) p1 = (a.x + (b.x - a.x) * (s + 1) / n, a.y + (b.y - a.y) * (s + 1) / n);
                        (double x, double y) m = ((p0.x + p1.x) / 2, (p0.y + p1.y) / 2);
                        int clase = 0;
                        foreach (var j in hermanas)
                        {
                            var c2 = contornos[j];
                            if (DistanciaAlBorde(c2, p0) <= Tolerancia && DistanciaAlBorde(c2, m) <= Tolerancia && DistanciaAlBorde(c2, p1) <= Tolerancia)
                            {
                                double l = Hypot(p1.x - p0.x, p1.y - p0.y);
                                if (l == 0) l = 1;
                                double f = sentido * 2 * Tolerancia / l;
                                (double x, double y) haciaDentro = (m.x - (p1.y - p0.y) * f, m.y + (p1.x - p0.x) * f);
                                clase = i > j ? 2 : Dentro(c2, haciaDentro) ? 0 : 1;
                                break;
                            }
                            if (Dentro(c2, m)) { clase = 2; break; }
                        }
                        if (tramos.Count > 0 && tramos[tramos.Count - 1].clase == clase) tramos[tramos.Count - 1].puntos.Add(p1);
                        else tramos.Add((clase, new List<(double x, double y)> { p0, p1 }));
                    }
                }
                if (tramos.Count > 1 && tramos[0].clase == tramos[tramos.Count - 1].clase)
                {
                    var ultimo = tramos[tramos.Count - 1];
                    tramos.RemoveAt(tramos.Count - 1);
                    var unidos = ultimo.puntos.ToList();
                    unidos.AddRange(tramos[0].puntos.Skip(1));
                    tramos[0] = (tramos[0].clase, unidos);
                }
                foreach (var t in tramos)
                    if (t.clase != 2 && t.puntos.Count >= 2) o.Add(new TramoDeFrontera { Zona = i, Tipo = t.clase == 0 ? TipoDeFrontera.Exterior : TipoDeFrontera.Interior, Puntos = t.puntos });
            }
            return o;
        }

        static List<(double x, double y)> Abierto(IReadOnlyList<(double x, double y)> c) =>
            c.Count > 1 && c[0].x == c[c.Count - 1].x && c[0].y == c[c.Count - 1].y ? c.Take(c.Count - 1).ToList() : c.ToList();

        static (double x0, double y0, double x1, double y1) CajaDe(IReadOnlyList<(double x, double y)> c)
        {
            double x0 = double.PositiveInfinity, y0 = double.PositiveInfinity, x1 = double.NegativeInfinity, y1 = double.NegativeInfinity;
            foreach (var p in c) { x0 = Math.Min(x0, p.x); y0 = Math.Min(y0, p.y); x1 = Math.Max(x1, p.x); y1 = Math.Max(y1, p.y); }
            return (x0, y0, x1, y1);
        }

        static bool Tocan((double x0, double y0, double x1, double y1) a, (double x0, double y0, double x1, double y1) b) =>
            a.x0 <= b.x1 + Tolerancia && b.x0 <= a.x1 + Tolerancia && a.y0 <= b.y1 + Tolerancia && b.y0 <= a.y1 + Tolerancia;

        /// <summary>¿Está `p` dentro del contorno? (par-impar)</summary>
        public static bool Dentro(IReadOnlyList<(double x, double y)> c, (double x, double y) p)
        {
            bool si = false;
            for (int i = 0, j = c.Count - 1; i < c.Count; j = i++)
            {
                var a = c[i];
                var b = c[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) si = !si;
            }
            return si;
        }

        static double DistanciaAlBorde(IReadOnlyList<(double x, double y)> c, (double x, double y) p)
        {
            double d = double.PositiveInfinity;
            for (int i = 0, j = c.Count - 1; i < c.Count; j = i++)
            {
                var a = c[j];
                var b = c[i];
                double dx = b.x - a.x, dy = b.y - a.y, l = dx * dx + dy * dy;
                double t = l != 0 ? Math.Max(0, Math.Min(1, ((p.x - a.x) * dx + (p.y - a.y) * dy) / l)) : 0;
                d = Math.Min(d, Hypot(p.x - a.x - t * dx, p.y - a.y - t * dy));
            }
            return d;
        }

        // ------------------------------------------------------------ zonas recordadas ------------------------------------------------------------

        public struct Disco
        {
            public string Id;
            public (double x, double y) Centro;
            public double Radio;
            public string FaccionId;
        }

        /// <summary>El radio de una silueta vista: lo más lejos que llega del centro.</summary>
        public static double RadioDeSilueta((double x, double y) centro, IReadOnlyList<(double x, double y)> zona)
        {
            double r = 0;
            foreach (var q in zona) r = Math.Max(r, Hypot(q.x - centro.x, q.y - centro.y));
            return r;
        }

        /// <summary>El polígono (48 lados) de la zona de `yo` recortado por los discos de otras Facciones que lo tocan (Sutherland-Hodgman, como el motor):
        /// cada uno por una recta perpendicular a la que une los centros, a d · r_yo / (r_yo + r_otro) del propio.</summary>
        public static List<(double x, double y)> PoligonoDeZona(Disco yo, IEnumerable<Disco> otros, int lados = 48)
        {
            double cx = yo.Centro.x, cy = yo.Centro.y;
            var pol = new List<(double x, double y)>();
            for (int i = 0; i < lados; i++)
            {
                double a = Math.PI * 2 * i / lados;
                pol.Add((cx + yo.Radio * Math.Cos(a), cy + yo.Radio * Math.Sin(a)));
            }
            foreach (var o in otros)
            {
                double vx = o.Centro.x - cx, vy = o.Centro.y - cy, d = Hypot(vx, vy);
                if (o.FaccionId == yo.FaccionId || d == 0 || d > yo.Radio + o.Radio) continue;
                double nx = vx / d, ny = vy / d, k = d * yo.Radio / (yo.Radio + o.Radio);
                double Fuera((double x, double y) p) => nx * (p.x - cx) + ny * (p.y - cy) - k;
                var nuevo = new List<(double x, double y)>();
                for (int i = 0; i < pol.Count; i++)
                {
                    var a = pol[i];
                    var b = pol[(i + 1) % pol.Count];
                    double fa = Fuera(a), fb = Fuera(b);
                    if (fa <= 0) nuevo.Add(a);
                    if ((fa <= 0) != (fb <= 0)) { double t = fa / (fa - fb); nuevo.Add((a.x + t * (b.x - a.x), a.y + t * (b.y - a.y))); }
                }
                pol = nuevo;
                if (pol.Count < 3) return new List<(double x, double y)>();
            }
            return pol;
        }

        /// <summary>La zona de cada plaza recordada con radio, recortada contra las demás que se conocen (las vistas, con el radio de su silueta; las
        /// propias, con el de su zona real; y las recordadas, con el suyo), por id. Una plaza sin Facción cuenta como de «sin-&lt;id&gt;».</summary>
        public static Dictionary<string, List<(double x, double y)>> ZonasDeLoRecordado(
            IEnumerable<(string id, (double x, double y) posicion, string faccionId, IReadOnlyList<(double x, double y)> zona)> avistados,
            IEnumerable<(string id, (double x, double y) posicion, string faccionId, double? radioPotencial)> conocidos,
            IEnumerable<(string id, (double x, double y) posicion, string faccionId, IReadOnlyList<(double x, double y)> zonaReal)> propias = null)
        {
            var discos = new List<Disco>();
            foreach (var a in avistados ?? Enumerable.Empty<(string, (double, double), string, IReadOnlyList<(double x, double y)>)>())
                if ((a.zona?.Count ?? 0) >= 3)
                    discos.Add(new Disco { Id = a.id, Centro = a.posicion, Radio = RadioDeSilueta(a.posicion, a.zona), FaccionId = string.IsNullOrEmpty(a.faccionId) ? "sin-" + a.id : a.faccionId });
            foreach (var a in propias ?? Enumerable.Empty<(string, (double, double), string, IReadOnlyList<(double x, double y)>)>())
            {
                if ((a.zonaReal?.Count ?? 0) < 3 || discos.Any(d => d.Id == a.id)) continue;
                discos.Add(new Disco { Id = a.id, Centro = a.posicion, Radio = RadioDeSilueta(a.posicion, a.zonaReal), FaccionId = string.IsNullOrEmpty(a.faccionId) ? "sin-" + a.id : a.faccionId });
            }
            var listaConocidos = (conocidos ?? Enumerable.Empty<(string, (double, double), string, double?)>()).ToList();
            foreach (var c in listaConocidos)
                if (c.radioPotencial.HasValue)
                    discos.Add(new Disco { Id = c.id, Centro = c.posicion, Radio = c.radioPotencial.Value, FaccionId = string.IsNullOrEmpty(c.faccionId) ? "sin-" + c.id : c.faccionId });
            var o = new Dictionary<string, List<(double x, double y)>>();
            foreach (var c in listaConocidos)
            {
                int i = discos.FindIndex(d => d.Id == c.id);
                if (i < 0) continue;
                var yo = discos[i];
                var pol = PoligonoDeZona(yo, discos.Where((d, j) => j != i));
                if (pol.Count >= 3) o[c.id] = pol;
            }
            return o;
        }

        // ------------------------------------------------------------ relleno ------------------------------------------------------------

        /// <summary>Cuánto más opaco va el tinte de un color oscuro sobre el suelo: hasta ×1,6 para el negro; nada desde una luminancia de 0,35.</summary>
        public static double Refuerzo(uint rgb)
        {
            double r = ((rgb >> 16) & 0xff) / 255.0, g = ((rgb >> 8) & 0xff) / 255.0, b = (rgb & 0xff) / 255.0;
            double luz = 0.2126 * r + 0.7152 * g + 0.0722 * b;
            return 1 + 0.6 * Math.Min(1, Math.Max(0, (0.35 - luz) / 0.35));
        }

        // ------------------------------------------------------------ de JavaScript ------------------------------------------------------------

        /// <summary>`Math.round` de JavaScript (los medios, hacia arriba).</summary>
        public static long Redondear(double x) => (long)Math.Floor(x + 0.5);

        /// <summary>`Math.hypot` de JavaScript (sin desbordes; con estos tamaños, igual que la raíz).</summary>
        static double Hypot(double x, double y) => Math.Sqrt(x * x + y * y);
    }
}

// La red de caminos de los carros de acarreo, para las escenas de banco (sin servidor): la misma regla del motor, escrita aquí solo para
// enseñarla (Unity no calcula rutas en la partida: las pinta del servidor).
//  * La ruta (`crates/rutas`, `ruta_con`): sobre el grafo de navegación del artefacto; si origen y destino distan un espaciado o menos, la
//    recta; si no, cada extremo se engancha a su nodo más cercano (radio de 2 espaciados que se dobla hasta 5 veces; sin la capa de
//    transitable del mundo, un solo enganche, como `Rutas::desde_grafo`) y A* entre ellos. Cada arista cuesta su `coste_milesimas` (el del
//    generador del mundo: pendiente y vados ya van en él), por el factor de atracción de la red si ya está en ella:
//    max(atraccionMin, 1 − atraccionPorPeso × peso). Los enganches cuestan su largo en llano (largo × 1000 / espaciado).
//  * La red (`comercio::red_caminos`): cada ruta registra sus aristas (los extremos redondeados a unidad entera, sin repetir); el peso de una
//    arista es cuántas rutas la pisan; el escalón, 0 sendero, 1 camino desde `escalonCamino` rutas y 2 calzada desde `escalonCalzada`; y
//    `Tramos` funde las aristas contiguas del mismo escalón en polilíneas (`tramos_de_red`).
//  * El carro va del centro del pueblo al centro de su plaza (`ruta_de_acarreo`), y las rutas se registran en el orden en que se piden.
// En milésimas (los `Punto` del contrato), en enteros como el motor; la heurística, en coma flotante (solo ordena la búsqueda).
using System;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;

namespace ConquestTactics.Mundo
{
    public sealed class RedDeAcarreo
    {
        readonly long[] x, y;
        readonly List<(int b, long coste)>[] vecinos;
        readonly Dictionary<(long, long), List<int>> celdas = new Dictionary<(long, long), List<int>>();
        readonly double ratioMin;
        public readonly long Espaciado;
        readonly double atraccionPorPeso, atraccionMin;
        readonly int escalonCamino, escalonCalzada;
        /// <summary>Peso de cada arista (cuántas rutas la pisan) y el orden en que entraron en la red.</summary>
        public readonly Dictionary<((long, long), (long, long)), int> Pesos = new Dictionary<((long, long), (long, long)), int>();
        readonly List<((long, long), (long, long))> orden = new List<((long, long), (long, long))>();

        public RedDeAcarreo(GrafoNavegacion g, double atraccionPorPeso = 0.1, double atraccionMin = 0.5, int escalonCamino = 2, int escalonCalzada = 6)
        {
            (this.atraccionPorPeso, this.atraccionMin, this.escalonCamino, this.escalonCalzada) = (atraccionPorPeso, atraccionMin, escalonCamino, escalonCalzada);
            Espaciado = g.EspaciadoMin > 0 ? g.EspaciadoMin : 8000;
            int n = 0;
            foreach (var nodo in g.Nodos) n = Math.Max(n, (int)nodo.Id + 1);
            x = new long[n];
            y = new long[n];
            vecinos = new List<(int, long)>[n];
            foreach (var nodo in g.Nodos)
            {
                int i = (int)nodo.Id;
                (x[i], y[i]) = (nodo.Posicion.X, nodo.Posicion.Y);
                vecinos[i] = new List<(int, long)>();
                var k = (Floor(x[i], 2 * Espaciado), Floor(y[i], 2 * Espaciado));
                if (!celdas.TryGetValue(k, out var l)) celdas[k] = l = new List<int>();
                l.Add(i);
            }
            ratioMin = 1000.0 / Espaciado;
            foreach (var a in g.Aristas)
            {
                int i = (int)a.A, j = (int)a.B;
                if (i >= n || j >= n || vecinos[i] == null || vecinos[j] == null) continue;
                vecinos[i].Add((j, a.CosteMilesimas));
                vecinos[j].Add((i, a.CosteMilesimas));
                double l = Math.Sqrt((double)(x[i] - x[j]) * (x[i] - x[j]) + (double)(y[i] - y[j]) * (y[i] - y[j]));
                if (l > 0) ratioMin = Math.Min(ratioMin, a.CosteMilesimas / l);
            }
        }

        static long Floor(long v, long lado) => (long)Math.Floor(v / (double)lado);
        static long Entero(long v) => (long)Math.Floor((v + 500) / 1000.0);

        /// <summary>La clave de una arista: los extremos redondeados a unidad entera y ordenados (`clave_arista`).</summary>
        public static ((long, long), (long, long)) Clave(long ax, long ay, long bx, long by)
        {
            var p = (Entero(ax), Entero(ay));
            var q = (Entero(bx), Entero(by));
            return p.CompareTo(q) <= 0 ? (p, q) : (q, p);
        }

        double Factor(int peso) => peso > 0 ? Math.Max(atraccionMin, 1 - atraccionPorPeso * peso) : 1;
        double Dist(long ax, long ay, long bx, long by) => Math.Sqrt((double)(ax - bx) * (ax - bx) + (double)(ay - by) * (ay - by));
        long CosteRecto(long ax, long ay, long bx, long by) => (long)((Dist(ax, ay, bx, by) * 1000 + Espaciado / 2) / Espaciado);

        /// <summary>El nodo de enganche de (px, py): el más cercano en un radio de 2 espaciados que se dobla hasta 5 veces; -1 si no hay.</summary>
        int Enganche(long px, long py)
        {
            long radio = 2 * Espaciado;
            for (int vez = 0; vez < 5; vez++, radio *= 2)
            {
                long cx0 = Floor(px - radio, 2 * Espaciado), cx1 = Floor(px + radio, 2 * Espaciado), cy0 = Floor(py - radio, 2 * Espaciado), cy1 = Floor(py + radio, 2 * Espaciado);
                int mejor = -1;
                double dm = double.MaxValue;
                for (long cy = cy0; cy <= cy1; cy++)
                    for (long cx = cx0; cx <= cx1; cx++)
                        if (celdas.TryGetValue((cx, cy), out var l))
                            foreach (var i in l)
                            {
                                double d = Dist(px, py, x[i], y[i]);
                                if (d <= radio && (d < dm || d == dm && i < mejor)) { dm = d; mejor = i; }
                            }
                if (mejor >= 0) return mejor;
            }
            return -1;
        }

        /// <summary>La ruta del carro de (ox, oy) a (dx, dy), en milésimas, con la atracción de lo que ya hay en la red; null si no la hay.</summary>
        public List<(long x, long y)> Ruta(long ox, long oy, long dx, long dy)
        {
            if (ox == dx && oy == dy) return new List<(long, long)> { (ox, oy) };
            if (Dist(ox, oy, dx, dy) <= Espaciado) return new List<(long, long)> { (ox, oy), (dx, dy) };
            int s = Enganche(ox, oy), t = Enganche(dx, dy);
            if (s < 0 || t < 0) return null;
            double fh = Pesos.Count > 0 ? Pesos.Values.Select(Factor).Min() : 1;
            double H(int i) => Dist(x[i], y[i], dx, dy) * ratioMin * fh;
            var g = new Dictionary<int, double> { [s] = CosteRecto(ox, oy, x[s], y[s]) };
            var padre = new Dictionary<int, int>();
            var abiertos = new SortedSet<(double f, int i)> { (g[s] + H(s), s) };
            var cerrados = new HashSet<int>();
            while (abiertos.Count > 0)
            {
                var (_, u) = abiertos.Min;
                abiertos.Remove(abiertos.Min);
                if (u == t) break;
                if (!cerrados.Add(u)) continue;
                foreach (var (v, c) in vecinos[u])
                {
                    double coste = c;
                    if (Pesos.TryGetValue(Clave(x[u], y[u], x[v], y[v]), out var peso)) coste = Math.Floor(c * Factor(peso));
                    double g2 = g[u] + coste;
                    if (g.TryGetValue(v, out var viejo))
                    {
                        if (g2 >= viejo) continue;
                        abiertos.Remove((viejo + H(v), v));
                    }
                    g[v] = g2;
                    padre[v] = u;
                    abiertos.Add((g2 + H(v), v));
                }
            }
            if (!g.ContainsKey(t)) return null;
            var nodos = new List<int>();
            for (int k = t; ; k = padre[k]) { nodos.Add(k); if (k == s) break; }
            nodos.Reverse();
            var r = new List<(long x, long y)> { (ox, oy) };
            foreach (var k in nodos) if (r[r.Count - 1] != (x[k], y[k])) r.Add((x[k], y[k]));
            if (r[r.Count - 1] != (dx, dy)) r.Add((dx, dy));
            return r;
        }

        /// <summary>Registra una ruta en la red (`registrar_ruta`): sus aristas, sin repetir y sin las degeneradas, suman 1 a su peso.</summary>
        public void Registrar(IReadOnlyList<(long x, long y)> ruta)
        {
            var vistas = new HashSet<((long, long), (long, long))>();
            for (int i = 1; i < ruta.Count; i++)
            {
                var a = Clave(ruta[i - 1].x, ruta[i - 1].y, ruta[i].x, ruta[i].y);
                if (a.Item1 == a.Item2 || !vistas.Add(a)) continue;
                if (!Pesos.ContainsKey(a)) { Pesos[a] = 0; orden.Add(a); }
                Pesos[a]++;
            }
        }

        public int EscalonDePeso(int peso) => peso >= escalonCalzada ? 2 : peso >= escalonCamino ? 1 : 0;

        /// <summary>Las polilíneas de la red (`tramos_de_red`), en unidades enteras: las aristas contiguas del mismo escalón (`forzar` ≥ 0, todas
        /// de ese escalón), encadenadas solo por los nodos de paso (dos aristas de ese escalón), en el orden en que entraron.</summary>
        public List<(int escalon, List<(long x, long y)> puntos)> Tramos(int forzar = -1)
        {
            var aristas = orden.Select(a => (a, forzar >= 0 ? forzar : EscalonDePeso(Pesos[a]))).ToList();
            var porNodo = new Dictionary<((long, long), int), List<((long, long), (long, long))>>();
            foreach (var (a, e) in aristas)
                foreach (var p in new[] { a.Item1, a.Item2 })
                {
                    if (!porNodo.TryGetValue((p, e), out var l)) porNodo[(p, e)] = l = new List<((long, long), (long, long))>();
                    l.Add(a);
                }
            var usadas = new HashSet<((long, long), (long, long))>();
            void Extender((long, long) p, int e, List<(long x, long y)> puntos)
            {
                while (true)
                {
                    if (!porNodo.TryGetValue((p, e), out var vecinas) || vecinas.Count != 2) return;
                    var siguiente = vecinas.FirstOrDefault(a => !usadas.Contains(a));
                    if (siguiente == default || usadas.Contains(siguiente)) return;
                    usadas.Add(siguiente);
                    p = siguiente.Item1 == p ? siguiente.Item2 : siguiente.Item1;
                    puntos.Add(p);
                }
            }
            var tramos = new List<(int, List<(long x, long y)>)>();
            foreach (var (a, e) in aristas)
            {
                if (!usadas.Add(a)) continue;
                var haciaA = new List<(long x, long y)> { a.Item1 };
                var haciaB = new List<(long x, long y)> { a.Item2 };
                Extender(a.Item2, e, haciaB);
                Extender(a.Item1, e, haciaA);
                haciaA.Reverse();
                haciaA.AddRange(haciaB);
                tramos.Add((e, haciaA));
            }
            return tramos;
        }
    }
}

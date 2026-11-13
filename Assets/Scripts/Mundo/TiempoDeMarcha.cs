// El tiempo de marcha hasta algo del mapa (BronzeAgeClient: `src/mundo/tiempoDeMarcha.ts` y `ui/velocidadColumna.ts`): el coste por el grafo de
// navegación del artefacto (A*, cada arista cuesta su largo por su `coste_milesimas` / 1000), más el tramo recto del origen y del destino a su
// nodo más cercano, partido por la velocidad de la columna (la de su tropa más lenta, o la del jugador solo; ×marcha forzada). Sin grafo, en
// línea recta. Los caminos construidos no cuentan (como en BronzeAgeClient). C# puro.
using System;
using System.Collections.Generic;
using BronzeAge.Contract.V1;

namespace ConquestTactics.Mundo
{
    public sealed class TiempoDeMarcha
    {
        /// <summary>Lado (u) de las casillas para buscar el nodo más cercano, y cuántas casillas alrededor se miran como mucho.</summary>
        const double Casilla = 8;
        const int AnillosDeBusqueda = 4, TopeDeNodos = 300_000;

        readonly double[] x, y;
        readonly List<(int b, double coste)>[] vecinos;
        readonly Dictionary<(int, int), List<int>> casillas = new Dictionary<(int, int), List<int>>();

        public TiempoDeMarcha(GrafoNavegacion g)
        {
            int n = 0;
            foreach (var nodo in g.Nodos) n = Math.Max(n, (int)nodo.Id + 1);
            x = new double[n];
            y = new double[n];
            vecinos = new List<(int, double)>[n];
            foreach (var nodo in g.Nodos)
            {
                int i = (int)nodo.Id;
                x[i] = nodo.Posicion.X / 1000.0;
                y[i] = nodo.Posicion.Y / 1000.0;
                vecinos[i] = new List<(int, double)>();
                var k = ((int)Math.Floor(x[i] / Casilla), (int)Math.Floor(y[i] / Casilla));
                if (!casillas.TryGetValue(k, out var l)) casillas[k] = l = new List<int>();
                l.Add(i);
            }
            foreach (var a in g.Aristas)
            {
                int i = (int)a.A, j = (int)a.B;
                if (i >= n || j >= n || vecinos[i] == null || vecinos[j] == null) continue;
                var c = Math.Sqrt((x[i] - x[j]) * (x[i] - x[j]) + (y[i] - y[j]) * (y[i] - y[j])) * a.CosteMilesimas / 1000.0;
                vecinos[i].Add((j, c));
                vecinos[j].Add((i, c));
            }
        }

        int Cercano(double px, double py)
        {
            int ci = (int)Math.Floor(px / Casilla), cj = (int)Math.Floor(py / Casilla), mejor = -1;
            double dmin = double.MaxValue;
            for (int anillo = 1; anillo <= AnillosDeBusqueda && mejor < 0; anillo++)
                for (int dj = -anillo; dj <= anillo; dj++)
                    for (int di = -anillo; di <= anillo; di++)
                        if (casillas.TryGetValue((ci + di, cj + dj), out var l))
                            foreach (var i in l)
                            {
                                var d = (x[i] - px) * (x[i] - px) + (y[i] - py) * (y[i] - py);
                                if (d < dmin) { dmin = d; mejor = i; }
                            }
            return mejor;
        }

        /// <summary>El coste (u ponderadas por el terreno) de ir de (ax, ay) a (bx, by) por tierra; null si no hay camino.</summary>
        public double? Coste(double ax, double ay, double bx, double by)
        {
            int o = Cercano(ax, ay), d = Cercano(bx, by);
            if (o < 0 || d < 0) return null;
            double recto = Math.Sqrt((x[o] - ax) * (x[o] - ax) + (y[o] - ay) * (y[o] - ay)) + Math.Sqrt((x[d] - bx) * (x[d] - bx) + (y[d] - by) * (y[d] - by));
            if (o == d) return recto;
            var g = new Dictionary<int, double> { [o] = 0 };
            var abiertos = new SortedSet<(double f, int i)> { (H(o, d), o) };
            int vistos = 0;
            while (abiertos.Count > 0 && vistos++ < TopeDeNodos)
            {
                var (_, i) = abiertos.Min;
                abiertos.Remove(abiertos.Min);
                if (i == d) return g[d] + recto;
                foreach (var (j, c) in vecinos[i])
                {
                    var nuevo = g[i] + c;
                    if (g.TryGetValue(j, out var viejo))
                    {
                        if (nuevo >= viejo) continue;
                        abiertos.Remove((viejo + H(j, d), j));
                    }
                    g[j] = nuevo;
                    abiertos.Add((nuevo + H(j, d), j));
                }
            }
            return null;
        }

        double H(int i, int d) => Math.Sqrt((x[i] - x[d]) * (x[i] - x[d]) + (y[i] - y[d]) * (y[i] - y[d]));

        /// <summary>La velocidad de la columna (u por minuto de mundo): la de su tropa más lenta (`TROPAS_RECLUTABLES.velocidad`), o
        /// `MOVIMIENTO.velocidadJugador` sin soldados; con marcha forzada, × `factorVelocidadMarchaForzada`.</summary>
        public static double Velocidad(IEnumerable<string> tropasDeLaColumna, DatosDeJuego datos, bool forzada)
        {
            var velocidades = new Dictionary<string, double>();
            foreach (var t in datos.TropasReclutables) velocidades[(string)t["id"]] = (double)t["velocidad"];
            double v = double.MaxValue;
            foreach (var t in tropasDeLaColumna) if (velocidades.TryGetValue(t, out var vt)) v = Math.Min(v, vt);
            if (v == double.MaxValue) v = datos.VelocidadJugador;
            return forzada ? v * datos.Numero("documentos.Movimiento_Ejercitos.MOVIMIENTO.factorVelocidadMarchaForzada") : v;
        }

        /// <summary>Lo que se tarda en tiempo real, «~X», con el ritmo del reloj del mundo (`factor`: minutos de mundo por minuto real).</summary>
        public static string Texto(double coste, double velocidad, double factor)
        {
            var segundos = coste / Math.Max(velocidad, 1e-6) * 60 / Math.Max(factor, 1e-6);
            if (segundos < 60) return $"~{Math.Max(1, Math.Round(segundos))} s";
            if (segundos < 3600) return $"~{Math.Round(segundos / 60)} min";
            return $"~{Math.Floor(segundos / 3600)} h {Math.Round(segundos % 3600 / 60)} min";
        }
    }
}

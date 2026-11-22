// Las parcelas de una aldea agrícola «Mejorado» (tema 6, parte 4, etapa B): el port de `parcelas` de `BronzeAgeArte/mapa/pueblos/aldea.py`
// (la ronda 2 de forma que aprobó Víctor), determinista con su propio azar (no da las mismas parcelas que el Python, sí las mismas reglas).
//   1. Un mosaico por Wave Function Collapse sobre una rejilla de casillas (0,12 unidades del modelo = 0,108 u) girada por la semilla: cada
//      casilla es un trozo de hilera de este a oeste (H) o de norte a sur (V), con su principio y su final, o un hueco; los enchufes de cada lado
//      hacen que las hileras sigan y que las zonas de una misma orientación se junten. Lo que no se labra (lo que dice `vedado`: agua, pendiente,
//      caminos, nodos, otras huellas; lo construido del núcleo; lo que está más cerca de otro asentamiento) se fija antes.
//   2. Las parcelas: dentro de cada zona de una orientación, bandas de 2 a 4 hileras y tramos de 4 a 10 casillas (en la ganadera, cercados de 4
//      a 6 por 4 a 6). Fuera las de centro más allá del borde irregular y los grupos de menos de 8 casillas. Cada parcela con su estado (maduro,
//      rastrojo, verde joven o barbecho arado, este en un 10 a 15 %) y su tono.
// Todo en unidades del modelo (0,9 u), en el marco local del núcleo: x e y como la máscara del suelo (`MaquetasDelMapa.Parche`: x, −z de Unity).
using System;
using System.Collections.Generic;
using System.Linq;

namespace ConquestTactics.Mundo.Mapa
{
    public sealed class ParcelasDeAldea
    {
        /// <summary>Radio de los campos por nivel (1 a 3), en unidades del modelo: 1,15, 1,5 y 1,79 u.</summary>
        public static readonly double[] Radios = { 0, 1.15 / 0.9, 1.5 / 0.9, 1.792 / 0.9 };
        public const double Celda = 0.12;
        /// <summary>El lado de la rejilla más grande (nivel 3); el mapa de casillas de cada aldea se guarda en uno de este lado.</summary>
        public static readonly int Maximo = Lado(3);
        public enum Estado : byte { Maduro, Rastrojo, Verde, Arado }

        public int N;
        /// <summary>El origen de la rejilla (su esquina, en el marco girado) y el giro de la rejilla (radianes).</summary>
        public double O, Giro, Radio;
        /// <summary>La parcela de cada casilla [i, j] (−1 si no hay) y si su hilera va de este a oeste.</summary>
        public int[,] Parcela;
        public bool[,] Horizontal;
        public Estado[] Estados;
        /// <summary>El tono de cada parcela: el valor (0,92 a 1,08) y el matiz (−0,04 a 0,04).</summary>
        public (float valor, float matiz)[] Tonos;

        public static int Lado(int nivel) => (int)(2 * Radios[Math.Max(1, Math.Min(3, nivel))] / Celda) + 3;

        /// <summary>El centro de la casilla (i, j) en el marco local del núcleo.</summary>
        public (double x, double y) Centro(int i, int j) => AlMarco(O + (i + 0.5) * Celda, O + (j + 0.5) * Celda);
        (double x, double y) AlMarco(double x, double y) => (x * Math.Cos(Giro) - y * Math.Sin(Giro), x * Math.Sin(Giro) + y * Math.Cos(Giro));

        static readonly string[] Nombres = { "H", "Hfin", "Hini", "V", "Vfin", "Vini", "B" };
        // los enchufes (norte, este, sur, oeste) y el peso de cada pieza
        static readonly char[][] Enchufes = { "chch".ToCharArray(), "ccch".ToCharArray(), "chcc".ToCharArray(), "vcvc".ToCharArray(), "ccvc".ToCharArray(), "vccc".ToCharArray(), "cccc".ToCharArray() };
        static readonly double[] Pesos = { 10, 1, 1, 10, 1, 1, 0.4 };
        // (di, dj, mi lado, su lado): j crece al norte
        static readonly (int di, int dj, int mio, int suyo)[] Dirs = { (0, 1, 0, 2), (1, 0, 1, 3), (0, -1, 2, 0), (-1, 0, 3, 1) };
        static bool Casa(char a, char b) => a == b || a == 'x' || b == 'x';

        /// <summary>Las parcelas de una aldea. `ocupado`: lo construido del núcleo [(x, y, r)] (las parcelas no llegan a una casilla de ello);
        /// `vedado`: lo que no se labra (en el marco local); `vecinos`: otros asentamientos (sus casillas más cercanas no son de esta aldea).</summary>
        public static ParcelasDeAldea Hacer(int nivel, bool ganado, int semilla, IReadOnlyList<(double x, double y, double r)> ocupado,
            Func<double, double, bool> vedado = null, IReadOnlyList<(double x, double y)> vecinos = null)
        {
            nivel = Math.Max(1, Math.Min(3, nivel));
            var p = new ParcelasDeAldea { Radio = Radios[nivel], N = Lado(nivel) };
            int n = p.N;
            p.O = -n * Celda / 2;
            p.Giro = new Random(semilla * 31 + 7).NextDouble() * Math.PI / 2;
            var rng = new Random(semilla * 1009 + nivel);
            var fija = new bool[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    var (wx, wy) = p.Centro(i, j);
                    double d = Math.Sqrt(wx * wx + wy * wy);
                    bool casa = ocupado.Any(o => Math.Sqrt((wx - o.x) * (wx - o.x) + (wy - o.y) * (wy - o.y)) < o.r + Celda * 0.9);
                    bool ajeno = vecinos != null && vecinos.Any(v => Math.Sqrt((wx - v.x) * (wx - v.x) + (wy - v.y) * (wy - v.y)) < d);
                    fija[i, j] = d > p.Radio + Celda || casa || ajeno || (vedado != null && vedado(wx, wy));
                }
            var celdas = Wfc(n, fija, rng);
            // (el hueco suelto del mosaico, B, se une a la orientación de un vecino)
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    if (celdas[i, j] != 6) continue;
                    int puesto = 0;
                    foreach (var (di, dj, _, _) in Dirs)
                    {
                        int a = i + di, b = j + dj;
                        if (a < 0 || b < 0 || a >= n || b >= n || celdas[a, b] < 0 || celdas[a, b] == 6) continue;
                        puesto = celdas[a, b] < 3 ? 0 : 3;
                        break;
                    }
                    celdas[i, j] = puesto;
                }
            p.Horizontal = new bool[n, n];
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) p.Horizontal[i, j] = celdas[i, j] >= 0 && celdas[i, j] < 3;
            bool Viva(int i, int j) => i >= 0 && j >= 0 && i < n && j < n && celdas[i, j] >= 0;
            // las zonas de una orientación (4-conexas)
            var zona = new int[n, n];
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) zona[i, j] = -1;
            int z = 0;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    if (!Viva(i, j) || zona[i, j] >= 0) continue;
                    var pila = new Stack<(int, int)>();
                    pila.Push((i, j));
                    zona[i, j] = z;
                    while (pila.Count > 0)
                    {
                        var (a, b) = pila.Pop();
                        foreach (var (di, dj, _, _) in Dirs)
                        {
                            int c = a + di, e = b + dj;
                            if (Viva(c, e) && zona[c, e] < 0 && p.Horizontal[c, e] == p.Horizontal[i, j]) { zona[c, e] = z; pila.Push((c, e)); }
                        }
                    }
                    z++;
                }
            // las parcelas: bandas a lo ancho y tramos a lo largo
            var anchos = new (int ancho, int desfase)[z];
            for (int k = 0; k < z; k++)
            {
                var zr = new Random(semilla * 97 + k);
                anchos[k] = (ganado ? zr.Next(4, 7) : zr.Next(2, 5), zr.Next(0, 6));
            }
            var cortes = new Dictionary<(int, int), List<int>>();
            var clave = new Dictionary<(int, int, int), int>();
            var parcela = new int[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    parcela[i, j] = -1;
                    if (!Viva(i, j)) continue;
                    int k = zona[i, j];
                    var (w, off) = anchos[k];
                    int largo = p.Horizontal[i, j] ? i : j, ancho = p.Horizontal[i, j] ? j : i;
                    int banda = (ancho + off) / w;
                    if (!cortes.TryGetValue((k, banda), out var cs))
                    {
                        var rr = new Random(semilla * 7919 + k * 131 + banda);
                        cs = new List<int>();
                        for (int pos = -50; pos < n + 50;) { pos += ganado ? rr.Next(4, 7) : rr.Next(4, 11); cs.Add(pos); }
                        cortes[(k, banda)] = cs;
                    }
                    int tramo = cs.Count(c => c <= largo);
                    if (!clave.TryGetValue((k, banda, tramo), out var id)) clave[(k, banda, tramo)] = id = clave.Count;
                    parcela[i, j] = id;
                }
            // el borde, de parcelas enteras: fuera la de centro más allá del borde irregular, y las muy pequeñas
            var miembros = new Dictionary<int, List<(int i, int j)>>();
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) if (parcela[i, j] >= 0) { if (!miembros.TryGetValue(parcela[i, j], out var l)) miembros[parcela[i, j]] = l = new List<(int, int)>(); l.Add((i, j)); }
            foreach (var kv in miembros)
            {
                double cx = kv.Value.Average(c => p.O + (c.i + 0.5) * Celda), cy = kv.Value.Average(c => p.O + (c.j + 0.5) * Celda);
                var (wx, wy) = p.AlMarco(cx, cy);
                double a = Math.Atan2(wy, wx), borde = p.Radio * (0.9 + 0.07 * Math.Sin(3 * a + semilla) + 0.04 * Math.Sin(7 * a + 2 * semilla));
                if (Math.Sqrt(wx * wx + wy * wy) > borde || kv.Value.Count < (ganado ? 6 : 4))
                    foreach (var (i, j) in kv.Value) parcela[i, j] = -1;
            }
            // y los grupos sueltos de menos de 8 casillas
            var visto = new bool[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    if (parcela[i, j] < 0 || visto[i, j]) continue;
                    var grupo = new List<(int, int)> { (i, j) };
                    var pila = new Stack<(int, int)>(grupo);
                    visto[i, j] = true;
                    while (pila.Count > 0)
                    {
                        var (a, b) = pila.Pop();
                        foreach (var (di, dj, _, _) in Dirs)
                        {
                            int c = a + di, e = b + dj;
                            if (c >= 0 && e >= 0 && c < n && e < n && parcela[c, e] >= 0 && !visto[c, e]) { visto[c, e] = true; grupo.Add((c, e)); pila.Push((c, e)); }
                        }
                    }
                    if (grupo.Count < 8) foreach (var (a, b) in grupo) parcela[a, b] = -1;
                }
            // los ids, seguidos; los estados (el barbecho arado en un 10-15 %) y los tonos
            var vivas = new SortedSet<int>();
            foreach (var v in parcela) if (v >= 0) vivas.Add(v);
            var nuevo = vivas.Select((v, k) => (v, k)).ToDictionary(t => t.v, t => t.k);
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) if (parcela[i, j] >= 0) parcela[i, j] = nuevo[parcela[i, j]];
            p.Parcela = parcela;
            var rs = new Random(semilla * 53 + 1);
            int cuantas = vivas.Count;
            p.Estados = new Estado[cuantas];
            p.Tonos = new (float, float)[cuantas];
            int arados = Math.Max(1, (int)Math.Round(cuantas * (0.1 + rs.NextDouble() * 0.05)));
            var orden = Enumerable.Range(0, cuantas).OrderBy(_ => rs.Next()).ToList();
            var aradas = new HashSet<int>(orden.Take(Math.Min(arados, cuantas)));
            for (int k = 0; k < cuantas; k++)
            {
                double t = rs.NextDouble() * 10;
                p.Estados[k] = aradas.Contains(k) ? Estado.Arado : t < 5 ? Estado.Maduro : t < 8 ? Estado.Rastrojo : Estado.Verde;
                p.Tonos[k] = ((float)(0.92 + rs.NextDouble() * 0.16), (float)(-0.04 + rs.NextDouble() * 0.08));
            }
            return p;
        }

        /// <summary>El mosaico: colapsa la casilla de menos opciones (con algo de azar), propaga a las vecinas y, si se queda sin opciones, vuelve
        /// a empezar. Devuelve la pieza de cada casilla (−1 en las fijas).</summary>
        static int[,] Wfc(int n, bool[,] fija, Random rng)
        {
            const int Todas = (1 << 7) - 1;
            // (las piezas que admite cada pieza a cada lado, en máscara: la propagación es un O con ellas)
            var admite = new int[7, 4];
            for (int m = 0; m < 7; m++)
                for (int d = 0; d < 4; d++)
                    for (int q = 0; q < 7; q++)
                        if (Casa(Enchufes[q][Dirs[d].suyo], Enchufes[m][Dirs[d].mio])) admite[m, d] |= 1 << q;
            for (int intento = 0; intento < 40; intento++)
            {
                var op = new int[n, n];
                var libres = new List<(int, int)>();
                for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) { op[i, j] = fija[i, j] ? 0 : Todas; if (!fija[i, j]) libres.Add((i, j)); }
                // el frente: las casillas tocadas por la propagación que aún tienen más de una opción (las de menos opciones están aquí)
                var frente = new List<(int, int)>();
                var enFrente = new bool[n, n];
                bool Propagar(Stack<(int, int)> pila)
                {
                    while (pila.Count > 0)
                    {
                        var (a, b) = pila.Pop();
                        for (int d = 0; d < 4; d++)
                        {
                            int c = a + Dirs[d].di, e = b + Dirs[d].dj;
                            if (c < 0 || e < 0 || c >= n || e >= n || fija[c, e]) continue;
                            int vale = 0;
                            for (int m = 0; m < 7; m++) if ((op[a, b] & (1 << m)) != 0) vale |= admite[m, d];
                            int antes = op[c, e], ahora = antes & vale;
                            if (ahora == 0) return false;
                            if (ahora == antes) continue;
                            op[c, e] = ahora;
                            pila.Push((c, e));
                            if (!enFrente[c, e] && Cuenta(ahora) > 1) { enFrente[c, e] = true; frente.Add((c, e)); }
                        }
                    }
                    return true;
                }
                bool ok = true;
                while (ok)
                {
                    int mi = -1, mj = -1;
                    double mejor = double.MaxValue;
                    for (int k = frente.Count - 1; k >= 0; k--)
                    {
                        var (i, j) = frente[k];
                        int c = Cuenta(op[i, j]);
                        if (c <= 1) { enFrente[i, j] = false; frente[k] = frente[frente.Count - 1]; frente.RemoveAt(frente.Count - 1); continue; }
                    }
                    foreach (var (i, j) in frente)
                    {
                        double v = Cuenta(op[i, j]) + rng.NextDouble() * 0.6;
                        if (v < mejor) { mejor = v; mi = i; mj = j; }
                    }
                    if (mi < 0)
                    {
                        // (sin frente: una casilla libre cualquiera, al azar; todas tienen todas las opciones)
                        while (libres.Count > 0)
                        {
                            int k = rng.Next(libres.Count);
                            var (i, j) = libres[k];
                            libres[k] = libres[libres.Count - 1];
                            libres.RemoveAt(libres.Count - 1);
                            if (Cuenta(op[i, j]) > 1) { mi = i; mj = j; break; }
                        }
                    }
                    if (mi < 0)
                    {
                        var r = new int[n, n];
                        for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) r[i, j] = fija[i, j] ? -1 : Primera(op[i, j]);
                        return r;
                    }
                    double total = 0;
                    for (int q = 0; q < 7; q++) if ((op[mi, mj] & (1 << q)) != 0) total += Pesos[q];
                    double t = rng.NextDouble() * total;
                    int elegida = Primera(op[mi, mj]);
                    for (int q = 0; q < 7; q++)
                    {
                        if ((op[mi, mj] & (1 << q)) == 0) continue;
                        if ((t -= Pesos[q]) <= 0) { elegida = q; break; }
                    }
                    op[mi, mj] = 1 << elegida;
                    var pila = new Stack<(int, int)>();
                    pila.Push((mi, mj));
                    ok = Propagar(pila);
                }
                rng = new Random(rng.Next() + intento);
            }
            throw new InvalidOperationException("el mosaico de la aldea no cierra");
        }

        static int Cuenta(int m) { int c = 0; for (; m != 0; m &= m - 1) c++; return c; }
        static int Primera(int m) { for (int q = 0; q < 7; q++) if ((m & (1 << q)) != 0) return q; return 0; }
    }
}

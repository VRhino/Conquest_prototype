// La textura de los caminos, portada tal cual del prototipo (BronzeAgeClient: `src/mundo3d/caminos.ts`): una por escalón, pintada píxel a
// píxel con el mismo azar (mulberry32, semillas 11 y 23). El sendero es una huella de tierra pisada que serpentea y de bordes deshilachados; el
// camino, tierra con dos rodadas, la orilla algo más oscura y una franja de hierba en medio. En el mundo abierto no hay calzadas: el escalón 2
// del servidor va como camino. 128 px de lado a lado de la cinta y 512 a lo largo; se repite a lo largo cada 4 veces el ancho. C# puro: el
// editor la guarda como PNG (`Resources/MapaMundo/Caminos`) y una prueba la compara con la del prototipo.
using System;
using System.Linq;

namespace ConquestTactics.Mundo
{
    public static class TexturaDeCamino
    {
        public const int AnchoPx = 128, LargoPx = 512;

        /// <summary>Lo que ocupa a lo largo una vez la textura de un camino de `ancho` u.</summary>
        public static double Periodo(double ancho) => ancho * LargoPx / AnchoPx;

        /// <summary>RGBA de 8 bits, fila a fila desde la primera (v = 0), del escalón 0 (sendero) o 1 (camino).</summary>
        public static byte[] Pintar(int escalon)
        {
            Func<double, double, (double r, double g, double b, double a)> pintar = escalon == 0 ? Sendero() : Camino();
            var datos = new byte[AnchoPx * LargoPx * 4];
            for (int y = 0; y < LargoPx; y++)
                for (int x = 0; x < AnchoPx; x++)
                {
                    var (r, g, b, a) = pintar((x + 0.5) / AnchoPx, (y + 0.5) / LargoPx);
                    int i = 4 * (y * AnchoPx + x);
                    datos[i] = Byte(r);
                    datos[i + 1] = Byte(g);
                    datos[i + 2] = Byte(b);
                    datos[i + 3] = Byte(a * 255);
                }
            return datos;
        }

        static byte Byte(double v) => (byte)Math.Max(0, Math.Min(255, TrazadoDelMapa.Redondear(v)));

        /// <summary>mulberry32, como en el prototipo.</summary>
        static Func<double> Azar(uint semilla)
        {
            uint a = semilla;
            return () =>
            {
                unchecked
                {
                    a += 0x6d2b79f5;
                    uint t = (a ^ (a >> 15)) * (a | 1);
                    t ^= t + (t ^ (t >> 7)) * (t | 61);
                    return (t ^ (t >> 14)) / 4294967296.0;
                }
            };
        }

        /// <summary>Una curva suave de −1 a 1 que se repite a lo largo: `n` armónicos enteros con fases al azar.</summary>
        static Func<double, double> Ondas(Func<double> r, int n)
        {
            var fases = Enumerable.Range(0, n).Select(_ => r() * Math.PI * 2).ToArray();
            var amplitudes = Enumerable.Range(0, n).Select(k => (0.4 + 0.6 * r()) / (k + 1)).ToArray();
            double total = amplitudes.Sum();
            return v =>
            {
                double s = 0;
                for (int k = 0; k < n; k++) s += amplitudes[k] * Math.Sin(2 * Math.PI * (k + 1) * v + fases[k]);
                return s / total;
            };
        }

        /// <summary>Ruido de valor de −1 a 1 en una rejilla de `cx` × `cy` (en float, como el `Float32Array` del prototipo); se repite a lo largo.</summary>
        static Func<double, double, double> Grano(Func<double> r, int cx, int cy)
        {
            var valores = new float[(cx + 1) * cy];
            for (int k = 0; k < valores.Length; k++) valores[k] = (float)(r() * 2 - 1);
            return (x, y) =>
            {
                double fx = x * cx, fy = y * cy;
                int i = Math.Min(cx - 1, Math.Max(0, (int)Math.Floor(fx))), j = (int)Math.Floor(fy);
                double tx = fx - i, ty = fy - j, sx = tx * tx * (3 - 2 * tx), sy = ty * ty * (3 - 2 * ty);
                int j0 = ((j % cy) + cy) % cy, j1 = (j0 + 1) % cy;
                double En(int ii, int jj) => valores[jj * (cx + 1) + ii];
                return (En(i, j0) * (1 - sx) + En(i + 1, j0) * sx) * (1 - sy) + (En(i, j1) * (1 - sx) + En(i + 1, j1) * sx) * sy;
            };
        }

        static double Paso(double a, double b, double x)
        {
            double t = Math.Min(1, Math.Max(0, (x - a) / (b - a)));
            return t * t * (3 - 2 * t);
        }

        static double Campana(double x, double centro, double ancho) => Math.Exp(-Math.Pow((x - centro) / ancho, 2));

        static Func<double, double, (double, double, double, double)> Sendero()
        {
            var r = Azar(11);
            var borde = Ondas(r, 5);
            var centro = Ondas(r, 4);
            var g1 = Grano(r, 16, 64);
            var g2 = Grano(r, 48, 192);
            return (u, v) =>
            {
                double d = Math.Abs(u - 0.5 - 0.07 * centro(v)), medio = 0.27 + 0.06 * borde(v), n = 0.6 * g1(u, v) + 0.4 * g2(u, v);
                double a = 1 - Paso(medio - 0.12, medio + 0.06, d + 0.05 * g2(u, v)), pisado = 1 - Paso(0, medio, d);
                return (150 + 22 * pisado + 14 * n, 120 + 18 * pisado + 11 * n, 82 + 12 * pisado + 8 * n, a);
            };
        }

        static Func<double, double, (double, double, double, double)> Camino()
        {
            var r = Azar(23);
            var bi = Ondas(r, 5);
            var bd = Ondas(r, 5);
            var g1 = Grano(r, 12, 48);
            var g2 = Grano(r, 40, 160);
            return (u, v) =>
            {
                double n = 0.6 * g1(u, v) + 0.4 * g2(u, v), w = u + 0.03 * g2(u, v);
                double izq = 0.1 + 0.04 * bi(v), der = 0.9 + 0.04 * bd(v);
                double a = Paso(izq - 0.06, izq + 0.06, w) * (1 - Paso(der - 0.06, der + 0.06, w));
                double surco = Math.Max(Campana(u, 0.32, 0.035), Campana(u, 0.68, 0.035)), orilla = Math.Max(Campana(u, izq + 0.05, 0.04), Campana(u, der - 0.05, 0.04));
                double oscuro = 0.3 * surco + 0.16 * orilla, hierba = 0.55 * Campana(u, 0.5, 0.07) * (0.5 + 0.5 * g1(0.3 + u * 0.5, v));
                double t0 = (142 + 14 * n) * (1 - oscuro), t1 = (112 + 11 * n) * (1 - oscuro), t2 = (76 + 8 * n) * (1 - oscuro);
                return (t0 + (104 - t0) * hierba, t1 + (112 - t1) * hierba, t2 + (62 - t2) * hierba, a);
            };
        }
    }
}

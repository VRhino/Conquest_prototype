// La sombra del relieve para un sol fijo (el mapa de mundo): desde cada punto se recorre el relieve hacia el sol y se mira el horizonte; si el
// relieve tapa el sol, sombra. Con penumbra suave (el ancho angular de `PenumbraGrados` alrededor de la elevación del sol). Se calcula en una
// rejilla alineada al mundo, así que dos teselas vecinas dan lo mismo en su borde (sin costura). C# puro: se prueba sin Unity.
using System;

namespace ConquestTactics.Mundo
{
    public static class SombraDelRelieve
    {
        /// <summary>Hasta dónde se mira hacia el sol (u): la sombra de una montaña de 600 m con el sol a 40° llega a unos 60 u.</summary>
        public const double Alcance = 64;
        /// <summary>Ancho angular total de la penumbra (grados): el disco del sol, agrandado para que el borde de la sombra del monte sea blando.</summary>
        public const double PenumbraGrados = 6;
        /// <summary>Paso de la rejilla de la tesela (u).</summary>
        public const double PasoRejilla = 2;

        /// <summary>El sol en el mapa: la dirección horizontal hacia él (x, y del mapa) y la tangente de su elevación, desde la dirección de escena
        /// hacia el sol (x, altura, −y).</summary>
        public static (double dx, double dy, double tanElev) DelSol(double sx, double sy, double sz)
        {
            double h = Math.Sqrt(sx * sx + sz * sz);
            return (sx / h, -sz / h, sy / h);
        }

        /// <summary>La luz del sol que llega a (x, y) u, de 0 (a la sombra del relieve) a 1, con `altura` del relieve en metros.</summary>
        public static float Visibilidad(Func<double, double, double> altura, double x, double y, double dx, double dy, double tanElev, double metrosPorUnidad,
            double alcance = Alcance, double penumbraGrados = PenumbraGrados)
        {
            double h0 = altura(x, y), maxTan = double.NegativeInfinity;
            // pasos que crecen con la distancia (lo lejano tapa con su bulto, no con su detalle)
            for (double t = 0.5; t <= alcance; t += Math.Max(0.5, t * 0.08))
            {
                double tan = (altura(x + dx * t, y + dy * t) - h0) / (t * metrosPorUnidad);
                if (tan > maxTan) maxTan = tan;
            }
            double margen = (Math.Atan(tanElev) - Math.Atan(maxTan)) * 180 / Math.PI;
            return (float)Math.Clamp(margen / penumbraGrados + 0.5, 0, 1);
        }

        /// <summary>La rejilla de `n` × `n` puntos (x0 + i·paso, y0 + j·paso), fila a fila.</summary>
        public static float[] Rejilla(Func<double, double, double> altura, double x0, double y0, int n, double paso, double dx, double dy, double tanElev,
            double metrosPorUnidad)
        {
            var r = new float[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                    r[j * n + i] = Visibilidad(altura, x0 + i * paso, y0 + j * paso, dx, dy, tanElev, metrosPorUnidad);
            return r;
        }

        /// <summary>La rejilla en (u, v) u desde su esquina, bilineal.</summary>
        public static float En(float[] rejilla, int n, double paso, double u, double v)
        {
            double fx = Math.Clamp(u / paso, 0, n - 1), fy = Math.Clamp(v / paso, 0, n - 1);
            int i = Math.Min(n - 2, (int)fx), j = Math.Min(n - 2, (int)fy);
            double a = fx - i, b = fy - j;
            float c00 = rejilla[j * n + i], c10 = rejilla[j * n + i + 1], c01 = rejilla[(j + 1) * n + i], c11 = rejilla[(j + 1) * n + i + 1];
            return (float)((c00 * (1 - a) + c10 * a) * (1 - b) + (c01 * (1 - a) + c11 * a) * b);
        }
    }
}

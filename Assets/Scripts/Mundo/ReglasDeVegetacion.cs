// Las reglas de la vegetación del mapa de mundo, portadas tal cual del cliente jugador (BronzeAgeClient: `src/mundo3d/ruido.ts`, `especies.ts`,
// `arboles.ts` y `matas.ts`; docs/Mundo_3D.md, «Suelo, vegetación y especies de árbol»), con la misma aritmética para que el mapa de especies,
// los rodales y el reparto salgan iguales que allí (`Math.imul` es la multiplicación de enteros de 32 bits con desbordamiento: la de `int` en
// C# sin comprobar). Pura: sin Unity, la usa `VegetacionDelMapa` y la prueban `ReglasDeVegetacionTests` contra valores sacados del TypeScript.
using System;

namespace ConquestTactics.Mundo
{
    public enum EspecieDeArbol { Encina, Olivo, Pino, Cipres, Abeto, Palmera, Alamo }

    public enum ZonaDeVegetacion { Ribera, RiberaAlta, Costa, Llano, Colina, Montana }

    public enum TipoDeMata { Hierba, Flor, Mata, Junco, Piedra }

    public static class ReglasDeVegetacion
    {
        /// <summary>Árboles por u² con el bosque al máximo y copa `c`: 0,55 / c² (las copas se tocan sin taparse del todo).</summary>
        public const double Porcion = 0.55;
        /// <summary>La copa del prototipo (u).</summary>
        public const double CopaDelPrototipo = 0.5;
        /// <summary>Pendiente (m/m) por encima de la cual no hay árboles.</summary>
        public const double PendienteMaxima = 1.1;
        /// <summary>Densidad de bosque por debajo de la cual no hay árboles.</summary>
        public const double DensidadMinima = 0.2;
        /// <summary>La ribera: lo bajo a menos de estas u del agua (lago, mar o río).</summary>
        public const double Ribera = 2.5;
        /// <summary>Bandas de distancia al héroe (u) y la parte de los árboles que queda en cada una (los que quedan son siempre los mismos; sus
        /// copas crecen 1/√parte para tapar lo mismo).</summary>
        public static readonly double[] BandasHasta = { 48, 80, 128, double.PositiveInfinity };
        public static readonly double[] BandasParte = { 1, 0.5, 0.25, 0.125 };
        /// <summary>Las matas: candidata cada 0,5 u y sus bandas (u, parte, crece).</summary>
        public const double PasoMatas = 0.5;
        public static readonly double[] BandasMatasHasta = { 20, 34, double.PositiveInfinity };
        public static readonly double[] BandasMatasParte = { 1, 0.5, 0.25 };
        public static readonly double[] BandasMatasCrece = { 1, 1.2, 1.4 };

        /// <summary>Un número fijo entre 0 y 1 por (i, j, k) (`azar` de ruido.ts).</summary>
        public static double Azar(int i, int j, int k)
        {
            unchecked
            {
                int h = i * 374761393 ^ j * 668265263 ^ k * 2147483647;
                h = (h ^ (int)((uint)h >> 13)) * 1274126177;
                return (uint)(h ^ (int)((uint)h >> 16)) / 4294967296.0;
            }
        }

        /// <summary>Ruido de valor (0..1) en (x, y) con manchas de unas `escala` u (`manchas` de ruido.ts).</summary>
        public static double Manchas(double x, double y, double escala, int semilla)
        {
            double fx = x / escala, fy = y / escala;
            int i = (int)Math.Floor(fx), j = (int)Math.Floor(fy);
            double a = fx - i, b = fy - j, sa = a * a * (3 - 2 * a), sb = b * b * (3 - 2 * b);
            double v00 = Azar(i, j, semilla), v10 = Azar(i + 1, j, semilla), v01 = Azar(i, j + 1, semilla), v11 = Azar(i + 1, j + 1, semilla);
            return (v00 * (1 - sa) + v10 * sa) * (1 - sb) + (v01 * (1 - sa) + v11 * sa) * sb;
        }

        /// <summary>Un raster de 0..255 en (x, y), interpolado entre los centros de sus celdas, en 0..1 (`rasterEn` de fuente.ts). Sin raster, 0.</summary>
        public static double RasterEn(int columnas, int filas, double celda, byte[] datos, double x, double y)
        {
            if (datos == null) return 0;
            double fi = Math.Min(Math.Max(x / celda - 0.5, 0), columnas - 1), fj = Math.Min(Math.Max(y / celda - 0.5, 0), filas - 1);
            int i = Math.Min(columnas - 2, (int)Math.Floor(fi)), j = Math.Min(filas - 2, (int)Math.Floor(fj));
            double a = fi - i, b = fj - j;
            int k = j * columnas + i;
            return ((datos[k] * (1 - a) + datos[k + 1] * a) * (1 - b) + (datos[k + columnas] * (1 - a) + datos[k + columnas + 1] * a) * b) / 255;
        }

        static readonly (EspecieDeArbol e, double peso)[][] Zonas =
        {
            new[] { (EspecieDeArbol.Alamo, 5.0), (EspecieDeArbol.Palmera, 3.0), (EspecieDeArbol.Encina, 1.0) },
            new[] { (EspecieDeArbol.Alamo, 5.0), (EspecieDeArbol.Encina, 2.0), (EspecieDeArbol.Cipres, 1.0) },
            new[] { (EspecieDeArbol.Pino, 4.0), (EspecieDeArbol.Olivo, 2.0), (EspecieDeArbol.Palmera, 1.5), (EspecieDeArbol.Encina, 1.0), (EspecieDeArbol.Cipres, 1.0) },
            new[] { (EspecieDeArbol.Encina, 5.0), (EspecieDeArbol.Olivo, 3.0), (EspecieDeArbol.Pino, 1.0), (EspecieDeArbol.Cipres, 1.0) },
            new[] { (EspecieDeArbol.Pino, 4.0), (EspecieDeArbol.Encina, 2.0), (EspecieDeArbol.Olivo, 2.0), (EspecieDeArbol.Cipres, 1.0), (EspecieDeArbol.Abeto, 1.0) },
            new[] { (EspecieDeArbol.Abeto, 6.0), (EspecieDeArbol.Pino, 2.0), (EspecieDeArbol.Cipres, 0.5) },
        };
        /// <summary>Las que pueden mandar en una mancha: todas menos el ciprés.</summary>
        static readonly (EspecieDeArbol e, double peso)[][] Mandan = Array.ConvertAll(Zonas, z => Array.FindAll(z, p => p.e != EspecieDeArbol.Cipres));

        /// <summary>La zona de (x, y) con el suelo a `h` m y el agua a `dAgua` u (`zonaDe`). Los cortes de altura bailan unos metros por manchas.
        /// `costa`, `colina` y `montana`: los umbrales del generador (m).</summary>
        public static ZonaDeVegetacion ZonaDe(double x, double y, double h, double dAgua, double costa, double colina, double montana)
        {
            double hh = h + (Manchas(x, y, 7, 101) - 0.5) * 30;
            if (dAgua < Ribera && hh < colina) return hh < costa + 15 ? ZonaDeVegetacion.Ribera : ZonaDeVegetacion.RiberaAlta;
            return hh < costa ? ZonaDeVegetacion.Costa : hh < colina ? ZonaDeVegetacion.Llano : hh < montana ? ZonaDeVegetacion.Colina : ZonaDeVegetacion.Montana;
        }

        /// <summary>Las especies que pueden mandar en una zona (todas menos el ciprés), con su peso.</summary>
        public static (EspecieDeArbol e, double peso)[] MandanEn(ZonaDeVegetacion zona) => Mandan[(int)zona];

        /// <summary>La especie que manda en la zona, de más peso, que no sea `sin` (el olivo no manda en el bosque cerrado).</summary>
        public static EspecieDeArbol SiguienteSin(ZonaDeVegetacion zona, EspecieDeArbol sin)
        {
            var mejor = EspecieDeArbol.Encina;
            double peso = -1;
            foreach (var (e, p) in Mandan[(int)zona]) if (e != sin && p > peso) { mejor = e; peso = p; }
            return mejor;
        }

        /// <summary>El peso de una especie en una zona (0 si no crece allí).</summary>
        public static double PesoEn(ZonaDeVegetacion zona, EspecieDeArbol e)
        {
            foreach (var (x, p) in Zonas[(int)zona]) if (x == e) return p;
            return 0;
        }

        /// <summary>La especie que manda en (x, y) según el prototipo (la del rodal de unas 22 u de `especieDe`).</summary>
        public static EspecieDeArbol DominanteDe(double x, double y, ZonaDeVegetacion zona)
        {
            double wx = x + (Manchas(x, y, 9, 211) - 0.5) * 14, wy = y + (Manchas(x, y, 9, 223) - 0.5) * 14;
            return Elegir(Mandan[(int)zona], Azar((int)Math.Floor(wx / 22), (int)Math.Floor(wy / 22), 307));
        }

        /// <summary>`especieDe` con la que manda ya decidida (la del rodal del mosaico): siete de cada diez, ella; el resto, cualquiera de la zona.</summary>
        public static EspecieDeArbol EspecieConDominante(ZonaDeVegetacion zona, EspecieDeArbol dominante, double t)
            => t < 0.7 ? dominante : Elegir(Zonas[(int)zona], (t - 0.7) / 0.3);

        static EspecieDeArbol Elegir((EspecieDeArbol e, double peso)[] lista, double t)
        {
            double total = 0;
            foreach (var (_, p) in lista) total += p;
            double s = t * total;
            foreach (var (e, p) in lista) if ((s -= p) < 0) return e;
            return lista[lista.Length - 1].e;
        }

        /// <summary>La especie de un árbol en (x, y) (`especieDe`). `t` es un azar del árbol. En cada mancha de unas 22 u (de borde irregular) manda
        /// una especie de la zona: siete de cada diez árboles son de ella; el resto, cualquiera de la zona según su peso.</summary>
        public static EspecieDeArbol EspecieDe(double x, double y, double h, double dAgua, double t, double costa, double colina, double montana)
        {
            var zona = (int)ZonaDe(x, y, h, dAgua, costa, colina, montana);
            double wx = x + (Manchas(x, y, 9, 211) - 0.5) * 14, wy = y + (Manchas(x, y, 9, 223) - 0.5) * 14;
            var dominante = Elegir(Mandan[zona], Azar((int)Math.Floor(wx / 22), (int)Math.Floor(wy / 22), 307));
            return t < 0.7 ? dominante : Elegir(Zonas[zona], (t - 0.7) / 0.3);
        }

        public static double Suave(double a, double b, double x)
        {
            double t = Math.Min(1, Math.Max(0, (x - a) / (b - a)));
            return t * t * (3 - 2 * t);
        }

        /// <summary>Lo que cabe en una candidata de vegetación baja (`probabilidades` de matas.ts): hierba, flor, mata, junco y piedra, en ese
        /// orden. `h` altura (m), `p` pendiente (m/m), `f` fertilidad, `b` densidad de bosque, `d` distancia al agua (u), `m` mancha de flores.</summary>
        public static double[] Probabilidades(double h, double p, double f, double b, double d, double m, double agua, double costa, double colina, double montana)
        {
            bool bajo = h < colina, monte = h > montana;
            double junco = d < 1.2 && bajo ? 0.5 * (1 - d / 1.2) + 0.15 : 0;
            double piedra = 0.012 + 0.22 * Suave(0.35, 1.0, p) + (monte ? 0.08 : h > colina ? 0.03 : 0);
            double borde = Suave(0.05, 0.25, b) * (1 - Suave(0.45, 0.75, b));
            double mata = (0.01 + 0.045 * (1 - f) * (h > costa ? 1 : 0.5) + 0.08 * borde) * (1 - Suave(0.7, 1.1, p));
            double flor = f > 0.35 && b < 0.3 && !monte ? 0.3 * Suave(0.6, 0.8, m) * Suave(0.35, 0.55, f) : 0;
            double hierba = (0.1 + 0.6 * Suave(0.12, 0.7, f)) * (1 - 0.65 * Suave(0.2, 0.7, b)) * (1 - Suave(0.6, 1.1, p)) * (h > montana + 20 ? 0.3 : 1) * Suave(agua - 2, agua + 12, h);
            double total = junco + piedra + mata + flor + hierba, k = total > 1 ? 1 / total : 1;
            return new[] { hierba * k, flor * k, mata * k, junco * k, piedra * k };
        }

        /// <summary>La distancia (en muestras) de cada muestra de tierra al agua, por chaflán de dos pasadas (`distanciaATierra` de agua.ts con los
        /// papeles cambiados): 0 en el agua; sin agua en la ventana, infinito.</summary>
        public static float[] DistanciaAlAgua(bool[] agua, int ancho, int alto)
        {
            var d = new float[ancho * alto];
            const float D = 1.41421356f;
            for (int k = 0; k < d.Length; k++) d[k] = agua[k] ? 0 : float.PositiveInfinity;
            for (int j = 0; j < alto; j++)
                for (int i = 0; i < ancho; i++)
                {
                    int k = j * ancho + i;
                    if (d[k] == 0) continue;
                    float v = d[k];
                    if (i > 0) v = Math.Min(v, d[k - 1] + 1);
                    if (j > 0)
                    {
                        v = Math.Min(v, d[k - ancho] + 1);
                        if (i > 0) v = Math.Min(v, d[k - ancho - 1] + D);
                        if (i < ancho - 1) v = Math.Min(v, d[k - ancho + 1] + D);
                    }
                    d[k] = v;
                }
            for (int j = alto - 1; j >= 0; j--)
                for (int i = ancho - 1; i >= 0; i--)
                {
                    int k = j * ancho + i;
                    if (d[k] == 0) continue;
                    float v = d[k];
                    if (i < ancho - 1) v = Math.Min(v, d[k + 1] + 1);
                    if (j < alto - 1)
                    {
                        v = Math.Min(v, d[k + ancho] + 1);
                        if (i < ancho - 1) v = Math.Min(v, d[k + ancho + 1] + D);
                        if (i > 0) v = Math.Min(v, d[k + ancho - 1] + D);
                    }
                    d[k] = v;
                }
            return d;
        }
    }
}

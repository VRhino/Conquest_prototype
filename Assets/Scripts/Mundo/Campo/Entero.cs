// La aritmética entera del campo de batalla del lugar (Campo_De_Batalla_Del_Lugar.md §8): cada parte genera el campo por su cuenta y tiene
// que salirle el mismo, así que lo que decide la forma no pasa por `float` ni por `Math.Sin`. Las fracciones van en Q16 (65536 = 1), los
// senos y cosenos en Q30, y los ángulos en vueltas de 2³² (un `uint` que da la vuelta solo). La raíz cuadrada sale de `Math.Sqrt`, que IEEE
// redondea igual en todas partes, y se corrige en enteros.
using System;

namespace ConquestTactics.Mundo.Campo
{
    public static class Entero
    {
        /// <summary>El 1 de las fracciones en Q16.</summary>
        public const int Uno = 65536;
        /// <summary>El 1 de los senos y cosenos (Q30).</summary>
        public const long Uno30 = 1L << 30;
        /// <summary>π en Q30.</summary>
        const long PiQ30 = 3373259426;

        public static int Clamp(int v, int a, int b) => v < a ? a : v > b ? b : v;
        public static long Clamp(long v, long a, long b) => v < a ? a : v > b ? b : v;

        /// <summary>⌊a / b⌋, con b &gt; 0.</summary>
        public static long DivSuelo(long a, long b)
        {
            long q = a / b;
            return a % b != 0 && a < 0 ? q - 1 : q;
        }

        /// <summary>a / b redondeado al más cercano, la mitad hacia arriba (como `Math.round` de JavaScript), con b &gt; 0.</summary>
        public static long DivRedondo(long a, long b) => DivSuelo(2 * a + b, 2 * b);

        /// <summary>⌊√n⌋.</summary>
        public static long Raiz(long n)
        {
            if (n <= 0) return 0;
            long r = (long)Math.Sqrt(n);
            while (r * r > n) r--;
            while ((r + 1) * (r + 1) <= n) r++;
            return r;
        }

        /// <summary>√(x² + y²), redondeada al entero más cercano.</summary>
        public static long Hipot(long x, long y)
        {
            long n = x * x + y * y, r = Raiz(n);
            return n - r * r > r ? r + 1 : r;
        }

        /// <summary>3t² − 2t³ de t (Q16), con t entre 0 y 1.</summary>
        public static long Suave(long t)
        {
            long u = Clamp(t, 0, Uno);
            return u * u * (3 * Uno - 2 * u) >> 32;
        }

        /// <summary>De a a b por t (Q16).</summary>
        public static long Lerp(long a, long b, long t) => a + ((b - a) * t >> 16);

        /// <summary>El `azar` de la §6.11: un número fijo por (i, j, k), en 32 bits (0 a 2³² − 1 = 0 a 1).</summary>
        public static uint Azar(long i, long j, long k)
        {
            unchecked
            {
                uint h = (uint)i * 374761393u ^ (uint)j * 668265263u ^ (uint)k * 2147483647u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        /// <summary>El `azar`, en Q16.</summary>
        public static long AzarQ16(long i, long j, long k) => Azar(i, j, k) >> 16;

        /// <summary>Las `manchas` de la §6.11 (ruido de valor, Q16) en (x, y), con manchas de `escala`: las tres cifras en las mismas unidades.</summary>
        public static long Manchas(long x, long y, long escala, int semilla)
        {
            long i = DivSuelo(x, escala), j = DivSuelo(y, escala);
            long sa = Suave((x - i * escala) * Uno / escala), sb = Suave((y - j * escala) * Uno / escala);
            long v00 = AzarQ16(i, j, semilla), v10 = AzarQ16(i + 1, j, semilla), v01 = AzarQ16(i, j + 1, semilla), v11 = AzarQ16(i + 1, j + 1, semilla);
            long arriba = v00 * (Uno - sa) + v10 * sa, abajo = v01 * (Uno - sa) + v11 * sa;
            return (arriba * (Uno - sb) + abajo * sb) >> 32;
        }

        /// <summary>Un ángulo en grados, en vueltas de 2³².</summary>
        public static uint Grados(long g) => unchecked((uint)(g * (1L << 32) / 360));

        /// <summary>El seno de `vuelta` (2³² = una vuelta), en Q30.</summary>
        public static long Seno(uint vuelta)
        {
            uint r = vuelta & 0x3FFFFFFF;
            switch (vuelta >> 30)
            {
                case 0: return SenoDelCuadrante(r);
                case 1: return CosenoDelCuadrante(r);
                case 2: return -SenoDelCuadrante(r);
                default: return -CosenoDelCuadrante(r);
            }
        }

        public static long Coseno(uint vuelta) => Seno(unchecked(vuelta + (1u << 30)));

        /// <summary>La tangente de `grados`, en Q30.</summary>
        public static long Tangente(long grados) => Seno(Grados(grados)) * Uno30 / Coseno(Grados(grados));

        // r, de 0 a 2³⁰ (un cuarto de vuelta): el seno y el coseno por Taylor (hasta x¹³ y x¹⁴; en π/2 el error es menor que una parte en 10⁹).
        static long SenoDelCuadrante(uint r)
        {
            long x = r * PiQ30 >> 31, x2 = x * x >> 30, t = Uno30;
            for (long k = 12; k >= 2; k -= 2) t = Uno30 - (x2 * t >> 30) / (k * (k + 1));
            return x * t >> 30;
        }

        static long CosenoDelCuadrante(uint r)
        {
            long x = r * PiQ30 >> 31, x2 = x * x >> 30, t = Uno30;
            for (long k = 13; k >= 1; k -= 2) t = Uno30 - (x2 * t >> 30) / (k * (k + 1));
            return t;
        }

        /// <summary>e^(−x), de x (Q16, x ≥ 0), en Q16.</summary>
        public static long ExpNegativa(long x)
        {
            if (x <= 0) return Uno;
            long entera = x >> 16, f = (x & 0xFFFF) << 14, r = ExpDeLaFraccion(f), e1 = ExpDeLaFraccion(Uno30);
            for (long n = 0; n < entera && r > 0; n++) r = r * e1 >> 30;
            return r >> 14;
        }

        // e^(−f) para f (Q30) entre 0 y 1, por Taylor hasta f¹³, en Q30.
        static long ExpDeLaFraccion(long f)
        {
            long t = Uno30;
            for (long k = 13; k >= 1; k--) t = Uno30 - (f * t >> 30) / k;
            return t;
        }
    }
}

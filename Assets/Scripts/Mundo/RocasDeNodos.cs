// Los nodos de recursos como el prototipo (BronzeAgeClient `mundo3d/paisaje.ts`, bloque de los nodos), tal cual: cada nodo es un montón de
// rocas, dodecaedros aplastados a 0,6. Piedra: 5 rocas; los metales (y cualquier otro tipo): 3. Con N = `MUNDO3D.nodo` (0,5):
//   a = azar(x + k, 1) · 2π; distancia = N · (1 piedra, 0,6 el resto) · √azar(y + k, 2); r = N · ((0,15 | 0,12) + (0,30 | 0,18) · azar(x, 3 + k));
//   la roca, a la altura del suelo + r · 0,25; el giro, Euler XYZ (0,2 · sen 3a, a, 0,2 · cos 5a); escala r.
// Color: la piedra #8d8d8d; los demás, ese gris con el 70 % de `RECURSO_COLOR` de su tipo (sin entrada, #8a4b3a), mezclado en lineal (three
// 0.170 con su gestor de color). Todo en el marco del prototipo (x, alto, y del mapa; mano derecha) y sin tipos de Unity (este ensamblado no
// los tiene): `NodosDelMapa` lo pasa a la escena.
// Las referencias de la prueba salen del propio código del prototipo: `tasks/referencias/nodos.mts`.
using System;
using System.Collections.Generic;

namespace ConquestTactics.Mundo
{
    public static class RocasDeNodos
    {
        /// <summary>La escala de los nodos del prototipo (`MUNDO3D.nodo`; la mitad: Víctor, 2026-11-07).</summary>
        public const double N = 0.5;
        public const string Gris = "#8d8d8d", SinColor = "#8a4b3a";

        public struct Roca
        {
            /// <summary>Dónde (u del mapa), su radio (u) y su giro (cuaternión x, y, z, w en el marco del prototipo).</summary>
            public double X, Y, R;
            public double Qx, Qy, Qz, Qw;
        }

        /// <summary>El `azar` del principio de `paisaje.ts`: un número fijo entre 0 y 1 por (a, b), con la aritmética de 32 bits de JavaScript.</summary>
        public static double Azar(double a, double b)
        {
            unchecked
            {
                int h = AInt32(Math.Floor(a * 16 + 0.5)) * 374761393 ^ AInt32(b) * 668265263;
                h = (h ^ (int)((uint)h >> 13)) * 1274126177;
                return (uint)(h ^ (int)((uint)h >> 16)) / 4294967296.0;
            }
        }

        /// <summary>`x | 0` de JavaScript: trunca y se queda con los 32 bits bajos.</summary>
        static int AInt32(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) return 0;
            var t = Math.Truncate(x) % 4294967296.0;
            return unchecked((int)(uint)(long)(t < 0 ? t + 4294967296.0 : t));
        }

        /// <summary>Las rocas de un nodo de `tipo` en (x, y), en el orden del prototipo.</summary>
        public static List<Roca> DelPrototipo(string tipo, double x, double y)
        {
            bool piedra = tipo == "piedra";
            int cuantas = piedra ? 5 : 3;
            var r = new List<Roca>(cuantas);
            for (int k = 0; k < cuantas; k++)
            {
                double a = Azar(x + k, 1) * Math.PI * 2, dist = N * (piedra ? 1 : 0.6) * Math.Sqrt(Azar(y + k, 2));
                double radio = N * ((piedra ? 0.15 : 0.12) + (piedra ? 0.3 : 0.18) * Azar(x, 3 + k));
                // Euler XYZ de three (`Quaternion.setFromEuler`, orden 'XYZ')
                double c1 = Math.Cos(0.1 * Math.Sin(a * 3)), s1 = Math.Sin(0.1 * Math.Sin(a * 3)), c2 = Math.Cos(a / 2), s2 = Math.Sin(a / 2);
                double c3 = Math.Cos(0.1 * Math.Cos(a * 5)), s3 = Math.Sin(0.1 * Math.Cos(a * 5));
                r.Add(new Roca
                {
                    X = x + Math.Cos(a) * dist, Y = y + Math.Sin(a) * dist, R = radio,
                    Qx = s1 * c2 * c3 + c1 * s2 * s3, Qy = c1 * s2 * c3 - s1 * c2 * s3, Qz = c1 * c2 * s3 + s1 * s2 * c3, Qw = c1 * c2 * c3 - s1 * s2 * s3,
                });
            }
            return r;
        }

        /// <summary>El color lineal (r, g, b) de las rocas de `tipo`: `colorDelTipo` es su `RECURSO_COLOR` (null si no tiene).</summary>
        public static (double r, double g, double b) ColorLineal(string tipo, string colorDelTipo)
        {
            var gris = Lineal(Gris);
            if (tipo == "piedra") return gris;
            var c = Lineal(colorDelTipo ?? SinColor);
            return (gris.r + (c.r - gris.r) * 0.7, gris.g + (c.g - gris.g) * 0.7, gris.b + (c.b - gris.b) * 0.7);
        }

        /// <summary>De "#rrggbb" (sRGB) a lineal, con la curva exacta de sRGB (la de three).</summary>
        public static (double r, double g, double b) Lineal(string hex)
        {
            var v = Convert.ToInt32(hex.TrimStart('#'), 16);
            static double L(int c) { var s = c / 255.0; return s < 0.04045 ? s * 0.0773993808 : Math.Pow(s * 0.9478672986 + 0.0521327014, 2.4); }
            return (L((v >> 16) & 255), L((v >> 8) & 255), L(v & 255));
        }

        /// <summary>La roca del prototipo: `DodecahedronGeometry(1, 0)` aplastado a 0,6 en alto, sin indexar (36 triángulos, 108 vértices), en el
        /// orden de three (cada cara (a, b, c) sale como b, c, a).</summary>
        public static (double x, double y, double z)[] Dodecaedro()
        {
            double t = (1 + Math.Sqrt(5)) / 2, r = 1 / t;
            double[] v =
            {
                -1, -1, -1, -1, -1, 1, -1, 1, -1, -1, 1, 1, 1, -1, -1, 1, -1, 1, 1, 1, -1, 1, 1, 1,
                0, -r, -t, 0, -r, t, 0, r, -t, 0, r, t, -r, -t, 0, -r, t, 0, r, -t, 0, r, t, 0,
                -t, 0, -r, t, 0, -r, -t, 0, r, t, 0, r,
            };
            int[] caras =
            {
                3, 11, 7, 3, 7, 15, 3, 15, 13, 7, 19, 17, 7, 17, 6, 7, 6, 15, 17, 4, 8, 17, 8, 10, 17, 10, 6, 8, 0, 16, 8, 16, 2, 8, 2, 10,
                0, 12, 1, 0, 1, 18, 0, 18, 16, 6, 10, 2, 6, 2, 13, 6, 13, 15, 2, 16, 18, 2, 18, 3, 2, 3, 13, 18, 1, 9, 18, 9, 11, 18, 11, 3,
                4, 14, 12, 4, 12, 0, 4, 0, 8, 11, 9, 5, 11, 5, 19, 11, 19, 7, 19, 5, 14, 19, 14, 4, 19, 4, 17, 1, 12, 14, 1, 14, 5, 1, 5, 9,
            };
            (double, double, double) P(int i)
            {
                double x = v[i * 3], y = v[i * 3 + 1], z = v[i * 3 + 2], l = Math.Sqrt(x * x + y * y + z * z);
                return (x / l, y / l * 0.6, z / l);
            }
            var o = new (double x, double y, double z)[caras.Length];
            for (int f = 0; f < caras.Length; f += 3)
            {
                o[f] = P(caras[f + 1]);
                o[f + 1] = P(caras[f + 2]);
                o[f + 2] = P(caras[f]);
            }
            return o;
        }
    }
}

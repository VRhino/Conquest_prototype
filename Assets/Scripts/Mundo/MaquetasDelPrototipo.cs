// Las maquetas de los asentamientos como el prototipo (BronzeAgeClient `mundo3d/maquetas.ts` y `huellas.ts`), tal cual: plazas (niveles 1 a 5,
// con o sin muralla), pueblos (el extractor con su bocamina y la aldea con los campos de su cultivo), avanzadas (atalaya, fuerte, ciudadela,
// en ruinas), obras, campamentos de mercenarios y de bandidos, alijos y batallas. Todo son cajas (y tiendas) por material y banderas del
// color de su Facción, en u del mapa y en el marco del prototipo (x, alto, y del mapa); sin tipos de Unity (este ensamblado no los tiene):
// `MaquetasDelMapa` las pone en la escena. Con la aritmética de 32 bits de JavaScript (`hash`, `azar`), su redondeo (`Math.round`) y su orden
// estable al ordenar. Las referencias de la prueba salen del propio código del prototipo: `tasks/referencias/maquetas.mts`.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ConquestTactics.Mundo
{
    public static class MaquetasDelPrototipo
    {
        /// <summary>`MUNDO3D`: la escala de las maquetas y sus factores (pueblos y avanzadas, campamentos y alijos) y lo que suben las plazas.</summary>
        public const double Maqueta = 0.8, Campamento = 2, AlijoF = 2, PuebloF = 1.6, AvanzadaF = 1.6, AltoPlaza = 1.7, HolguraAro = 0.12;

        /// <summary>Los colores de los materiales (sRGB), los del prototipo.</summary>
        public static readonly Dictionary<string, uint> Colores = new Dictionary<string, uint>
        {
            ["muro"] = 0xc9b48f, ["adobe"] = 0xd2bf9a, ["techo"] = 0xa8714a, ["madera"] = 0x8a6440, ["lona"] = 0xd8cfbd, ["ruina"] = 0x7d7466,
            ["campo"] = 0xb59a3a, ["mina"] = 0x4a4038, ["lonaOscura"] = 0x4a3f36, ["fuego"] = 0xd35400, ["oro"] = 0xe0b422, ["filo"] = 0xf4ead2,
            ["batallaPropia"] = 0x2f6b3a, ["batallaAjena"] = 0x8a3b12, ["tierra"] = 0x8b6e4e, ["rastrojo"] = 0xa89064, ["vid"] = 0x4f7a2e,
            ["olivo"] = 0x7d8a58, ["cebada"] = 0xcdbb74, ["cebadaVerde"] = 0x95a256, ["sesamo"] = 0x5a8a40, ["florSesamo"] = 0xeed6dc,
            ["pasto"] = 0x9c8656, ["res"] = 0x7a5a3e, ["oveja"] = 0xe6dfcc,
        };
        public const string ColorMercenarios = "#6fa3d6", ColorBandidos = "#c0392b";

        /// <summary>¿Son tiendas (cono de cuatro caras) las piezas de este material?</summary>
        public static bool EsTienda(string mat) => mat == "lona" || mat == "lonaOscura";

        /// <summary>Una pieza: caja (o tienda) en (X, Y) de Ancho × Fondo × Alto u, girada Giro rad, inclinada Inclina rad de lado y subida Base u.</summary>
        public sealed class Pieza
        {
            public double X, Y, Ancho, Fondo, Alto, Giro, Base, Inclina;
        }

        /// <summary>Las piezas por material y las banderas por color, en el orden en que se añadieron (como los `Map` del prototipo).</summary>
        public sealed class Piezas
        {
            public readonly List<string> Materiales = new List<string>();
            public readonly Dictionary<string, List<Pieza>> Por = new Dictionary<string, List<Pieza>>();
            public readonly List<string> Colores = new List<string>();
            public readonly Dictionary<string, List<Pieza>> Banderas = new Dictionary<string, List<Pieza>>();

            public void Add(string mat, Pieza p)
            {
                if (!Por.TryGetValue(mat, out var l)) { Por[mat] = l = new List<Pieza>(); Materiales.Add(mat); }
                l.Add(p);
            }

            public void Caja(string mat, double x, double y, double ancho, double fondo, double alto, double giro = 0, double @base = 0) =>
                Add(mat, new Pieza { X = x, Y = y, Ancho = ancho, Fondo = fondo, Alto = alto, Giro = giro, Base = @base });

            /// <summary>Un anillo de tramos rectos de radio r, con un hueco (la puerta) en el tramo `hueco`.</summary>
            public void Anillo(double cx, double cy, double r, double alto, double grueso, string mat, int hueco = 7)
            {
                int seg = Math.Max(14, (int)Redondear(2 * Math.PI * r / 0.25));
                for (int k = 0; k < seg; k++)
                {
                    if (k == hueco) continue;
                    double a0 = (double)k / seg * 2 * Math.PI, a1 = (double)(k + 1) / seg * 2 * Math.PI;
                    double x0 = cx + r * Math.Cos(a0), y0 = cy + r * Math.Sin(a0), x1 = cx + r * Math.Cos(a1), y1 = cy + r * Math.Sin(a1);
                    Caja(mat, (x0 + x1) / 2, (y0 + y1) / 2, Hypot(x1 - x0, y1 - y0) + 0.02, grueso, alto, Math.Atan2(y1 - y0, x1 - x0));
                }
            }

            /// <summary>Un asta de `alto` u con su bandera de `color` arriba.</summary>
            public void Bandera(double x, double y, double alto, string color, double @base = 0)
            {
                const double M = Maqueta;
                Caja("madera", x, y, 0.02 * M, 0.02 * M, alto, 0, @base);
                if (!Banderas.TryGetValue(color, out var l)) { Banderas[color] = l = new List<Pieza>(); Colores.Add(color); }
                l.Add(new Pieza { X = x, Y = y, Ancho = 0.16 * M, Fondo = 0.012 * M, Alto = 0.1 * M, Giro = 0, Base = @base + alto });
            }

            /// <summary>Todas las piezas, por material en su orden; y las banderas, por color.</summary>
            public IEnumerable<(string mat, Pieza p)> Todas() => Materiales.SelectMany(m => Por[m].Select(p => (m, p)));
            public IEnumerable<(string color, Pieza p)> TodasLasBanderas() => Colores.SelectMany(c => Banderas[c].Select(p => (c, p)));
        }

        static double Hypot(double a, double b) => Math.Sqrt(a * a + b * b);
        /// <summary>`Math.round` de JavaScript: los medios, hacia arriba.</summary>
        static double Redondear(double v) => Math.Floor(v + 0.5);

        /// <summary>`x | 0` de JavaScript: trunca y se queda con los 32 bits bajos.</summary>
        internal static int AInt32(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) return 0;
            var t = Math.Truncate(x) % 4294967296.0;
            return unchecked((int)(uint)(long)(t < 0 ? t + 4294967296.0 : t));
        }

        /// <summary>El `hash` de maquetas.ts: un número fijo entre 0 y 1 por (a, b, c).</summary>
        public static double Hash(double a, double b, double c)
        {
            unchecked
            {
                int h = AInt32(a) * 374761393 ^ AInt32(b) * 668265263 ^ AInt32(c) * 2147483647;
                h = (h ^ (int)((uint)h >> 13)) * 1274126177;
                return (uint)(h ^ (int)((uint)h >> 16)) / 4294967296.0;
            }
        }

        /// <summary>El `azar` de maquetas.ts: como `Hash`, mejor mezclado.</summary>
        public static double Azar(double a, double b, double c)
        {
            unchecked
            {
                int h = AInt32(a) ^ AInt32(b) * (int)0x9e3779b1 ^ AInt32(c) * (int)0x85ebca77;
                for (int k = 0; k < 2; k++)
                {
                    h = (h ^ (int)((uint)h >> 16)) * 0x7feb352d;
                    h = (h ^ (int)((uint)h >> 15)) * (int)0x846ca68b;
                }
                return (uint)(h ^ (int)((uint)h >> 16)) / 4294967296.0;
            }
        }

        /// <summary>La semilla de una maqueta por su sitio.</summary>
        public static double Semilla(double x, double y) => Redondear(x * 7 + y * 13);

        // ---------- huellas.ts ----------
        const double M0 = Maqueta, P0 = Maqueta * PuebloF, A0 = Maqueta * AvanzadaF;
        public static double RadioDePlaza(double radioCiudad, double nivel) => radioCiudad * M0 * (nivel <= 1 ? 0.62 : nivel == 2 ? 0.8 : 1);
        public static double RadioDePueblo() => 0.58 * P0;
        public static double RadioDeCampos() => 1.4 * P0;
        public static double RadioDeAvanzada(double etapa) => (etapa <= 1 ? 0.3 : 0.5) * A0;
        public static double RadioDeObra() => 0.4 * M0;
        public static double RadioDeCampamento() => 0.63 * M0 * Campamento;
        public static double RadioDeBandidos() => 0.48 * M0 * Campamento;
        public static double RadioDeAlijo() => 0.14 * M0 * AlijoF;
        public const double RadioDeBatalla = 0.65;

        /// <summary>Los claros sin árboles de `Maquetas.actualizar` (el radio, en u).</summary>
        public static double ClaroDePlaza(double rc, double nivel) => RadioDePlaza(rc, nivel) + 0.3;
        public static double ClaroDePueblo(bool aldea) => aldea ? 1.45 * PuebloF * Maqueta : RadioDePueblo() + 0.3;
        public static double ClaroDeAvanzada(double etapa) => RadioDeAvanzada(etapa) + 0.3;
        public static double ClaroDeObra() => RadioDeObra() + 0.25;
        public static double ClaroDeCampamento() => RadioDeCampamento() + 0.3;
        public static double ClaroDeBandidos() => RadioDeBandidos() + 0.25;

        // ---------- maquetas.ts ----------

        static void Torre(Piezas p, string mat, double x, double y, double lado, double alto, double giro = 0)
        {
            p.Caja(mat, x, y, lado, lado, alto, giro);
            p.Caja(mat, x, y, lado * 1.25, lado * 1.25, lado * 0.3, giro, alto);
        }

        static void TorresEnAnillo(Piezas p, string mat, double x, double y, double r, int n, double lado, double alto)
        {
            for (int k = 0; k < n; k++)
            {
                double a = (k + 0.5) / n * 2 * Math.PI;
                Torre(p, mat, x + r * Math.Cos(a), y + r * Math.Sin(a), lado, alto, a);
            }
        }

        struct Hueco { public double X, Y, R; }

        static void Casas(Piezas p, double x, double y, int n, double desde, double hasta, double tam, double sem, double techo, double giro = 0, Hueco[] huecos = null)
        {
            huecos ??= new Hueco[0];
            double area = Math.PI * hasta * hasta - 4 * desde * desde;
            double L = Math.Max(0.25 * tam, Math.Min(0.29 * tam, Math.Sqrt(Math.Max(0, area) * 0.8 / Math.Max(1, n)))), calle = 0.06 * tam, cg = Math.Cos(giro), sg = Math.Sin(giro);
            var solares = new List<(double u, double v, int i, int j, double orden)>();
            int max = (int)Math.Ceiling(hasta / L) + 1;
            for (int i = -max; i < max; i++)
                for (int j = -max; j < max; j++)
                {
                    double u = Math.Sign(i + 0.5) * (calle + (Math.Abs(i + 0.5) - 0.5) * L + L / 2), v = Math.Sign(j + 0.5) * (calle + (Math.Abs(j + 0.5) - 0.5) * L + L / 2);
                    double d = Hypot(u, v);
                    if (d + L * 0.45 > hasta) continue;
                    if (Math.Abs(u) - L / 2 < desde && Math.Abs(v) - L / 2 < desde) continue;
                    double wx = x + u * cg - v * sg, wy = y + u * sg + v * cg;
                    if (huecos.Any(h => Hypot(wx - h.X, wy - h.Y) < h.R + L * 0.5)) continue;
                    solares.Add((u, v, i, j, d / L + 1.6 * Azar(sem, i * 131 + j, 7)));
                }
            // (el `sort` de JavaScript es estable: OrderBy también)
            foreach (var s in solares.OrderBy(s => s.orden).Take(n))
            {
                int k = s.i * 131 + s.j;
                double ancho = (0.16 + 0.06 * Azar(sem, k, 3)) * tam, fondo = (0.15 + 0.05 * Azar(sem, k, 4)) * tam;
                double holgura = Math.Max(0, L - Math.Max(ancho, fondo)) * 0.6;
                double u = s.u + (Azar(sem, k, 8) - 0.5) * holgura, v = s.v + (Azar(sem, k, 9) - 0.5) * holgura;
                p.Caja(Azar(sem, k, 6) < techo ? "techo" : "adobe", x + u * cg - v * sg, y + u * sg + v * cg, ancho, fondo, (0.07 + 0.06 * Azar(sem, k, 5)) * tam,
                    giro + (Azar(sem, k, 10) - 0.5) * 0.12);
            }
        }

        /// <summary>La ciudad del juego, distinta en cada nivel. `muralla`: null sin saberlo (la plaza recordada), lo del nivel.</summary>
        public static void Ciudad(Piezas p, double x, double y, double rc, double nivel, string color, double sem, bool? muralla)
        {
            var antes = p.Materiales.ToDictionary(m => m, m => p.Por[m].Count);
            var banderas = p.Colores.ToDictionary(c => c, c => p.Banderas[c].Count);
            CiudadPlana(p, x, y, rc, nivel, color, sem, muralla);
            const double H = AltoPlaza;
            foreach (var mat in p.Materiales)
            {
                var l = p.Por[mat];
                for (int k = antes.TryGetValue(mat, out var a) ? a : 0; k < l.Count; k++) { l[k].Alto *= H; l[k].Base *= H; }
            }
            foreach (var c in p.Colores)
            {
                var l = p.Banderas[c];
                for (int k = banderas.TryGetValue(c, out var a) ? a : 0; k < l.Count; k++) l[k].Base *= H;
            }
        }

        static void CiudadPlana(Piezas p, double x, double y, double rc, double nivel, string color, double sem, bool? muralla)
        {
            double r = rc * Maqueta, M = Maqueta * Math.Max(1, rc / 2);
            int n = (int)Math.Max(1, Math.Min(5, Redondear(nivel)));
            const int cuantas = 400;
            double borde = Math.Max(0.3 * M, RadioDePlaza(rc, n) - 0.05 * M);
            double giro = Azar(sem, 0, 11) * Math.PI / 2, cg = Math.Cos(giro), sg = Math.Sin(giro);
            (double, double) En(double u, double v) => (x + u * M * cg - v * M * sg, y + u * M * sg + v * M * cg);
            if (n <= 2 && muralla == true) p.Anillo(x, y, RadioDePlaza(rc, n), 0.07 * M, 0.025 * M, "madera");
            if (n == 1)
            {
                var (jx, jy) = En(0, -0.2);
                var (px, py) = En(0.05, 0.05);
                var (cx, cy) = En(-0.55, -0.55);
                p.Caja("techo", jx, jy, 0.2 * M, 0.14 * M, 0.11 * M, giro);
                p.Caja("muro", px, py, 0.05 * M, 0.05 * M, 0.035 * M, giro);
                p.Anillo(cx, cy, 0.12 * M, 0.04 * M, 0.015 * M, "madera", -1);
                Casas(p, x, y, cuantas, 0.3 * M, borde, M * 0.85, sem, 0.15, giro, new[] { new Hueco { X = cx, Y = cy, R = 0.13 * M } });
                var (bx, by) = En(0.1, -0.14);
                p.Bandera(bx, by, 0.4 * M, color);
                return;
            }
            if (n == 2)
            {
                var (hx, hy) = En(0, -0.32);
                var (gx, gy) = En(-0.3, 0.3);
                var (px, py) = En(0.08, 0.08);
                p.Caja("adobe", hx, hy, 0.3 * M, 0.2 * M, 0.16 * M, giro);
                p.Caja("techo", hx, hy, 0.32 * M, 0.22 * M, 0.03 * M, giro, 0.16 * M);
                foreach (var dv in new[] { -0.13, 0.13 }) { var (ax, ay) = En(dv * 1.6, -0.32); p.Caja("adobe", ax, ay, 0.06 * M, 0.22 * M, 0.07 * M, giro); }
                p.Caja("madera", gx, gy, 0.12 * M, 0.12 * M, 0.14 * M, giro);
                p.Caja("techo", gx, gy, 0.14 * M, 0.14 * M, 0.025 * M, giro, 0.14 * M);
                p.Caja("muro", px, py, 0.05 * M, 0.05 * M, 0.035 * M, giro);
                Casas(p, x, y, cuantas, 0.48 * M, borde, M, sem, 0.3, giro);
                var (bx, by) = En(0.14, -0.2);
                p.Bandera(bx, by, 0.5 * M, color);
                return;
            }
            double alto = new[] { 0.14, 0.18, 0.22 }[n - 3] * M, grueso = new[] { 0.05, 0.065, 0.08 }[n - 3] * M;
            bool amurallada = muralla != false;
            if (amurallada)
            {
                p.Anillo(x, y, r, alto, grueso, "muro");
                int seg = Math.Max(14, (int)Redondear(2 * Math.PI * r / 0.25));
                var lados = new[] { 7, 8 }.Select(k => { double a = (double)k / seg * 2 * Math.PI; return (a, x: x + r * Math.Cos(a), y: y + r * Math.Sin(a)); }).ToArray();
                foreach (var t in lados)
                {
                    Torre(p, "muro", t.x, t.y, grueso * 2, alto * 1.35, t.a);
                    p.Bandera(t.x, t.y, 0.2 * M, color, alto * 1.35 + grueso * 0.6);
                }
                var (t0, t1) = (lados[0], lados[1]);
                p.Caja("madera", (t0.x + t1.x) / 2, (t0.y + t1.y) / 2, Hypot(t1.x - t0.x, t1.y - t0.y), grueso * 1.1, alto * 0.22, Math.Atan2(t1.y - t0.y, t1.x - t0.x), alto * 0.78);
                if (n >= 4) TorresEnAnillo(p, "muro", x, y, r, Math.Max(6, (int)Redondear(r / (0.45 * M))), grueso * 1.9, alto * 1.3);
            }
            if (n == 3)
            {
                p.Caja("muro", x, y, 0.34 * M, 0.26 * M, 0.22 * M);
                p.Caja("techo", x + 0.32 * M, y - 0.12 * M, 0.16 * M, 0.24 * M, 0.16 * M);
            }
            else if (n == 4)
            {
                p.Caja("muro", x, y, 0.4 * M, 0.3 * M, 0.18 * M);
                p.Caja("muro", x, y, 0.26 * M, 0.2 * M, 0.14 * M, 0, 0.18 * M);
                p.Caja("muro", x + 0.38 * M, y - 0.12 * M, 0.24 * M, 0.32 * M, 0.03 * M);
                foreach (var dx in new[] { -0.08, 0.08 })
                    foreach (var dy in new[] { -0.12, -0.04, 0.04, 0.12 })
                        p.Caja("filo", x + 0.38 * M + dx * M, y - 0.12 * M + dy * M, 0.025 * M, 0.025 * M, 0.14 * M, 0, 0.03 * M);
                p.Caja("techo", x + 0.38 * M, y - 0.12 * M, 0.24 * M, 0.32 * M, 0.04 * M, 0, 0.17 * M);
            }
            else
            {
                double ri0 = Math.Min(0.75 * M, r * 0.45);
                p.Anillo(x, y, ri0, alto * 0.85, grueso * 0.8, "muro", 3);
                TorresEnAnillo(p, "muro", x, y, ri0, 4, grueso * 1.7, alto * 1.15);
                for (int k = 0; k < 4; k++) p.Caja("muro", x, y, (0.42 - 0.09 * k) * M, (0.42 - 0.09 * k) * M, 0.09 * M, 0, k * 0.09 * M);
                p.Caja("techo", x, y, 0.1 * M, 0.1 * M, 0.08 * M, 0, 0.36 * M);
                double tx = x + (ri0 + 0.35 * M), ty = y + 0.2 * M;
                p.Caja("muro", tx, ty, 0.26 * M, 0.34 * M, 0.03 * M);
                foreach (var dx in new[] { -0.09, 0.09 })
                    foreach (var dy in new[] { -0.13, -0.045, 0.045, 0.13 })
                        p.Caja("filo", tx + dx * M, ty + dy * M, 0.025 * M, 0.025 * M, 0.15 * M, 0, 0.03 * M);
                p.Caja("techo", tx, ty, 0.26 * M, 0.34 * M, 0.045 * M, 0, 0.18 * M);
            }
            double ri = Math.Min(0.75 * M, r * 0.45);
            var huecos = n == 3 ? new[] { new Hueco { X = x + 0.32 * M, Y = y - 0.12 * M, R = 0.16 * M } }
                : n == 4 ? new[] { new Hueco { X = x + 0.38 * M, Y = y - 0.12 * M, R = 0.22 * M } }
                : new[] { new Hueco { X = x, Y = y, R = ri + 0.04 * M }, new Hueco { X = x + ri + 0.35 * M, Y = y + 0.2 * M, R = 0.24 * M } };
            Casas(p, x, y, cuantas, n == 5 ? 0 : 0.3 * M, r - (amurallada ? grueso : 0) - 0.05 * M, M, sem, 0.25 + 0.1 * (n - 3), 0, huecos);
            p.Bandera(x + 0.12 * M, y + 0.1 * M, (n == 5 ? 0.7 : 0.55) * M, color, n == 5 ? 0.36 * M : n == 4 ? 0.32 * M : 0.22 * M);
        }

        /// <summary>Un pueblo: empalizada y casas; la aldea con los campos de su cultivo, el extractor con su bocamina.</summary>
        public static void Pueblo(Piezas p, double x, double y, bool aldea, string color, double sem, string cultivo)
        {
            const double M = Maqueta * PuebloF;
            p.Anillo(x, y, 0.55 * M, 0.06 * M, 0.03 * M, "madera");
            for (int k = 0; k < (aldea ? 8 : 5); k++)
            {
                double a = Hash(sem, k, 1) * 2 * Math.PI, d = (0.12 + 0.33 * Math.Sqrt(Hash(sem, k, 2))) * M;
                p.Caja(Hash(sem, k, 6) < 0.5 ? "techo" : "adobe", x + d * Math.Cos(a), y + d * Math.Sin(a), (0.09 + 0.05 * Hash(sem, k, 3)) * M,
                    (0.09 + 0.05 * Hash(sem, k, 4)) * M, (0.06 + 0.04 * Hash(sem, k, 5)) * M, a);
            }
            if (aldea)
            {
                for (int k = 0; k < 5; k++)
                {
                    double a = k / 5.0 * 2 * Math.PI + Hash(sem, k, 7), d = (0.85 + 0.3 * Hash(sem, k, 8)) * M;
                    double cx = x + d * Math.Cos(a), cy = y + d * Math.Sin(a), ca = Math.Cos(a), sa = Math.Sin(a);
                    (double, double) En(double u, double v) => (cx + u * ca - v * sa, cy + u * sa + v * ca);
                    if (cultivo == "vid")
                    {
                        p.Caja("tierra", cx, cy, 0.42 * M, 0.3 * M, 0.012 * M, a);
                        for (int i = 0; i < 5; i++) { var (rx, ry) = En(0, (-0.12 + i * 0.06) * M); p.Caja("vid", rx, ry, 0.38 * M, 0.022 * M, 0.035 * M, a); }
                    }
                    else if (cultivo == "oliva")
                    {
                        p.Caja("rastrojo", cx, cy, 0.42 * M, 0.3 * M, 0.012 * M, a);
                        for (int j = 0; j < 3; j++)
                            for (int i = 0; i < 4; i++)
                            {
                                if (j == 1 && i == 3) continue;
                                var (ox, oy) = En((-0.15 + i * 0.1 + (j == 1 ? 0.05 : 0)) * M, (-0.09 + j * 0.09) * M);
                                p.Caja("olivo", ox, oy, 0.06 * M, 0.06 * M, 0.05 * M, a + i, 0.012 * M);
                            }
                    }
                    else if (cultivo == "cebada")
                    {
                        p.Caja("cebada", cx, cy, 0.42 * M, 0.3 * M, 0.018 * M, a);
                        for (int i = 0; i < 4; i++) { var (rx, ry) = En(0, (-0.105 + i * 0.07) * M); p.Caja("cebadaVerde", rx, ry, 0.38 * M, 0.02 * M, 0.024 * M, a); }
                        var (hx, hy) = En(0.15 * M, 0.1 * M);
                        p.Caja("cebada", hx, hy, 0.05 * M, 0.05 * M, 0.05 * M, a + 0.7, 0.018 * M);
                    }
                    else if (cultivo == "sesamo")
                    {
                        p.Caja("tierra", cx, cy, 0.42 * M, 0.3 * M, 0.012 * M, a);
                        for (int i = 0; i < 4; i++)
                        {
                            var (rx, ry) = En(0, (-0.105 + i * 0.07) * M);
                            p.Caja("sesamo", rx, ry, 0.38 * M, 0.026 * M, 0.05 * M, a);
                            for (int q = 0; q < 3; q++)
                            {
                                var (fx, fy) = En((-0.12 + q * 0.12 + (i % 2) * 0.05) * M, (-0.105 + i * 0.07) * M);
                                p.Caja("florSesamo", fx, fy, 0.03 * M, 0.03 * M, 0.012 * M, a, 0.05 * M);
                            }
                        }
                    }
                    else if (cultivo == "ganado")
                    {
                        p.Caja("pasto", cx, cy, 0.38 * M, 0.38 * M, 0.01 * M, a);
                        p.Anillo(cx, cy, 0.2 * M, 0.04 * M, 0.012 * M, "madera", 3);
                        for (int i = 0; i < 4; i++)
                        {
                            var (rx, ry) = En((-0.1 + (i % 2) * 0.13 + 0.03 * Hash(sem, k, 11 + i)) * M, (-0.06 + Math.Floor(i / 2.0) * 0.11) * M);
                            p.Caja(i == 3 ? "oveja" : "res", rx, ry, 0.075 * M, 0.035 * M, 0.035 * M, a + 2 * Hash(sem, k, 15 + i), 0.02 * M);
                        }
                    }
                    else p.Caja("campo", cx, cy, 0.42 * M, 0.3 * M, 0.015 * M, a);
                }
            }
            else p.Caja("mina", x + 0.2 * M, y - 0.15 * M, 0.2 * M, 0.16 * M, 0.08 * M, Hash(sem, 0, 9));
            p.Bandera(x - 0.1 * M, y + 0.05 * M, 0.4 * M, color);
        }

        /// <summary>Una avanzada: atalaya (1), fuerte (2) o ciudadela (3); en ruinas, baja y gris, sin bandera.</summary>
        public static void Avanzada(Piezas p, double x, double y, double etapa, bool ruinas, string color)
        {
            const double M = Maqueta * AvanzadaF;
            double e = Math.Max(1, Math.Min(3, etapa)), b = ruinas ? 0.4 : 1;
            string Mat(string m) => ruinas ? "ruina" : m;
            double cima;
            if (e == 1)
            {
                double alto = 0.5 * M * b;
                foreach (var (dx, dy) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) }) p.Caja(Mat("madera"), x + dx * 0.06 * M, y + dy * 0.06 * M, 0.025 * M, 0.025 * M, alto);
                p.Caja(Mat("madera"), x, y, 0.18 * M, 0.18 * M, 0.025 * M, 0, alto);
                if (!ruinas) p.Caja("techo", x, y, 0.2 * M, 0.2 * M, 0.035 * M, 0, alto + 0.12 * M);
                p.Anillo(x, y, 0.26 * M, 0.05 * M * b, 0.02 * M, Mat("madera"), 3);
                p.Caja(Mat("adobe"), x + 0.15 * M, y + 0.1 * M, 0.08 * M, 0.07 * M, 0.06 * M);
                cima = alto + 0.15 * M;
            }
            else if (e == 2)
            {
                double l = 0.32 * M, alto = 0.12 * M * b, g = 0.035 * M;
                foreach (var (dx, dy, gi) in new[] { (0.0, -1.0, 0.0), (0.0, 1.0, 0.0), (-1.0, 0.0, Math.PI / 2), (1.0, 0.0, Math.PI / 2) })
                {
                    if (dy == 1)
                    {
                        p.Caja(Mat("muro"), x - l * 0.6, y + l, l * 0.8, g, alto, gi);
                        p.Caja(Mat("muro"), x + l * 0.6, y + l, l * 0.8, g, alto, gi);
                    }
                    else p.Caja(Mat("muro"), x + dx * l, y + dy * l, 2 * l, g, alto, gi);
                }
                foreach (var (dx, dy) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) }) Torre(p, Mat("muro"), x + dx * l, y + dy * l, 0.08 * M, alto * 1.5);
                double homenaje = 0.45 * M * b;
                Torre(p, Mat("muro"), x, y - 0.06 * M, 0.15 * M, homenaje);
                p.Caja(Mat("techo"), x + 0.14 * M, y + 0.12 * M, 0.12 * M, 0.08 * M, 0.06 * M);
                cima = homenaje + 0.05 * M;
            }
            else
            {
                double r = 0.45 * M, alto = 0.16 * M * b, g = 0.045 * M;
                p.Anillo(x, y, r, alto, g, Mat("muro"), 3);
                TorresEnAnillo(p, Mat("muro"), x, y, r, 6, 0.09 * M, alto * 1.4);
                double mayor = 0.62 * M * b;
                Torre(p, Mat("muro"), x, y, 0.18 * M, mayor);
                p.Caja(Mat("muro"), x, y, 0.28 * M, 0.28 * M, 0.1 * M * b);
                p.Caja(Mat("techo"), x - 0.24 * M, y + 0.06 * M, 0.1 * M, 0.22 * M, 0.07 * M, 0.3);
                p.Caja(Mat("madera"), x + 0.22 * M, y - 0.12 * M, 0.12 * M, 0.12 * M, 0.08 * M, -0.4);
                cima = mayor + 0.055 * M;
            }
            if (!ruinas) p.Bandera(x, y, 0.14 * M, color, cima);
        }

        /// <summary>Una obra: andamio y el montón de material; arrancada, con los muros a media altura.</summary>
        public static void Obra(Piezas p, double x, double y, bool arrancada, string color)
        {
            const double M = Maqueta;
            foreach (var (dx, dy) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) }) p.Caja("madera", x + dx * 0.15 * M, y + dy * 0.15 * M, 0.02 * M, 0.02 * M, 0.35 * M);
            p.Caja("adobe", x + 0.05 * M, y + 0.28 * M, 0.22 * M, 0.18 * M, 0.06 * M);
            if (arrancada) p.Caja("adobe", x, y, 0.26 * M, 0.26 * M, 0.12 * M);
            p.Bandera(x - 0.15 * M, y - 0.15 * M, 0.15 * M, color, 0.35 * M - 0.15 * M);
        }

        /// <summary>Un campamento de mercenarios: empalizada, tiendas en corro y el asta en medio.</summary>
        public static void CampamentoDeMercenarios(Piezas p, double x, double y, double sem)
        {
            const double M = Maqueta * Campamento;
            p.Anillo(x, y, 0.6 * M, 0.07 * M, 0.03 * M, "madera");
            for (int k = 0; k < 10; k++)
            {
                double a = k / 10.0 * 2 * Math.PI + Hash(sem, k, 1) * 0.4, d = (0.25 + 0.2 * Hash(sem, k, 2)) * M;
                p.Add("lona", new Pieza { X = x + d * Math.Cos(a), Y = y + d * Math.Sin(a), Ancho = 0.18 * M, Fondo = 0.18 * M, Alto = 0.14 * M, Giro = a });
            }
            p.Bandera(x, y, 0.5 * M, ColorMercenarios);
        }

        /// <summary>Un campamento de bandidos: tiendas oscuras en corro (más cuanto más nivel), la hoguera y su bandera roja.</summary>
        public static void Bandidos(Piezas p, double x, double y, double nivel, double sem)
        {
            const double M = Maqueta * Campamento;
            int n = 2 + 2 * (int)Math.Max(1, Math.Min(3, nivel));
            for (int k = 0; k < n; k++)
            {
                double a = (double)k / n * 2 * Math.PI + Hash(sem, k, 1) * 0.5, d = (0.3 + 0.12 * Hash(sem, k, 2)) * M;
                p.Add("lonaOscura", new Pieza { X = x + d * Math.Cos(a), Y = y + d * Math.Sin(a), Ancho = 0.16 * M, Fondo = 0.16 * M, Alto = 0.12 * M, Giro = a });
            }
            p.Caja("fuego", x, y, 0.07 * M, 0.07 * M, 0.04 * M, 0.4);
            p.Bandera(x + 0.1 * M, y - 0.12 * M, 0.42 * M, ColorBandidos);
        }

        /// <summary>Un alijo: fardos y un cofre con el oro encima.</summary>
        public static void Alijo(Piezas p, double x, double y)
        {
            const double M = Maqueta * AlijoF;
            p.Caja("adobe", x - 0.06 * M, y, 0.1 * M, 0.08 * M, 0.06 * M, 0.3);
            p.Caja("madera", x + 0.05 * M, y + 0.03 * M, 0.09 * M, 0.07 * M, 0.07 * M, -0.2);
            p.Caja("oro", x + 0.05 * M, y + 0.03 * M, 0.06 * M, 0.05 * M, 0.025 * M, -0.2, 0.07 * M);
        }

        /// <summary>Una batalla: dos espadas cruzadas sobre un aro, verde si combates en ella.</summary>
        public static void Batalla(Piezas p, double x, double y, bool propia)
        {
            p.Anillo(x, y, 0.6, 0.03, 0.06, propia ? "batallaPropia" : "batallaAjena", -1);
            foreach (var lado in new[] { -1, 1 })
                p.Add("filo", new Pieza { X = x + lado * 0.17, Y = y, Ancho = 0.035, Fondo = 0.012, Alto = 0.55, Giro = 0, Base = 0.15, Inclina = lado * 0.6 });
        }
    }
}

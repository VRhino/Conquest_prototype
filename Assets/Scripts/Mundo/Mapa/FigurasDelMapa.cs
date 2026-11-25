// Las figuras de bloques de las columnas, las caravanas y los Aedas en el mapa (tema 7): las piezas de `figuras.ts` del prototipo (cajas; los
// cilindros del escudo y las ruedas, como cajas de su tamaño), en unidades de figura (×`ColumnasDelMapa.Tropa` u), y su sitio por el camino de
// la cabeza (`ColumnasDelMapa.Colocar`), pegadas al suelo y giradas por su tramo. Se pintan instanciadas con las cajas de `CosasEnElMapa`, en
// lugar de la caja de cada cosa; luego quedan como el nivel de detalle lejano de las figuras con su modelo.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ConquestTactics.Mundo.Mapa
{
    public static class FigurasDelMapa
    {
        /// <summary>El color de la Facción (la túnica, el penacho y el estandarte).</summary>
        const uint Faccion = 0xffffffff;
        const uint Pelaje = 0x6b4a2e, Blanco = 0xd8cfbd, Buey = 0x8c7a64, Camello = 0xc2a070, Arpillera = 0xb89a6a, Bronce = 0xc08a3e, Piel = 0xc89a78,
            Madera = 0x8a6440, Lana = 0xe9dcc0, Ocre = 0xa4452c, Ceniza = 0x8f877a;

        struct Pieza
        {
            public Matrix4x4 M;
            public uint Color;
        }

        /// <summary>Una caja de `figuras.ts`: ancho, alto y fondo, y su centro (x a un lado, y arriba, z hacia delante); `rx`, girada sobre x.</summary>
        static Pieza P(float w, float h, float d, float x, float y, float z, uint color, float rx = 0) =>
            new Pieza { M = Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(rx * Mathf.Rad2Deg, 0, 0), new Vector3(w, h, d)), Color = color };

        static readonly Pieza[] Soldado =
        {
            P(0.055f, 0.1f, 0.045f, 0, 0.05f, 0, Faccion), P(0.036f, 0.036f, 0.036f, 0, 0.118f, 0, Piel), P(0.008f, 0.24f, 0.008f, 0.034f, 0.11f, 0, Madera),
            P(0.008f, 0.064f, 0.064f, -0.034f, 0.06f, 0, Bronce),
        };
        static readonly Pieza[] Heroe =
        {
            P(0.05f, 0.055f, 0.16f, 0, 0.085f, 0, Pelaje), P(0.035f, 0.07f, 0.04f, 0, 0.13f, 0.075f, Pelaje, -0.5f), P(0.04f, 0.06f, 0.12f, 0, 0.03f, 0, Pelaje),
            P(0.045f, 0.07f, 0.035f, 0, 0.145f, -0.01f, Bronce), P(0.032f, 0.034f, 0.032f, 0, 0.197f, -0.01f, Bronce), P(0.008f, 0.03f, 0.05f, 0, 0.225f, -0.015f, Faccion),
        };
        static readonly Pieza[] Aeda =
        {
            P(0.056f, 0.11f, 0.046f, 0, 0.055f, 0, Lana), P(0.058f, 0.014f, 0.048f, 0, 0.078f, 0, Ocre), P(0.034f, 0.034f, 0.034f, 0, 0.127f, 0.002f, Piel),
            P(0.042f, 0.036f, 0.016f, 0, 0.13f, -0.022f, Ocre), P(0.007f, 0.21f, 0.007f, 0.038f, 0.105f, 0.014f, Madera),
            P(0.007f, 0.07f, 0.007f, -0.05f, 0.085f, -0.016f, Bronce), P(0.007f, 0.07f, 0.007f, -0.05f, 0.085f, 0.016f, Bronce),
            P(0.007f, 0.007f, 0.046f, -0.05f, 0.12f, 0, Bronce), P(0.012f, 0.016f, 0.036f, -0.05f, 0.05f, 0, Madera),
        };
        static readonly Pieza[] Burro =
        {
            P(0.036f, 0.04f, 0.09f, 0, 0.055f, 0, Ceniza), P(0.03f, 0.04f, 0.075f, 0, 0.02f, 0, Ceniza), P(0.022f, 0.045f, 0.03f, 0, 0.088f, 0.055f, Ceniza, -0.5f),
            P(0.026f, 0.02f, 0.006f, 0, 0.118f, 0.06f, Ceniza), P(0.058f, 0.028f, 0.045f, 0, 0.068f, -0.008f, Arpillera),
        };
        static readonly Pieza[] Carro =
        {
            P(0.1f, 0.05f, 0.16f, 0, 0.075f, 0, Madera), P(0.008f, 0.008f, 0.08f, 0, 0.05f, 0.11f, Madera),
            P(0.012f, 0.064f, 0.064f, -0.058f, 0.032f, -0.05f, Madera), P(0.012f, 0.064f, 0.064f, 0.058f, 0.032f, -0.05f, Madera),
            P(0.012f, 0.064f, 0.064f, -0.058f, 0.032f, 0.05f, Madera), P(0.012f, 0.064f, 0.064f, 0.058f, 0.032f, 0.05f, Madera),
        };
        static readonly Pieza Lona = P(0.1f, 0.06f, 0.13f, 0, 0.13f, -0.01f, Blanco);
        static readonly Pieza[] TiroBuey =
        {
            P(0.05f, 0.05f, 0.11f, 0, 0.055f, 0.19f, Buey), P(0.035f, 0.035f, 0.04f, 0, 0.075f, 0.26f, Buey), P(0.064f, 0.008f, 0.008f, 0, 0.097f, 0.262f, Blanco),
        };
        static readonly Pieza[] TiroCaballo =
        {
            P(0.04f, 0.045f, 0.12f, 0, 0.075f, 0.2f, Pelaje), P(0.034f, 0.055f, 0.1f, 0, 0.027f, 0.2f, Pelaje), P(0.028f, 0.06f, 0.032f, 0, 0.115f, 0.265f, Pelaje, -0.6f),
            P(0.024f, 0.024f, 0.05f, 0, 0.14f, 0.295f, Pelaje),
        };
        static readonly Pieza[] TiroCamello =
        {
            P(0.05f, 0.05f, 0.13f, 0, 0.095f, 0.2f, Camello), P(0.036f, 0.032f, 0.05f, 0, 0.134f, 0.19f, Camello), P(0.04f, 0.07f, 0.11f, 0, 0.035f, 0.2f, Camello),
            P(0.022f, 0.075f, 0.024f, 0, 0.13f, 0.28f, Camello, -0.45f), P(0.024f, 0.022f, 0.042f, 0, 0.165f, 0.3f, Camello),
        };
        static readonly Pieza[] Carrito =
        {
            P(0.07f, 0.035f, 0.085f, 0, 0.05f, 0, Madera), P(0.056f, 0.026f, 0.065f, 0, 0.08f, -0.004f, Arpillera),
            P(0.006f, 0.006f, 0.09f, -0.026f, 0.045f, 0.085f, Madera), P(0.006f, 0.006f, 0.09f, 0.026f, 0.045f, 0.085f, Madera),
            P(0.01f, 0.056f, 0.056f, -0.042f, 0.028f, -0.01f, Madera), P(0.01f, 0.056f, 0.056f, 0.042f, 0.028f, -0.01f, Madera),
        };

        static readonly Dictionary<uint, Vector4> Lineales = new Dictionary<uint, Vector4>();

        static Vector4 Lineal(uint c)
        {
            if (!Lineales.TryGetValue(c, out var v)) Lineales[c] = v = CosasEnElMapa.Lineal(c);
            return v;
        }

        static readonly Dictionary<string, (double x, double y, float t, double v)> cabezas = new Dictionary<string, (double, double, float, double)>();

        /// <summary>Lo que anda de verdad la cabeza de `id` (u por segundo de reloj, suavizado): el paso de sus figuras va con lo que se ve, sea
        /// cual sea el ritmo del mundo.</summary>
        /// <summary>Lo último medido de la cabeza de `id`: dónde y a qué velocidad (u/s), para las pruebas.</summary>
        public static (double x, double y, double v) CabezaMedida(string id) => cabezas.TryGetValue(id, out var c) ? (c.x, c.y, c.v) : (double.NaN, double.NaN, double.NaN);

        static double Velocidad(string id, double x, double y)
        {
            float t = Time.time;
            if (!cabezas.TryGetValue(id, out var c) || t - c.t > 2) { cabezas[id] = (x, y, t, 0); return 0; }
            if (t - c.t < 0.05f) return c.v;
            double v = Math.Sqrt((x - c.x) * (x - c.x) + (y - c.y) * (y - c.y)) / (t - c.t);
            v = c.v + (v - c.v) * 0.3;
            cabezas[id] = (x, y, t, v);
            return v;
        }

        // ------------------------------------------------------------------ cada tipo de figura, una malla (dos: lo fijo y lo de la Facción)

        /// <summary>El cubo con el que se hacen las mallas de las figuras (el de las cajas del mapa).</summary>
        public static Mesh Cubo;

        /// <summary>Una tanda de una malla: sus matrices y colores en arrays que se reutilizan (sin basura por fotograma).</summary>
        sealed class Tanda
        {
            public Mesh Malla;
            public bool DeFaccion;
            /// <summary>Se mece con el viento (el paño del estandarte: el alfa de sus vértices dice cuánto).</summary>
            public bool Mece;
            public Matrix4x4[] M = new Matrix4x4[256];
            public Vector4[] C = new Vector4[256];
            public int N;

            public void Poner(in Matrix4x4 m, in Vector4 c)
            {
                if (N == M.Length) { Array.Resize(ref M, N * 2); Array.Resize(ref C, N * 2); }
                M[N] = m;
                C[N++] = c;
            }
        }

        /// <summary>Por tipo de figura (con su animal y si lleva estandarte), la malla de lo fijo (los colores en los vértices) y la de lo que va del
        /// color de la Facción.</summary>
        static readonly Dictionary<int, (Tanda fija, Tanda faccion)> tandas = new Dictionary<int, (Tanda, Tanda)>();
        static readonly List<Tanda> todas = new List<Tanda>();

        static int Clave(in SitioDeFigura f) => (int)f.Figura * 16 + (int)f.Animal * 2;
        const int ClaveEstandarte = -1;

        static IEnumerable<Pieza> PiezasDe(in SitioDeFigura f)
        {
            switch (f.Figura)
            {
                case FiguraDeColumna.Soldado: return Soldado;
                case FiguraDeColumna.Heroe: return Heroe;
                case FiguraDeColumna.Aeda: return Aeda;
                case FiguraDeColumna.Burro: return Burro;
                case FiguraDeColumna.Carrito: return Carrito;
                default:
                    var tiro = f.Animal == AnimalDeTiro.Caballo ? TiroCaballo : f.Animal == AnimalDeTiro.Camello ? TiroCamello : TiroBuey;
                    var r = Carro.Concat(tiro);
                    return f.Figura == FiguraDeColumna.CarroDeFundacion ? r.Append(Lona) : r;
            }
        }

        static Mesh Juntar(IEnumerable<Pieza> piezas, bool deFaccion)
        {
            var vs = new List<Vector3>();
            var ns = new List<Vector3>();
            var cs = new List<Color>();
            var ts = new List<int>();
            var cv = Cubo.vertices;
            var cn = Cubo.normals;
            var ct = Cubo.triangles;
            foreach (var p in piezas)
            {
                if ((p.Color == Faccion) != deFaccion) continue;
                int b = vs.Count;
                var color = deFaccion ? Color.white : (Color)Lineal(p.Color);
                for (int i = 0; i < cv.Length; i++)
                {
                    vs.Add(p.M.MultiplyPoint3x4(cv[i]));
                    ns.Add(p.M.MultiplyVector(cn[i]).normalized);
                    cs.Add(color);
                }
                foreach (var t in ct) ts.Add(b + t);
            }
            if (vs.Count == 0) return null;
            var m = new Mesh { name = deFaccion ? "figura (Facción)" : "figura" };
            m.SetVertices(vs);
            m.SetNormals(ns);
            m.SetColors(cs);
            m.SetTriangles(ts, 0);
            m.RecalculateBounds();
            m.UploadMeshData(true);
            return m;
        }

        // ------------------------------------------------------------------ el estandarte, como pieza de juego

        /// <summary>Metros por unidad de figura (el soldado de bloques, 0,136 de alto, es un hombre de 1,7 m).</summary>
        const float MetrosPorFigura = 12.5f;
        /// <summary>Dónde va el asta respecto a quien lo lleva (en unidades de figura): pegada a su costado derecho, algo adelantada.</summary>
        const float EstandarteLado = 0.026f, EstandarteDelante = 0.006f;
        const uint MaderaOscura = 0x5e4128, Fleco = 0xc9a24a;

        /// <summary>El estandarte: una insignia de asta de la Edad del Bronce (como los estandartes de Alaca Höyük o las insignias egipcias sobre
        /// pértiga), no un paño que ondea. Asta de madera de 4,2 m con regatón; arriba, entre dos travesaños de bronce con su marco, un panel rígido de
        /// 1,2 × 1,44 m que lleva el blasón de la Facción de frente (`PanelEstandarte`, con la textura del sigilo); remate de disco de bronce encima;
        /// borlas cortas colgando del travesaño de abajo, lo único que se mece. En unidades de figura, con el asta en el origen, mirando a +z.
        /// `faccion`: solo las borlas (blancas, que tiñe el color de la Facción); si no, todo lo demás con sus colores. El alfa de los vértices es
        /// cuánto se mece.</summary>
        const float AltoAsta = 4.2f, PanelAbajo = 2.5f, PanelAncho = 1.2f, PanelAlto = 1.44f, PanelDelante = 0.04f;

        static Mesh Estandarte(bool faccion)
        {
            var vs = new List<Vector3>();
            var cs = new List<Color>();
            var ts = new List<int>();
            const float k = 1f / MetrosPorFigura;
            float arriba = PanelAbajo + PanelAlto, mitad = PanelAncho / 2;

            void Tri(Vector3 a, Vector3 b, Vector3 c, Color col)
            {
                int i = vs.Count;
                vs.Add(a * k); vs.Add(b * k); vs.Add(c * k);
                cs.Add(col); cs.Add(col); cs.Add(col);
                ts.Add(i); ts.Add(i + 1); ts.Add(i + 2);
            }
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col) { Tri(a, b, c, col); Tri(a, c, d, col); }
            Color Fijo(uint c) { var v = Lineal(c); return new Color(v.x, v.y, v.z, 0); }
            void Cilindro(Vector3 a, Vector3 b, float r, int lados, Color col, bool tapas = true)
            {
                var d = (b - a).normalized;
                var u = Vector3.Cross(d, Mathf.Abs(d.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                var w = Vector3.Cross(d, u);
                for (int i = 0; i < lados; i++)
                {
                    float t0 = i * 2 * Mathf.PI / lados, t1 = (i + 1) * 2 * Mathf.PI / lados;
                    var o0 = (u * Mathf.Cos(t0) + w * Mathf.Sin(t0)) * r;
                    var o1 = (u * Mathf.Cos(t1) + w * Mathf.Sin(t1)) * r;
                    Quad(a + o0, b + o0, b + o1, a + o1, col);
                    if (tapas) { Tri(b, b + o1, b + o0, col); Tri(a, a + o0, a + o1, col); }
                }
            }

            if (!faccion)
            {
                var madera = Fijo(MaderaOscura);
                var bronce = Fijo(Bronce);
                Cilindro(new Vector3(0, 0.05f, 0), new Vector3(0, AltoAsta, 0), 0.035f, 6, madera);
                Cilindro(new Vector3(0, 0f, 0), new Vector3(0, 0.14f, 0), 0.04f, 6, bronce);
                // los travesaños y el marco de bronce, delante del asta, abrazando el panel
                float z = PanelDelante;
                foreach (var y in new[] { PanelAbajo - 0.03f, arriba + 0.03f })
                {
                    Cilindro(new Vector3(-mitad - 0.1f, y, z), new Vector3(mitad + 0.1f, y, z), 0.03f, 6, bronce);
                    foreach (var sx in new[] { -1f, 1f })
                        Cilindro(new Vector3(sx * (mitad + 0.08f), y, z), new Vector3(sx * (mitad + 0.16f), y, z), 0.048f, 6, bronce);
                }
                foreach (var sx in new[] { -1f, 1f })
                    Cilindro(new Vector3(sx * mitad, PanelAbajo - 0.03f, z), new Vector3(sx * mitad, arriba + 0.03f, z), 0.018f, 4, bronce);
                // el remate: un cuello y un disco de bronce de cara
                Cilindro(new Vector3(0, AltoAsta - 0.02f, 0), new Vector3(0, AltoAsta + 0.1f, 0), 0.05f, 6, bronce);
                Cilindro(new Vector3(0, AltoAsta + 0.3f, -0.025f), new Vector3(0, AltoAsta + 0.3f, 0.025f), 0.2f, 12, bronce);
                Cilindro(new Vector3(0, AltoAsta + 0.3f, 0.024f), new Vector3(0, AltoAsta + 0.3f, 0.034f), 0.08f, 8, Fijo(MaderaOscura));
                // las borlas: cinco cordones cortos de lana dorada que cuelgan del travesaño de abajo (lo único que se mece)
                var lana = Fijo(Fleco);
                for (int i = 0; i < 5; i++)
                {
                    float x = -mitad + 0.12f + i * (PanelAncho - 0.24f) / 4;
                    float y0 = PanelAbajo - 0.06f, y1 = y0 - 0.16f;
                    var c0 = lana; c0.a = 0.15f;
                    var c1 = lana; c1.a = 1f;
                    int n0 = vs.Count;
                    Cilindro(new Vector3(x, y0, PanelDelante), new Vector3(x, y1, PanelDelante), 0.018f, 4, c0, tapas: false);
                    for (int q = n0; q < vs.Count; q++) if (vs[q].y < (y0 - 0.08f) * k) cs[q] = c1;
                    Cilindro(new Vector3(x, y1 + 0.02f, PanelDelante), new Vector3(x, y1 - 0.07f, PanelDelante), 0.038f, 5, c1);
                }
            }
            else
                return null;   // (sin parte del color de la Facción: el blasón ya lo lleva)
            var m = new Mesh { name = faccion ? "estandarte (borlas)" : "estandarte" };
            m.SetVertices(vs);
            m.SetColors(cs);
            m.SetTriangles(ts, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            m.UploadMeshData(true);
            return m;
        }

        static Mesh panel;

        /// <summary>El panel del blasón: un cuadro de 1,2 × 1,44 m (la proporción del sigilo, 100 × 120) con su textura (el blasón recorta su
        /// silueta por el alfa), por delante y por detrás.</summary>
        static Mesh PanelEstandarte()
        {
            if (panel != null) return panel;
            const float k = 1f / MetrosPorFigura;
            float x0 = -PanelAncho / 2 * k, x1 = PanelAncho / 2 * k, y0 = PanelAbajo * k, y1 = (PanelAbajo + PanelAlto) * k, z = PanelDelante * k;
            panel = new Mesh { name = "estandarte (panel)" };
            panel.SetVertices(new List<Vector3> { new Vector3(x0, y0, z), new Vector3(x1, y0, z), new Vector3(x1, y1, z), new Vector3(x0, y1, z),
                                                  new Vector3(x0, y0, z - 0.004f * k), new Vector3(x1, y0, z - 0.004f * k), new Vector3(x1, y1, z - 0.004f * k), new Vector3(x0, y1, z - 0.004f * k) });
            panel.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
                                                new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1) });
            panel.SetNormals(new List<Vector3> { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward, Vector3.back, Vector3.back, Vector3.back, Vector3.back });
            panel.SetColors(new List<Color> { Color.white, Color.white, Color.white, Color.white, Color.white, Color.white, Color.white, Color.white });
            panel.SetTriangles(new List<int> { 0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7 }, 0);
            panel.RecalculateBounds();
            panel.UploadMeshData(true);
            return panel;
        }

        // ------------------------------------------------------------------ el paño del estandarte
        /// <summary>Los paños de época entre los que se elige el de cada blasón: lino crudo para los blasones oscuros o de color fuerte, y pardo
        /// casi negro o añil muy oscuro para los claros (ninguno es un color de bando). Y su ribete.</summary>
        static readonly Color32 PanoLino = new Color32(0xCF, 0xC0, 0x98, 255), PanoPardo = new Color32(0x2B, 0x1F, 0x16, 255),
            PanoAnil = new Color32(0x1B, 0x21, 0x3A, 255), RibeteOscuro = new Color32(0x4A, 0x33, 0x1E, 255), RibeteClaro = new Color32(0xB8, 0x8A, 0x3C, 255);
        /// <summary>Con el blasón sobre su paño (si no, recortado por su silueta, como antes; para comparar).</summary>
        public static bool EstandarteConPano = true;
        /// <summary>Cuánto del ancho del paño ocupa el blasón.</summary>
        public const float BlasonEnPano = 0.8f;
        static readonly Dictionary<Texture, Texture> conPano = new Dictionary<Texture, Texture>();
        /// <summary>Lo que se eligió para cada blasón (luminancia de su borde y de la media, y el paño), para las pruebas.</summary>
        public static readonly Dictionary<Texture, string> PanoElegido = new Dictionary<Texture, string>();

        static float Luminancia(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        /// <summary>El blasón sobre su paño: el sigilo (recortado por su alfa) centrado y al `BlasonEnPano` del ancho, sobre un paño rígido que
        /// contrasta con él (con el color del borde exterior del blasón, que es el que lo toca, y con su luminancia media) y un ribete fino. Una
        /// textura por blasón, con el alto y el ancho del panel.</summary>
        static Texture ConPano(Texture sigilo)
        {
            if (conPano.TryGetValue(sigilo, out var c)) return c;
            if (!(sigilo is Texture2D s) || !s.isReadable) return conPano[sigilo] = sigilo;
            var px = s.GetPixels();
            int w = s.width, h = s.height;
            bool Opaco(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && px[y * w + x].a > 0.5f;
            Color borde = Color.clear, media = Color.clear;
            int nb = 0, nm = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!Opaco(x, y)) continue;
                    media += px[y * w + x]; nm++;
                    // el borde que se ve contra el paño: la franja de 4 a 10 px por dentro del contorno (el contorno de los sigilos es una línea
                    // oscura en todos; lo que decide el contraste es el color que va justo dentro)
                    bool dentro4 = Opaco(x - 4, y) && Opaco(x + 4, y) && Opaco(x, y - 4) && Opaco(x, y + 4);
                    bool cerca10 = !Opaco(x - 10, y) || !Opaco(x + 10, y) || !Opaco(x, y - 10) || !Opaco(x, y + 10);
                    if (dentro4 && cerca10) { borde += px[y * w + x]; nb++; }
                }
            if (nm == 0) return conPano[sigilo] = sigilo;
            borde = nb > 0 ? borde / nb : media / nm;
            media /= nm;
            // el paño que más contrasta: con el borde, sobre todo, y algo con la media
            Color mejor = PanoLino;
            float puntos = float.MinValue;
            foreach (Color p in new Color[] { PanoLino, PanoPardo, PanoAnil })
            {
                float d = Mathf.Abs(Luminancia(p) - Luminancia(borde)) + 0.35f * Mathf.Abs(Luminancia(p) - Luminancia(media))
                          + 0.15f * new Vector3(p.r - borde.r, p.g - borde.g, p.b - borde.b).magnitude;
                if (d > puntos) { puntos = d; mejor = p; }
            }
            Color ribete = Luminancia(mejor) > 0.4f ? (Color)RibeteOscuro : RibeteClaro;
            int W = 128, H = Mathf.RoundToInt(W * PanelAlto / PanelAncho), r = 3;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = sigilo.name + " (con paño)", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var o = new Color[W * H];
            // (el blasón con su proporción, la del lienzo del sigilo: 100 × 120)
            float lado = W * BlasonEnPano, alto = lado * s.height / s.width, x0 = (W - lado) / 2, y0 = (H - alto) / 2;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var col = x < r || y < r || x >= W - r || y >= H - r ? ribete : mejor;
                    float u = (x + 0.5f - x0) / lado, v = (y + 0.5f - y0) / alto;
                    if (u >= 0 && u <= 1 && v >= 0 && v <= 1)
                    {
                        var b = s.GetPixelBilinear(u, v);
                        col = Color.Lerp(col, new Color(b.r, b.g, b.b, 1), b.a);
                    }
                    col.a = 1;
                    o[y * W + x] = col;
                }
            t.SetPixels(o);
            t.Apply(true, false);
            PanoElegido[t] = $"borde {Luminancia(borde):0.00}, media {Luminancia(media):0.00}, paño {(mejor == (Color)PanoLino ? "lino" : mejor == (Color)PanoPardo ? "pardo" : "añil")}";
            return conPano[sigilo] = t;
        }

        /// <summary>Los paneles de este fotograma, por blasón (una llamada por Facción); y los de las Facciones sin sigilo, con su color liso.</summary>
        static readonly Dictionary<Texture, List<Matrix4x4>> paneles = new Dictionary<Texture, List<Matrix4x4>>();
        static readonly Dictionary<Texture, Material> materialesDePanel = new Dictionary<Texture, Material>();
        static Tanda panelesLisos;
        static Shader sombreadoPanel;
        /// <summary>El ancho (px) del sigilo que se pide para el panel.</summary>
        public static int PixelesSigilo = 128;
        /// <summary>El estandarte es un marcador: puede ir mayor que la escala real (1 = asta de 4,2 m y panel de 1,2 × 1,44 m).</summary>
        public static float EstandarteEscala = 1.5f;
        /// <summary>Hasta dónde llega el estandarte sobre el suelo (u), con su disco de remate: la etiqueta de la columna va por encima.</summary>
        public static double AltoDelEstandarte => ColumnasDelMapa.Tropa * EstandarteEscala * (AltoAsta + 0.5f) / MetrosPorFigura;

        static (Tanda fija, Tanda faccion) TandaDe(in SitioDeFigura f, bool estandarte = false)
        {
            int k = estandarte ? ClaveEstandarte : Clave(f);
            if (!tandas.TryGetValue(k, out var t))
            {
                Mesh a, b;
                if (estandarte) { a = Estandarte(false); b = Estandarte(true); }
                else
                {
                    var piezas = PiezasDe(f).ToList();
                    a = Juntar(piezas, false);
                    b = Juntar(piezas, true);
                }
                t = (a == null ? null : new Tanda { Malla = a, Mece = estandarte }, b == null ? null : new Tanda { Malla = b, DeFaccion = true, Mece = estandarte });
                if (t.fija != null) todas.Add(t.fija);
                if (t.faccion != null) todas.Add(t.faccion);
                tandas[k] = t;
            }
            return t;
        }

        static readonly Plane[] planos = new Plane[6];
        static int fotogramaPlanos = -1;

        /// <summary>¿Entra en cámara algo de la columna? (su cabeza con un radio de todo su largo, más un margen).</summary>
        static bool EnCamara(CosaDelMapa cosa, Func<double, double, float> altura)
        {
            var cam = ModelosDeFiguras.Camara;
            if (cam == null) return true;
            if (fotogramaPlanos != Time.frameCount) { GeometryUtility.CalculateFrustumPlanes(cam, planos); fotogramaPlanos = Time.frameCount; }
            var e = (float)EscalaDelMapa.MetrosPorUnidad;
            float r = ((float)ColumnasDelMapa.Largo(cosa.Figuras) + 1) * e;
            // (a la altura del suelo de su cabeza: el relieve sube cientos de metros)
            var c = EscalaDelMapa.AEscena(cosa.Camino[0].x, cosa.Camino[0].y, altura(cosa.Camino[0].x, cosa.Camino[0].y));
            return GeometryUtility.TestPlanesAABB(planos, new Bounds(c, new Vector3(2 * r, 2 * r + 200, 2 * r)));
        }

        /// <summary>Pone las figuras de `cosa` (con su modelo, si lo tiene y se ve grande; si no, de bloques) para este fotograma. `altura(x, y)`:
        /// el suelo visible (m).</summary>
        public static void Anadir(CosaDelMapa cosa, Func<double, double, float> altura, List<Matrix4x4> matrices, List<Vector4> colores)
        {
            if (cosa.Figuras == null || cosa.Camino == null || cosa.Camino.Count == 0) return;
            if (!EnCamara(cosa, altura)) return;
            var e = EscalaDelMapa.MetrosPorUnidad;
            var faccion = Lineal(cosa.Color);
            var blanco = new Vector4(1, 1, 1, 1);
            double velocidad = Velocidad(cosa.Id, cosa.Camino[0].x, cosa.Camino[0].y);
            float faseColumna = ModelosDeFiguras.FaseDeColumna(cosa.Id);
            // el color de bando de los modelos, por la relación con quien mira
            var equipo = cosa.Propia ? ModelosDeFiguras.Propio
                : cosa.Clase == ClaseDeCosa.Ejercito || cosa.Clase == ClaseDeCosa.Formacion || cosa.Clase == ClaseDeCosa.Caravana ? ModelosDeFiguras.Aliado
                : cosa.Clase == ClaseDeCosa.EjercitoAjeno || cosa.Clase == ClaseDeCosa.CaravanaAjena ? (string.IsNullOrEmpty(cosa.FaccionId) ? ModelosDeFiguras.Neutral : ModelosDeFiguras.Enemigo)
                : ModelosDeFiguras.Neutral;
            // dónde va cada figura (por el camino de la cabeza y a ras de suelo): se recalcula cada `cada` fotogramas (cada uno de cerca, cada dos a
            // media distancia, cada tres de lejos: la columna anda despacio y no se nota) y, entre medias, se reutiliza
            int cada = ModelosDeFiguras.PixelesSoldado >= ModelosDeFiguras.PixelesCompleto ? 1 : ModelosDeFiguras.PixelesSoldado >= ModelosDeFiguras.PixelesLejos ? 3 : 4;
            if (!sitios.TryGetValue(cosa.Id, out var st) || st.Figuras != cosa.Figuras || Time.frameCount - st.Fotograma >= cada)
            {
                if (st == null || st.Pie.Length < cosa.Figuras.Count)
                    sitios[cosa.Id] = st = new Sitios { Pie = new Vector3[cosa.Figuras.Count], Adelante = new Vector3[cosa.Figuras.Count], Giro = new Quaternion[cosa.Figuras.Count],
                                                        Figura = new Matrix4x4[cosa.Figuras.Count], Modelo = new Matrix4x4[cosa.Figuras.Count],
                                                        Tiras = new (TiraDeFiguras, TiraDeFiguras, TiraDeFiguras)[cosa.Figuras.Count] };

                st.Figuras = cosa.Figuras;
                st.Fotograma = Time.frameCount;
                double atrasAntes = double.NaN;
                (double x, double y, double rumbo) fila = default;
                for (int i = 0; i < cosa.Figuras.Count; i++)
                {
                    var f = cosa.Figuras[i];
                    // (las figuras de una misma fila comparten su sitio en el camino: se busca una vez por fila y cada una se aparta a su lado, como
                    // hace `Colocar`)
                    double atras = f.Atras * ColumnasDelMapa.Tropa, lado = f.Lado * 0.1 * ColumnasDelMapa.Tropa;
                    if (atras != atrasAntes) { fila = ColumnasDelMapa.Colocar(cosa.Camino, atras, 0); atrasAntes = atras; }
                    double rumbo = fila.rumbo, x = fila.x - Math.Sin(rumbo) * lado, y = fila.y + Math.Cos(rumbo) * lado;
                    var ad = new Vector3((float)Math.Cos(rumbo), 0, (float)-Math.Sin(rumbo));
                    st.Pie[i] = EscalaDelMapa.AEscena(x, y, altura(x, y));
                    st.Adelante[i] = ad;
                    st.Giro[i] = Quaternion.LookRotation(ad, Vector3.up);
                    st.Figura[i] = Matrix4x4.TRS(st.Pie[i], st.Giro[i], Vector3.one * (float)(ColumnasDelMapa.Tropa * e * f.Escala));
                    st.Modelo[i] = Matrix4x4.TRS(st.Pie[i], st.Giro[i], Vector3.one * ModelosDeFiguras.MetrosAEscena);
                    st.Tiras[i] = ModelosDeFiguras.TirasDe(f);   // (las que aún no están horneadas aparecen al recolocar, en pocos fotogramas)
                }
            }
            for (int i = 0; i < cosa.Figuras.Count; i++)
            {
                var f = cosa.Figuras[i];
                var adelante = st.Adelante[i];
                var pie = st.Pie[i];
                var giro = st.Giro[i];
                ref var figura = ref st.Figura[i];
                if (f.Estandarte)
                {
                    // pegado al costado derecho de quien lo lleva; el paño gira sobre el asta (solo en vertical) para dar la cara a la cámara
                    var escala = (float)(ColumnasDelMapa.Tropa * e * f.Escala);
                    var asta0 = pie + giro * (new Vector3(EstandarteLado, 0, EstandarteDelante) * escala);
                    escala *= EstandarteEscala;
                    var mira = ModelosDeFiguras.Camara != null ? ModelosDeFiguras.Camara.transform.position - asta0 : -adelante;
                    mira.y = 0;
                    var cara = mira.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(mira.normalized, Vector3.up) : giro;
                    var m = Matrix4x4.TRS(asta0, cara, Vector3.one * escala);
                    var (asta, borlas) = TandaDe(f, true);
                    asta?.Poner(m, blanco);
                    borlas?.Poner(m, faccion);
                    var sigilo = string.IsNullOrEmpty(cosa.FaccionId) ? null : CosasEnElMapa.SigiloDe?.Invoke(cosa.FaccionId, PixelesSigilo);
                    if (sigilo != null)
                    {
                        var tela = EstandarteConPano ? ConPano(sigilo) : sigilo;     // (el blasón sobre su paño de contraste)
                        if (!paneles.TryGetValue(tela, out var l)) paneles[tela] = l = new List<Matrix4x4>();
                        l.Add(m);
                    }
                    else
                    {
                        if (panelesLisos == null) { panelesLisos = new Tanda { Malla = PanelEstandarte(), DeFaccion = true }; todas.Add(panelesLisos); }
                        panelesLisos.Poner(m, string.IsNullOrEmpty(cosa.FaccionId) ? Lineal(0xE8E2D2) : faccion);   // (sin Facción: el paño neutral)
                    }
                }
                var tiras = st.Tiras[i];
                if (tiras.propia != null && ModelosDeFiguras.Apuntar(f, tiras.propia, tiras.media, tiras.lejana, ModelosDeFiguras.Pixeles(pie), st.Modelo[i],
                                                                    velocidad, faseColumna, equipo)) continue;
                var (fija, deFaccion) = TandaDe(f);
                fija?.Poner(figura, blanco);
                deFaccion?.Poner(figura, faccion);
            }
        }


        sealed class Sitios
        {
            public List<SitioDeFigura> Figuras;
            public int Fotograma;
            public Vector3[] Pie, Adelante;
            public Quaternion[] Giro;
            public Matrix4x4[] Figura, Modelo;
            public (TiraDeFiguras propia, TiraDeFiguras media, TiraDeFiguras lejana)[] Tiras;
        }
        static readonly Dictionary<string, Sitios> sitios = new Dictionary<string, Sitios>();

        /// <summary>Un estandarte suelto, el de las columnas (asta, travesaño, paño con el blasón de su Facción y disco), con el pie en `pie` (la
        /// escena) y `altoM` metros hasta el disco; el paño, de cara a la cámara. Las plazas lo ponen en las anclas de su modelo (repaso del 23-11).
        /// Se pinta con las figuras (`Pintar`, el fotograma siguiente si se añade después).</summary>
        public static void EstandarteSuelto(Vector3 pie, float altoM, string faccionId, Vector4 faccion)
        {
            // (la malla va en metros de figura / `MetrosPorFigura`: su alto, (AltoAsta + 0,5) / MetrosPorFigura)
            var escala = altoM * MetrosPorFigura / (AltoAsta + 0.5f);
            var mira = ModelosDeFiguras.Camara != null ? ModelosDeFiguras.Camara.transform.position - pie : Vector3.forward;
            mira.y = 0;
            var cara = mira.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(mira.normalized, Vector3.up) : Quaternion.identity;
            var m = Matrix4x4.TRS(pie, cara, Vector3.one * escala);
            var (asta, borlas) = TandaDe(default, true);
            asta?.Poner(m, new Vector4(1, 1, 1, 1));
            borlas?.Poner(m, faccion);
            var sigilo = string.IsNullOrEmpty(faccionId) ? null : CosasEnElMapa.SigiloDe?.Invoke(faccionId, PixelesSigilo);
            if (sigilo != null)
            {
                var tela = EstandarteConPano ? ConPano(sigilo) : sigilo;
                if (!paneles.TryGetValue(tela, out var l)) paneles[tela] = l = new List<Matrix4x4>();
                l.Add(m);
            }
            else
            {
                if (panelesLisos == null) { panelesLisos = new Tanda { Malla = PanelEstandarte(), DeFaccion = true }; todas.Add(panelesLisos); }
                panelesLisos.Poner(m, string.IsNullOrEmpty(faccionId) ? Lineal(0xE8E2D2) : faccion);
            }
            EstandartesSueltos++;
        }

        /// <summary>Estandartes sueltos puestos desde el último `Pintar`, y los que pintó el último (para las pruebas).</summary>
        public static int EstandartesSueltos, EstandartesSueltosPintados;

        /// <summary>Dónde se pusieron en el último fotograma las figuras de `id` que cumplen `cual` (en la escena; vacío si no se ha pintado).</summary>
        public static List<Vector3> Pies(string id, Func<SitioDeFigura, bool> cual)
        {
            var r = new List<Vector3>();
            if (!sitios.TryGetValue(id, out var st)) return r;
            for (int i = 0; i < st.Figuras.Count && i < st.Pie.Length; i++) if (cual(st.Figuras[i])) r.Add(st.Pie[i]);
            return r;
        }

        static readonly Matrix4x4[] bloque = new Matrix4x4[1023];
        static readonly Vector4[] bloqueC = new Vector4[1023];
        public static int Llamadas, Figuras;

        /// <summary>Pinta las figuras de bloques de este fotograma (una llamada por tipo de figura y lote de 1.023) y vacía las tandas.</summary>
        public static void Pintar(Material material, MaterialPropertyBlock propiedades, bool sombras)
        {
            Llamadas = Figuras = 0;
            EstandartesSueltosPintados = EstandartesSueltos;
            EstandartesSueltos = 0;
            var rp = new RenderParams(material)
            {
                shadowCastingMode = sombras ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off, matProps = propiedades,
            };
            foreach (var t in todas)
            {
                for (int i = 0; i < t.N; i += bloque.Length)
                {
                    int n = Math.Min(bloque.Length, t.N - i);
                    Array.Copy(t.M, i, bloque, 0, n);
                    Array.Copy(t.C, i, bloqueC, 0, n);
                    propiedades.SetVectorArray("_Color", bloqueC);
                    propiedades.SetFloat("_Mece", t.Mece ? 1 : 0);
                    Graphics.RenderMeshInstanced(rp, t.Malla, 0, bloque, n);
                    Llamadas++;
                }
                if (!t.DeFaccion) Figuras += t.N;
                t.N = 0;
            }
            if (paneles.Count == 0) return;
            if (sombreadoPanel == null) sombreadoPanel = Resources.Load<Shader>("MapaMundo/MapaFigura");
            foreach (var kv in paneles)
            {
                if (kv.Value.Count == 0) continue;
                if (!materialesDePanel.TryGetValue(kv.Key, out var mat))
                {
                    mat = new Material(sombreadoPanel) { enableInstancing = true, name = "blasón del estandarte" };
                    mat.SetTexture("_BaseMap", kv.Key);
                    mat.SetFloat("_Recorte", 0.5f);
                    mat.SetFloat("_Cull", 0);
                    materialesDePanel[kv.Key] = mat;
                }
                var rpp = new RenderParams(mat) { shadowCastingMode = sombras ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off };
                for (int i = 0; i < kv.Value.Count; i += bloque.Length)
                {
                    int n = Math.Min(bloque.Length, kv.Value.Count - i);
                    kv.Value.CopyTo(i, bloque, 0, n);
                    Graphics.RenderMeshInstanced(rpp, PanelEstandarte(), 0, bloque, n);
                    Llamadas++;
                }
                kv.Value.Clear();
            }
        }
    }
}

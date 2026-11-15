// El suelo del mapa estratégico 2D (paso 9 del bloque 4), calcado de BronzeAgeClient (`src/mundo/pintarArtefacto.ts`):
//  * de LEJOS (menos de 10 px por celda de 4 u): una imagen de una celda por píxel, hecha una vez por mundo (color por clase de terreno,
//    sombreado de ladera con luz del noroeste, bosque por densidad y ríos);
//  * de CERCA: la vista a media resolución de pantalla, con la altura interpolada, bordes de clase orgánicos (la clase se mira en un punto
//    desplazado por ruido determinista) y el suelo del bosque oscurecido; encima, árboles sueltos a escala real (`Arboles`).
// Y la capa de fertilidad de un cultivo (`pintarFertilidad`) y la niebla (`pintarNiebla`), como texturas que se estiran sobre el mundo.
using System;
using System.Collections.Generic;
using BronzeAge.Contract.V1;
using UnityEngine;

namespace ConquestTactics.Mundo.Mapa
{
    public sealed class SueloDelMapa2D
    {
        public const double PxPorCeldaCerca = 10;
        const int UmbralBosque = 38;
        const double EscalaRelieve = 350;
        static readonly Color32[] ColorClase =
        {
            new Color32(38, 82, 130, 255), new Color32(38, 82, 130, 255), new Color32(196, 186, 132, 255), new Color32(147, 194, 107, 255),
            new Color32(128, 168, 90, 255), new Color32(128, 116, 98, 255), new Color32(226, 226, 222, 255),
        };
        static readonly Vector3 Bosque = new Vector3(38, 92, 52), RioColor = new Vector3(38, 90, 145);

        readonly RasterDeMundo terreno, bosque;
        readonly ushort[] altura;
        readonly int altCols, altFilas;
        readonly double altCelda;
        readonly ArtefactoDeMundo artefacto;
        Texture2D lejos, cerca;
        string claveCerca;

        public double Celda => terreno.Celda;

        public SueloDelMapa2D(ArtefactoDeMundo a)
        {
            artefacto = a;
            terreno = new RasterDeMundo(a.Terreno);
            bosque = a.DensidadBosque != null && a.DensidadBosque.Encoding == EncodingRaster.EncodingU8 ? new RasterDeMundo(a.DensidadBosque) : null;
            if (a.Altura != null && a.Altura.Encoding == EncodingRaster.EncodingU16Le)
            {
                var bytes = a.Altura.Zstd ? new ZstdSharp.Decompressor().Unwrap(a.Altura.Datos.Span).ToArray() : a.Altura.Datos.ToByteArray();
                altCols = (int)a.Altura.Columnas;
                altFilas = (int)a.Altura.Filas;
                altCelda = a.Altura.TamanoCelda / 1000.0;
                altura = new ushort[altCols * altFilas];
                for (int i = 0; i < altura.Length; i++) altura[i] = (ushort)(bytes[2 * i] | (bytes[2 * i + 1] << 8));
            }
        }

        public bool DeCerca(double escala) => escala * terreno.Celda >= PxPorCeldaCerca;

        // ─────────────────── muestreo ───────────────────

        static double Bilineal(int cols, int filas, double celda, Func<int, double> dato, double x, double y)
        {
            double fx = Math.Min(cols - 1, Math.Max(0, x / celda - 0.5)), fy = Math.Min(filas - 1, Math.Max(0, y / celda - 0.5));
            int c0 = (int)fx, f0 = (int)fy, c1 = Math.Min(cols - 1, c0 + 1), f1 = Math.Min(filas - 1, f0 + 1);
            double tx = fx - c0, ty = fy - f0;
            var arriba = dato(f0 * cols + c0) * (1 - tx) + dato(f0 * cols + c1) * tx;
            var abajo = dato(f1 * cols + c0) * (1 - tx) + dato(f1 * cols + c1) * tx;
            return arriba * (1 - ty) + abajo * ty;
        }

        double Alt(double x, double y) => altura == null ? 0 : Bilineal(altCols, altFilas, altCelda, i => altura[i], x, y) / 65535.0;
        double Dens(double x, double y) => bosque == null ? 0 : Bilineal(bosque.Columnas, bosque.Filas, bosque.Celda, i => bosque.Datos[i], x, y);
        double H(double x, double y) => Alt(x, y) * EscalaRelieve;

        /// <summary>Hash entero → [0, 1). Determinista: el mismo sitio sale igual en cada repintado.</summary>
        public static double Hash2(int x, int y, int semilla)
        {
            unchecked
            {
                int h = (x * 374761393) ^ (y * 668265263) ^ (semilla * 1274126177);
                h = (h ^ (int)((uint)h >> 13)) * 1103515245;
                h ^= (int)((uint)h >> 16);
                return (uint)h / 4294967296.0;
            }
        }

        static double RuidoValor(double x, double y, int semilla)
        {
            int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y);
            double tx = x - xi, ty = y - yi, sx = tx * tx * (3 - 2 * tx), sy = ty * ty * (3 - 2 * ty);
            double a = Hash2(xi, yi, semilla), b = Hash2(xi + 1, yi, semilla), c = Hash2(xi, yi + 1, semilla), d = Hash2(xi + 1, yi + 1, semilla);
            return ((a * (1 - sx) + b * sx) * (1 - sy) + (c * (1 - sx) + d * sx) * sy) * 2 - 1;
        }

        static double Desplazamiento(double x, double y, double celda, int semilla)
        {
            double u = x / celda, v = y / celda;
            return (RuidoValor(u / 2, v / 2, semilla) * 0.3 + RuidoValor(u, v, semilla + 7) * 0.15) * celda;
        }

        static double Luz(double dzdx, double dzdy)
        {
            var luzZ = Math.Sqrt(0.5);
            var lambert = Math.Max(0, (dzdx * 0.5 + dzdy * 0.5 + luzZ) / Math.Sqrt(dzdx * dzdx + dzdy * dzdy + 1));
            return Math.Max(0.35, lambert / luzZ);
        }

        static Vector3 V(Color32 c) => new Vector3(c.r, c.g, c.b);

        static Color32 Pixel(Vector3 c, double k) =>
            new Color32((byte)Math.Min(255, c.x * k), (byte)Math.Min(255, c.y * k), (byte)Math.Min(255, c.z * k), 255);

        // ─────────────────── de lejos ───────────────────

        /// <summary>Una celda del raster por píxel, con relieve, bosque y ríos. Se hace una vez. Fila 0 de la textura = fila de abajo del mundo
        /// (Unity pinta de abajo arriba): se escribe ya invertida.</summary>
        public Texture2D Lejos()
        {
            if (lejos != null) return lejos;
            int cols = terreno.Columnas, filas = terreno.Filas;
            var celda = terreno.Celda;
            var px = new Color32[cols * filas];
            for (int f = 0; f < filas; f++)
            {
                var y = (f + 0.5) * celda;
                for (int c = 0; c < cols; c++)
                {
                    var x = (c + 0.5) * celda;
                    int clase = terreno.Datos[f * cols + c];
                    var color = V(clase < ColorClase.Length ? ColorClase[clase] : ColorClase[1]);
                    var dens = Dens(x, y);
                    if (clase > 1 && dens >= UmbralBosque)
                    {
                        var a = 0.85 + 0.3 * Alt(x, y);
                        color = Vector3.Lerp(color, Bosque * (float)a, (float)Math.Min(0.85, dens / 255 * 1.15));
                    }
                    var k = clase <= 1 ? 1 : Luz((H(x + celda, y) - H(x - celda, y)) / (2 * celda), (H(x, y + celda) - H(x, y - celda)) / (2 * celda));
                    px[(filas - 1 - f) * cols + c] = Pixel(color, k);
                }
            }
            // Ríos, una celda de ancho (Bresenham).
            var rio = Pixel(RioColor, 1);
            foreach (var r in artefacto.Rios)
                for (int k = 1; k < r.Puntos.Count; k++)
                {
                    int x0 = (int)Math.Floor(r.Puntos[k - 1].X / 1000.0 / celda), y0 = (int)Math.Floor(r.Puntos[k - 1].Y / 1000.0 / celda);
                    int x1 = (int)Math.Floor(r.Puntos[k].X / 1000.0 / celda), y1 = (int)Math.Floor(r.Puntos[k].Y / 1000.0 / celda);
                    int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
                    for (;;)
                    {
                        if (x0 >= 0 && y0 >= 0 && x0 < cols && y0 < filas) px[(filas - 1 - y0) * cols + x0] = rio;
                        if (x0 == x1 && y0 == y1) break;
                        var e2 = 2 * err;
                        if (e2 >= dy) { err += dy; x0 += sx; }
                        if (e2 <= dx) { err += dx; y0 += sy; }
                    }
                }
            lejos = new Texture2D(cols, filas, TextureFormat.RGBA32, false) { name = "suelo de lejos", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
            lejos.SetPixels32(px);
            lejos.Apply(false);
            return lejos;
        }

        /// <summary>Sin suavizar de cerca (se ven las celdas); suavizado cuando una celda mide menos de 2 px.</summary>
        public void FiltroDeLejos(double escala)
        {
            if (lejos != null) lejos.filterMode = escala * terreno.Celda < 2 ? FilterMode.Bilinear : FilterMode.Point;
        }

        // ─────────────────── de cerca ───────────────────

        /// <summary>El rectángulo de mundo (x0, y0)–(x1, y1) a `ancho` × `alto` px, muestreando los rasters punto a punto. Se guarda por clave.</summary>
        public Texture2D Cerca(double x0, double y0, double x1, double y1, int ancho, int alto)
        {
            ancho = Math.Max(1, ancho);
            alto = Math.Max(1, alto);
            var clave = $"{x0:0.###}|{y0:0.###}|{x1:0.###}|{y1:0.###}|{ancho}|{alto}";
            if (cerca != null && clave == claveCerca) return cerca;
            if (cerca == null || cerca.width != ancho || cerca.height != alto)
            {
                if (cerca != null) UnityEngine.Object.Destroy(cerca);
                cerca = new Texture2D(ancho, alto, TextureFormat.RGBA32, false) { name = "suelo de cerca", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            }
            claveCerca = clave;
            var px = new Color32[ancho * alto];
            double paso = (x1 - x0) / ancho, pasoY = (y1 - y0) / alto, dz = terreno.Celda;
            for (int j = 0; j < alto; j++)
            {
                var y = y0 + (j + 0.5) * pasoY;
                for (int i = 0; i < ancho; i++)
                {
                    var x = x0 + (i + 0.5) * paso;
                    int clase = terreno.En(x + Desplazamiento(x, y, terreno.Celda, 11), y + Desplazamiento(x, y, terreno.Celda, 23));
                    var color = V(clase < ColorClase.Length ? ColorClase[clase] : ColorClase[1]);
                    if (bosque != null && clase > 1)
                    {
                        var dens = Dens(x, y);
                        if (dens >= UmbralBosque) color = Vector3.Lerp(color, Bosque * 1.1f, (float)Math.Min(0.55, dens / 255 * 0.7));
                    }
                    var k = clase <= 1 ? 1 : Luz((H(x + dz, y) - H(x - dz, y)) / (2 * dz), (H(x, y + dz) - H(x, y - dz)) / (2 * dz));
                    px[(alto - 1 - j) * ancho + i] = Pixel(color, k);
                }
            }
            cerca.SetPixels32(px);
            cerca.Apply(false);
            return cerca;
        }

        /// <summary>Un árbol suelto: centro (u), radio (u) y su color.</summary>
        public struct Arbol { public float X, Y, R; public Color Color; }

        /// <summary>Árboles sueltos, como el admin: uno como mucho cada 1,2 u si la densidad lo pide (azar fijo por sitio), ni en el agua ni en las
        /// cimas. Solo de cerca y con 3 px o más entre árboles.</summary>
        public void Arboles(double x0, double y0, double x1, double y1, double escala, List<Arbol> salida)
        {
            salida.Clear();
            const double Paso = 1.2;
            if (bosque == null || escala * Paso < 3) return;
            for (int gy = (int)Math.Floor(y0 / Paso) - 1; gy <= (int)Math.Ceiling(y1 / Paso); gy++)
                for (int gx = (int)Math.Floor(x0 / Paso) - 1; gx <= (int)Math.Ceiling(x1 / Paso); gx++)
                {
                    var x = (gx + 0.15 + 0.7 * Hash2(gx, gy, 3)) * Paso;
                    var y = (gy + 0.15 + 0.7 * Hash2(gx, gy, 5)) * Paso;
                    int clase = terreno.En(x, y);
                    if (clase <= 1 || clase == 6) continue;
                    var dens = Dens(x, y);
                    if (dens < UmbralBosque || Hash2(gx, gy, 9) > dens / 255 * 1.1) continue;
                    salida.Add(new Arbol
                    {
                        X = (float)x, Y = (float)y, R = (float)(0.38 + 0.26 * Hash2(gx, gy, 14)),
                        Color = new Color32((byte)(30 + Hash2(gx, gy, 15) * 25), (byte)(80 + Hash2(gx, gy, 16) * 35), (byte)(40 + Hash2(gx, gy, 17) * 20), 255),
                    });
                }
        }

        // ─────────────────── la fertilidad ───────────────────

        static readonly Vector3 Baja = new Vector3(140, 66, 44), Casi = new Vector3(204, 132, 66), Corte = new Vector3(232, 214, 104),
            Buena = new Vector3(118, 178, 66), Honda = new Vector3(30, 110, 50);
        const byte AlfaFertilidad = 160;

        static float Entre(double x, double a, double b) => (float)Math.Min(1, Math.Max(0, (x - a) / (b - a)));

        /// <summary>El color de una fertilidad `f` (0..1) con el corte de la aldea en `minima` (`colorDeFertilidad`): de óxido a ocre por debajo
        /// del corte, de paja a verde hondo desde él.</summary>
        public static Color32 ColorDeFertilidad(double f, double minima)
        {
            var c = f < minima ? Vector3.Lerp(Baja, Casi, Entre(f, 0, minima))
                : f < minima + 0.3 ? Vector3.Lerp(Corte, Buena, Entre(f, minima, minima + 0.3)) : Vector3.Lerp(Buena, Honda, Entre(f, minima + 0.3, minima + 0.6));
            return new Color32((byte)Math.Round(c.x), (byte)Math.Round(c.y), (byte)Math.Round(c.z), AlfaFertilidad);
        }

        Texture2D fertilidad;
        string claveFertilidad;

        /// <summary>La capa de fertilidad de un raster (una celda por píxel): transparente en el agua y en lo no explorado (`explorado`).</summary>
        public Texture2D Fertilidad(RasterDeMundo f, string clave, Func<double, double, bool> explorado, double minima)
        {
            if (fertilidad != null && clave == claveFertilidad) return fertilidad;
            claveFertilidad = clave;
            if (fertilidad == null || fertilidad.width != f.Columnas || fertilidad.height != f.Filas)
            {
                if (fertilidad != null) UnityEngine.Object.Destroy(fertilidad);
                fertilidad = new Texture2D(f.Columnas, f.Filas, TextureFormat.RGBA32, false) { name = "fertilidad", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            }
            var tabla = new Color32[256];
            for (int v = 0; v < 256; v++) tabla[v] = ColorDeFertilidad(v / 255.0, minima);
            var px = new Color32[f.Columnas * f.Filas];
            for (int j = 0; j < f.Filas; j++)
                for (int i = 0; i < f.Columnas; i++)
                {
                    double x = (i + 0.5) * f.Celda, y = (j + 0.5) * f.Celda;
                    if (!explorado(x, y) || terreno.En(x, y) == 1) continue;
                    px[(f.Filas - 1 - j) * f.Columnas + i] = tabla[f.Datos[j * f.Columnas + i]];
                }
            fertilidad.SetPixels32(px);
            fertilidad.Apply(false);
            return fertilidad;
        }

        public void Destruir()
        {
            if (lejos != null) UnityEngine.Object.Destroy(lejos);
            if (cerca != null) UnityEngine.Object.Destroy(cerca);
            if (fertilidad != null) UnityEngine.Object.Destroy(fertilidad);
        }
    }
}

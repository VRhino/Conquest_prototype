// Dónde cabe una aldea (paso 9 del bloque 4), calcado de BronzeAgeClient (`src/mundo/fertilidad.ts`: `sitiosDeAldea`). El motor la acepta en
// tierra firme, dentro de la zona de la plaza, a `separacionMinima` de toda plaza y todo pueblo y con fertilidad de `fertilidadMinima` o más en
// ese punto para su cultivo; la siguiente va además en su anillo. El camino hasta la plaza no se sabe aquí (es el A* del motor). C# puro.
using System;
using System.Collections.Generic;

namespace ConquestTactics.Mundo
{
    public sealed class SitiosDeAldea
    {
        /// <summary>Paso de la rejilla de búsqueda (u).</summary>
        public const double Paso = 0.5;
        public readonly List<(double x, double y)> Validos = new List<(double, double)>(), Cerca = new List<(double, double)>();
        public double Mejor;

        /// <summary>¿Está `p` dentro del polígono? (par-impar)</summary>
        public static bool Dentro(double x, double y, IReadOnlyList<(double x, double y)> pol)
        {
            bool dentro = false;
            for (int i = 0, j = pol.Count - 1; i < pol.Count; j = i++)
            {
                var (ax, ay) = pol[i];
                var (bx, by) = pol[j];
                if ((ay > y) != (by > y) && x < (bx - ax) * (y - ay) / (by - ay) + ax) dentro = !dentro;
            }
            return dentro;
        }

        /// <summary>Los sitios de la zona (`zona`, el polígono real de la plaza) en su anillo (desde, hasta] u de `centro`. `fertilidad` y `terreno`,
        /// los rasters del cultivo y de la clase de terreno; `ocupados`, las plazas, pueblos y obras de pueblo que se conocen.</summary>
        public static SitiosDeAldea Buscar(RasterDeMundo fertilidad, RasterDeMundo terreno, IReadOnlyList<(double x, double y)> zona, (double x, double y) centro,
            (double desde, double hasta)? anillo, IEnumerable<(double x, double y)> ocupados, double fertilidadMinima, double separacionMinima,
            Func<double, double, bool> transitable = null)
        {
            var r = new SitiosDeAldea();
            if (fertilidad == null || zona == null || zona.Count < 3) return r;
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            foreach (var (x, y) in zona) { x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y); }
            var otros = new List<(double x, double y)>(ocupados);
            var s2 = separacionMinima * separacionMinima;
            for (var y = Math.Ceiling(y0 / Paso) * Paso; y <= y1; y += Paso)
                for (var x = Math.Ceiling(x0 / Paso) * Paso; x <= x1; x += Paso)
                {
                    if (!Dentro(x, y, zona) || !Firme(terreno, x, y) || transitable != null && !transitable(x, y)) continue;
                    if (anillo != null)
                    {
                        var d = Math.Sqrt((x - centro.x) * (x - centro.x) + (y - centro.y) * (y - centro.y));
                        if (d <= anillo.Value.desde || d > anillo.Value.hasta) continue;
                    }
                    var f = FertilidadEn(fertilidad, x, y);
                    if (f < fertilidadMinima) continue;
                    if (otros.Exists(o => (o.x - x) * (o.x - x) + (o.y - y) * (o.y - y) < s2)) { r.Cerca.Add((x, y)); continue; }
                    r.Validos.Add((x, y));
                    r.Mejor = Math.Max(r.Mejor, f);
                }
            return r;
        }

        /// <summary>La fertilidad (0..1) de la celda donde cae (x, y), como la lee el motor; 0 fuera del mapa.</summary>
        public static double FertilidadEn(RasterDeMundo f, double x, double y)
        {
            if (x < 0 || y < 0) return 0;
            int c = (int)Math.Floor(x / f.Celda), fi = (int)Math.Floor(y / f.Celda);
            return c >= f.Columnas || fi >= f.Filas ? 0 : f.Datos[fi * f.Columnas + c] / 255.0;
        }

        /// <summary>¿Tierra firme, como la mira el motor (`zonas.rs: es_tierra_firme`)? Dentro del mapa y no agua (1); que además se pueda pisar lo
        /// dice el raster `transitable` (`Transitable`). El prototipo (`fertilidad.ts: firme`) quitaba además las cimas y no miraba si se pisa: pintaba en
        /// verde sitios que el motor rechaza («no es tierra firme»).</summary>
        static bool Firme(RasterDeMundo t, double x, double y)
        {
            if (t == null) return true;
            if (x < 0 || y < 0) return false;
            int c = (int)Math.Floor(x / t.Celda), f = (int)Math.Floor(y / t.Celda);
            if (c >= t.Columnas || f >= t.Filas) return false;
            return t.Datos[f * t.Columnas + c] != 1;
        }

        /// <summary>El raster de bits `transitable` del artefacto (1 = se puede pisar; mismo orden que la máscara de niebla), como función de (x, y)
        /// en unidades; null si el artefacto no lo trae.</summary>
        public static Func<double, double, bool> Transitable(BronzeAge.Contract.V1.Raster r)
        {
            if (r == null || r.Encoding != BronzeAge.Contract.V1.EncodingRaster.EncodingBits) return null;
            var bits = r.Zstd ? new ZstdSharp.Decompressor().Unwrap(r.Datos.Span).ToArray() : r.Datos.ToByteArray();
            int cols = (int)r.Columnas, filas = (int)r.Filas;
            var celda = r.TamanoCelda / 1000.0;
            return (x, y) =>
            {
                if (x < 0 || y < 0) return false;
                int c = (int)Math.Floor(x / celda), f = (int)Math.Floor(y / celda);
                if (c >= cols || f >= filas) return false;
                var i = f * cols + c;
                return (i >> 3) < bits.Length && ((bits[i >> 3] >> (i & 7)) & 1) == 1;
            };
        }
    }
}

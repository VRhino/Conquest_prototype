// El relieve fino del mundo (contrato 06 §8, `PaqueteRelieve`; puerto de `BronzeAgeClient/src/mundo/relieve.ts`): la altura cada 0,5 u de todo
// el mapa y un bit de agua por muestra, en teselas de 64 u que se abren solo cuando se piden. La altura se dibuja tal cual: la exageración vive
// en el generador, así las pendientes que se ven son las que usa el servidor. Y los rasters del artefacto (clase de terreno, bosque...).
using System;
using System.Collections.Generic;
using BronzeAge.Contract.V1;

namespace ConquestTactics.Mundo
{
    public sealed class Relieve
    {
        /// <summary>Metros que vale la altura máxima (65535), la escala del raster `altura` del artefacto.</summary>
        public const double MetrosAltura = 1000;

        public readonly string Hash;
        /// <summary>Unidades del mapa entre muestras (0,5).</summary>
        public readonly double Paso;
        /// <summary>Intervalos por lado de tesela (128): cada tesela guarda (lado + 1)² muestras.</summary>
        public readonly int Lado;
        public readonly int Columnas, Filas, TeselasX, TeselasY;
        /// <summary>Lado de una tesela en unidades del mapa (64).</summary>
        public double LadoEnUnidades => Lado * Paso;

        readonly IList<TeselaRelieve> teselas;
        readonly int maxAbiertas;
        // Las abiertas, de la menos a la más usada hace poco.
        readonly LinkedList<int> orden = new LinkedList<int>();
        readonly Dictionary<int, (TeselaAbierta t, LinkedListNode<int> nodo)> abiertas = new Dictionary<int, (TeselaAbierta, LinkedListNode<int>)>();
        readonly ZstdSharp.Decompressor zstd = new ZstdSharp.Decompressor();

        public sealed class TeselaAbierta
        {
            /// <summary>(lado + 1)² muestras en orden de filas, de 0 a 65535 sobre `MetrosAltura`.</summary>
            public ushort[] Altura;
            /// <summary>(lado + 1)² muestras: true = agua (mar o lago).</summary>
            public bool[] Agua;
        }

        /// <summary>`maxAbiertas`: cuántas teselas se guardan abiertas (cada una ocupa unos 50 KB).</summary>
        public Relieve(PaqueteRelieve p, int maxAbiertas = 96)
        {
            Hash = p.Hash;
            Paso = p.Paso / 1000.0;
            Lado = (int)p.LadoTesela;
            Columnas = (int)p.Columnas;
            Filas = (int)p.Filas;
            TeselasX = (int)p.TeselasX;
            TeselasY = (int)p.TeselasY;
            teselas = p.Teselas;
            this.maxAbiertas = maxAbiertas;
            if (Lado <= 0 || teselas.Count != TeselasX * TeselasY) throw new InvalidOperationException("paquete de relieve mal formado");
        }

        /// <summary>Abre una tesela: deshace el zstd y la predicción de cada altura (izquierda + arriba − diagonal, módulo 2^16).</summary>
        public static TeselaAbierta Abrir(TeselaRelieve t, int lado, ZstdSharp.Decompressor zstd)
        {
            int n = lado + 1, total = n * n;
            var crudo = zstd.Unwrap(t.Altura.Span).ToArray();
            if (crudo.Length != total * 2) throw new InvalidOperationException($"tesela de relieve de {crudo.Length} bytes; se esperaban {total * 2}");
            var h = new ushort[total];
            for (int j = 0, k = 0; j < n; j++)
                for (int i = 0; i < n; i++, k++)
                {
                    int prediccion = i > 0 && j > 0 ? h[k - 1] + h[k - n] - h[k - n - 1] : i > 0 ? h[k - 1] : j > 0 ? h[k - n] : 0;
                    h[k] = (ushort)(((crudo[2 * k] | (crudo[2 * k + 1] << 8)) + prediccion) & 0xffff);
                }
            var agua = new bool[total];
            if (t.Agua.Length > 0)
            {
                var bits = zstd.Unwrap(t.Agua.Span).ToArray();
                for (int k = 0; k < total; k++) agua[k] = ((bits[k >> 3] >> (k & 7)) & 1) == 1;
            }
            return new TeselaAbierta { Altura = h, Agua = agua };
        }

        /// <summary>La tesela (tx, ty), abierta. Las últimas `maxAbiertas` se quedan abiertas.</summary>
        public TeselaAbierta Tesela(int tx, int ty)
        {
            int k = ty * TeselasX + tx;
            if (abiertas.TryGetValue(k, out var a))
            {
                if (a.nodo != orden.Last) { orden.Remove(a.nodo); orden.AddLast(a.nodo); }
                return a.t;
            }
            var t = Abrir(teselas[k], Lado, zstd);
            if (abiertas.Count >= maxAbiertas)
            {
                abiertas.Remove(orden.First.Value);
                orden.RemoveFirst();
            }
            abiertas[k] = (t, orden.AddLast(k));
            return t;
        }

        void Situar(double x, double y, out TeselaAbierta t, out double i, out double j)
        {
            double fi = Math.Min(Math.Max(x / Paso, 0), Columnas - 1), fj = Math.Min(Math.Max(y / Paso, 0), Filas - 1);
            int tx = Math.Min(TeselasX - 1, (int)Math.Floor(fi / Lado)), ty = Math.Min(TeselasY - 1, (int)Math.Floor(fj / Lado));
            t = Tesela(tx, ty);
            i = fi - tx * Lado;
            j = fj - ty * Lado;
        }

        /// <summary>La altura en metros en (x, y), en unidades del mapa, interpolada entre las cuatro muestras de alrededor; fuera, la del borde.</summary>
        public double AlturaEn(double x, double y)
        {
            Situar(x, y, out var t, out var i, out var j);
            int n = Lado + 1, i0 = Math.Min(Lado - 1, (int)Math.Floor(i)), j0 = Math.Min(Lado - 1, (int)Math.Floor(j)), k = j0 * n + i0;
            double a = i - i0, b = j - j0, h0 = t.Altura[k] * (1 - a) + t.Altura[k + 1] * a, h1 = t.Altura[k + n] * (1 - a) + t.Altura[k + n + 1] * a;
            return (h0 * (1 - b) + h1 * b) / 65535 * MetrosAltura;
        }

        /// <summary>¿Hay agua en la muestra más cercana a (x, y)?</summary>
        public bool AguaEn(double x, double y)
        {
            Situar(x, y, out var t, out var i, out var j);
            return t.Agua[(int)Math.Round(j) * (Lado + 1) + (int)Math.Round(i)];
        }
    }

    /// <summary>Un raster del artefacto ya descomprimido (contrato 06): clase de terreno, fertilidad, bosque... Celdas en orden de filas.</summary>
    public sealed class RasterDeMundo
    {
        public readonly int Columnas, Filas;
        /// <summary>Unidades del mapa por celda.</summary>
        public readonly double Celda;
        public readonly byte[] Datos;

        public RasterDeMundo(Raster r)
        {
            if (r.Encoding != EncodingRaster.EncodingU8) throw new InvalidOperationException($"raster {r.Encoding}: solo se leen los de un byte");
            Columnas = (int)r.Columnas;
            Filas = (int)r.Filas;
            Celda = r.TamanoCelda / 1000.0;
            Datos = r.Zstd ? new ZstdSharp.Decompressor().Unwrap(r.Datos.Span).ToArray() : r.Datos.ToByteArray();
        }

        /// <summary>El valor de la celda que contiene (x, y), en unidades del mapa; fuera, el del borde.</summary>
        public byte En(double x, double y)
        {
            int i = Math.Min(Columnas - 1, Math.Max(0, (int)(x / Celda))), j = Math.Min(Filas - 1, Math.Max(0, (int)(y / Celda)));
            return Datos[j * Columnas + i];
        }
    }
}

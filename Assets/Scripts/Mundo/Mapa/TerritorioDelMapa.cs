// El territorio sobre el suelo del mapa de mundo, como en el prototipo (BronzeAgeClient: `mundo3d/territorio.ts`): las zonas de cada Facción se
// pintan en una textura de 1024 px que cubre 1200 u alrededor del héroe (se recentra cuando cruza una celda de 128 u), con el color de su
// Facción y poca opacidad (0,18 la propia, 0,13 las ajenas, más en los colores oscuros) y un borde por dentro que se desvanece hacia el centro
// (6 trazos de opacidad 0,05 con medios anchos de 4 a 0,67 u, recortados al interior). La zona propia fusionada, con sus huecos (par-impar);
// las demás, juntas (distinto de cero). Se pinta en otro hilo, sin tirón al recentrar; `MapaSuelo` lo mezcla con su color antes de la luz.
// Estilo «Mejorado» (`Estilo`): el interior, muy suave (0,05 la propia, 0,035 las ajenas), y un filo pintado junto a la frontera, por dentro:
// pico de 0,55 que cae a la mitad cada 0,45 u (un ancho visible de alrededor de 1 u). `MapaSuelo` le pone ahí al suelo el tono de la Facción y una
// saturación de 0,30 a 0,45, con el contraste de brillo comprimido; las oscuras, solo más oscuro.
// Las Facciones que contrastan poco con el suelo (oscuras, o de tono de pasto o de tierra) llevan además un reborde fino más claro por dentro,
// en otra textura (`_TerritorioBorde`).
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace ConquestTactics.Mundo.Mapa
{
    public sealed class TerritorioDelMapa
    {
        public const int Px = 2048;
        public const double Lado = 1200, PasoRecentrar = 128, AnchoBorde = 4, AlfaBorde = 0.05;
        public const int CapasBorde = 6;
        /// <summary>Estilo «Mejorado»: el interior de lo propio y de lo ajeno, la banda junto a la frontera (u) y lo que llega a tener en el borde.</summary>
        /// <summary>Cómo se pinta el territorio: como el prototipo; «Mejorado» (el filo de la ronda 4, por defecto); o «Suave» (la opción 3 que se le
        /// ofrece a Víctor: un tinte suave en todo el territorio, la mitad que el prototipo, y un filo más fino y menos vivo).</summary>
        public enum EstiloDeTerritorio { Prototipo, Mejorado, Suave }
        public static EstiloDeTerritorio Estilo = EstiloDeTerritorio.Mejorado;
        /// <summary>Lo que cambia entre «Mejorado» y «Suave»: el interior propio y ajeno, el pico del filo, cada cuánto cae a la mitad (u) y la
        /// saturación del filo (mínima y máxima).</summary>
        public static (double propio, double ajeno, double pico, double mitad, float satMin, float satMax) Parametros(EstiloDeTerritorio e) =>
            e == EstiloDeTerritorio.Suave ? (0.09, 0.065, 0.40, 0.35, 0.25f, 0.38f) : (0.05, 0.035, 0.55, 0.45, 0.30f, 0.45f);
        public const double Banda = 3;
        static readonly int TexturaBorde = Shader.PropertyToID("_TerritorioBorde"), Modo = Shader.PropertyToID("_TerritorioModo"),
            Filo = Shader.PropertyToID("_TerritorioFilo");
        readonly Texture2D reborde;

        static readonly int Textura = Shader.PropertyToID("_Territorio"), Caja = Shader.PropertyToID("_TerritorioCaja");
        readonly Texture2D textura;
        List<RellenoDeTerritorio> rellenos = new List<RellenoDeTerritorio>();
        string claveRellenos = "", clave = "";
        Task<(string clave, double ox, double oy, Color32[] px, byte[] borde, double ms)> enCurso;

        /// <summary>Lo más que ha tardado en pintarse (ms, en su hilo) y lo más que ha tardado en subir a la GPU (ms, en este).</summary>
        public double PeorMsPintar { get; private set; }
        public double PeorMsSubir { get; private set; }
        public int Pintadas { get; private set; }

        public TerritorioDelMapa()
        {
            textura = new Texture2D(Px, Px, TextureFormat.RGBA32, false, false) { name = "territorio", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            textura.SetPixelData(new Color32[Px * Px], 0);
            textura.Apply(false);
            Shader.SetGlobalTexture(Textura, textura);
            reborde = new Texture2D(Px, Px, TextureFormat.R8, false, true) { name = "reborde del territorio", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            reborde.SetPixelData(new byte[Px * Px], 0);
            reborde.Apply(false);
            Shader.SetGlobalTexture(TexturaBorde, reborde);
            Shader.SetGlobalFloat(Modo, Estilo == EstiloDeTerritorio.Prototipo ? 0 : 1);
            // para `MapaSuelo`: hasta dónde llega el tinte suave del interior, el pico del filo y su saturación
            var pr = Parametros(Estilo);
            Shader.SetGlobalVector(Filo, new Vector4((float)Math.Max(pr.propio, pr.ajeno) + 0.01f, (float)pr.pico, pr.satMin, pr.satMax));
            Shader.SetGlobalVector(Caja, Vector4.zero);
        }

        public void Fijar(List<RellenoDeTerritorio> nuevos)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var r in nuevos)
            {
                sb.Append(r.Color).Append(':').Append(r.Alfa).Append(':').Append(r.ParImpar).Append(':');
                foreach (var c in r.Contornos)
                {
                    var a = c.Count > 0 ? c[0] : (0, 0);
                    var b = c.Count > 0 ? c[c.Count >> 1] : (0, 0);
                    sb.Append(c.Count).Append('@').Append(a.x).Append(',').Append(a.y).Append('/').Append(b.x).Append(',').Append(b.y).Append(';');
                }
                sb.Append('|');
            }
            claveRellenos = sb.ToString();
            rellenos = nuevos;
        }

        /// <summary>Repinta (en otro hilo) si cambiaron los rellenos o si el héroe, en (`cx`, `cy`) u, cambió de celda; sube lo pintado cuando acaba.</summary>
        public void Actualizar(double cx, double cy)
        {
            if (enCurso != null && enCurso.IsCompleted)
            {
                var (k, ox, oy, px, borde, ms) = enCurso.Result;
                enCurso = null;
                var reloj = System.Diagnostics.Stopwatch.StartNew();
                textura.SetPixelData(px, 0);
                textura.Apply(false);
                reborde.SetPixelData(borde, 0);
                reborde.Apply(false);
                var e = EscalaDelMapa.MetrosPorUnidad;
                Shader.SetGlobalVector(Caja, new Vector4((float)(ox * e), (float)(oy * e), (float)(Lado * e), 1));
                PeorMsSubir = Math.Max(PeorMsSubir, reloj.Elapsed.TotalMilliseconds);
                PeorMsPintar = Math.Max(PeorMsPintar, ms);
                Pintadas++;
            }
            double x0 = TrazadoDelMapa.Redondear(cx / PasoRecentrar) * PasoRecentrar - Lado / 2, y0 = TrazadoDelMapa.Redondear(cy / PasoRecentrar) * PasoRecentrar - Lado / 2;
            var nueva = $"{x0},{y0}|{claveRellenos}";
            if (nueva == clave || enCurso != null) return;
            clave = nueva;
            var lista = rellenos;
            var estilo = Estilo;
            enCurso = Task.Run(() =>
            {
                var reloj = System.Diagnostics.Stopwatch.StartNew();
                var px = Pintar(lista, x0, y0, estilo, out var borde);
                return (nueva, x0, y0, px, borde, reloj.Elapsed.TotalMilliseconds);
            });
        }

        /// <summary>El lienzo (fila 0 = la y más pequeña), como el `canvas` del prototipo: cada relleno encima del anterior (`source-over`).</summary>
        public static Color32[] Pintar(List<RellenoDeTerritorio> rellenos, double ox, double oy) => Pintar(rellenos, ox, oy, EstiloDeTerritorio.Prototipo, out _);

        /// <summary>¿Contrasta poco el color con el suelo? Oscuro (se confunde con la sombra de una nube) o de tono de pasto (verde y amarillo
        /// verdoso) o de tierra (pardo anaranjado), con algo de saturación.</summary>
        public static bool PocoContraste(uint rgb)
        {
            float r = ((rgb >> 16) & 0xff) / 255f, g = ((rgb >> 8) & 0xff) / 255f, b = (rgb & 0xff) / 255f;
            if (0.2126f * r + 0.7152f * g + 0.0722f * b < 0.2f) return true;
            Color.RGBToHSV(new Color(r, g, b), out var h, out var s, out _);
            float tono = h * 360;
            float Lejos(float a, float c) { float d = Mathf.Abs(a - c) % 360; return Mathf.Min(d, 360 - d); }
            return s > 0.25f && (Lejos(tono, 85) < 65 || Lejos(tono, 35) < 12);
        }

        /// <summary>Como el otro; con `mejorado`, el estilo «Mejorado» (interior suave, banda de 6 u con degradado continuo) y, en `borde`, el reborde
        /// claro de las Facciones que contrastan poco (0..255).</summary>
        public static Color32[] Pintar(List<RellenoDeTerritorio> rellenos, double ox, double oy, EstiloDeTerritorio estilo, out byte[] borde)
        {
            bool mejorado = estilo != EstiloDeTerritorio.Prototipo;
            var pr = Parametros(estilo);
            var rb = new float[Px * Px];
            var r = new float[Px * Px];
            var g = new float[Px * Px];
            var b = new float[Px * Px];
            var a = new float[Px * Px];
            double k = Px / Lado;
            var dentro = new bool[Px * Px];
            var distancia = new float[Px * Px];
            foreach (var rel in rellenos)
            {
                // las aristas del relleno, en píxeles
                var aristas = new List<(double x0, double y0, double x1, double y1)>();
                foreach (var c in rel.Contornos)
                {
                    if (c.Count < 3) continue;
                    for (int i = 0; i < c.Count; i++)
                    {
                        var p = c[i];
                        var q = c[(i + 1) % c.Count];
                        aristas.Add(((p.x - ox) * k, (p.y - oy) * k, (q.x - ox) * k, (q.y - oy) * k));
                    }
                }
                if (aristas.Count == 0) continue;
                // dentro: por filas, con la regla del relleno (par-impar o distinto de cero)
                Array.Clear(dentro, 0, dentro.Length);
                var cortes = new List<(double x, int sentido)>();
                for (int j = 0; j < Px; j++)
                {
                    double y = j + 0.5;
                    cortes.Clear();
                    foreach (var e in aristas)
                        if ((e.y0 <= y) != (e.y1 <= y)) cortes.Add((e.x0 + (y - e.y0) / (e.y1 - e.y0) * (e.x1 - e.x0), e.y1 > e.y0 ? 1 : -1));
                    if (cortes.Count == 0) continue;
                    cortes.Sort((u, v) => u.x.CompareTo(v.x));
                    int vueltas = 0;
                    for (int c = 0; c < cortes.Count - 1; c++)
                    {
                        vueltas += rel.ParImpar ? 1 : cortes[c].sentido;
                        bool lleno = rel.ParImpar ? (vueltas & 1) == 1 : vueltas != 0;
                        if (!lleno) continue;
                        int i0 = Math.Max(0, (int)Math.Ceiling(cortes[c].x - 0.5)), i1 = Math.Min(Px - 1, (int)Math.Ceiling(cortes[c + 1].x - 0.5) - 1);
                        for (int i = i0; i <= i1; i++) dentro[j * Px + i] = true;
                    }
                }
                // la distancia al borde (u), solo cerca de él: lo que alcanzan los trazos del borde
                for (int i = 0; i < distancia.Length; i++) distancia[i] = float.PositiveInfinity;
                double alcance = (mejorado ? Banda : AnchoBorde) * k;
                foreach (var e in aristas)
                {
                    int i0 = Math.Max(0, (int)Math.Floor(Math.Min(e.x0, e.x1) - alcance)), i1 = Math.Min(Px - 1, (int)Math.Ceiling(Math.Max(e.x0, e.x1) + alcance));
                    int j0 = Math.Max(0, (int)Math.Floor(Math.Min(e.y0, e.y1) - alcance)), j1 = Math.Min(Px - 1, (int)Math.Ceiling(Math.Max(e.y0, e.y1) + alcance));
                    double dx = e.x1 - e.x0, dy = e.y1 - e.y0, l = dx * dx + dy * dy;
                    for (int j = j0; j <= j1; j++)
                        for (int i = i0; i <= i1; i++)
                        {
                            double px = i + 0.5, py = j + 0.5;
                            double t = l > 0 ? Math.Max(0, Math.Min(1, ((px - e.x0) * dx + (py - e.y0) * dy) / l)) : 0;
                            double ex = px - e.x0 - t * dx, ey = py - e.y0 - t * dy;
                            float d = (float)(Math.Sqrt(ex * ex + ey * ey) / k);
                            if (d < distancia[j * Px + i]) distancia[j * Px + i] = d;
                        }
                }
                double refuerzo = mejorado ? 1 : TrazadoDelMapa.Refuerzo(rel.Color);
                double interior = rel.Alfa >= TrazosYTerritorio.AlfaPropio - 1e-6 ? pr.propio : pr.ajeno;
                bool conReborde = mejorado && PocoContraste(rel.Color);
                double alfaFondo = Math.Min(1, rel.Alfa * refuerzo), alfaCapa = Math.Min(1, AlfaBorde * refuerzo);
                float cr = ((rel.Color >> 16) & 0xff) / 255f, cg = ((rel.Color >> 8) & 0xff) / 255f, cb = (rel.Color & 0xff) / 255f;
                for (int i = 0; i < dentro.Length; i++)
                {
                    if (!dentro[i]) continue;
                    // los trazos del borde (medios anchos 4, 3,33 ... 0,67 u) que llegan hasta aquí, cada uno encima del anterior
                    float alfa;
                    if (mejorado)
                    {
                        double d = distancia[i];
                        // un filo pintado junto a la línea, no una cinta: pico de 0,55 y caída a la mitad cada 0,45 u (a 1 u, hacia 0,12; a 2 u, el interior)
                        alfa = (float)(interior + (pr.pico - interior) * Math.Pow(0.5, d / pr.mitad));
                        // el reborde claro: una línea fina pegada a los trazos por dentro (centro a 0,35 u, σ 0,15 u, opacidad 0,5), nunca más fina que
                        // dos píxeles del lienzo
                        double sigma = Math.Max(0.15, Lado / Px);
                        if (conReborde) rb[i] = Math.Max(rb[i], (float)(0.5 * Math.Exp(-Math.Pow((d - 0.35) / sigma, 2))));
                    }
                    else
                    {
                        int capas = 0;
                        for (int c = 0; c < CapasBorde; c++) if (distancia[i] < AnchoBorde * (CapasBorde - c) / CapasBorde) capas++;
                        alfa = (float)(1 - (1 - alfaFondo) * Math.Pow(1 - alfaCapa, capas));
                    }
                    // source-over del color de la Facción sobre lo que había
                    float ao = alfa + a[i] * (1 - alfa);
                    if (ao <= 0) continue;
                    r[i] = (cr * alfa + r[i] * a[i] * (1 - alfa)) / ao;
                    g[i] = (cg * alfa + g[i] * a[i] * (1 - alfa)) / ao;
                    b[i] = (cb * alfa + b[i] * a[i] * (1 - alfa)) / ao;
                    a[i] = ao;
                }
            }
            var o = new Color32[Px * Px];
            for (int i = 0; i < o.Length; i++)
                o[i] = new Color32((byte)Math.Round(r[i] * 255), (byte)Math.Round(g[i] * 255), (byte)Math.Round(b[i] * 255), (byte)Math.Round(a[i] * 255));
            borde = new byte[Px * Px];
            for (int i = 0; i < borde.Length; i++) borde[i] = (byte)Math.Round(Math.Min(1, rb[i]) * 255);
            return o;
        }

        public void Destruir()
        {
            Shader.SetGlobalVector(Caja, Vector4.zero);
            UnityEngine.Object.Destroy(textura);
            UnityEngine.Object.Destroy(reborde);
        }
    }
}

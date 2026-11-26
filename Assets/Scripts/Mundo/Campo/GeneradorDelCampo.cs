// El generador del campo de batalla del lugar (Campo_De_Batalla_Del_Lugar.md §5 y §6; §6.0 da el paso a paso): el puerto a enteros de la mesa
// (`generarCampo` de `pruebas/campo3d/generador.ts`, BronzeAgeClient), en su mismo orden y con sus cifras, para que se puedan comparar línea a
// línea. Todo en el marco del campo: mm desde el centro (x al este, y al sur); fracciones en Q16. Lo que solo es informe en la mesa (rumbo,
// pendiente media, variación interna) y la decoración del suelo, que solo se ve, no están: se añaden con el montaje en Unity.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static ConquestTactics.Mundo.Campo.Entero;

namespace ConquestTactics.Mundo.Campo
{
    /// <summary>Las cifras de la §13 (las de la mesa), en mm, Q16 o grados.</summary>
    public sealed class Parametros
    {
        public long LadoPequena = 600_000, DistanciaPequena = 275_000, LadoGrande = 1_000_000, DistanciaGrande = 450_000, AnilloFondo = 700_000;
        public long RelieveMinimo = 8_000;
        public long DetalleAmplitud = 2_000, DetalleRugosidad = Uno, DetalleLongitud = 400_000, DetallePendiente = 19_661, DetallePersistencia = 32_768;
        public long AcentoRelieve = Uno;
        /// <summary>En milésimas de u del mundo.</summary>
        public long RadioAcento = 3_000;
        public long PendienteMaxTransitable = 45;
        public long RadioPlaca = 25_000, RelieveColinas = 15_000;
        public long BosqueCampo = 19_661, UmbralBosqueClaro = 16_384, UmbralBosqueCerrado = 39_322;
        public long SeparacionCerrado = 4_000;
        /// <summary>En milésimas de u del mundo (se multiplican por la escala del campo).</summary>
        public long AnchoRioMundo = 1_500, AnchoVadoMundo = 800;
        public long AlturaOrilla = 2_000, AnchoPlaya = 60_000, AnchoSomero = 25_000;
        public int MaxArboles = 6000;
        public long SotosCelda = 70_000, SotosProbabilidad = 16_384;

        public static readonly Parametros PorDefecto = new Parametros();
    }

    public enum Zona : byte { Llano, Colina, BosqueClaro, BosqueCerrado, Arena, Somero, Honda, Camino, Acantilado }

    public enum TipoDeCampo { Vado, Paso, Costa, Lago, Ribera, Colinas, Bosque, Llano }

    public sealed class Lectura
    {
        public TipoDeCampo Tipo;
        /// <summary>El vado del tipo vado (el más cercano al centro), en el campo; y el índice de su río.</summary>
        public Punto? Vado;
        public int RioDelVado = -1;
        /// <summary>Fracción de montaña o cima (Q16), desnivel del mundo (mm) y bosque medio (Q16) del área jugable.</summary>
        public long Montana, Desnivel, BosqueMedio;
    }

    public sealed class Spawns
    {
        /// <summary>El centro de la placa de cada bando, en el campo.</summary>
        public Punto Atacante, Defensor;
        /// <summary>Los puntos 1-3 (atacante) y 4-6 (defensor).</summary>
        public Punto[] Puntos;
        /// <summary>Del defensor al atacante, unitario en Q30.</summary>
        public Punto Eje;
        /// <summary>Lo que hubo que girar el eje (grados) y la distancia al centro que quedó (mm).</summary>
        public long Giro, Distancia;
    }

    public sealed class Informe
    {
        public TipoDeCampo Tipo;
        public bool LlanoForzado;
        /// <summary>Árboles de todo el campo y del área jugable, y sotos puestos.</summary>
        public int Arboles, ArbolesJugables, Sotos;
        /// <summary>Fracción del área jugable que es acantilado (Q16) y desnivel del área jugable (mm).</summary>
        public long Acantilado, Desnivel;
        public List<string> Avisos = new List<string>();
    }

    public sealed class CampoGenerado
    {
        /// <summary>Lado de todo el campo (jugable más el anillo) y del área jugable, en mm.</summary>
        public long Lado, Jugable;
        public Lugar Lugar;
        public Lectura Lectura;
        /// <summary>(n × n) alturas (mm) cada `PasoAlturas`, desde la esquina noroeste (−lado/2, −lado/2).</summary>
        public int NAlturas;
        public long PasoAlturas;
        public int[] Alturas;
        /// <summary>(n × n) celdas de `CeldaZonas`, desde la misma esquina.</summary>
        public int NZonas;
        public long CeldaZonas;
        public Zona[] Zonas;
        /// <summary>Altura de la lámina de agua de cada celda de zonas; `SinAgua` en seco.</summary>
        public int[] Superficie;
        /// <summary>La altura del mundo (mm) de una del campo: `AlturaBase + altura · 4 / 25` (las alturas del campo van con el zoom, ×6,25).</summary>
        public long AlturaBase;
        /// <summary>Por celda de zonas: la densidad de bosque (Q16, con su ruido).</summary>
        public int[] Bosque;
        /// <summary>Por vértice de las alturas: la lámina de agua (mm; `SinAgua` en seco) y lo pisado del camino que baja a los vados (Q16).</summary>
        public int[] Lamina, Pisado;
        /// <summary>x, y de cada árbol, en mm del campo.</summary>
        public long[] Arboles;
        public List<Punto> Vados;
        /// <summary>Ancho del paso de los vados, en mm.</summary>
        public long AnchoVado;
        public Spawns Spawns;
        public Informe Informe;

        public const int SinAgua = int.MinValue;
    }

    public static class GeneradorDelCampo
    {
        // lo que no se calibra en la mesa (mm, Q16 o grados)
        const long PasoAlturas = 2_000, CeldaZonas = 4_000;
        const long MmPorMilesimaDelMundo = 12; // la escala del mapa de mundo (12 m por u)
        const long Zoom25 = 25, Zoom4 = 4; // el zoom de las alturas: 75 / 12 = 25 / 4
        const long ProfundidadCauce = 1_500, RampaOrilla = 8_000, RampaVado = 15_000;
        const long PendienteBajadaVado = 13_107, PendienteFinBajada = 58_982, BajadaVado = 100_000;
        const long VadoLejosDelAgua = 750 * Lugar.EscalaCampo, IgualarAlLago = 120_000;
        const int VueltasLamina = 4, SuaveLamina = 3;
        const long RadioLamina = 12_000, RadioLecho = 20_000;
        const long EmbudoAncho = 45_000, EmbudoLargo = 25_000, EmbudoPendiente = 32_768;
        const long PendienteColina = 12;
        const long SeparacionPuntos = 15_000, DistanciaMinimaSpawn = 100_000, PasoAcorte = 25_000, PasoGiro = 5, Coinciden = 20_000;
        const long BosqueMinimo = 13_107;
        const long LargoMinimoDetalle = 10_000, AltoPorLargo = 13_107, PendienteTopeDetalle = 22_938;
        const long DetalleLlano = 32_768, DetalleColina = Uno, DetalleMontana = 2 * Uno;
        const long BuscaFondo = 3_000 * Lugar.EscalaCampo, HondoFondo = 8_000;
        const int SuaveFondo = 12;
        const long Infinito = long.MaxValue / 4;
        // las vueltas (2³²) de cada octava del detalle: n · 0,9 + 0,4 radianes
        static readonly uint[] GiroDeOctava = { 273426110, 888634858, 1503843606, 2119052354, 2734261102, 3349469850 };

        // ─────────────────────────── el tipo de campo (§5) ───────────────────────────

        /// <summary>¿Cae el vado `v` (en el campo) en un lago o en el mar, o a menos de 0,75 u de su orilla?</summary>
        static bool VadoEnAgua(Lugar r, Punto v)
        {
            long e = VadoLejosDelAgua;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (r.AguaQ16.Cercana(v.X + dx * e, v.Y + dy * e) >= Uno) return true;
            return false;
        }

        /// <summary>¿Corta el tramo a-b la caja? (Liang-Barsky, con los parámetros como fracciones.)</summary>
        static bool TramoCortaCaja(Punto a, Punto b, long x0, long y0, long x1, long y1)
        {
            long n0 = 0, d0 = 1, n1 = 1, d1 = 1;
            long dx = b.X - a.X, dy = b.Y - a.Y;
            var pq = new[] { (-dx, a.X - x0), (dx, x1 - a.X), (-dy, a.Y - y0), (dy, y1 - a.Y) };
            foreach (var (p, q) in pq)
            {
                if (p == 0)
                {
                    if (q < 0) return false;
                    continue;
                }
                // t = q / p, con el denominador positivo
                long tn = p < 0 ? -q : q, td = Math.Abs(p);
                if (p < 0) { if (tn * d0 > n0 * td) { n0 = tn; d0 = td; } }
                else if (tn * d1 < n1 * td) { n1 = tn; d1 = td; }
                if (n0 * d1 > n1 * d0) return false;
            }
            return true;
        }

        static long Medio(Parametros p, Lugar r) => (r.Grande ? p.LadoGrande : p.LadoPequena) / 2;

        /// <summary>El primer tipo de la tabla de la §5 que se cumple en el área jugable.</summary>
        public static Lectura LeerElLugar(Lugar r, Parametros p = null)
        {
            p = p ?? Parametros.PorDefecto;
            long h = Medio(p, r), paso = r.Alturas.Paso;
            // montaña o cima, por celdas de 4 u, con muestras cada 0,5 u
            long montana = 0, celdas = 0;
            for (long y = -h + paso / 2; y < h; y += paso)
                for (long x = -h + paso / 2; x < h; x += paso)
                {
                    celdas++;
                    if (r.Terreno.Cercana(x, y) >= 5) montana++;
                }
            long min = long.MaxValue, max = long.MinValue, bosque = 0, n = 0;
            bool hayMar = false, hayLago = false;
            for (long y = -h; y <= h; y += paso)
                for (long x = -h; x <= h; x += paso)
                {
                    long a = r.Alturas.Muestra(x, y);
                    n++;
                    min = Math.Min(min, a);
                    max = Math.Max(max, a);
                    bosque += r.Bosque.Muestra(x, y);
                    int agua = r.Agua.Cercana(x, y);
                    if (agua == 2) hayMar = true;
                    else if (agua == 1) hayLago = true;
                }
            var l = new Lectura { Montana = montana * Uno / celdas, Desnivel = max - min, BosqueMedio = bosque / n };
            long mejor = long.MaxValue;
            for (int i = 0; i < r.Rios.Count; i++)
                foreach (var v in r.Rios[i].Vados)
                {
                    long d = v.X * v.X + v.Y * v.Y;
                    if (Math.Abs(v.X) <= h && Math.Abs(v.Y) <= h && d < mejor && !VadoEnAgua(r, v))
                    {
                        l.Vado = v;
                        l.RioDelVado = i;
                        mejor = d;
                    }
                }
            bool rioCruza = r.Rios.Any(rio => Enumerable.Range(0, rio.Puntos.Length - 1).Any(i => TramoCortaCaja(rio.Puntos[i], rio.Puntos[i + 1], -h, -h, h, h)));
            l.Tipo = l.Vado.HasValue ? TipoDeCampo.Vado
                : l.Montana >= Uno / 2 ? TipoDeCampo.Paso
                : hayMar ? TipoDeCampo.Costa
                : hayLago ? TipoDeCampo.Lago
                : rioCruza ? TipoDeCampo.Ribera
                : l.Desnivel > p.RelieveColinas ? TipoDeCampo.Colinas
                : l.BosqueMedio > p.BosqueCampo ? TipoDeCampo.Bosque
                : TipoDeCampo.Llano;
            return l;
        }

        // ─────────────────────────── utilidades ───────────────────────────

        /// <summary>La media de caja de radio `R` muestras, en x y en y, `vueltas` veces (con dos, casi una gaussiana).</summary>
        static long[] Suavizar(long[] d, int n, int m, int R, int vueltas)
        {
            var s = (long[])d.Clone();
            for (int vuelta = 0; vuelta < vueltas; vuelta++)
                foreach (bool enX in new[] { true, false })
                {
                    var t = new long[n * m];
                    int largo = enX ? n : m, filas = enX ? m : n;
                    for (int f = 0; f < filas; f++)
                    {
                        long Lee(int q) => enX ? s[f * n + Clamp(q, 0, n - 1)] : s[Clamp(q, 0, m - 1) * n + f];
                        long suma = 0;
                        for (int k = -R; k <= R; k++) suma += Lee(k);
                        for (int q = 0; q < largo; q++)
                        {
                            t[enX ? f * n + q : q * n + f] = DivRedondo(suma, 2 * R + 1);
                            suma += Lee(q + R + 1) - Lee(q - R);
                        }
                    }
                    s = t;
                }
            return s;
        }

        static long[] ALargo(int[] a) => Array.ConvertAll(a, v => (long)v);
        static int[] AEntero(long[] a) => Array.ConvertAll(a, v => (int)v);

        /// <summary>Los rasgos del relieve del mundo: la altura acentuada, la rugosidad (mm del mundo) y la pendiente media (Q16, m por m).</summary>
        static (Rejilla acentuada, Rejilla rugosidad, Rejilla pendiente) RasgosDelMundo(Rejilla a, Parametros p, long pasoMilesimas)
        {
            int n = a.Columnas, m = a.Filas;
            var datos = ALargo(a.Datos);
            var s = Suavizar(datos, n, m, (int)Math.Max(1, DivRedondo(p.RadioAcento, pasoMilesimas)), 2);
            var acentuada = new int[n * m];
            for (int k = 0; k < n * m; k++) acentuada[k] = (int)(s[k] + (p.AcentoRelieve * (datos[k] - s[k]) >> 16));
            var vecinas = Suavizar(datos, n, m, 1, 1);
            var cuadrados = new long[n * m];
            for (int k = 0; k < n * m; k++) cuadrados[k] = (datos[k] - vecinas[k]) * (datos[k] - vecinas[k]);
            var rugosidad = Array.ConvertAll(Suavizar(cuadrados, n, m, 2, 1), v => (int)Raiz(v));
            long At(int i, int j) => datos[Clamp(j, 0, m - 1) * n + Clamp(i, 0, n - 1)];
            var cruda = new long[n * m];
            long denominador = 2 * pasoMilesimas * MmPorMilesimaDelMundo;
            for (int j = 0, k = 0; j < m; j++)
                for (int i = 0; i < n; i++, k++) cruda[k] = Hipot(At(i + 1, j) - At(i - 1, j), At(i, j + 1) - At(i, j - 1)) * Uno / denominador;
            return (a.ConDatos(acentuada), a.ConDatos(rugosidad), a.ConDatos(AEntero(Suavizar(cruda, n, m, 2, 1))));
        }

        /// <summary>Distancia (mm) de cada celda a la marcada más cercana (chaflán, dos pasadas).</summary>
        static long[] DistanciaA(bool[] marcadas, int n, long paso)
        {
            var d = new long[n * n];
            for (int k = 0; k < n * n; k++) d[k] = marcadas[k] ? 0 : Infinito;
            long recto = paso, diagonal = DivRedondo(paso * 14142136, 10000000);
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    int k = j * n + i;
                    long v = d[k];
                    if (i > 0) v = Math.Min(v, d[k - 1] + recto);
                    if (j > 0)
                    {
                        v = Math.Min(v, d[k - n] + recto);
                        if (i > 0) v = Math.Min(v, d[k - n - 1] + diagonal);
                        if (i + 1 < n) v = Math.Min(v, d[k - n + 1] + diagonal);
                    }
                    d[k] = v;
                }
            for (int j = n - 1; j >= 0; j--)
                for (int i = n - 1; i >= 0; i--)
                {
                    int k = j * n + i;
                    long v = d[k];
                    if (i + 1 < n) v = Math.Min(v, d[k + 1] + recto);
                    if (j + 1 < n)
                    {
                        v = Math.Min(v, d[k + n] + recto);
                        if (i + 1 < n) v = Math.Min(v, d[k + n + 1] + diagonal);
                        if (i > 0) v = Math.Min(v, d[k + n - 1] + diagonal);
                    }
                    d[k] = v;
                }
            return d;
        }

        /// <summary>Componentes conexas (4 vecinos) de las celdas transitables; −1 en las que no lo son.</summary>
        static int[] Componentes(Func<int, bool> transitable, int n)
        {
            var comp = new int[n * n];
            for (int k = 0; k < comp.Length; k++) comp[k] = -1;
            var cola = new int[n * n];
            int siguiente = 0;
            for (int k0 = 0; k0 < n * n; k0++)
            {
                if (comp[k0] != -1 || !transitable(k0)) continue;
                int ini = 0, fin = 0;
                cola[fin++] = k0;
                comp[k0] = siguiente;
                while (ini < fin)
                {
                    int k = cola[ini++], i = k % n, j = k / n;
                    foreach (int v in new[] { i > 0 ? k - 1 : -1, i + 1 < n ? k + 1 : -1, j > 0 ? k - n : -1, j + 1 < n ? k + n : -1 })
                        if (v >= 0 && comp[v] == -1 && transitable(v))
                        {
                            comp[v] = siguiente;
                            cola[fin++] = v;
                        }
                }
                siguiente++;
            }
            return comp;
        }

        /// <summary>El vector `v` (Q30) girado `grados`.</summary>
        static Punto Girar(Punto v, long grados)
        {
            uint a = Grados(grados);
            long c = Coseno(a), s = Seno(a);
            return new Punto((v.X * c - v.Y * s) >> 30, (v.X * s + v.Y * c) >> 30);
        }

        /// <summary>¿Sube más de `grados` la pendiente de (dx, dy) mm de desnivel en `largo` mm?</summary>
        static bool MasEmpinado(long dx, long dy, long largo, long tangenteQ30)
        {
            long umbral = largo * tangenteQ30 >> 20; // mm en 1/1024
            return (dx * dx + dy * dy) << 20 > umbral * umbral;
        }

        /// <summary>Cuánto de la hondonada del cauce hay a `d` mm de su eje (Q16): entera en el cauce y, fuera, se apaga en una orilla de medio
        /// ancho de cauce (como poco `RampaOrilla`).</summary>
        static long HondonadaDelCauce(long d, long medio) => Uno - Suave((d - medio) * Uno / Math.Max(RampaOrilla, medio / 2));

        /// <summary>El suavizado de Chaikin de la mesa (`lineas.ts`): `vueltas` cortes de esquinas, de un cuarto del tramo y como mucho `corteMax`.</summary>
        static List<Punto> Chaikin(IReadOnlyList<Punto> puntos, int vueltas, long corteMax)
        {
            var ps = new List<Punto>(puntos);
            for (int v = 0; v < vueltas && ps.Count >= 3; v++)
            {
                var o = new List<Punto> { ps[0] };
                for (int k = 0; k < ps.Count - 1; k++)
                {
                    Punto a = ps[k], b = ps[k + 1];
                    long l = Hipot(b.X - a.X, b.Y - a.Y), t = l > 0 ? Math.Min(Uno / 4, corteMax * Uno / l) : 0;
                    if (k > 0) o.Add(new Punto(a.X + ((b.X - a.X) * t >> 16), a.Y + ((b.Y - a.Y) * t >> 16)));
                    if (k < ps.Count - 2) o.Add(new Punto(b.X - ((b.X - a.X) * t >> 16), b.Y - ((b.Y - a.Y) * t >> 16)));
                }
                o.Add(ps[ps.Count - 1]);
                ps = o;
            }
            return ps;
        }

        /// <summary>El parámetro (Q16) del punto del tramo a-b más cercano a q, y el cuadrado de la distancia.</summary>
        static long Proyectar(Punto a, Punto b, long x, long y, out long distancia2)
        {
            long dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
            if (l2 == 0) l2 = 1;
            long t = Clamp(DivSuelo(((x - a.X) * dx + (y - a.Y) * dy) * Uno, l2), 0, Uno);
            long px = a.X + (dx * t >> 16) - x, py = a.Y + (dy * t >> 16) - y;
            distancia2 = px * px + py * py;
            return t;
        }

        /// <summary>El ancho (mm del campo) del río junto a (x, y): el de su tramo más cercano, interpolado (`anchoJunto` de la mesa).</summary>
        static long AnchoJunto(RioDelLugar r, long x, long y)
        {
            long mejor = long.MaxValue, ancho = r.Anchos[0];
            for (int k = 0; k + 1 < r.Puntos.Length; k++)
            {
                long t = Proyectar(r.Puntos[k], r.Puntos[k + 1], x, y, out long d2);
                long a0 = r.Anchos[Math.Min(k, r.Anchos.Length - 1)], a1 = r.Anchos[Math.Min(k + 1, r.Anchos.Length - 1)];
                if (d2 < mejor) { mejor = d2; ancho = a0 + ((a1 - a0) * t >> 16); }
            }
            return ancho;
        }

        /// <summary>El río por el fondo de su valle (`alFondoDelValle` de la mesa): cada punto se mueve de lado, hasta `alcance`, al punto más
        /// bajo del suelo del campo, si es un fondo de verdad; lo que se mueve se suaviza a lo largo del río.</summary>
        static List<Punto> AlFondoDelValle(List<Punto> puntos, Rejilla H, long alcance)
        {
            int n = puntos.Count;
            if (n < 3) return puntos;
            // la normal de cada punto, sin normalizar, con su largo
            var nx = new long[n]; var ny = new long[n]; var nl = new long[n];
            for (int i = 0; i < n; i++)
            {
                Punto a = puntos[Math.Max(0, i - 2)], b = puntos[Math.Min(n - 1, i + 2)];
                nx[i] = -(b.Y - a.Y);
                ny[i] = b.X - a.X;
                nl[i] = Math.Max(1, Hipot(nx[i], ny[i]));
            }
            long paso = Math.Max(2_000, alcance / 40);
            var ts = new List<long>();
            for (long t = -alcance; t <= alcance; t += paso) ts.Add(t);
            int m = ts.Count;
            var hs = new long[m]; var izq = new long[m]; var der = new long[m];
            var crudo = new long[n];
            for (int i = 0; i < n; i++)
            {
                Punto q = puntos[i];
                for (int k = 0; k < m; k++) hs[k] = H.Muestra(q.X + nx[i] * ts[k] / nl[i], q.Y + ny[i] * ts[k] / nl[i]);
                // el máximo a cada lado de cada muestra
                for (int k = 0; k < m; k++) izq[k] = k == 0 ? long.MinValue : Math.Max(izq[k - 1], hs[k - 1]);
                for (int k = m - 1; k >= 0; k--) der[k] = k == m - 1 ? long.MinValue : Math.Max(der[k + 1], hs[k + 1]);
                long mejor = 0, bajo = long.MaxValue;
                for (int k = 1; k + 1 < m; k++)
                {
                    if (!(hs[k] < hs[k - 1] && hs[k] <= hs[k + 1])) continue;
                    if (izq[k] - hs[k] < HondoFondo || der[k] - hs[k] < HondoFondo) continue;
                    if (hs[k] < bajo - 1 || (Math.Abs(hs[k] - bajo) <= 1 && Math.Abs(ts[k]) < Math.Abs(mejor))) { bajo = hs[k]; mejor = ts[k]; }
                }
                crudo[i] = bajo < H.Muestra(q.X, q.Y) ? mejor : 0;
            }
            var salida = new List<Punto>(n);
            for (int i = 0; i < n; i++)
            {
                long s = 0, c = 0;
                for (int j = Math.Max(0, i - SuaveFondo); j <= Math.Min(n - 1, i + SuaveFondo); j++) { s += crudo[j]; c++; }
                long t = DivRedondo(s, c);
                salida.Add(new Punto(puntos[i].X + nx[i] * t / nl[i], puntos[i].Y + ny[i] * t / nl[i]));
            }
            return salida;
        }

        // ─────────────────────────── el campo (§6) ───────────────────────────

        /// <summary>El campo de la batalla a partir del recorte del lugar.</summary>
        public static CampoGenerado Generar(Lugar r, Parametros p = null, bool llanoForzado = false)
        {
            p = p ?? Parametros.PorDefecto;
            var lectura = LeerElLugar(r, p);
            var tipo = llanoForzado ? TipoDeCampo.Llano : lectura.Tipo;
            var avisos = new List<string>();
            long J = r.Grande ? p.LadoGrande : p.LadoPequena, L = J + 2 * p.AnilloFondo, distanciaAlCentro = r.Grande ? p.DistanciaGrande : p.DistanciaPequena;
            long AX = r.AbsolutoX, AY = r.AbsolutoY;
            bool Jugable(long x, long y) => Math.Abs(x) <= J / 2 && Math.Abs(y) <= J / 2;
            long tanTransitable = Tangente(p.PendienteMaxTransitable), tanColina = Tangente(PendienteColina);

            // ── relieve (§6.2)
            long pasoMilesimas = r.Alturas.Paso / Lugar.EscalaCampo;
            var (acentuada, rugosidad, pendienteMundo) = RasgosDelMundo(r.Alturas, p, pasoMilesimas);
            Rejilla DeClase(Func<int, bool> f) => r.Terreno.ConDatos(Array.ConvertAll(r.Terreno.Datos, c => f(c) ? Uno : 0));
            Rejilla esColina = DeClase(c => c == 4), esMontana = DeClase(c => c >= 5);
            int nA = (int)(L / PasoAlturas) + 1;
            var H = new int[nA * nA];
            long Vx(int i) => -L / 2 + i * PasoAlturas;
            long media = 0, nMedia = 0;
            for (int j = 0, k = 0; j < nA; j++)
                for (int i = 0; i < nA; i++, k++)
                {
                    H[k] = (int)acentuada.Cubica(Vx(i), Vx(j));
                    if (Jugable(Vx(i), Vx(j))) { media += H[k]; nMedia++; }
                }
            media = DivRedondo(media, nMedia);
            long Rango(int[] a)
            {
                long min = long.MaxValue, max = long.MinValue;
                for (int j = 0, k = 0; j < nA; j++)
                    for (int i = 0; i < nA; i++, k++)
                        if (Jugable(Vx(i), Vx(j))) { min = Math.Min(min, a[k]); max = Math.Max(max, a[k]); }
                return max - min;
            }
            // el campo es un zoom sobre el sitio: la altura crece lo mismo que el plano (75 m por u contra los 12 del mapa de mundo)
            for (int k = 0; k < H.Length; k++) H[k] = (int)DivRedondo((H[k] - media) * Zoom25, Zoom4);
            long desnivelMundo = Rango(H);
            if (desnivelMundo < p.RelieveMinimo)
            {
                var onda = new int[H.Length];
                for (int j = 0, k = 0; j < nA; j++)
                    for (int i = 0; i < nA; i++, k++) onda[k] = (int)(Manchas(AX + Vx(i), AY + Vx(j), 220_000, 11) - Uno / 2);
                long rangoOnda = Math.Max(1, Rango(onda));
                for (int k = 0; k < H.Length; k++) H[k] += (int)DivRedondo(onda[k] * (p.RelieveMinimo - desnivelMundo), rangoOnda);
            }
            // ── el detalle fractal (§6.2): octavas de `DetalleLongitud` a unos 10 m, con la altura que admite la pendiente del mundo
            var octavas = new List<(long largo, long peso, long cos, long sen, int semilla)>();
            for (long largo = p.DetalleLongitud, peso = Uno; largo >= LargoMinimoDetalle; largo /= 2, peso = peso * p.DetallePersistencia >> 16)
            {
                int n = octavas.Count;
                octavas.Add((largo, peso, Coseno(GiroDeOctava[n]), Seno(GiroDeOctava[n]), 20 + n));
            }
            long sumaPesos = octavas.Sum(o => o.peso);
            for (int j = 0, k = 0; j < nA; j++)
                for (int i = 0; i < nA; i++, k++)
                {
                    long X = AX + Vx(i), Y = AY + Vx(j), x = Vx(i), y = Vx(j);
                    long colina = esColina.Muestra(x, y), montana = esMontana.Muestra(x, y);
                    long amplitud = (p.DetalleAmplitud * (DetalleLlano + (colina * DetalleColina >> 16) + (montana * DetalleMontana >> 16)) >> 16)
                        + (p.DetalleRugosidad * rugosidad.Muestra(x, y) * Zoom25 / Zoom4 >> 16);
                    long ladera = p.DetallePendiente * Math.Min(pendienteMundo.Muestra(x, y), PendienteTopeDetalle) >> 16;
                    long suma = 0;
                    foreach (var o in octavas)
                    {
                        long xr = (X * o.cos - Y * o.sen) >> 30, yr = (X * o.sen + Y * o.cos) >> 30;
                        long ondulado = 2 * Manchas(xr, yr, o.largo, o.semilla) - Uno, arista = Uno - Math.Abs(ondulado);
                        // la arista, de media casi cero: 1,5 · arista² − 0,5
                        long forma = ((Uno - montana) * ondulado + montana * ((3 * (arista * arista >> 16) - Uno) / 2)) >> 16;
                        long alto = Math.Min((ladera * o.largo >> 16) + amplitud * o.peso / sumaPesos, o.largo * AltoPorLargo >> 16);
                        suma += alto * forma >> 16;
                    }
                    H[k] += (int)suma;
                }
            var rejillaH = new Rejilla(-L / 2, -L / 2, PasoAlturas, nA, nA, H);
            var lamina = new int[nA * nA];
            for (int k = 0; k < lamina.Length; k++) lamina[k] = CampoGenerado.SinAgua;
            var pisado = new int[nA * nA];

            // ── agua de mar y lago (§6.3)
            int nZ = (int)(L / CeldaZonas);
            long Zc(int i) => -L / 2 + i * CeldaZonas + CeldaZonas / 2;
            var zonas = new Zona[nZ * nZ];
            var superficie = new int[nZ * nZ];
            for (int k = 0; k < superficie.Length; k++) superficie[k] = CampoGenerado.SinAgua;
            var esAgua = new bool[nZ * nZ];
            var esTierra = new bool[nZ * nZ];
            for (int j = 0, k = 0; j < nZ; j++)
                for (int i = 0; i < nZ; i++, k++)
                {
                    esAgua[k] = r.AguaQ16.Muestra(Zc(i), Zc(j)) >= Uno / 2;
                    esTierra[k] = !esAgua[k];
                }
            long[] dAgua = DistanciaA(esAgua, nZ, CeldaZonas), dTierra = DistanciaA(esTierra, nZ, CeldaZonas);
            Rejilla RejillaZ(int[] datos) => new Rejilla(-L / 2 + CeldaZonas / 2, -L / 2 + CeldaZonas / 2, CeldaZonas, nZ, nZ, datos);
            // (las distancias, topadas para caber en la rejilla: más allá del campo no cuentan)
            int[] Topadas(long[] d) => Array.ConvertAll(d, v => (int)Math.Min(v, 100_000_000));
            Rejilla zAgua = RejillaZ(Array.ConvertAll(esAgua, a => a ? Uno : 0)), zdAgua = RejillaZ(Topadas(dAgua)), zdTierra = RejillaZ(Topadas(dTierra));
            long nivel = 0, nOrilla = 0;
            for (int j = 0, k = 0; j < nZ; j++)
                for (int i = 0; i < nZ; i++, k++)
                    if (!esAgua[k] && dAgua[k] <= 8_000) { nivel += rejillaH.Muestra(Zc(i), Zc(j)); nOrilla++; }
            bool hayMar = nOrilla > 0;
            nivel = hayMar ? DivRedondo(nivel, nOrilla) : 0;
            if (hayMar)
            {
                for (int j = 0, k = 0; j < nA; j++)
                    for (int i = 0; i < nA; i++, k++)
                    {
                        long x = Vx(i), y = Vx(j);
                        if (zAgua.Muestra(x, y) >= Uno / 2)
                        {
                            long dt = zdTierra.Muestra(x, y);
                            H[k] = (int)(nivel - (dt <= p.AnchoSomero ? 200 + 600 * dt / p.AnchoSomero : 800 + Math.Min(3_000, (dt - p.AnchoSomero) / 8)));
                            lamina[k] = (int)nivel;
                        }
                        else
                        {
                            long da = zdAgua.Muestra(x, y);
                            if (da < p.AnchoPlaya) H[k] = (int)Lerp(nivel + 250, Math.Max(H[k], nivel + 250), Suave(da * Uno / p.AnchoPlaya));
                        }
                    }
                for (int k = 0; k < nZ * nZ; k++)
                    if (esAgua[k])
                    {
                        zonas[k] = dTierra[k] <= p.AnchoSomero ? Zona.Somero : Zona.Honda;
                        superficie[k] = (int)nivel;
                    }
            }

            // ── ríos (§6.3, §6.4, §6.5)
            var vados = new List<Punto>();
            var esVado = new bool[nZ * nZ];
            var esBajada = new bool[nZ * nZ];
            long medioVado = p.AnchoVadoMundo * Lugar.EscalaCampo / 2;
            var rioDelVado = new List<Punto>();
            {
                var dV = Llenos(nA * nA, Infinito); var mV = new long[nA * nA]; var lV = new long[nA * nA]; var aV = Llenos(nA * nA, Infinito);
                var dZ = Llenos(nZ * nZ, Infinito); var mZ = new long[nZ * nZ]; var lZ = new long[nZ * nZ]; var aZ = Llenos(nZ * nZ, Infinito);
                for (int ri = 0; ri < r.Rios.Count; ri++)
                {
                    var rio = r.Rios[ri];
                    long medioFijo = (rio.Navegable ? 2 : 1) * p.AnchoRioMundo * Lugar.EscalaCampo / 2;
                    var puntos = AlFondoDelValle(Chaikin(rio.Puntos, 4, 200_000), rejillaH, BuscaFondo);
                    int np = puntos.Count;
                    var medios = puntos.Select(q => rio.Anchos.Length > 0 ? AnchoJunto(rio, q.X, q.Y) / 2 : medioFijo).ToArray();
                    long medioMax = medios.Max(), alcance = medioMax + Math.Max(RampaVado, medioMax / 2);
                    if (ri == lectura.RioDelVado) rioDelVado = puntos;
                    // la lámina: en cada punto, `ProfundidadCauce` sobre lo más hondo de su corte ya tallado
                    var crudo = new long[np];
                    for (int i = 0; i < np; i++)
                    {
                        Punto a = puntos[Math.Max(0, i - 1)], b = puntos[Math.Min(np - 1, i + 1)], q = puntos[i];
                        long l = Math.Max(1, Hipot(b.X - a.X, b.Y - a.Y)), ex = -(b.Y - a.Y), ey = b.X - a.X, m = medios[i];
                        long menor = long.MaxValue;
                        for (int f4 = -4; f4 <= 4; f4++)
                        {
                            long desvio = m * f4 / 4;
                            long v = rejillaH.Muestra(q.X + ex * desvio / l, q.Y + ey * desvio / l) - ((p.AlturaOrilla + ProfundidadCauce) * HondonadaDelCauce(Math.Abs(desvio), m) >> 16);
                            menor = Math.Min(menor, v);
                        }
                        crudo[i] = menor + ProfundidadCauce;
                    }
                    // y, al llegar a un lago o al mar, la de su agua: en los últimos `IgualarAlLago` se va igualando a su nivel
                    var laminaRio = new long[np];
                    for (int i = 0; i < np; i++)
                    {
                        long s = 0, c = 0;
                        for (int d = -SuaveLamina; d <= SuaveLamina; d++) if (i + d >= 0 && i + d < np) { s += crudo[i + d]; c++; }
                        long liso = DivRedondo(s, c);
                        laminaRio[i] = hayMar ? Lerp(nivel, liso, Suave(zdAgua.Muestra(puntos[i].X, puntos[i].Y) * Uno / IgualarAlLago)) : liso;
                    }
                    var largoHasta = new long[np];
                    for (int i = 1; i < np; i++) largoHasta[i] = largoHasta[i - 1] + Hipot(puntos[i].X - puntos[i - 1].X, puntos[i].Y - puntos[i - 1].Y);
                    // el tramo de cada vado de este río dentro del campo
                    var sVados = new List<long>();
                    foreach (var vc in rio.Vados)
                    {
                        if (Math.Abs(vc.X) > L / 2 || Math.Abs(vc.Y) > L / 2 || VadoEnAgua(r, vc)) continue;
                        long mejor = long.MaxValue, sv = 0;
                        for (int i = 0; i + 1 < np; i++)
                        {
                            long t = Proyectar(puntos[i], puntos[i + 1], vc.X, vc.Y, out long d2);
                            if (d2 < mejor) { mejor = d2; sv = largoHasta[i] + ((largoHasta[i + 1] - largoHasta[i]) * t >> 16); }
                        }
                        sVados.Add(sv);
                        vados.Add(vc);
                    }
                    // los segmentos, en una rejilla de celdas de `cerca`: cada punto solo mira los que pueden tocarle
                    long cerca = alcance + 2_000;
                    var rejilla = new Dictionary<long, List<int>>();
                    long Clave(long cx, long cy) => cx * 100003 + cy;
                    var porVado = new Dictionary<int, List<int>>();
                    long radioVado = medioMax + 2 * BajadaVado;
                    List<int> Junto(long x, long y)
                    {
                        int iv = vados.FindIndex(q => Math.Abs(x - q.X) <= radioVado && Math.Abs(y - q.Y) <= radioVado);
                        if (iv < 0) return null;
                        if (!porVado.TryGetValue(iv, out var lista))
                        {
                            Punto v = vados[iv];
                            long M = radioVado + BajadaVado;
                            lista = new List<int>();
                            for (int i = 0; i + 1 < np; i++)
                            {
                                Punto a = puntos[i], b = puntos[i + 1];
                                if (Math.Max(a.X, b.X) >= v.X - M && Math.Min(a.X, b.X) <= v.X + M && Math.Max(a.Y, b.Y) >= v.Y - M && Math.Min(a.Y, b.Y) <= v.Y + M) lista.Add(i);
                            }
                            porVado[iv] = lista;
                        }
                        return lista;
                    }
                    for (int i = 0; i + 1 < np; i++)
                    {
                        Punto a = puntos[i], b = puntos[i + 1];
                        for (long cy = DivSuelo(Math.Min(a.Y, b.Y) - cerca, cerca); cy <= DivSuelo(Math.Max(a.Y, b.Y) + cerca, cerca); cy++)
                            for (long cx = DivSuelo(Math.Min(a.X, b.X) - cerca, cerca); cx <= DivSuelo(Math.Max(a.X, b.X) + cerca, cerca); cx++)
                            {
                                long c = Clave(cx, cy);
                                if (!rejilla.TryGetValue(c, out var l)) rejilla[c] = l = new List<int>();
                                l.Add(i);
                            }
                    }
                    var vacia = new List<int>();
                    void Marcar(long x, long y, int k, long[] d, long[] m, long[] l, long[] av)
                    {
                        var lista = Junto(x, y);
                        if (lista == null && !rejilla.TryGetValue(Clave(DivSuelo(x, cerca), DivSuelo(y, cerca)), out lista)) lista = vacia;
                        foreach (int i in lista)
                        {
                            long t = Proyectar(puntos[i], puntos[i + 1], x, y, out long d2), dist = Raiz(d2);
                            long medio = Lerp(medios[i], medios[i + 1], t);
                            if (dist - medio < d[k] - m[k])
                            {
                                d[k] = dist;
                                m[k] = medio;
                                l[k] = Lerp(laminaRio[i], laminaRio[i + 1], t);
                                long sp = largoHasta[i] + ((largoHasta[i + 1] - largoHasta[i]) * t >> 16), menor = Infinito;
                                foreach (long sv in sVados) menor = Math.Min(menor, Math.Abs(sp - sv));
                                av[k] = menor;
                            }
                        }
                    }
                    // solo lo que cae cerca de la caja del río y, junto a cada vado, lo que alcanzan sus bajadas
                    long x0 = puntos.Min(q => q.X) - alcance, y0 = puntos.Min(q => q.Y) - alcance, x1 = puntos.Max(q => q.X) + alcance, y1 = puntos.Max(q => q.Y) + alcance;
                    bool Dentro(long x, long y) => (x >= x0 && x <= x1 && y >= y0 && y <= y1) || vados.Exists(v => Math.Abs(x - v.X) <= radioVado && Math.Abs(y - v.Y) <= radioVado);
                    for (int j = 0, k = 0; j < nA; j++) for (int i = 0; i < nA; i++, k++) if (Dentro(Vx(i), Vx(j))) Marcar(Vx(i), Vx(j), k, dV, mV, lV, aV);
                    for (int j = 0, k = 0; j < nZ; j++) for (int i = 0; i < nZ; i++, k++) if (Dentro(Zc(i), Zc(j))) Marcar(Zc(i), Zc(j), k, dZ, mZ, lZ, aZ);
                }
                // los vértices del lago o del mar: el borde fijo al suavizar la lámina de los ríos; y los del lecho de un vado
                var esLagoV = Array.ConvertAll(lamina, v => v != CampoGenerado.SinAgua);
                var esVadoV = new bool[nA * nA];
                // el paso del vado, natural: su medio ancho con ondulaciones, y más ancho junto a las orillas
                long AnchoDelPaso(long x, long y, long d, long medio)
                {
                    long X = AX + x, Y = AY + y;
                    long onda = Uno + (Manchas(X, Y, 40_000, 91) - Uno / 2) / 2 + (Manchas(X, Y, 12_000, 92) - Uno / 2) / 5;
                    long f = Math.Min(Uno, d * Uno / Math.Max(1_000, medio));
                    return (medioVado * onda >> 16) * (Uno + (22_938 * (f * f >> 16) >> 16)) >> 16;
                }
                // el embudo de la bajada: el medio ancho del pasillo a `fuera` del agua
                long Embudo(long fuera) => medioVado + (EmbudoAncho * (Uno - ExpNegativa(fuera * Uno / EmbudoLargo)) >> 16) + (EmbudoPendiente * fuera >> 16);
                // el camino pisado (Q16)
                // (sin vado en el río, `av` es infinito: lejos de todo vado no hay camino)
                long PisadoDe(long av, long fuera) => av >= Infinito ? 0 :
                    (Uno - Suave((av - (medioVado * 4 / 5 + fuera * 35 / 100)) * Uno / (12_000 + fuera / 4))) * (Uno - Suave((fuera - 40_000) * Uno / 60_000)) >> 16;
                // la altura de la bajada a `rr` de la entrada del vado: suave junto al agua y cada vez más empinada
                long Subida(long rr)
                {
                    long u = Math.Min(Uno, rr * Uno / BajadaVado), extra = PendienteFinBajada - PendienteBajadaVado;
                    long u3 = (u * u >> 16) * u >> 16, u4 = u3 * u >> 16;
                    return (PendienteBajadaVado * rr >> 16) + (extra * BajadaVado * (u3 - u4 / 2) >> 32) + (Math.Max(0, rr - BajadaVado) * extra >> 16);
                }
                // el suelo alisado: el lecho de un río es liso, sin los bultos del detalle
                var alisado = Suavizar(ALargo(H), nA, nA, (int)Math.Max(1, DivRedondo(RadioLecho, PasoAlturas)), 1);
                for (int j = 0, k = 0; j < nA; j++)
                    for (int i = 0; i < nA; i++, k++)
                    {
                        long d = dV[k], sup = lV[k], medio = mV[k], av = aV[k];
                        if (d == Infinito) continue;
                        // donde ya hay lago o mar, el río se ha acabado: solo su cauce, que sigue hasta lo hondo
                        if (lamina[k] != CampoGenerado.SinAgua)
                        {
                            if (d <= medio) H[k] = (int)Math.Min(H[k], lamina[k] - ProfundidadCauce);
                            continue;
                        }
                        long x = Vx(i), y = Vx(j);
                        bool cercaDelVado = av <= medioVado + RampaVado;
                        long rampa = cercaDelVado ? RampaVado : RampaOrilla;
                        if (d <= medio && av <= AnchoDelPaso(x, y, d, medio))
                        {
                            // el lecho del vado, de grava, a poca agua y no del todo liso
                            H[k] = (int)(sup - 250 - (300 * Manchas(AX + x, AY + y, 6_000, 93) >> 16));
                            lamina[k] = (int)sup;
                            esVadoV[k] = true;
                            continue;
                        }
                        // el cauce sigue al relieve: se le resta una hondonada suave, honda en el medio y nada en el borde
                        long hondonada = HondonadaDelCauce(d, medio);
                        if (hondonada > 0) H[k] = (int)(Lerp(H[k], alisado[k], hondonada) - ((p.AlturaOrilla + ProfundidadCauce) * hondonada >> 16));
                        if (d <= medio)
                        {
                            lamina[k] = (int)sup;
                            continue;
                        }
                        if (cercaDelVado && d <= medio + rampa)
                        {
                            // junto al vado, la orilla baja hasta el agua: por ahí se entra
                            long orilla = Math.Max(H[k], sup + 200);
                            H[k] = (int)Lerp(sup - 100, orilla, Suave((d - medio) * Uno / rampa));
                        }
                        // la bajada al vado, en embudo
                        long fuera = d - medio, ancho = Embudo(fuera), borde = 15_000 + fuera * 3 / 10;
                        if (av <= ancho + borde)
                        {
                            long tope = sup - 100 + Subida(Hipot(fuera, Math.Max(0, av - medioVado))), w = Uno - Suave((av - ancho) * Uno / borde);
                            if (H[k] > tope) H[k] = (int)Lerp(H[k], tope, w);
                            pisado[k] = (int)Math.Max(pisado[k], PisadoDe(av, fuera));
                        }
                    }
                for (int j = 0, k = 0; j < nZ; j++)
                    for (int i = 0; i < nZ; i++, k++)
                    {
                        long av = aZ[k];
                        // el lago o el mar se quedan como son, salvo el cauce del río que entra en ellos, que es hondo
                        if (esAgua[k])
                        {
                            if (dZ[k] <= mZ[k]) zonas[k] = Zona.Honda;
                            continue;
                        }
                        if (dZ[k] <= mZ[k])
                        {
                            bool enVado = av <= AnchoDelPaso(Zc(i), Zc(j), dZ[k], mZ[k]);
                            zonas[k] = enVado ? Zona.Somero : Zona.Honda;
                            esVado[k] = enVado;
                            superficie[k] = (int)lZ[k];
                        }
                        else if (dZ[k] != Infinito && PisadoDe(av, dZ[k] - mZ[k]) > Uno / 2) esBajada[k] = true;
                    }
                // el agua sigue al cauce: en cada vértice de río, `ProfundidadCauce` sobre lo más hondo del lecho a su alrededor
                {
                    int R = (int)Math.Max(1, DivRedondo(RadioLamina, PasoAlturas));
                    var fila = new int[nA * nA]; var hondo = new int[nA * nA];
                    for (int j = 0; j < nA; j++)
                        for (int i = 0; i < nA; i++)
                        {
                            int m = int.MaxValue;
                            for (int d = Math.Max(0, i - R); d <= Math.Min(nA - 1, i + R); d++) m = Math.Min(m, H[j * nA + d]);
                            fila[j * nA + i] = m;
                        }
                    for (int j = 0; j < nA; j++)
                        for (int i = 0; i < nA; i++)
                        {
                            int m = int.MaxValue;
                            for (int d = Math.Max(0, j - R); d <= Math.Min(nA - 1, j + R); d++) m = Math.Min(m, fila[d * nA + i]);
                            hondo[j * nA + i] = m;
                        }
                    for (int k = 0; k < nA * nA; k++) if (!esLagoV[k] && !esVadoV[k] && lamina[k] != CampoGenerado.SinAgua) lamina[k] = (int)(hondo[k] + ProfundidadCauce);
                }
                // una sola lámina de agua, continua: se suaviza sobre los vértices de río, con el lago o el mar como borde fijo; el lecho baja con ella
                var hondura = new int[nA * nA];
                var deRio = new List<int>();
                for (int k = 0; k < nA * nA; k++) if (!esLagoV[k] && lamina[k] != CampoGenerado.SinAgua) { hondura[k] = lamina[k] - H[k]; deRio.Add(k); }
                for (int vuelta = 0; vuelta < VueltasLamina; vuelta++)
                {
                    var antes = (int[])lamina.Clone();
                    foreach (int k in deRio)
                    {
                        int i = k % nA, j = k / nA;
                        long s = antes[k], c = 1;
                        foreach (int v in new[] { i > 0 ? k - 1 : -1, i + 1 < nA ? k + 1 : -1, j > 0 ? k - nA : -1, j + 1 < nA ? k + nA : -1 })
                            if (v >= 0 && antes[v] != CampoGenerado.SinAgua) { s += antes[v]; c++; }
                        lamina[k] = (int)DivRedondo(s, c);
                    }
                }
                foreach (int k in deRio) H[k] = lamina[k] - hondura[k];
            }

            // ── zonas de tierra: playa, pendiente (colina, acantilado) y bosque (§6.6)
            var densidad = new int[nZ * nZ];
            for (int j = 0, k = 0; j < nZ; j++)
                for (int i = 0; i < nZ; i++, k++)
                    densidad[k] = (int)(r.Bosque.Muestra(Zc(i), Zc(j)) + ((Manchas(AX + Zc(i), AY + Zc(j), 90_000, 13) - Uno / 2) * 22_938 >> 16));
            bool EsAguaZona(Zona z) => z == Zona.Somero || z == Zona.Honda;
            void ZonasDeTierra()
            {
                long e = CeldaZonas / 2;
                for (int j = 0, k = 0; j < nZ; j++)
                    for (int i = 0; i < nZ; i++, k++)
                    {
                        if (EsAguaZona(zonas[k])) continue;
                        long x = Zc(i), y = Zc(j);
                        long gx = rejillaH.Muestra(x + e, y) - rejillaH.Muestra(x - e, y), gy = rejillaH.Muestra(x, y + e) - rejillaH.Muestra(x, y - e);
                        Zona z = hayMar && dAgua[k] <= p.AnchoPlaya ? Zona.Arena : Zona.Llano;
                        if (MasEmpinado(gx, gy, 2 * e, tanTransitable)) z = Zona.Acantilado;
                        else if (esBajada[k]) z = Zona.Camino;
                        else if (z == Zona.Llano)
                        {
                            if (densidad[k] >= p.UmbralBosqueCerrado) z = Zona.BosqueCerrado;
                            else if (densidad[k] >= p.UmbralBosqueClaro) z = Zona.BosqueClaro;
                            else if (MasEmpinado(gx, gy, 2 * e, tanColina)) z = Zona.Colina;
                        }
                        zonas[k] = z;
                    }
            }
            ZonasDeTierra();

            // ── spawns (§6.8): a la misma distancia del centro, en lados opuestos del eje; cada placa se comprueba
            int CeldaDe(long x, long y) => (int)(Clamp(DivSuelo(y + L / 2, CeldaZonas), 0, nZ - 1) * nZ + Clamp(DivSuelo(x + L / 2, CeldaZonas), 0, nZ - 1));
            bool Transitable(int k) => zonas[k] != Zona.Honda && zonas[k] != Zona.Acantilado;
            int[] comp = Componentes(Transitable, nZ), compSinVado = Componentes(k => Transitable(k) && !esVado[k], nZ);
            Punto punto = r.PuntoDefensor, pa = r.ProcedenciaAtacante ?? punto, pd = r.ProcedenciaDefensor ?? punto;
            var eje = new Punto(pa.X - pd.X, pa.Y - pd.Y);
            if (Hipot(eje.X, eje.Y) < Coinciden) eje = new Punto(pa.X - punto.X, pa.Y - punto.Y);
            if (Hipot(eje.X, eje.Y) < Coinciden)
            {
                uint a = Azar(DivRedondo(r.Centro.X, 1000), DivRedondo(r.Centro.Y, 1000), 31);
                eje = new Punto(Coseno(a), Seno(a));
                avisos.Add("los dos bandos quietos: el eje sale de la semilla del lugar");
            }
            {
                long le = Hipot(eje.X, eje.Y);
                eje = new Punto(eje.X * Uno30 / le, eje.Y * Uno30 / le);
            }
            int OrillaDe(Punto q) => Math.Abs(q.X) < L / 2 && Math.Abs(q.Y) < L / 2 ? compSinVado[CeldaDe(q.X, q.Y)] : -1;
            int orillaA = OrillaDe(pa), orillaD = OrillaDe(pd);
            if (tipo == TipoDeCampo.Ribera && orillaA >= 0 && orillaD >= 0 && orillaA != orillaD)
                avisos.Add("los bandos llegan por orillas opuestas de un río sin vado: el mundo no da esta batalla (§6.4, §6.5)");
            long aguaAlCentro2 = long.MaxValue;
            for (int j = 0, k = 0; j < nZ; j++) for (int i = 0; i < nZ; i++, k++) if (zonas[k] == Zona.Honda) aguaAlCentro2 = Math.Min(aguaAlCentro2, Zc(i) * Zc(i) + Zc(j) * Zc(j));
            if (tipo == TipoDeCampo.Ribera && aguaAlCentro2 < (p.RadioPlaca + CeldaZonas) * (p.RadioPlaca + CeldaZonas))
                avisos.Add($"el centro está a {DivRedondo(Raiz(aguaAlCentro2), 1000)} m del río: con los spawns a la misma distancia del centro y en lados opuestos, uno cae siempre al otro lado");
            long R2 = p.RadioPlaca * p.RadioPlaca;
            bool FueraDelArea(Punto q) => Math.Abs(q.X) + p.RadioPlaca > J / 2 + CeldaZonas || Math.Abs(q.Y) + p.RadioPlaca > J / 2 + CeldaZonas;
            bool PlacaCumple(Punto q, Func<Zona, bool> mala)
            {
                if (FueraDelArea(q)) return false;
                for (long y = q.Y - p.RadioPlaca; y <= q.Y + p.RadioPlaca; y += CeldaZonas)
                    for (long x = q.X - p.RadioPlaca; x <= q.X + p.RadioPlaca; x += CeldaZonas)
                        if ((x - q.X) * (x - q.X) + (y - q.Y) * (y - q.Y) <= R2 && mala(zonas[CeldaDe(x, y)])) return false;
                return true;
            }
            bool PlacaValida(Punto q) => PlacaCumple(q, z => EsAguaZona(z) || z == Zona.Acantilado);
            bool PlacaSeca(Punto q) => PlacaCumple(q, EsAguaZona);
            var giros = new List<long> { 0 };
            for (long g = PasoGiro; g <= 180; g += PasoGiro) { giros.Add(g); giros.Add(-g); }
            int descartesPlaca = 0, descartesAlcance = 0, descartesVado = 0, descartesOrilla = 0;
            Spawns Hacer(Punto e, Punto A, long g, long d)
            {
                var nn = new Punto(-e.Y, e.X);
                Punto[] Tres(Punto q) => new[] { -1, 0, 1 }.Select(f => new Punto(q.X + (nn.X * f * SeparacionPuntos >> 30), q.Y + (nn.Y * f * SeparacionPuntos >> 30))).ToArray();
                var D = new Punto(-A.X, -A.Y);
                return new Spawns { Atacante = A, Defensor = D, Puntos = Tres(A).Concat(Tres(D)).ToArray(), Eje = e, Giro = g, Distancia = d };
            }
            Spawns BuscarSpawns(bool conOrillas, bool alcanzarse = true)
            {
                for (long d = distanciaAlCentro; d >= DistanciaMinimaSpawn; d -= PasoAcorte)
                    foreach (long g in giros)
                    {
                        Punto e = Girar(eje, g), A = new Punto(e.X * d >> 30, e.Y * d >> 30), D = new Punto(-A.X, -A.Y);
                        if (!PlacaValida(A) || !PlacaValida(D)) { descartesPlaca++; continue; }
                        int ka = CeldaDe(A.X, A.Y), kd = CeldaDe(D.X, D.Y);
                        if (alcanzarse && comp[ka] != comp[kd]) { descartesAlcance++; continue; } // §6.9: cada spawn llega al otro
                        if (conOrillas)
                        {
                            if (tipo == TipoDeCampo.Vado && compSinVado[ka] == compSinVado[kd]) { descartesVado++; continue; } // orillas opuestas
                            if ((orillaA >= 0 && compSinVado[ka] != orillaA) || (orillaD >= 0 && compSinVado[kd] != orillaD)) { descartesOrilla++; continue; }
                        }
                        return Hacer(e, A, g, d);
                    }
                return null;
            }
            // §6.4, en un vado: se gira sobre el centro hasta que cada spawn cae a un lado del río del vado, con la placa en seco
            int LadoDelRio(Punto q)
            {
                long mejor = long.MaxValue;
                int lado = 0;
                for (int i = 0; i + 1 < rioDelVado.Count; i++)
                {
                    Punto a = rioDelVado[i], b = rioDelVado[i + 1];
                    Proyectar(a, b, q.X, q.Y, out long d2);
                    if (d2 < mejor) { mejor = d2; lado = Math.Sign((b.X - a.X) * (q.Y - a.Y) - (b.Y - a.Y) * (q.X - a.X)); }
                }
                return lado;
            }
            Spawns BuscarEnElVado(int ladoAtacante)
            {
                for (long d = distanciaAlCentro; d >= DistanciaMinimaSpawn; d -= PasoAcorte)
                    foreach (long g in giros)
                    {
                        Punto e = Girar(eje, g), A = new Punto(e.X * d >> 30, e.Y * d >> 30), D = new Punto(-A.X, -A.Y);
                        int la = LadoDelRio(A), ld = LadoDelRio(D);
                        if (la == 0 || ld == 0 || la == ld || (ladoAtacante != 0 && la != ladoAtacante) || !PlacaSeca(A) || !PlacaSeca(D)) continue;
                        return Hacer(e, A, g, d);
                    }
                return null;
            }
            Spawns spawns = null;
            if (tipo == TipoDeCampo.Vado && rioDelVado.Count > 1)
            {
                int ladoAtacante = r.ProcedenciaAtacante.HasValue ? LadoDelRio(pa) : 0;
                spawns = BuscarEnElVado(ladoAtacante) ?? (ladoAtacante != 0 ? BuscarEnElVado(0) : null);
                if (spawns != null && ladoAtacante != 0 && LadoDelRio(spawns.Atacante) != ladoAtacante) avisos.Add("el atacante no cae en la orilla de la que viene");
                if (spawns != null && comp[CeldaDe(spawns.Atacante.X, spawns.Atacante.Y)] != comp[CeldaDe(spawns.Defensor.X, spawns.Defensor.Y)])
                    avisos.Add("el relieve tal como sale no deja llegar por tierra de un spawn al otro");
            }
            if (spawns == null) spawns = BuscarSpawns(true);
            if (spawns == null)
            {
                spawns = BuscarSpawns(false);
                if (spawns != null) avisos.Add(tipo == TipoDeCampo.Vado ? "el río no parte el campo en dos orillas: los bandos no quedan en orillas opuestas" : "ningún spawn cae en la orilla de su procedencia");
            }
            if (spawns == null)
            {
                if (!llanoForzado)
                {
                    var llano = Generar(r, p, true);
                    avisos.Add($"el tipo {lectura.Tipo} no dejó sitio a los spawns: cae al llano con el relieve del lugar (§6.9). Pares descartados: {descartesPlaca} por la placa (agua o acantilado), {descartesAlcance} porque no se alcanzan, {descartesVado} por no quedar en orillas opuestas, {descartesOrilla} por la orilla de su bando");
                    llano.Informe.Avisos.InsertRange(0, avisos);
                    return llano;
                }
                // en el llano, con el agua del lugar, los bandos pueden quedar sin llegarse por tierra: se avisa y el campo se queda como es
                spawns = BuscarSpawns(false, false);
                avisos.Add(spawns != null ? "el agua del lugar no deja llegar por tierra de un spawn al otro" : "ni el llano deja sitio a los spawns");
            }

            // ── placas (§6.2, paso 5): el plano que mejor se ajusta al suelo del sitio, fundido con el relieve a lo largo de otro radio
            if (spawns != null)
            {
                foreach (var q in new[] { spawns.Atacante, spawns.Defensor })
                {
                    long R = p.RadioPlaca, borde = R, Rb = R + borde;
                    int i0 = (int)Clamp(DivSuelo(q.X - Rb + L / 2, PasoAlturas), 0, nA - 1), i1 = (int)Clamp(DivSuelo(q.X + Rb + L / 2, PasoAlturas) + 1, 0, nA - 1);
                    int j0 = (int)Clamp(DivSuelo(q.Y - Rb + L / 2, PasoAlturas), 0, nA - 1), j1 = (int)Clamp(DivSuelo(q.Y + Rb + L / 2, PasoAlturas) + 1, 0, nA - 1);
                    // mínimos cuadrados de h = a + b·dx + c·dy en el disco (sumas en long, el 3×3 por Cramer en BigInteger)
                    long n = 0, sx = 0, sy = 0, sxx = 0, syy = 0, sxy = 0, sh = 0, sxh = 0, syh = 0;
                    for (int j = j0; j <= j1; j++)
                        for (int i = i0; i <= i1; i++)
                        {
                            long dx = Vx(i) - q.X, dy = Vx(j) - q.Y;
                            if (dx * dx + dy * dy > R * R) continue;
                            long h = H[j * nA + i];
                            n++; sx += dx; sy += dy; sxx += dx * dx; syy += dy * dy; sxy += dx * dy; sh += h; sxh += dx * h; syh += dy * h;
                        }
                    if (n == 0) continue;
                    BigInteger Det(params long[] m) => (BigInteger)m[0] * ((BigInteger)m[4] * m[8] - (BigInteger)m[5] * m[7]) - (BigInteger)m[1] * ((BigInteger)m[3] * m[8] - (BigInteger)m[5] * m[6]) + (BigInteger)m[2] * ((BigInteger)m[3] * m[7] - (BigInteger)m[4] * m[6]);
                    BigInteger D = Det(n, sx, sy, sx, sxx, sxy, sy, sxy, syy);
                    if (D.IsZero) D = BigInteger.One;
                    BigInteger da = Det(sh, sx, sy, sxh, sxx, sxy, syh, sxy, syy), db = Det(n, sh, sy, sx, sxh, sxy, sy, syh, syy), dc = Det(n, sx, sh, sx, sxx, sxh, sy, sxy, syh);
                    for (int j = j0; j <= j1; j++)
                        for (int i = i0; i <= i1; i++)
                        {
                            long dx = Vx(i) - q.X, dy = Vx(j) - q.Y, d2 = dx * dx + dy * dy;
                            if (d2 > Rb * Rb) continue;
                            long plano = (long)BigInteger.Divide(da + db * dx + dc * dy, D);
                            int k = j * nA + i;
                            H[k] = (int)Lerp(plano, H[k], Suave((Raiz(d2) - R) * Uno / borde));
                        }
                }
                ZonasDeTierra();
            }

            // ── árboles (§6.6): los mismos por m² que en el mapa de mundo, a la misma densidad; fuera de agua, playa, acantilado y placas
            var arboles = new List<long>();
            long sep = p.SeparacionCerrado, sep45 = sep * 4 / 5;
            var placas = spawns != null ? new[] { spawns.Atacante, spawns.Defensor } : new Punto[0];
            bool CercaDeUnaPlaca(long x, long y, long radio) => placas.Any(q => (x - q.X) * (x - q.X) + (y - q.Y) * (y - q.Y) < radio * radio);
            int arbolesJugables = 0;
            for (long y = -L / 2 + sep / 2; y < L / 2; y += sep)
                for (long x = -L / 2 + sep / 2; x < L / 2; x += sep)
                {
                    int k = CeldaDe(x, y);
                    Zona z = zonas[k];
                    long d = densidad[k], gi = DivRedondo(AX + x, sep), gj = DivRedondo(AY + y, sep);
                    bool enTierra = z != Zona.Somero && z != Zona.Honda && z != Zona.Arena && z != Zona.Acantilado;
                    // 0,55 árboles por copa de 6 m de lado, por la densidad y por el hueco
                    long prob = enTierra && d >= BosqueMinimo ? Math.Min(Uno, d * 55 * sep * sep / 3_600_000_000L) : 0;
                    if (Azar(gi, gj, 21) >= (ulong)prob << 16) continue;
                    long ax = x + (((long)Azar(gi, gj, 22) - (1L << 31)) * sep45 >> 32), ay = y + (((long)Azar(gi, gj, 23) - (1L << 31)) * sep45 >> 32);
                    if (CercaDeUnaPlaca(ax, ay, p.RadioPlaca + 6_000)) continue;
                    arboles.Add(ax); arboles.Add(ay);
                    if (Jugable(ax, ay)) arbolesJugables++;
                }
            // ── sotos: donde el bosque del mundo no da árboles, de vez en cuando un grupito de 3 a 10
            int sotos = 0;
            long cs = p.SotosCelda, cs7 = cs * 7 / 10;
            for (long y = -L / 2 + cs / 2; y < L / 2; y += cs)
                for (long x = -L / 2 + cs / 2; x < L / 2; x += cs)
                {
                    long gi = DivRedondo(AX + x, cs), gj = DivRedondo(AY + y, cs);
                    long sx = x + (((long)Azar(gi, gj, 61) - (1L << 31)) * cs7 >> 32), sy = y + (((long)Azar(gi, gj, 62) - (1L << 31)) * cs7 >> 32);
                    int k = CeldaDe(sx, sy);
                    Zona z = zonas[k];
                    if (densidad[k] >= BosqueMinimo || (z != Zona.Llano && z != Zona.Colina)) continue;
                    long fertil = r.Fertilidad.Muestra(sx, sy);
                    if (Azar(gi, gj, 63) >= (ulong)(p.SotosProbabilidad * (Uno / 2 + fertil) >> 16) << 16) continue;
                    if (CercaDeUnaPlaca(sx, sy, p.RadioPlaca + 25_000)) continue;
                    long cuantos = 3 + (Azar(gi, gj, 64) >> 29), radio = 8_000 + ((long)Azar(gi, gj, 65) * 12_000 >> 32);
                    for (long t = 0; t < cuantos; t++)
                    {
                        // más juntos en el centro del soto
                        uint a = Azar(gi * 31 + t, gj, 66);
                        long d = radio * Raiz(Azar(gi, gj * 31 + t, 67)) >> 16;
                        d = d * 9 / 10;
                        long ax = sx + (Coseno(a) * d >> 30), ay = sy + (Seno(a) * d >> 30);
                        Zona zt = zonas[CeldaDe(ax, ay)];
                        if (zt != Zona.Llano && zt != Zona.Colina && zt != Zona.BosqueClaro) continue;
                        arboles.Add(ax); arboles.Add(ay);
                        if (Jugable(ax, ay)) arbolesJugables++;
                    }
                    sotos++;
                }
            if (arbolesJugables > p.MaxArboles) avisos.Add($"{arbolesJugables} árboles en el área jugable: más que max_arboles ({p.MaxArboles})");

            // ── informe
            long acantilado = 0, celdasJugables = 0;
            for (int j = 0, k = 0; j < nZ; j++)
                for (int i = 0; i < nZ; i++, k++)
                {
                    if (!Jugable(Zc(i), Zc(j))) continue;
                    celdasJugables++;
                    if (zonas[k] == Zona.Acantilado) acantilado++;
                }
            if (lectura.Tipo == TipoDeCampo.Vado && !llanoForzado && vados.Count == 0) avisos.Add("el vado de la lectura no cae en el campo");
            var informe = new Informe
            {
                Tipo = tipo, LlanoForzado = llanoForzado, Arboles = arboles.Count / 2, ArbolesJugables = arbolesJugables, Sotos = sotos,
                Acantilado = acantilado * Uno / Math.Max(1, celdasJugables), Desnivel = Rango(H), Avisos = avisos,
            };
            return new CampoGenerado
            {
                Lado = L, Jugable = J, Lugar = r, Lectura = lectura,
                NAlturas = nA, PasoAlturas = PasoAlturas, Alturas = H,
                NZonas = nZ, CeldaZonas = CeldaZonas, Zonas = zonas, Superficie = superficie,
                AlturaBase = media, Bosque = densidad, Lamina = lamina, Pisado = pisado,
                Arboles = arboles.ToArray(), Vados = vados, AnchoVado = 2 * medioVado, Spawns = spawns, Informe = informe,
            };
        }

        static long[] Llenos(int n, long v)
        {
            var a = new long[n];
            for (int k = 0; k < n; k++) a[k] = v;
            return a;
        }
    }
}

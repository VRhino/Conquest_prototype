// Los ríos y sus vados (del artefacto) y la niebla de guerra (de la sesión), en el mapa de mundo.
using System;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;
using UnityEngine;
using UnityEngine.Rendering;

namespace ConquestTactics.Mundo.Mapa
{
    /// <summary>Los ríos del artefacto, sobre la base de BronzeAgeClient (`src/mundo3d/paisaje.ts`, `lineas.ts`, `agua.ts`) con más detalle:
    /// el trazado suavizado (Chaikin, dos pasadas), alargado hasta el agua si acaba a menos de 8 u y entrando 0,5 u en la lámina; el ancho
    /// (3 u los navegables, 1,5 u los demás) varía con ruido y crece río abajo, y se abre en abanico en la desembocadura; la cinta de agua va
    /// plana de orilla a orilla en lo más bajo del cauce, con UV0 (x de orilla a orilla, y los u andados, z lo rápido que baja, w lo cerca de la
    /// desembocadura); una orilla mojada a cada lado; y los vados, un tramo ancho y somero de guijarros claros. Se hacen una vez, todos.</summary>
    public static class RiosDelMapa
    {
        /// <summary>Lo que va el agua sobre lo más bajo del cauce (0,04 u, el `asiento: 'bajo'` de BronzeAgeClient), el paso de muestreo (u), lo
        /// que entra el río en la lámina, hasta dónde se alarga para llegar a ella (u); el largo del abanico de la desembocadura (u) y cuánto se abre.
        /// (El agua del río va en la lámina de la tesela desde el 26-11: `CampoDeRios`; aquí quedan el trazado y su ancho.)</summary>
        const float Sobre = 0.04f, PasoMuestra = 0.5f, Entra = 0.5f, Alargar = 8, Abanico = 3, Abre = 1.5f;

        public enum ModoAgua { Lamina = 0, Rio = 1, Orilla = 2, Vado = 3 }

        /// <summary>Un material de `Conquest/MapaAgua` para lo que va: la lámina (cola 3000), la orilla mojada, el río y el vado encima, en ese
        /// orden y con un desfase de profundidad creciente, para que no parpadeen sobre el mar.</summary>
        public static Material MaterialDeAgua(ModoAgua modo)
        {
            var m = new Material(Resources.Load<Shader>("MapaMundo/MapaAgua")) { name = "agua " + modo };
            m.SetFloat("_Modo", (int)modo);
            m.SetFloat("_Desfase", modo == ModoAgua.Lamina ? 0 : modo == ModoAgua.Orilla ? -1 : modo == ModoAgua.Rio ? -2 : -4);
            m.renderQueue = 3000 + (modo == ModoAgua.Lamina ? 0 : modo == ModoAgua.Orilla ? 1 : modo == ModoAgua.Rio ? 2 : 3);
            m.SetTexture("_Olas", Resources.Load<Texture2D>("MapaMundo/Agua/agua_olas"));
            m.SetColor("_Somero", PinturaDelMapa.AguaSomera);
            m.SetColor("_Turquesa", PinturaDelMapa.AguaTurquesa);
            m.SetColor("_Hondo", PinturaDelMapa.AguaHonda);
            m.SetColor("_Espuma", PinturaDelMapa.Espuma);
            m.SetColor("_OrillaMojada", PinturaDelMapa.OrillaMojada);
            m.SetColor("_Vado", PinturaDelMapa.Vado);
            m.SetFloat("_Detalle", PinturaDelMapa.AguaConDetalle ? 1 : 0);
            // la barra de guijarros de orilla a orilla en el núcleo del vado, solo con el vado corto (la B; la C, el trecho largo, sin ella)
            m.SetFloat("_Barra", PinturaDelMapa.VadoComoTrecho && PinturaDelMapa.VadoMedioLargo <= 1.5f ? 1 : 0);
            return m;
        }

        /// <summary>Un tramo de río en tierra: los puntos (u), el ancho en cada uno (u) y lo cerca que está de la desembocadura (0..1).</summary>
        public sealed class Tramo
        {
            public readonly List<Vector2> Puntos = new List<Vector2>();
            public readonly List<float> Anchos = new List<float>(), Boca = new List<float>();
        }

        /// <summary>Los tramos en tierra de un río, con su ancho: el trazado suavizado, alargado hasta el agua si acaba cerca, y cortado donde ya
        /// hay agua (cada tramo entra `Entra` u en ella). El ancho varía con ruido (±25 %), crece río abajo (hasta +60 % al final) y se abre en
        /// abanico los últimos `Abanico` u antes del agua. Lo comparten la cinta y el cauce, para que el agua caiga justo en lo excavado.</summary>
        /// Con los anchos del motor (contrato 0.32, `Rio.anchos`, milésimas de u por punto; 26-11), el ancho es el suyo en cada punto, sin ruido ni
        /// crecida ni abanico (el estuario ya viene del motor), y el trazado pasa por sus puntos con Catmull-Rom centrípeta cada 0,5 u, con el
        /// ancho interpolado (`CurvaConAncho`, la `curvaConAncho` del prototipo): Chaikin recortaba los meandros. Sin ellos (artefactos viejos),
        /// como antes.
        public static List<Tramo> Tramos(Rio rio, Relieve relieve)
        {
            var pts = new List<Vector2>();
            foreach (var p in rio.Puntos) pts.Add(Punto(p));
            bool delMotor = rio.Anchos.Count == rio.Puntos.Count && rio.Anchos.Count > 1;
            var anchosMotor = new List<float>();
            if (delMotor) (pts, anchosMotor) = CurvaConAncho(pts, rio.Anchos, PasoMuestra);
            else for (int pasada = 0; pasada < 2; pasada++) pts = Chaikin(pts);
            int puntosDelRio = pts.Count;
            if (pts.Count >= 2)
            {
                var fin = pts[pts.Count - 1];
                var dir = (fin - pts[pts.Count - 2]).normalized;
                bool llega = false;
                for (float d = 0.25f; d <= Alargar; d += 0.25f)
                    if (relieve.AguaEn(fin.x + dir.x * d, fin.y + dir.y * d))
                    {
                        for (float s = 0.25f; s <= d + Entra; s += 0.25f) pts.Add(fin + dir * s);
                        llega = true;
                        break;
                    }
                // si el agua no queda en línea recta, al agua más cercana por delante (a menos de `Alargar` u): si no, el río acababa en un
                // tapón de tierra antes de la costa
                if (!llega)
                {
                    Vector2? mejor = null;
                    for (float dy = -Alargar; dy <= Alargar; dy += 0.5f)
                        for (float dx = -Alargar; dx <= Alargar; dx += 0.5f)
                        {
                            var v = new Vector2(dx, dy);
                            if (v.sqrMagnitude > Alargar * Alargar || Vector2.Dot(v, dir) <= 0.3f * v.magnitude || !relieve.AguaEn(fin.x + dx, fin.y + dy)) continue;
                            if (mejor == null || v.sqrMagnitude < mejor.Value.sqrMagnitude) mejor = v;
                        }
                    if (mejor is Vector2 m)
                    {
                        var hacia = m.normalized;
                        for (float s = 0.25f; s <= m.magnitude + Entra; s += 0.25f) pts.Add(fin + hacia * s);
                    }
                }
            }
            float total = 0;
            for (int i = 0; i + 1 < pts.Count; i++) total += Vector2.Distance(pts[i], pts[i + 1]);
            float baseAncho = rio.Navegable ? PinturaDelMapa.RioNavegable : PinturaDelMapa.Rio, andado = 0, anchoAhora = 0;
            var tramos = new List<Tramo>();
            var tramo = new Tramo();
            var andados = new List<float>();
            float enAgua = 0;
            void Cerrar()
            {
                if (tramo.Puntos.Count > 1)
                {
                    // El abanico: los últimos `Abanico` u del tramo, si acaba en el agua.
                    var ultimo = tramo.Puntos[tramo.Puntos.Count - 1];
                    bool desemboca = relieve.AguaEn(ultimo.x, ultimo.y);
                    float finTramo = andados[andados.Count - 1];
                    for (int i = 0; i < tramo.Puntos.Count; i++)
                    {
                        float boca = desemboca && !delMotor ? Mathf.SmoothStep(0, 1, 1 - Mathf.Clamp01((finTramo - andados[i]) / Abanico)) : 0;
                        tramo.Boca.Add(boca);
                        tramo.Anchos[i] *= 1 + Abre * boca;
                    }
                    tramos.Add(tramo);
                }
                tramo = new Tramo();
                andados.Clear();
            }
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                Vector2 p0 = pts[i], p1 = pts[i + 1];
                float largo = Vector2.Distance(p0, p1);
                int pasos = Mathf.Max(1, Mathf.CeilToInt(largo / PasoMuestra));
                for (int k = 0; k < pasos; k++, andado += largo / pasos)
                {
                    var p = Vector2.Lerp(p0, p1, k / (float)pasos);
                    // (el ancho del motor en ese punto: entre los de sus dos puntos; lo alargado hasta el agua, el del último)
                    anchoAhora = delMotor ? (i + 1 < puntosDelRio ? Mathf.Lerp(anchosMotor[i], anchosMotor[i + 1], k / (float)pasos) : anchosMotor[puntosDelRio - 1]) : 0;
                    if (relieve.AguaEn(p.x, p.y))
                    {
                        enAgua += largo / pasos;
                        if (enAgua <= Entra && tramo.Puntos.Count > 0) { Agregar(p); continue; }
                        Cerrar();
                        continue;
                    }
                    enAgua = 0;
                    Agregar(p);
                }
            }
            if (delMotor) anchoAhora = anchosMotor[puntosDelRio - 1];
            if (pts.Count > 0 && tramo.Puntos.Count > 0) Agregar(pts[pts.Count - 1]);
            Cerrar();
            return tramos;

            void Agregar(Vector2 p)
            {
                tramo.Puntos.Add(p);
                andados.Add(andado);
                if (delMotor) { tramo.Anchos.Add(anchoAhora); return; }
                float ruido = Mathf.PerlinNoise(p.x * 0.12f + 31.7f, p.y * 0.12f + 7.3f);
                tramo.Anchos.Add(baseAncho * (0.75f + 0.5f * ruido) * (1 + 0.6f * (total > 0 ? andado / total : 0)));
            }
        }

        /// <summary>La curva de un río del motor por sus puntos: Catmull-Rom centrípeta muestreada cada `paso` u (como mucho), con el ancho (u) de
        /// los puntos (`anchos`, milésimas de u) interpolado a lo largo de cada tramo (BronzeAgeClient: `curvaConAncho` de `src/mundo3d/rios.ts`).</summary>
        public static (List<Vector2> puntos, List<float> anchos) CurvaConAncho(List<Vector2> ps, IList<uint> anchos, float paso)
        {
            // (en doble y respecto a p1: en float y a 1500 u del origen, los pesos de los tramos casi nulos se desbordaban y salían rayas rectas)
            var o = new List<Vector2> { ps[0] };
            var w = new List<float> { anchos[0] / 1000f };
            static double T(double ax, double ay, double bx, double by) { double t = Math.Sqrt(Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay))); return t > 1e-6 ? t : 1e-6; }
            for (int i = 0; i + 1 < ps.Count; i++)
            {
                Vector2 q0 = ps[Math.Max(0, i - 1)], q1 = ps[i], q2 = ps[i + 1], q3 = ps[Math.Min(ps.Count - 1, i + 2)];
                double ox = q1.x, oy = q1.y;
                double x0 = q0.x - ox, y0 = q0.y - oy, x1 = 0, y1 = 0, x2 = q2.x - ox, y2 = q2.y - oy, x3 = q3.x - ox, y3 = q3.y - oy;
                double t1 = T(x0, y0, x1, y1), t2 = t1 + T(x1, y1, x2, y2), t3 = t2 + T(x2, y2, x3, y3);
                int n = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(x2 * x2 + y2 * y2) / paso));
                float wa = anchos[i] / 1000f, wb = anchos[i + 1] / 1000f;
                for (int k = 1; k <= n; k++)
                {
                    if (k == n) { o.Add(q2); w.Add(wb); break; }
                    double u = t1 + (t2 - t1) * k / n;
                    (double, double) M((double x, double y) a, (double x, double y) b, double ta, double tb) =>
                        (((tb - u) * a.x + (u - ta) * b.x) / (tb - ta), ((tb - u) * a.y + (u - ta) * b.y) / (tb - ta));
                    var a1 = M((x0, y0), (x1, y1), 0, t1);
                    var a2 = M((x1, y1), (x2, y2), t1, t2);
                    var a3 = M((x2, y2), (x3, y3), t2, t3);
                    var (qx, qy) = M(M(a1, a2, 0, t2), M(a2, a3, t1, t3), t1, t2);
                    o.Add(new Vector2((float)(ox + qx), (float)(oy + qy)));
                    w.Add(wa + (wb - wa) * k / n);
                }
            }
            return (o, w);
        }

        static List<Vector2> Chaikin(List<Vector2> p)
        {
            if (p.Count < 3) return p;
            var q = new List<Vector2> { p[0] };
            for (int i = 0; i + 1 < p.Count; i++)
            {
                q.Add(Vector2.Lerp(p[i], p[i + 1], 0.25f));
                q.Add(Vector2.Lerp(p[i], p[i + 1], 0.75f));
            }
            q.Add(p[p.Count - 1]);
            return q;
        }

        /// <summary>La altura (metros) de la lámina del río en (x, y) con medio ancho `medio`: lo más bajo del corte (centro y bordes) más `Sobre`.</summary>
        public static float NivelDelRio(Relieve relieve, Vector2 p, Vector2 normal, float medio) =>
            (float)Math.Min(relieve.AlturaEn(p.x, p.y), Math.Min(relieve.AlturaEn(p.x + normal.x * medio, p.y + normal.y * medio),
                relieve.AlturaEn(p.x - normal.x * medio, p.y - normal.y * medio))) + Sobre * EscalaDelMapa.MetrosPorUnidad;

        static Vector2 Punto(Punto p) => new Vector2(p.X / 1000f, p.Y / 1000f);
    }

    /// <summary>Los cauces de los ríos, para bajar el suelo hasta ellos con un perfil de verdad: el relieve excava el cauce, pero la malla del suelo
    /// (paso de 0,5 a 2 u) es más gruesa que él y, al interpolar, lo rellenaría y enterraría el agua. Cada vértice cerca del eje (el mismo trazado
    /// y el mismo ancho que la cinta) baja a una parábola: dentro del medio ancho efectivo (el del río más medio paso de la tesela, para que en las
    /// teselas gruesas la cinta no quede enterrada entre vértices), por debajo de la lámina y más hondo en el centro; fuera, una ribera que sube
    /// suave hasta el suelo. Y dice cuánto es ribera (para que `MapaSuelo` pinte arena, guijarros y tierra mojada junto al agua).</summary>
    public sealed class CaucesDelMapa
    {
        /// <summary>Lado de la celda de búsqueda (u), cuánto baja el centro bajo la lámina, el ancho de la ribera y cuánto sube en ella (u), y el
        /// ancho de la ribera pintada (u).</summary>
        const double Celda = 2;
        const float Centro = 0.12f, Ribera = 1, SubeRibera = 0.8f, RiberaPintada = 1.6f;
        readonly Dictionary<(int, int), List<Muestra>> celdas = new Dictionary<(int, int), List<Muestra>>();

        /// <summary>Una muestra del eje: dónde, el nivel de la lámina (m), el medio ancho (u), hacia dónde va y cuánto gira (rad/u, + a la izquierda);
        /// de qué río es (su índice en el artefacto) y lo andado desde su nacimiento (u, como lo cuenta la cinta del río).</summary>
        struct Muestra { public double X, Y; public float Nivel, Medio, DirX, DirY, Curva, S; public int Rio; }
        float medioMaximo;

        /// <summary>Un vado: su punto de `Rio.vados`, su río (índice en el artefacto), dónde cae a lo largo de él (u andados por el cauce), a la
        /// medida de qué río (`Escala`: 1 desde `AnchoDeVadoEntero` u de ancho; menos, en proporción, como poco `EscalaMinima`) y cuánto se
        /// aparta el punto del motor de la curva del río (u).</summary>
        public struct Trecho { public int Rio; public float Centro, Escala, Aparte; public double X, Y; }
        /// <summary>El ancho de río (u) desde el que el vado es entero (núcleo de 2 × `VadoMedioLargo` y transición de `VadoTransicion`), y lo
        /// menos que se encoge en uno estrecho (para que se vea).</summary>
        public const float AnchoDeVadoEntero = 1.5f, EscalaMinima = 0.4f;
        /// <summary>Los vados de `Rio.vados` proyectados sobre su río (los que caen en un cauce de tierra; si no, no cuentan para lo somero).</summary>
        public readonly List<Trecho> Trechos = new List<Trecho>();
        readonly Dictionary<int, List<Trecho>> trechosPorRio = new Dictionary<int, List<Trecho>>();
        /// <summary>Todos los puntos de `Rio.vados` (u), para la franja de antes.</summary>
        public readonly List<(double x, double y)> Vados = new List<(double x, double y)>();
        /// <summary>Lo somero: el centro del cauce baja una fracción de lo de siempre, la ribera es más ancha y sube menos.</summary>
        const float CentroSomero = 0.3f, RiberaSomera = 2.6f, SubeSomera = 0.35f;

        public CaucesDelMapa(ArtefactoDeMundo a, Relieve relieve)
        {
            for (int ir = 0; ir < a.Rios.Count; ir++)
            {
                float andado = 0;
                foreach (var tramo in RiosDelMapa.Tramos(a.Rios[ir], relieve))
                {
                    var pts = tramo.Puntos;
                    for (int i = 0; i < pts.Count; i++)
                    {
                        var p = pts[i];
                        if (i > 0) andado += Vector2.Distance(pts[i - 1], p); // como en `RiosDelMapa.Construir`
                        if (relieve.AguaEn(p.x, p.y)) continue;
                        float medio = tramo.Anchos[i] / 2;
                        medioMaximo = Mathf.Max(medioMaximo, medio);
                        var dir = (pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
                        // La curvatura: cuánto gira el rumbo entre dos muestras atrás y dos adelante, por u.
                        Vector2 a0 = pts[Mathf.Max(i - 2, 0)], a1 = pts[i], a2 = pts[Mathf.Min(i + 2, pts.Count - 1)];
                        Vector2 d0 = (a1 - a0).normalized, d1 = (a2 - a1).normalized;
                        float giro = Mathf.Atan2(d0.x * d1.y - d0.y * d1.x, Vector2.Dot(d0, d1)), largo = Mathf.Max(0.1f, Vector2.Distance(a0, a2));
                        var key = ((int)Math.Floor(p.x / Celda), (int)Math.Floor(p.y / Celda));
                        if (!celdas.TryGetValue(key, out var l)) celdas[key] = l = new List<Muestra>();
                        l.Add(new Muestra { X = p.x, Y = p.y, Nivel = RiosDelMapa.NivelDelRio(relieve, p, new Vector2(-dir.y, dir.x), medio), Medio = medio,
                            DirX = dir.x, DirY = dir.y, Curva = giro / largo, S = andado, Rio = ir });
                    }
                }
            }
            // Los vados: cada punto de `Rio.vados`, sobre la muestra más cercana de su propio río (a menos de 6 u).
            for (int ir = 0; ir < a.Rios.Count; ir++)
                foreach (var v in a.Rios[ir].Vados)
                {
                    double vx = v.X / 1000.0, vy = v.Y / 1000.0;
                    Vados.Add((vx, vy));
                    if (!(Cercana(vx, vy, 6, ir) is Muestra m)) continue;
                    // (el vado, sobre la curva del río: en la muestra más cercana, a 0,5 u como mucho una de otra)
                    var t = new Trecho { Rio = ir, Centro = m.S, X = vx, Y = vy, Escala = Mathf.Clamp(2 * m.Medio / AnchoDeVadoEntero, EscalaMinima, 1),
                        Aparte = (float)Math.Sqrt((m.X - vx) * (m.X - vx) + (m.Y - vy) * (m.Y - vy)) };
                    Trechos.Add(t);
                    if (!trechosPorRio.TryGetValue(ir, out var lt)) trechosPorRio[ir] = lt = new List<Trecho>();
                    lt.Add(t);
                }
        }

        /// <summary>Lo que queda (u) de (x, y) al punto de vado más cercano (infinito si no hay ninguno a menos de 10 u).</summary>
        public double AUnVado(double x, double y)
        {
            double d = double.PositiveInfinity;
            foreach (var v in Vados)
                if (Math.Abs(v.x - x) < 10 && Math.Abs(v.y - y) < 10) d = Math.Min(d, Math.Sqrt((v.x - x) * (v.x - x) + (v.y - y) * (v.y - y)));
            return d;
        }

        Muestra? Cercana(double x, double y, double alcance, int rio = -1)
        {
            int ci = (int)Math.Floor(x / Celda), cj = (int)Math.Floor(y / Celda), r = (int)Math.Ceiling(alcance / Celda);
            double mejor = alcance * alcance;
            Muestra? cerca = null;
            for (int dj = -r; dj <= r; dj++)
                for (int di = -r; di <= r; di++)
                    if (celdas.TryGetValue((ci + di, cj + dj), out var l))
                        foreach (var p in l)
                        {
                            if (rio >= 0 && p.Rio != rio) continue;
                            var d = (p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y);
                            if (d < mejor) { mejor = d; cerca = p; }
                        }
            return cerca;
        }

        /// <summary>Hacia dónde va el río más cercano a (x, y) (a menos de 6 u); si no hay, (1, 0).</summary>
        public (double x, double y) DireccionEn(double x, double y) => Cercana(x, y, 6) is Muestra m ? (m.DirX, m.DirY) : (1, 0);

        /// <summary>El punto del eje del río `rio` a `s` u andados de su nacimiento (la muestra más cercana), o null si no la hay.</summary>
        public (double x, double y)? PuntoDe(int rio, float s)
        {
            (double x, double y)? r = null;
            float mejor = float.MaxValue;
            foreach (var l in celdas.Values)
                foreach (var m in l)
                    if (m.Rio == rio && Math.Abs(m.S - s) < mejor) { mejor = Math.Abs(m.S - s); r = (m.X, m.Y); }
            return r;
        }

        /// <summary>Lo somero del río `rio` a `s` u andados de su nacimiento: 1 a menos de `PinturaDelMapa.VadoMedioLargo` u de un vado, bajando a 0
        /// en `PinturaDelMapa.VadoTransicion` u más.</summary>
        public float SomeroDe(int rio, float s)
        {
            if (!PinturaDelMapa.VadoComoTrecho || !trechosPorRio.TryGetValue(rio, out var l)) return 0;
            float r = 0;
            foreach (var t in l)
            {
                // a la medida del río: el núcleo y la transición, en proporción a su ancho
                float medio = PinturaDelMapa.VadoMedioLargo * t.Escala, transicion = Mathf.Max(0.01f, PinturaDelMapa.VadoTransicion * t.Escala);
                r = Mathf.Max(r, 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(medio, medio + transicion, Mathf.Abs(s - t.Centro))));
            }
            return r;
        }

        /// <summary>Lo somero en (x, y): el de la muestra de cauce más cercana, si (x, y) cae en su cauce o a menos de `alcance` u de él; si no, 0.</summary>
        public float SomeroEn(double x, double y, double alcance = 1)
        {
            if (!(Cercana(x, y, medioMaximo + alcance) is Muestra m)) return 0;
            double d = Math.Sqrt((m.X - x) * (m.X - x) + (m.Y - y) * (m.Y - y));
            return d <= m.Medio + alcance ? SomeroDe(m.Rio, m.S) : 0;
        }

        /// <summary>¿Hay algún tramo de río a menos de lo que alcanza el cauce (con su ribera) del rectángulo (u)? Para no consultar muestra a
        /// muestra en las teselas sin río (casi todas).</summary>
        public bool HayRioCerca(double x0, double y0, double x1, double y1)
        {
            double m = medioMaximo + 1 + Math.Max(Ribera, RiberaPintada * 3.5);
            int i0 = (int)Math.Floor((x0 - m) / Celda), i1 = (int)Math.Floor((x1 + m) / Celda);
            int j0 = (int)Math.Floor((y0 - m) / Celda), j1 = (int)Math.Floor((y1 + m) / Celda);
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                    if (celdas.ContainsKey((i, j))) return true;
            return false;
        }

        /// <summary>A qué altura (metros) tiene que bajar el suelo en (x, y) en una tesela de paso `paso` (u), o null si no hay río cerca.</summary>
        public float? Fondo(double x, double y, float paso) => Consultar(x, y, paso, out _);

        /// <summary>Lo que queda (u) de (x, y) al borde del cauce más cercano, negativo dentro (`aCauce` de BronzeAgeClient, con el ancho de cada
        /// tramo); infinito si no hay río a menos de `alcance` u.</summary>
        /// `medioMinimo` (u): el medio ancho con que cuenta un río más estrecho (el pintado: `CampoDeRios.MedioMinimo` × el paso de la malla).
        public double ACauce(double x, double y, double alcance = 4, double medioMinimo = 0)
        {
            int ci = (int)Math.Floor(x / Celda), cj = (int)Math.Floor(y / Celda), r = (int)Math.Ceiling((alcance + medioMaximo) / Celda);
            double mejor = double.PositiveInfinity;
            for (int dj = -r; dj <= r; dj++)
                for (int di = -r; di <= r; di++)
                    if (celdas.TryGetValue((ci + di, cj + dj), out var l))
                        foreach (var p in l)
                            mejor = Math.Min(mejor, Math.Sqrt((p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y)) - Math.Max(p.Medio, medioMinimo));
            return mejor;
        }

        /// <summary>Cuánto se ensancha la ribera en (x, y) (para `CampoDeRios`): en el lado de dentro de las curvas (la barra de arena y guijarros)
        /// y en el trecho de vado (la playa de grava); y lo somero ahí. 1 y 0 lejos de todo río.</summary>
        public float Ensanche(double x, double y, out float somero)
        {
            somero = 0;
            if (!(Cercana(x, y, medioMaximo + RiberaPintada * 3.5) is Muestra m)) return 1;
            somero = SomeroDe(m.Rio, m.S);
            float lado = (float)(m.DirX * (y - m.Y) - m.DirY * (x - m.X));
            float barra = Math.Sign(lado) == Math.Sign(m.Curva) ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.03f, 0.25f, Mathf.Abs(m.Curva))) : 0;
            return (1 + 2.5f * barra) * (1 + 1.2f * somero);
        }

        /// <summary>Como `Fondo`, y además cuánto es ribera en (x, y): 1 junto al agua, 0 a `RiberaPintada` u de la orilla.</summary>
        public float? Consultar(double x, double y, float paso, out float ribera) => Consultar(x, y, paso, out ribera, out _);

        /// <summary>Como el otro, y lo somero (el trecho de vado) de la muestra más cercana: ahí el cauce es menos hondo y la ribera, más ancha y
        /// tendida (la playa de grava).</summary>
        public float? Consultar(double x, double y, float paso, out float ribera, out float somero)
        {
            ribera = 0;
            somero = 0;
            var e = EscalaDelMapa.MetrosPorUnidad;
            double alcance = medioMaximo + paso / 2 + Math.Max(Ribera * RiberaSomera, RiberaPintada * 3.5);
            int ci = (int)Math.Floor(x / Celda), cj = (int)Math.Floor(y / Celda), r = (int)Math.Ceiling(alcance / Celda);
            double mejor = double.MaxValue;
            Muestra cerca = default;
            for (int dj = -r; dj <= r; dj++)
                for (int di = -r; di <= r; di++)
                    if (celdas.TryGetValue((ci + di, cj + dj), out var l))
                        foreach (var p in l)
                        {
                            var d = (p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y);
                            if (d < mejor) { mejor = d; cerca = p; }
                        }
            if (mejor == double.MaxValue) return null;
            float dist = (float)Math.Sqrt(mejor), efectivo = cerca.Medio + paso / 2;
            somero = SomeroDe(cerca.Rio, cerca.S);
            // La ribera es más ancha en el lado de dentro de las curvas (las barras de arena y guijarros): el lado hacia el que gira el río.
            float lado = (float)(cerca.DirX * (y - cerca.Y) - cerca.DirY * (x - cerca.X));
            float barra = Math.Sign(lado) == Math.Sign(cerca.Curva) ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.03f, 0.25f, Mathf.Abs(cerca.Curva))) : 0;
            // en el vado, la playa de grava a los dos lados: ribera pintada más ancha
            ribera = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((dist - cerca.Medio) / (RiberaPintada * (1 + 2.5f * barra) * (1 + 1.2f * somero))));
            float centro = Centro * Mathf.Lerp(1, CentroSomero, somero), riberaU = Ribera * Mathf.Lerp(1, RiberaSomera, somero), sube = SubeRibera * Mathf.Lerp(1, SubeSomera, somero);
            if (dist < efectivo) return cerca.Nivel - centro * e * (1 - (dist / efectivo) * (dist / efectivo));
            if (dist < efectivo + riberaU) return cerca.Nivel + 0.02f * e + sube * e * ((dist - efectivo) / riberaU) * ((dist - efectivo) / riberaU);
            return null;
        }
    }

    /// <summary>La niebla de guerra (la `NieblaProyectada` de la VISTA, celdas de 5 u): una textura de la rejilla (R = explorado, G = a la vista)
    /// que el sombreado (MapaComun.hlsl) lee con filtro bilineal y mezcla con su color en tres estados. Se rehace solo cuando cambia la VISTA.</summary>
    public sealed class NieblaDelMapa
    {
        static readonly int Tex = Shader.PropertyToID("_NieblaTex"), Rejilla = Shader.PropertyToID("_NieblaRejilla"),
            Tamano = Shader.PropertyToID("_NieblaTamano"), ColorNiebla = Shader.PropertyToID("_NieblaColor"), Alfas = Shader.PropertyToID("_NieblaAlfas"),
            Modo = Shader.PropertyToID("_NieblaModo"), Papiro = Shader.PropertyToID("_DibujoPapiro"), Trazos = Shader.PropertyToID("_DibujoTrazos"),
            Agua = Shader.PropertyToID("_DibujoAgua"), Apagado = Shader.PropertyToID("_NieblaApagado");
        /// <summary>Lo que tarda lo recién explorado en pasar del dibujo al suelo (s): la frontera avanza y lo de debajo crece.</summary>
        public const float SegundosDeCambio = 1.5f;

        Texture2D textura;
        byte[] pixeles;
        bool primera;

        public NieblaDelMapa()
        {
            // En sRGB: como en BronzeAgeClient, la niebla se mezcla después de pasar el color a sRGB (MapaComun.hlsl).
            Shader.SetGlobalVector(ColorNiebla, PinturaDelMapa.Niebla);
            Shader.SetGlobalVector(Alfas, new Vector4(PinturaDelMapa.AlfaSinVer, PinturaDelMapa.AlfaRecordado, PinturaDelMapa.AlfaVisible, 0));
            Shader.SetGlobalVector(Rejilla, Vector4.zero); // sin niebla hasta que llegue la VISTA
            Shader.SetGlobalTexture(Papiro, Resources.Load<Texture2D>("MapaMundo/Niebla/papiro"));
            Shader.SetGlobalTexture(Trazos, Resources.Load<Texture2D>("MapaMundo/Niebla/trazos"));
            Shader.SetGlobalTexture(Agua, Resources.Load<Texture2D>("MapaMundo/Niebla/agua"));
            Shader.SetGlobalVector(Apagado, PinturaDelMapa.Apagado);
            Shader.SetGlobalVector(Modo, new Vector4(PinturaDelMapa.NieblaDibujada ? 1 : 0, 0, 1e-3f, 0));
        }

        public void Poner(NieblaProyectada n)
        {
            if (n == null || n.Columnas == 0) { Shader.SetGlobalVector(Rejilla, Vector4.zero); return; }
            int columnas = (int)n.Columnas, filas = (int)n.Filas;
            if (textura == null || textura.width != columnas || textura.height != filas)
            {
                if (textura != null) UnityEngine.Object.Destroy(textura);
                // Bilineal, como en BronzeAgeClient: el borde de la niebla se funde en una celda.
                // R, G: lo explorado y lo visto de ahora; B, A: los de antes (la primera vez, los mismos: sin paso).
                textura = new Texture2D(columnas, filas, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                pixeles = new byte[columnas * filas * 4];
                primera = true;
            }
            var celdas = n.Celdas.Span;
            var visibles = n.Visibles.Span;
            (exploradas, columnasE, filasE, celdaE) = (celdas.ToArray(), columnas, filas, n.TamanoCelda / 1000.0);
            for (int i = 0; i < columnas * filas; i++)
            {
                byte e = Bit(celdas, i) ? (byte)255 : (byte)0, v = Bit(visibles, i) ? (byte)255 : (byte)0;
                pixeles[4 * i + 2] = primera ? e : pixeles[4 * i];
                pixeles[4 * i + 3] = primera ? v : pixeles[4 * i + 1];
                pixeles[4 * i] = e;
                pixeles[4 * i + 1] = v;
            }
            primera = false;
            Shader.SetGlobalVector(Modo, new Vector4(PinturaDelMapa.NieblaDibujada ? 1 : 0, Time.timeSinceLevelLoad, SegundosDeCambio, 0));
            Shader.SetGlobalVector(Apagado, PinturaDelMapa.Apagado);
            textura.SetPixelData(pixeles, 0);
            textura.Apply(false);
            Shader.SetGlobalTexture(Tex, textura);
            Shader.SetGlobalVector(Tamano, new Vector4(columnas, filas, 0, 0));
            Shader.SetGlobalVector(Rejilla, new Vector4(0, 0, n.TamanoCelda / 1000f * EscalaDelMapa.MetrosPorUnidad, 1));
        }

        static bool Bit(System.ReadOnlySpan<byte> m, int i) => (i >> 3) < m.Length && ((m[i >> 3] >> (i & 7)) & 1) == 1;

        byte[] exploradas;
        int columnasE, filasE;
        double celdaE;

        /// <summary>Si (x, y) (u) ya se exploró; sin niebla (la previa, la mesa), todo.</summary>
        public bool Explorado(double x, double y)
        {
            if (exploradas == null || celdaE <= 0) return true;
            int c = (int)Math.Floor(x / celdaE), f = (int)Math.Floor(y / celdaE);
            return c >= 0 && f >= 0 && c < columnasE && f < filasE && Bit(exploradas, f * columnasE + c);
        }

        public void Destruir()
        {
            if (textura != null) UnityEngine.Object.Destroy(textura);
            Shader.SetGlobalVector(Rejilla, Vector4.zero);
        }
    }
}

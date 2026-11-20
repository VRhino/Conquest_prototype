// Los ríos y sus vados (del artefacto) y la niebla de guerra (de la sesión), en el mapa de mundo.
using System;
using System.Collections.Generic;
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
        /// <summary>Lo que va el agua sobre lo más bajo del cauce (0,04 u, el `asiento: 'bajo'` de BronzeAgeClient), el paso de muestreo (u), el
        /// ancho de los vados a lo largo del río, lo que entra el río en la lámina, hasta dónde se alarga para llegar a ella y el ancho de la orilla
        /// mojada (u); el largo del abanico de la desembocadura (u) y cuánto se abre.</summary>
        const float Sobre = 0.04f, PasoMuestra = 0.5f, AnchoVado = 1.6f, Entra = 0.5f, Alargar = 8, AnchoOrilla = 1.2f, Abanico = 3, Abre = 1.5f;

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
        public static List<Tramo> Tramos(Rio rio, Relieve relieve)
        {
            var pts = new List<Vector2>();
            foreach (var p in rio.Puntos) pts.Add(Punto(p));
            for (int pasada = 0; pasada < 2; pasada++) pts = Chaikin(pts);
            if (pts.Count >= 2)
            {
                var fin = pts[pts.Count - 1];
                var dir = (fin - pts[pts.Count - 2]).normalized;
                for (float d = 0.25f; d <= Alargar; d += 0.25f)
                    if (relieve.AguaEn(fin.x + dir.x * d, fin.y + dir.y * d))
                    {
                        for (float s = 0.25f; s <= d + Entra; s += 0.25f) pts.Add(fin + dir * s);
                        break;
                    }
            }
            float total = 0;
            for (int i = 0; i + 1 < pts.Count; i++) total += Vector2.Distance(pts[i], pts[i + 1]);
            float baseAncho = rio.Navegable ? PinturaDelMapa.RioNavegable : PinturaDelMapa.Rio, andado = 0;
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
                        float boca = desemboca ? Mathf.SmoothStep(0, 1, 1 - Mathf.Clamp01((finTramo - andados[i]) / Abanico)) : 0;
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
            if (pts.Count > 0 && tramo.Puntos.Count > 0) Agregar(pts[pts.Count - 1]);
            Cerrar();
            return tramos;

            void Agregar(Vector2 p)
            {
                tramo.Puntos.Add(p);
                andados.Add(andado);
                float ruido = Mathf.PerlinNoise(p.x * 0.12f + 31.7f, p.y * 0.12f + 7.3f);
                tramo.Anchos.Add(baseAncho * (0.75f + 0.5f * ruido) * (1 + 0.6f * (total > 0 ? andado / total : 0)));
            }
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

        static Vector2 Normal(List<Vector2> puntos, int i)
        {
            var dir = (puntos[Mathf.Min(i + 1, puntos.Count - 1)] - puntos[Mathf.Max(i - 1, 0)]).normalized;
            return new Vector2(-dir.y, dir.x);
        }

        /// <summary>Los ríos, sus orillas mojadas y sus vados, en tres hijos de un objeto «Rios» (el de las cintas de agua se llama así).
        /// `suelo(x, y)`: la altura del suelo ya excavado, para pegar la orilla mojada y el vado.</summary>
        public static GameObject Construir(ArtefactoDeMundo a, Relieve relieve, Func<double, double, float> suelo, Transform padre)
        {
            var e = EscalaDelMapa.MetrosPorUnidad;
            var rios = new Cintas();
            var orillas = new Cintas();
            var vados = new Cintas();
            foreach (var rio in a.Rios)
            {
                var todos = new List<(Vector2 p, float ancho)>();
                float andado = 0;
                foreach (var tramo in Tramos(rio, relieve))
                {
                    var pts = tramo.Puntos;
                    for (int i = 0; i < pts.Count; i++) todos.Add((pts[i], tramo.Anchos[i]));
                    var alturas = new List<float>();
                    var vs = new List<float>();
                    for (int i = 0; i < pts.Count; i++)
                    {
                        if (i > 0) andado += Vector2.Distance(pts[i - 1], pts[i]);
                        alturas.Add(NivelDelRio(relieve, pts[i], Normal(pts, i), tramo.Anchos[i] / 2));
                        vs.Add(andado);
                    }
                    // Lo rápido que baja (aguas blancas): la pendiente de la lámina a lo largo del río, del 10 al 30 %.
                    var rapido = new List<float>();
                    for (int i = 0; i < pts.Count; i++)
                    {
                        int i0 = Mathf.Max(0, i - 2), i1 = Mathf.Min(pts.Count - 1, i + 2);
                        float largo = Mathf.Max(0.01f, (vs[i1] - vs[i0]) * e);
                        rapido.Add(Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.1f, 0.3f, (alturas[i0] - alturas[i1]) / largo)));
                    }
                    rios.Agregar(pts, i => Normal(pts, i), i => 0, i => tramo.Anchos[i], i => (alturas[i], alturas[i]), i => new Vector2(rapido[i], tramo.Boca[i]), vs);
                    // La orilla mojada, a cada lado, pegada al suelo ya excavado (0,03 u por encima); x = 0 junto al agua, 1 fuera.
                    foreach (var lado in new[] { 1f, -1f })
                    {
                        orillas.Agregar(pts, i => Normal(pts, i) * lado, i => tramo.Anchos[i] / 2 + AnchoOrilla / 2 - 0.2f, i => AnchoOrilla, i =>
                        {
                            var nn = Normal(pts, i) * lado;
                            Vector2 dentro = pts[i] + nn * (tramo.Anchos[i] / 2 - 0.2f), fuera = pts[i] + nn * (tramo.Anchos[i] / 2 + AnchoOrilla - 0.2f);
                            return (Mathf.Max(suelo(dentro.x, dentro.y), alturas[i]) + 0.03f * e, suelo(fuera.x, fuera.y) + 0.03f * e);
                        }, i => Vector2.zero, vs);
                    }
                }
                foreach (var vado in rio.Vados)
                {
                    var p = Punto(vado);
                    if (relieve.AguaEn(p.x, p.y) || todos.Count < 2) continue;
                    // La dirección del río junto al vado y un tramo ancho y somero de orilla a orilla, algo por encima del agua del río.
                    int cerca = 0;
                    for (int i = 1; i < todos.Count; i++) if (Vector2.Distance(todos[i].p, p) < Vector2.Distance(todos[cerca].p, p)) cerca = i;
                    var dir = (todos[Mathf.Min(cerca + 1, todos.Count - 1)].p - todos[Mathf.Max(cerca - 1, 0)].p).normalized;
                    var normal = new Vector2(-dir.y, dir.x);
                    float ancho = todos[cerca].ancho, largo = (ancho + 1.2f) / 2;
                    var nivel = NivelDelRio(relieve, p, normal, ancho / 2) + 0.03f * e;
                    var cruce = new List<Vector2>();
                    for (float s = -largo; s <= largo + 1e-3f; s += 0.25f) cruce.Add(p + normal * s);
                    var vs = new List<float>();
                    for (int i = 0; i < cruce.Count; i++) vs.Add(i / (float)(cruce.Count - 1)); // de 0 a 1 de orilla a orilla
                    vados.Agregar(cruce, i => -dir, i => 0, i => AnchoVado, i =>
                    {
                        var h = Mathf.Max(nivel, suelo(cruce[i].x, cruce[i].y) + 0.03f * e);
                        return (h, h);
                    }, i => Vector2.zero, vs);
                }
            }
            var go = rios.Objeto("Rios", MaterialDeAgua(ModoAgua.Rio), padre);
            orillas.Objeto("Orillas", MaterialDeAgua(ModoAgua.Orilla), go.transform);
            vados.Objeto("Vados", MaterialDeAgua(ModoAgua.Vado), go.transform);
            return go;
        }

        /// <summary>Cintas a lo largo de una línea, con UV0 (x de un borde al otro, y los u andados, z y w lo que se pase) y las caras hacia arriba.</summary>
        sealed class Cintas
        {
            readonly List<Vector3> v = new List<Vector3>();
            readonly List<Vector4> uv = new List<Vector4>();
            readonly List<int> t = new List<int>();

            /// <summary>`normal(i)`: hacia dónde va x = 1; `desplazar(i)`: cuánto se aparta el centro de la línea (u, por la normal); `ancho(i)` (u);
            /// `alturas(i)`: la del borde x = 0 y la del x = 1 (metros); `zw(i)`: lo que va en UV0.zw.</summary>
            public void Agregar(List<Vector2> puntos, Func<int, Vector2> normal, Func<int, float> desplazar, Func<int, float> ancho,
                Func<int, (float, float)> alturas, Func<int, Vector2> zw, List<float> andado)
            {
                for (int i = 0; i < puntos.Count; i++)
                {
                    var nn = normal(i).normalized;
                    var c = puntos[i] + nn * desplazar(i);
                    Vector2 p0 = c - nn * (ancho(i) / 2), p1 = c + nn * (ancho(i) / 2);
                    var (h0, h1) = alturas(i);
                    if (i > 0)
                    {
                        int b = v.Count - 2;
                        t.AddRange(new[] { b, b + 1, b + 2, b + 1, b + 3, b + 2 });
                    }
                    var z = zw(i);
                    v.Add(EscalaDelMapa.AEscena(p0.x, p0.y, h0));
                    v.Add(EscalaDelMapa.AEscena(p1.x, p1.y, h1));
                    uv.Add(new Vector4(0, andado[i], z.x, z.y));
                    uv.Add(new Vector4(1, andado[i], z.x, z.y));
                }
            }

            public GameObject Objeto(string nombre, Material material, Transform padre)
            {
                var malla = new Mesh { name = nombre, indexFormat = IndexFormat.UInt32 };
                malla.SetVertices(v);
                malla.SetUVs(0, uv);
                // Cada triángulo, mirando hacia arriba (el sombreado no descarta caras, pero las normales cuentan para la luz).
                for (int k = 0; k < t.Count; k += 3)
                {
                    var n = Vector3.Cross(v[t[k + 1]] - v[t[k]], v[t[k + 2]] - v[t[k]]);
                    if (n.y < 0) { var x = t[k + 1]; t[k + 1] = t[k + 2]; t[k + 2] = x; }
                }
                malla.SetTriangles(t, 0);
                var arriba = new Vector3[v.Count];
                for (int k = 0; k < arriba.Length; k++) arriba[k] = Vector3.up;
                malla.SetNormals(arriba);
                malla.RecalculateBounds();
                var go = new GameObject(nombre);
                go.transform.SetParent(padre, false);
                go.AddComponent<MeshFilter>().sharedMesh = malla;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.Off;
                return go;
            }
        }

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

        /// <summary>Una muestra del eje: dónde, el nivel de la lámina (m), el medio ancho (u), hacia dónde va y cuánto gira (rad/u, + a la izquierda).</summary>
        struct Muestra { public double X, Y; public float Nivel, Medio, DirX, DirY, Curva; }
        float medioMaximo;

        public CaucesDelMapa(ArtefactoDeMundo a, Relieve relieve)
        {
            foreach (var rio in a.Rios)
                foreach (var tramo in RiosDelMapa.Tramos(rio, relieve))
                {
                    var pts = tramo.Puntos;
                    for (int i = 0; i < pts.Count; i++)
                    {
                        var p = pts[i];
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
                            DirX = dir.x, DirY = dir.y, Curva = giro / largo });
                    }
                }
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
        public double ACauce(double x, double y, double alcance = 4)
        {
            int ci = (int)Math.Floor(x / Celda), cj = (int)Math.Floor(y / Celda), r = (int)Math.Ceiling((alcance + medioMaximo) / Celda);
            double mejor = double.PositiveInfinity;
            for (int dj = -r; dj <= r; dj++)
                for (int di = -r; di <= r; di++)
                    if (celdas.TryGetValue((ci + di, cj + dj), out var l))
                        foreach (var p in l)
                            mejor = Math.Min(mejor, Math.Sqrt((p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y)) - p.Medio);
            return mejor;
        }

        /// <summary>Como `Fondo`, y además cuánto es ribera en (x, y): 1 junto al agua, 0 a `RiberaPintada` u de la orilla.</summary>
        public float? Consultar(double x, double y, float paso, out float ribera)
        {
            ribera = 0;
            var e = EscalaDelMapa.MetrosPorUnidad;
            double alcance = medioMaximo + paso / 2 + Math.Max(Ribera, RiberaPintada * 3.5);
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
            // La ribera es más ancha en el lado de dentro de las curvas (las barras de arena y guijarros): el lado hacia el que gira el río.
            float lado = (float)(cerca.DirX * (y - cerca.Y) - cerca.DirY * (x - cerca.X));
            float barra = Math.Sign(lado) == Math.Sign(cerca.Curva) ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.03f, 0.25f, Mathf.Abs(cerca.Curva))) : 0;
            ribera = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((dist - cerca.Medio) / (RiberaPintada * (1 + 2.5f * barra))));
            if (dist < efectivo) return cerca.Nivel - Centro * e * (1 - (dist / efectivo) * (dist / efectivo));
            if (dist < efectivo + Ribera) return cerca.Nivel + 0.02f * e + SubeRibera * e * ((dist - efectivo) / Ribera) * ((dist - efectivo) / Ribera);
            return null;
        }
    }

    /// <summary>La niebla de guerra (la `NieblaProyectada` de la VISTA, celdas de 5 u): una textura de la rejilla (R = explorado, G = a la vista)
    /// que el sombreado (MapaComun.hlsl) lee con filtro bilineal y mezcla con su color en tres estados. Se rehace solo cuando cambia la VISTA.</summary>
    public sealed class NieblaDelMapa
    {
        static readonly int Tex = Shader.PropertyToID("_NieblaTex"), Rejilla = Shader.PropertyToID("_NieblaRejilla"),
            Tamano = Shader.PropertyToID("_NieblaTamano"), ColorNiebla = Shader.PropertyToID("_NieblaColor"), Alfas = Shader.PropertyToID("_NieblaAlfas");

        Texture2D textura;
        byte[] pixeles;

        public NieblaDelMapa()
        {
            // En sRGB: como en BronzeAgeClient, la niebla se mezcla después de pasar el color a sRGB (MapaComun.hlsl).
            Shader.SetGlobalVector(ColorNiebla, PinturaDelMapa.Niebla);
            Shader.SetGlobalVector(Alfas, new Vector4(PinturaDelMapa.AlfaSinVer, PinturaDelMapa.AlfaRecordado, PinturaDelMapa.AlfaVisible, 0));
            Shader.SetGlobalVector(Rejilla, Vector4.zero); // sin niebla hasta que llegue la VISTA
        }

        public void Poner(NieblaProyectada n)
        {
            if (n == null || n.Columnas == 0) { Shader.SetGlobalVector(Rejilla, Vector4.zero); return; }
            int columnas = (int)n.Columnas, filas = (int)n.Filas;
            if (textura == null || textura.width != columnas || textura.height != filas)
            {
                if (textura != null) UnityEngine.Object.Destroy(textura);
                // Bilineal, como en BronzeAgeClient: el borde de la niebla se funde en una celda.
                textura = new Texture2D(columnas, filas, TextureFormat.RG16, false, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                pixeles = new byte[columnas * filas * 2];
            }
            var celdas = n.Celdas.Span;
            var visibles = n.Visibles.Span;
            for (int i = 0; i < columnas * filas; i++)
            {
                pixeles[2 * i] = Bit(celdas, i) ? (byte)255 : (byte)0;
                pixeles[2 * i + 1] = Bit(visibles, i) ? (byte)255 : (byte)0;
            }
            textura.SetPixelData(pixeles, 0);
            textura.Apply(false);
            Shader.SetGlobalTexture(Tex, textura);
            Shader.SetGlobalVector(Tamano, new Vector4(columnas, filas, 0, 0));
            Shader.SetGlobalVector(Rejilla, new Vector4(0, 0, n.TamanoCelda / 1000f * EscalaDelMapa.MetrosPorUnidad, 1));
        }

        static bool Bit(System.ReadOnlySpan<byte> m, int i) => (i >> 3) < m.Length && ((m[i >> 3] >> (i & 7)) & 1) == 1;

        public void Destruir()
        {
            if (textura != null) UnityEngine.Object.Destroy(textura);
            Shader.SetGlobalVector(Rejilla, Vector4.zero);
        }
    }
}

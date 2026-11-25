// Los nodos de recursos del mapa de mundo (tema 5). Tres estilos, conmutables (`Estilo`):
//  * «Cajas»: lo de antes, la caja de su color de `CosasEnElMapa` (aquí no se pinta nada).
//  * «Prototipo» (etapa 1): las rocas del prototipo tal cual (`RocasDeNodos`: dodecaedros aplastados, 5 la piedra y 3 los
//    metales, con sus posiciones, radios, giros y colores), con el sombreado de color plano del mapa (`Conquest/MapaColor`: el sol con algo de
//    cielo, la sombra real, la niebla de guerra, las nubes y la bruma); arrojan y reciben sombra.
//  * «Mejorado» (etapa 2, por defecto): un afloramiento de Blender por nodo (`BronzeAgeArte/mapa/nodos`, `Conquest/MapaNodo`) que llena la huella
//    (`RadioPiedra`, `RadioMetal`; el prototipo tiene los metales a 0,3 u: diferencia declarada), en tres variantes elegidas por el hash del
//    nodo y tres niveles por distancia a la cámara, siempre vertical (estratos horizontales) y con la base enterrada en la ladera; un parche de
//    suelo pedregoso alrededor (`Conquest/MapaParcheNodo`, `Parche` veces la huella, con el tinte del mineral, nunca sobre el agua) y cascotes
//    (la roca del prototipo, pequeña, con `MapaColor`). La vegetación deja el parche limpio (`MapaDeMundo.Claros` y `Ralo`).
// Por teselas de 64 u, como el prototipo: las de dentro del alcance se arman (matrices, colores y el parche) en el hilo principal, porque la
// altura sale de la malla de suelo ya pintada (`AlturaPintada`); cuando cambia el suelo (`TerrenoDelMapa.Version`) se rehacen solo las
// teselas cuya firma cambió. Se dibujan las que ve la cámara, en tandas de 1023 instancias.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using BronzeAge.Contract.V1;
using UnityEngine;
using UnityEngine.Rendering;

namespace ConquestTactics.Mundo.Mapa
{
    /// <summary>«Ninguno»: sin nodos (para las pruebas: contar los píxeles de un nodo).</summary>
    public enum EstiloDeNodos { Cajas, Prototipo, Mejorado, Ninguno }

    public sealed class NodosDelMapa
    {
        /// <summary>Por defecto «Mejorado» (la segunda pasada, aprobada por Víctor el 2026-11-21); «Prototipo» y «Cajas» quedan como interruptor.</summary>
        public static EstiloDeNodos Estilo = EstiloDeNodos.Mejorado;
        /// <summary>«Mejorado»: el radio de la huella (u) de la piedra y de los metales; el parche, tantas veces la huella.</summary>
        public static double RadioPiedra = 0.65, RadioMetal = 0.5, Parche = 1.7;
        /// <summary>Para comparar en la previa del mapa: los nodos, modelo y parche, por este factor (1: como están, que Víctor da por buenos).</summary>
        public static double FactorDeNodos = 1;
        double factorHecho = 1;
        /// <summary>«Mejorado»: hasta dónde (m de la cámara) va cada nivel del modelo: cerca, medio; más allá, lejos.</summary>
        public static float NivelCerca = 160, NivelMedio = 400;
        /// <summary>Para las pruebas («Mejorado»): dibujar los modelos; el parche y los cascotes; y el claro en la vegetación.</summary>
        public static bool DibujarModelos = true, DibujarParche = true, ClaroEnLaVegetacion = true;
        static readonly string[] Tipos = { "piedra", "cobre", "estano", "oro", "hierro" };
        static readonly string[] Niveles = { "cerca", "medio", "lejos" };
        /// <summary>El tinte del parche por tipo (multiplica, 0,5 = ninguno; se aplica al 15 %): ninguno la piedra, el estaño y el oro; gris
        /// verdoso leve el cobre; pardo óxido el hierro. Siempre hacia oscuro.</summary>
        static readonly Color[] TinteDelParche =
        {
            new Color(0.5f, 0.5f, 0.5f), new Color(0.40f, 0.46f, 0.42f), new Color(0.5f, 0.5f, 0.5f), new Color(0.5f, 0.5f, 0.5f),
            new Color(0.44f, 0.30f, 0.24f),
        };
        const double Lado = 64;
        const int PorLlamada = 1023;

        sealed class Tesela
        {
            /// <summary>«Prototipo»: las rocas; «Mejorado»: los cascotes (las dos, con la roca del prototipo y `MapaColor`).</summary>
            public Matrix4x4[] Matrices;
            public Vector4[] Colores;
            /// <summary>«Mejorado»: cada afloramiento y cada cascote (tipo, variante, dónde, su tono; los cascotes, siempre el nivel lejos).</summary>
            public List<(int tipo, int v, Matrix4x4 m, Vector4 tono, bool cascote)> Modelos;
            public Mesh Parche;
            public Bounds Caja;
            public int Firma;
            public EstiloDeNodos Estilo;
        }

        readonly TerrenoDelMapa terreno;
        readonly Func<double, double, bool> agua;
        readonly Dictionary<(int, int), List<NodoRecurso>> porTesela = new Dictionary<(int, int), List<NodoRecurso>>();
        readonly Dictionary<(int, int), Tesela> hechas = new Dictionary<(int, int), Tesela>();
        readonly Material material, materialParche;
        readonly Material[,] materialModelo = new Material[5, 3];
        readonly Mesh[,,] modelo = new Mesh[5, 3, 3];
        readonly float[,] fondoModelo = new float[5, 3];
        readonly Mesh roca;
        readonly MaterialPropertyBlock propiedades = new MaterialPropertyBlock();
        readonly Matrix4x4[] tanda = new Matrix4x4[PorLlamada];
        readonly Vector4[] tandaColores = new Vector4[PorLlamada];
        readonly Plane[] planos = new Plane[6];
        readonly Dictionary<string, Vector4> colorDe = new Dictionary<string, Vector4>();
        readonly Dictionary<(int, int, int), (List<Matrix4x4> m, List<Vector4> c)> lotes = new Dictionary<(int, int, int), (List<Matrix4x4>, List<Vector4>)>();
        int version = -1;
        readonly List<(int, int)> pendientes = new List<(int, int)>();
        /// <summary>Lo que puede tardar `Actualizar` en armar teselas en un fotograma (ms; la primera, siempre).</summary>
        public static double Presupuesto = 2;
        /// <summary>¿Quedan teselas por armar? (las pruebas esperan a que no.)</summary>
        public bool Completo { get; private set; } = true;

        /// <summary>Lo que costó lo último (ms): armar teselas en `Actualizar` y mandar a dibujar en `Dibujar`; y cuántas rocas (o modelos y
        /// cascotes) se mandaron.</summary>
        public double MsArmar, MsDibujar, MsTeselaMax;
        public int RocasDibujadas, ModelosDibujados, TeselasHechas;
        /// <summary>¿Están los modelos de «Mejorado» (si no, se pinta como el prototipo)?</summary>
        public bool HayModelos { get; }
        /// <summary>«Mejorado»: los nodos que tienen pueblo encima (su id) y el giro de ese pueblo (grados, en Unity): se dibujan con su variante 1
        /// (contra la que está hecho el pueblo) y ese giro. Lo pone `MapaDeMundo` desde `MaquetasDelMapa`; con otra `VersionGiros`, se rehacen
        /// las teselas.</summary>
        public IReadOnlyDictionary<string, float> GirosDePueblo = new Dictionary<string, float>();
        public int VersionGiros;
        int versionGirosHecha;

        public NodosDelMapa(TerrenoDelMapa terreno, IEnumerable<NodoRecurso> nodos, Func<double, double, bool> agua, Material suelo)
        {
            this.terreno = terreno;
            this.agua = agua;
            foreach (var n in nodos)
            {
                if (n.Posicion == null) continue;
                var k = ((int)Math.Floor(n.Posicion.X / 1000.0 / Lado), (int)Math.Floor(n.Posicion.Y / 1000.0 / Lado));
                if (!porTesela.TryGetValue(k, out var l)) porTesela[k] = l = new List<NodoRecurso>();
                l.Add(n);
            }
            material = new Material(Resources.Load<Shader>("MapaMundo/MapaColor")) { name = "rocas de los nodos", enableInstancing = true };
            roca = MallaDeRoca();
            // «Mejorado»: los modelos y sus texturas
            HayModelos = true;
            var sombreado = Resources.Load<Shader>("MapaMundo/MapaNodo");
            for (int t = 0; t < Tipos.Length; t++)
                for (int v = 0; v < 3; v++)
                {
                    var raiz = Resources.Load<GameObject>($"MapaMundo/Nodos/{Tipos[t]}_{v + 1}");
                    for (int n = 0; n < 3; n++)
                    {
                        var hijo = raiz != null ? raiz.transform.Find($"{Tipos[t]}_{v + 1}_{Niveles[n]}") : null;
                        modelo[t, v, n] = hijo != null ? hijo.GetComponent<MeshFilter>()?.sharedMesh : null;
                        if (modelo[t, v, n] == null) HayModelos = false;
                    }
                    if (modelo[t, v, 0] != null) fondoModelo[t, v] = modelo[t, v, 0].bounds.min.y;
                    var m = new Material(sombreado) { name = $"nodo {Tipos[t]} {v + 1}", enableInstancing = true };
                    m.SetTexture("_ColorTex", Resources.Load<Texture2D>($"MapaMundo/Nodos/{Tipos[t]}_{v + 1}_color"));
                    m.SetTexture("_Mascara", Resources.Load<Texture2D>($"MapaMundo/Nodos/{Tipos[t]}_{v + 1}_mascara"));
                    materialModelo[t, v] = m;
                }
            materialParche = new Material(Resources.Load<Shader>("MapaMundo/MapaParcheNodo")) { name = "parche de los nodos" };
            if (suelo != null)
            {
                materialParche.SetTexture("_SueloColor", suelo.GetTexture("_SueloColor"));
                materialParche.SetTexture("_SueloRuido", suelo.GetTexture("_SueloRuido"));
                materialParche.SetVector("_Escalas", suelo.GetVector("_Escalas"));
            }
        }

        bool ConModelos => Estilo == EstiloDeNodos.Mejorado && HayModelos;

        static int TipoDe(string tipo) => tipo switch { "piedra" => 0, "cobre" => 1, "estano" => 2, "oro" => 3, "hierro" => 4, _ => -1 };

        /// <summary>El radio de la huella (u) de un nodo de `tipo` con «Mejorado».</summary>
        public static double Huella(string tipo) => (tipo == "piedra" ? RadioPiedra : RadioMetal) * FactorDeNodos;

        /// <summary>«Mejorado»: cuánta capa baja queda en (x, y) (de 0 a 1): rala dentro del parche de un nodo (un tercio) y entera a 1,3 veces su
        /// radio. Desde cualquier hilo (los nodos no cambian).</summary>
        public double Ralo(double x, double y)
        {
            if (Estilo != EstiloDeNodos.Mejorado) return 1;
            double r = 1;
            int tx = (int)Math.Floor(x / Lado), ty = (int)Math.Floor(y / Lado);
            for (int j = ty - 1; j <= ty + 1; j++)
                for (int i = tx - 1; i <= tx + 1; i++)
                    if (porTesela.TryGetValue((i, j), out var l))
                        foreach (var n in l)
                        {
                            double rp = Huella(n.Tipo) * Parche, dx = x - n.Posicion.X / 1000.0, dy = y - n.Posicion.Y / 1000.0;
                            if (Math.Abs(dx) > rp * 1.3 || Math.Abs(dy) > rp * 1.3) continue;
                            double d = Math.Sqrt(dx * dx + dy * dy) / rp;
                            r = Math.Min(r, d < 0.9 ? 0.33 : d > 1.3 ? 1 : 0.33 + 0.67 * (d - 0.9) / 0.4);
                        }
            return r;
        }

        /// <summary>Cuántos nodos tiene la tesela que más tiene (para medir el peor caso), y dónde está su centro (u).</summary>
        public (int nodos, double x, double y) TeselaMasLlena()
        {
            (int n, double x, double y) r = (0, 0, 0);
            foreach (var kv in porTesela)
                if (kv.Value.Count > r.n) r = (kv.Value.Count, (kv.Key.Item1 + 0.5) * Lado, (kv.Key.Item2 + 0.5) * Lado);
            return r;
        }

        /// <summary>Suelta todas las teselas (para medir lo que cuesta armarlas).</summary>
        public void Soltar()
        {
            foreach (var t in hechas.Values) if (t.Parche != null) UnityEngine.Object.Destroy(t.Parche);
            hechas.Clear();
        }

        /// <summary>Arma las teselas a menos de `alcance` u de (cx, cy) que falten o cuyo suelo cambió, y suelta las que quedan lejos.</summary>
        public void Actualizar(double cx, double cy, double alcance)
        {
            if (Estilo == EstiloDeNodos.Cajas || Estilo == EstiloDeNodos.Ninguno) { Soltar(); return; }
            var reloj = Stopwatch.StartNew();
            var fuera = new List<(int, int)>();
            foreach (var kv in hechas) if (Distancia(kv.Key, cx, cy) > alcance + Lado || kv.Value.Estilo != Estilo) fuera.Add(kv.Key);
            foreach (var k in fuera)
            {
                if (hechas[k].Parche != null) UnityEngine.Object.Destroy(hechas[k].Parche);
                hechas.Remove(k);
            }
            bool cambio = terreno.Version != version || VersionGiros != versionGirosHecha;
            if (VersionGiros != versionGirosHecha || FactorDeNodos != factorHecho)
            {
                foreach (var t in hechas.Values) if (t.Parche != null) UnityEngine.Object.Destroy(t.Parche);
                hechas.Clear();
            }
            versionGirosHecha = VersionGiros;
            if (FactorDeNodos != factorHecho) { factorHecho = FactorDeNodos; cambio = true; }
            version = terreno.Version;
            int t0x = (int)Math.Floor((cx - alcance) / Lado), t1x = (int)Math.Floor((cx + alcance) / Lado);
            int t0y = (int)Math.Floor((cy - alcance) / Lado), t1y = (int)Math.Floor((cy + alcance) / Lado);
            if (cambio) pendientes.Clear();
            for (int ty = t0y; ty <= t1y; ty++)
                for (int tx = t0x; tx <= t1x; tx++)
                {
                    var k = (tx, ty);
                    if (!porTesela.ContainsKey(k) || Distancia(k, cx, cy) > alcance) continue;
                    if (hechas.TryGetValue(k, out var t) && (!cambio || terreno.Firma(tx * Lado, ty * Lado, (tx + 1) * Lado, (ty + 1) * Lado) == t.Firma)) continue;
                    pendientes.Add(k);
                }
            // las que faltan, las más cercanas primero, con un presupuesto por fotograma (la primera siempre)
            pendientes.Sort((a, b) => Distancia(a, cx, cy).CompareTo(Distancia(b, cx, cy)));
            int hechasAhora = 0;
            Completo = true;
            foreach (var k in pendientes)
            {
                if (hechasAhora > 0 && reloj.Elapsed.TotalMilliseconds > Presupuesto) { Completo = false; break; }
                var t0 = reloj.Elapsed.TotalMilliseconds;
                if (hechas.TryGetValue(k, out var t) && t.Parche != null) UnityEngine.Object.Destroy(t.Parche);
                hechas[k] = Armar(k, porTesela[k]);
                MsTeselaMax = Math.Max(MsTeselaMax, reloj.Elapsed.TotalMilliseconds - t0);
                TeselasHechas++;
                hechasAhora++;
            }
            pendientes.Clear();
            MsArmar = reloj.Elapsed.TotalMilliseconds;
        }

        static double Distancia((int, int) k, double x, double y)
        {
            double dx = Math.Max(0, Math.Max(k.Item1 * Lado - x, x - (k.Item1 + 1) * Lado));
            double dy = Math.Max(0, Math.Max(k.Item2 * Lado - y, y - (k.Item2 + 1) * Lado));
            return Math.Sqrt(dx * dx + dy * dy);
        }

        Tesela Armar((int, int) k, List<NodoRecurso> lista)
        {
            var e = EscalaDelMapa.MetrosPorUnidad;
            var m = new List<Matrix4x4>();
            var c = new List<Vector4>();
            var t = new Tesela { Estilo = Estilo, Firma = terreno.Firma(k.Item1 * Lado, k.Item2 * Lado, (k.Item1 + 1) * Lado, (k.Item2 + 1) * Lado) };
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            void Abarcar(Vector3 p, float r) { min = Vector3.Min(min, p - Vector3.one * r); max = Vector3.Max(max, p + Vector3.one * r); }
            if (!ConModelos)
            {
                foreach (var n in lista)
                {
                    double x = n.Posicion.X / 1000.0, y = n.Posicion.Y / 1000.0;
                    var color = ColorDe(n.Tipo);
                    foreach (var r in RocasDeNodos.DelPrototipo(n.Tipo, x, y))
                    {
                        float radio = (float)(r.R * e);
                        var p = EscalaDelMapa.AEscena(r.X, r.Y, terreno.AlturaPintada(r.X, r.Y) + r.R * 0.25 * e);
                        m.Add(Matrix4x4.TRS(p, GiroEnUnity(r), Vector3.one * radio));
                        c.Add(color);
                        Abarcar(p, radio);
                    }
                }
            }
            else
            {
                t.Modelos = new List<(int, int, Matrix4x4, Vector4, bool)>();
                var parche = new MallaDeParche();
                foreach (var n in lista)
                {
                    int tipo = TipoDe(n.Tipo);
                    if (tipo < 0) continue;
                    double x = n.Posicion.X / 1000.0, y = n.Posicion.Y / 1000.0, h = Huella(n.Tipo), rp = h * Parche;
                    int v = Math.Min(2, (int)(RocasDeNodos.Azar(x + y * 0.37, 91) * 3));
                    float giro = (float)(RocasDeNodos.Azar(y, 92) * 360);
                    // (con pueblo encima: su variante 1 y su giro, y el pedregal y los cascotes solo al pie del afloramiento; lo demás es la tierra
                    // pisada del pueblo)
                    float gp = 0;
                    bool conPueblo = n.Id != null && GirosDePueblo.TryGetValue(n.Id, out gp);
                    if (conPueblo) { v = 0; giro = gp; rp = h * 1.1; }
                    // La base: el suelo bajo la huella (el centro y ocho puntos a 0,85 de su radio). Se apoya en el más bajo; si la ladera es
                    // fuerte, sube hasta el centro sin dejar que el lado de abajo se quede sin la base enterrada del modelo.
                    double hc = terreno.AlturaPintada(x, y), hMin = hc;
                    for (int i = 0; i < 8; i++)
                    {
                        double a = i * Math.PI / 4;
                        hMin = Math.Min(hMin, terreno.AlturaPintada(x + Math.Cos(a) * h * 0.85, y + Math.Sin(a) * h * 0.85));
                    }
                    float escala = (float)(h * e);
                    double enterrado = -fondoModelo[tipo, v] * escala * 0.85;
                    double baseY = Math.Min(hc, hMin + enterrado);
                    var p = EscalaDelMapa.AEscena(x, y, baseY);
                    float tono = (float)(0.92 + 0.14 * RocasDeNodos.Azar(x, 93));
                    t.Modelos.Add((tipo, v, Matrix4x4.TRS(p, Quaternion.Euler(0, giro, 0), Vector3.one * escala), new Vector4(tono, tono, tono, 1), false));
                    Abarcar(p + Vector3.up * escala * 0.5f, escala * 1.2f);
                    // los cascotes: fragmentos de la misma roca (el modelo, pequeño y volcado), medio enterrados, mayores junto al afloramiento
                    // y menores al alejarse; en el parche, nunca sobre el agua
                    var tinte = TinteDelParche[tipo];
                    int cuantos = conPueblo ? 0 : 7 + (int)(RocasDeNodos.Azar(x, 94) * 6);
                    for (int i = 0; i < cuantos; i++)
                    {
                        double f = RocasDeNodos.Azar(y + i, 96), a = RocasDeNodos.Azar(x + i, 95) * Math.PI * 2, d = h * (0.9 + 0.75 * f);
                        double rx = x + Math.Cos(a) * d, ry = y + Math.Sin(a) * d;
                        if (agua(rx, ry)) continue;
                        double rr = h * (0.2 - 0.12 * f) * (0.8 + 0.4 * RocasDeNodos.Azar(x + i, 97));
                        var q = EscalaDelMapa.AEscena(rx, ry, terreno.AlturaPintada(rx, ry) - rr * 0.25 * e);
                        var vuelco = Quaternion.Euler((float)(RocasDeNodos.Azar(x + i, 99) * 360), (float)(a * 57.3), (float)(RocasDeNodos.Azar(y + i, 98) * 360));
                        t.Modelos.Add((tipo, v, Matrix4x4.TRS(q, vuelco, Vector3.one * (float)(rr * e)), new Vector4(tono * 0.95f, tono * 0.95f, tono * 0.95f, 1), true));
                    }
                    parche.Nodo(terreno, agua, x, y, rp, tinte);
                }
                t.Parche = parche.Hacer();
                if (t.Parche != null) { min = Vector3.Min(min, t.Parche.bounds.min); max = Vector3.Max(max, t.Parche.bounds.max); }
            }
            t.Matrices = m.ToArray();
            t.Colores = c.ToArray();
            var caja = new Bounds();
            if (min.x <= max.x) caja.SetMinMax(min, max);
            t.Caja = caja;
            return t;
        }

        /// <summary>El parche de suelo de los nodos de una tesela, en una malla: un disco por nodo (4 anillos de 16 puntos), pegado a la malla de
        /// suelo pintada; uv: lo que se aleja del centro en radios del parche; color: el tinte (rgb) y 0 en alfa sobre el agua.</summary>
        sealed class MallaDeParche
        {
            const int Anillos = 4, Puntos = 16;
            readonly List<Vector3> v = new List<Vector3>();
            readonly List<Vector2> uv = new List<Vector2>();
            readonly List<Vector2> fases = new List<Vector2>();
            readonly List<Color> c = new List<Color>();
            readonly List<int> i = new List<int>();

            public void Nodo(TerrenoDelMapa terreno, Func<double, double, bool> agua, double x, double y, double r, Color tinte)
            {
                int b = v.Count;
                // ladera abajo, el parche se alarga (hasta 1,8 veces con pendiente fuerte)
                double gx = terreno.AlturaPintada(x + 0.5, y) - terreno.AlturaPintada(x - 0.5, y), gy = terreno.AlturaPintada(x, y + 0.5) - terreno.AlturaPintada(x, y - 0.5);
                double pend = Math.Sqrt(gx * gx + gy * gy) / EscalaDelMapa.MetrosPorUnidad, alarga = Math.Min(0.8, pend * 1.6);
                double bx = pend > 1e-6 ? -gx / (pend * EscalaDelMapa.MetrosPorUnidad) : 0, by = pend > 1e-6 ? -gy / (pend * EscalaDelMapa.MetrosPorUnidad) : 0;
                var fase = new Vector2((float)(RocasDeNodos.Azar(x, 81) * 6.283), (float)(RocasDeNodos.Azar(y, 82) * 6.283));
                void Punto(double px, double py, double u, double w)
                {
                    double ox = px - x, oy = py - y, abajo = ox * bx + oy * by;
                    if (abajo > 0) { px += bx * abajo * alarga; py += by * abajo * alarga; }
                    fases.Add(fase);
                    v.Add(EscalaDelMapa.AEscena(px, py, terreno.AlturaPintada(px, py) + 0.04));
                    uv.Add(new Vector2((float)u, (float)w));
                    c.Add(new Color(tinte.r, tinte.g, tinte.b, agua(px, py) ? 0 : 1));
                }
                Punto(x, y, 0, 0);
                for (int a = 1; a <= Anillos; a++)
                    for (int p = 0; p < Puntos; p++)
                    {
                        double ang = p * Math.PI * 2 / Puntos, f = (double)a / Anillos;
                        Punto(x + Math.Cos(ang) * r * f, y + Math.Sin(ang) * r * f, Math.Cos(ang) * f, Math.Sin(ang) * f);
                    }
                // (z = −y: el sentido de las caras de Unity, en el del reloj visto desde arriba)
                for (int p = 0; p < Puntos; p++) { i.Add(b); i.Add(b + 1 + (p + 1) % Puntos); i.Add(b + 1 + p); }
                for (int a = 1; a < Anillos; a++)
                    for (int p = 0; p < Puntos; p++)
                    {
                        int a0 = b + 1 + (a - 1) * Puntos, a1 = b + 1 + a * Puntos, q = (p + 1) % Puntos;
                        i.Add(a0 + p); i.Add(a0 + q); i.Add(a1 + p);
                        i.Add(a0 + q); i.Add(a1 + q); i.Add(a1 + p);
                    }
            }

            public Mesh Hacer()
            {
                if (v.Count == 0) return null;
                var m = new Mesh { name = "parche de los nodos", indexFormat = IndexFormat.UInt32 };
                m.SetVertices(v);
                m.SetUVs(0, uv);
                m.SetUVs(1, fases);
                m.SetColors(c);
                m.SetTriangles(i, 0);
                // las normales, de la propia malla (pegada al suelo), siempre hacia arriba
                m.RecalculateNormals();
                var n = m.normals;
                for (int k = 0; k < n.Length; k++) if (n[k].y < 0) n[k] = -n[k];
                m.normals = n;
                m.RecalculateBounds();
                return m;
            }
        }

        public Vector4 ColorDe(string tipo)
        {
            if (colorDe.TryGetValue(tipo ?? "", out var v)) return v;
            // `RECURSO_COLOR` del prototipo, en la lista de iconos de la migración (la de las barras y chips de recursos)
            var hex = (string)IconosDelMundo.Lista["recurso"]?[tipo ?? ""]?["color"];
            var (cr, cg, cb) = RocasDeNodos.ColorLineal(tipo, hex);
            return colorDe[tipo ?? ""] = new Vector4((float)cr, (float)cg, (float)cb, 1);
        }

        /// <summary>Manda a dibujar las teselas que ve la cámara.</summary>
        public void Dibujar(Camera camara)
        {
            RocasDibujadas = ModelosDibujados = 0;
            if (Estilo == EstiloDeNodos.Cajas || Estilo == EstiloDeNodos.Ninguno || hechas.Count == 0) { MsDibujar = 0; return; }
            var reloj = Stopwatch.StartNew();
            GeometryUtility.CalculateFrustumPlanes(camara, planos);
            var sombras = PinturaDelMapa.SombrasReales ? ShadowCastingMode.On : ShadowCastingMode.Off;
            var rp = new RenderParams(material) { shadowCastingMode = sombras, receiveShadows = true, matProps = propiedades };
            foreach (var l in lotes.Values) { l.m.Clear(); l.c.Clear(); }
            var cam = camara.transform.position;
            int n = 0;
            foreach (var t in hechas.Values)
            {
                // con sombras, también lo que queda fuera pero cerca (su sombra puede caer dentro)
                if (t.Caja.size == Vector3.zero || !GeometryUtility.TestPlanesAABB(planos, PinturaDelMapa.SombrasReales ? Holgada(t.Caja) : t.Caja)) continue;
                for (int i = 0; i < t.Matrices.Length && (t.Modelos == null || DibujarParche); i++)
                {
                    tanda[n] = t.Matrices[i];
                    tandaColores[n] = t.Colores[i];
                    if (++n == PorLlamada) { Mandar(rp, n); n = 0; }
                }
                if (t.Modelos != null)
                    foreach (var (tipo, v, m, tono, cascote) in t.Modelos)
                    {
                        if (cascote ? !DibujarParche : !DibujarModelos) continue;
                        float d = Vector3.Distance(cam, m.GetColumn(3));
                        int nivel = cascote ? 2 : d < NivelCerca ? 0 : d < NivelMedio ? 1 : 2;
                        if (!lotes.TryGetValue((tipo, v, nivel), out var l)) lotes[(tipo, v, nivel)] = l = (new List<Matrix4x4>(), new List<Vector4>());
                        l.m.Add(m);
                        l.c.Add(tono);
                    }
                if (t.Parche != null && DibujarParche) Graphics.RenderMesh(new RenderParams(materialParche) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true }, t.Parche, 0, Matrix4x4.identity);
            }
            if (n > 0) Mandar(rp, n);
            foreach (var kv in lotes)
            {
                var (tipo, v, nivel) = kv.Key;
                var malla = modelo[tipo, v, nivel];
                if (malla == null || kv.Value.m.Count == 0) continue;
                var rm = new RenderParams(materialModelo[tipo, v]) { shadowCastingMode = sombras, receiveShadows = true, matProps = propiedades };
                for (int i = 0; i < kv.Value.m.Count; i += PorLlamada)
                {
                    int k = Math.Min(PorLlamada, kv.Value.m.Count - i);
                    kv.Value.m.CopyTo(i, tanda, 0, k);
                    kv.Value.c.CopyTo(i, tandaColores, 0, k);
                    propiedades.SetVectorArray("_Color", tandaColores);
                    Graphics.RenderMeshInstanced(rm, malla, 0, tanda, k);
                    ModelosDibujados += k;
                }
            }
            MsDibujar = reloj.Elapsed.TotalMilliseconds;
        }

        static Bounds Holgada(Bounds b) { b.Expand(new Vector3(20, 40, 20) * EscalaDelMapa.MetrosPorUnidad * 0.1f); return b; }

        void Mandar(RenderParams rp, int n)
        {
            propiedades.SetVectorArray("_Color", tandaColores);
            Graphics.RenderMeshInstanced(rp, roca, 0, tanda, n);
            RocasDibujadas += n;
        }

        /// <summary>La malla de la roca en Unity: z cambiada de signo (del marco del prototipo al de Unity), sin indexar y con la normal de cada
        /// cara (el `flatShading` del prototipo) y color blanco (el de cada roca va por instancia). Con el espejo, cada cara cambia de sentido:
        /// se le dan la vuelta (b y c) para que mire hacia fuera.</summary>
        public static Mesh MallaDeRoca()
        {
            var d = RocasDeNodos.Dodecaedro();
            var p = new Vector3[d.Length];
            var n = new Vector3[d.Length];
            var c = new Color[d.Length];
            var i = new int[d.Length];
            for (int k = 0; k < d.Length; k++) { p[k] = new Vector3((float)d[k].x, (float)d[k].y, -(float)d[k].z); c[k] = Color.white; i[k] = k; }
            for (int k = 0; k < p.Length; k += 3)
            {
                (p[k + 1], p[k + 2]) = (p[k + 2], p[k + 1]);
                var cara = Vector3.Cross(p[k + 1] - p[k], p[k + 2] - p[k]).normalized;
                n[k] = n[k + 1] = n[k + 2] = cara;
            }
            var m = new Mesh { name = "roca de nodo (prototipo)", vertices = p, normals = n, colors = c };
            m.SetIndices(i, MeshTopology.Triangles, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Un giro del marco del prototipo (mano derecha, z = y del mapa) en el de Unity (mano izquierda, z = −y del mapa).</summary>
        public static Quaternion GiroEnUnity(RocasDeNodos.Roca r) => new Quaternion((float)-r.Qx, (float)-r.Qy, (float)r.Qz, (float)r.Qw);

        public void Destruir()
        {
            Soltar();
            UnityEngine.Object.Destroy(material);
            UnityEngine.Object.Destroy(materialParche);
            foreach (var m in materialModelo) if (m != null) UnityEngine.Object.Destroy(m);
            UnityEngine.Object.Destroy(roca);
        }
    }
}

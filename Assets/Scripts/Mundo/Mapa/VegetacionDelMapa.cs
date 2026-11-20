// La vegetación del mapa de mundo: árboles y matas (`Resources/MapaMundo/Vegetacion`, de BronzeAgeArte) repartidos por tesela de 64 u y
// dibujados por instancias con `Conquest/MapaVegetacion`.
//  * Reparto: las reglas del cliente jugador tal cual (`ReglasDeVegetacion`, portadas de `arboles.ts`, `especies.ts` y `matas.ts`). Árboles
//    por celda de 1 u con la densidad de bosque del artefacto (interpolada; nada por debajo de 0,2), 0,55 / copa² por u², en su sitio al azar;
//    fuera el agua (las cuatro muestras de alrededor), las pendientes de más de 1,1, los cauces (con 0,2 u de margen) y los claros de los sitios.
//    La especie, por zona (altura y agua cercana) y por rodales de unas 22 u donde manda una; tamaño 0,75 a 1,25 de la copa y alto 0,9 a 1,2.
//    Matas (el lentisco) con las probabilidades de `matas.ts`, una candidata cada 0,5 u. Las especies que aún no tienen modelo se dibujan
//    con el de la encina. Determinista: misma tesela, mismas plantas.
//  * Mosaico (`Mosaico`, encendido por defecto): encima del prototipo, `MosaicoDeVegetacion` (wave function collapse por reglas de vecindad)
//    decide la estructura en celdas de 2 u (bosque cerrado, abierto, linde, claro, matorral, pradera, ribera, cañaveral, pedregal) y qué
//    especie manda en cada rodal (celdas de 8 u). El prototipo sigue poniendo cada árbol con sus mismos azares; la estructura solo cambia
//    cuántos caben en cada celda (y el total de la tesela se mantiene) y las matas. Apagado, sale exactamente el reparto del prototipo.
//  * Hilos: las alturas y el agua se muestrean en el hilo principal (`Relieve` no se puede leer desde otro), repartido en varios fotogramas
//    con un tope de tiempo; el reparto y el armado de los trozos (matrices) van en un hilo aparte. Al cambiar los claros solo se rehacen las
//    teselas que tocan, y las de antes se siguen dibujando hasta tener las nuevas.
//  * Dibujo: por trozos de 8 u. Cada trozo elige nivel por distancia a la cámara (cerca hasta 160 m, medio hasta 400 m, lejos más allá) y se
//    entresaca por distancia al héroe con las bandas del prototipo (los que quedan son siempre los mismos y crecen 1/√parte); se descartan
//    los que no ve la cámara. Una llamada por especie, variante, nivel y submalla (en tandas de 1023), y la mancha de sombra bajo cada árbol
//    (alargada hacia el lado contrario del sol, tumbada sobre la pendiente). El tono por planta y por rodal lo pone el sombreado.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace ConquestTactics.Mundo.Mapa
{
    public sealed class VegetacionDelMapa
    {
        /// <summary>La copa (u): de ella salen la densidad (0,55 / copa² árboles por u²) y el tamaño de los árboles. La del prototipo es 0,5 u
        /// (una encina de unos 6,6 m); por defecto, ×1,4 (1,06 u: unos 14 m, junto a las plazas y las columnas). Antes de montar el mapa.</summary>
        public const double CopaGrande = 1.06;
        public static double Copa = CopaGrande;
        /// <summary>Escala de los modelos (van en metros) para la copa de ahora: con 0,5 u, la encina mide como la del prototipo (1,1 u × copa).</summary>
        public static float Tamano => (float)(Copa * 1.32);
        /// <summary>El mosaico de reglas encima del prototipo (apagado: el reparto del prototipo tal cual) y la vista de depuración que pinta
        /// cada celda con el color de su estado. Antes de montar el mapa.</summary>
        public static bool Mosaico = true, VerMosaico;
        /// <summary>Lado del trozo de dibujo (u) y distancias (m) de los niveles.</summary>
        const float Trozo = 8, Cerca = 160, Medio = 300;
        /// <summary>Tope de tiempo por fotograma (ms) para muestrear alturas en el hilo principal, y teselas en marcha a la vez.</summary>
        const double TopeMs = 1.5;
        const int EnMarchaMax = 4;
        /// <summary>Cuánto se hunde la base del tronco (m): que no flote sobre las teselas lejanas de paso 2 u.</summary>
        const float Hundir = 0.7f;

        static readonly string[] Niveles = { "cerca", "medio", "lejos" };

        sealed class Especie
        {
            public string Nombre;
            public int Variantes;
            public float RadioCopa; // m, a escala 1
            public Mesh[,] Mallas; // [variante, nivel]
        }

        /// <summary>Una planta repartida: su especie en las reglas (`EspecieDeArbol`, o `Lentisco`, `Retama`, `Adelfa` o `Canas` si es mata), el modelo con que se dibuja y
        /// su variante, posición (escena), normal del suelo, giro, escala, alto (escala en vertical) y su número al azar (para entresacar).</summary>
        public struct Planta { public byte Clase, Esp, Var, Hondura; public bool Mata; public Vector3 Pos, Suelo; public float Giro, Escala, Alto, Azar; }
        // Hondura del dosel (0 fuera del bosque, 1 abierto, 2 cerrado): aquí solo agranda la sombra del cerrado; lo oscuro de la base de la copa
        // y de la sombra lo saca el sombreado del bosque del motor (`SueloBajo`), sin tandas aparte.
        public const byte Lentisco = 7, Retama = 8, Adelfa = 9, Canas = 10, Hierba = 11, Flor = 12, Piedra = 13;
        /// <summary>La capa baja (hierba, flores y piedras sueltas, como `matas.ts` del prototipo). Las capturas la apagan para comparar.</summary>
        public static bool CapaBaja = true;
        /// <summary>Hasta dónde (u desde el héroe) se dibuja la capa baja: como el prototipo (trozos hasta 48 u).</summary>
        public const double CapaHasta = 48;
        /// <summary>Lo que mide cada tipo de mata en el prototipo (`TAMANO`, u): hierba, flor, mata, junco, piedra.</summary>
        static readonly double[] TamanoDelPrototipo = { 0.15, 0.14, 0.2, 0.32, 0.12 };
        /// <summary>La capa baja, ×1,3 del prototipo (es suelo: no debe competir con las matas); las matas, ×1,6 (los árboles subieron ×2,1).</summary>
        const double EscalaCapa = 1.3, EscalaMatas = 1.6;
        /// <summary>El nombre de la clase de una planta (para los registros de las pruebas).</summary>
        public static string NombreDeClase(byte clase) => clase switch
        {
            Lentisco => "lentisco", Retama => "retama", Adelfa => "adelfa", Canas => "cañas", Hierba => "hierba", Flor => "flor", Piedra => "piedra",
            _ => ((EspecieDeArbol)clase).ToString().ToLowerInvariant(),
        };

        sealed class TrozoDeDibujo
        {
            public Bounds Caja;
            public Vector2 CentroU;
            // [banda][especie * 3 + variante] -> matrices: los árboles (4 bandas), las matas (3) y las sombras de los árboles
            public List<Matrix4x4>[][] Arboles, Matas, Capa;
            // [banda] -> las claves (especie * 3 + variante) que tienen algo: al dibujar no se recorren las vacías
            public int[][] ConArboles, ConMatas, ConCapa;
            public List<Matrix4x4>[] Sombras;
        }

        sealed class Muestras { public int N, Fila; public double X0, Y0; public float[] H; public bool[] Agua; public int Tx, Ty, Gen; }

        readonly Relieve relieve;
        readonly RasterDeMundo fertilidad, humedad, bosque;
        readonly Vector4 umbrales;
        readonly float nieve;
        readonly Especie[] especies;
        readonly Material materialSombra;
        readonly Mesh cuadro;
        public CaucesDelMapa Cauces;
        readonly TablaDelMosaico tabla;
        readonly int semillaMundo;
        readonly Material materialMosaico;
        readonly Dictionary<(int, int), Mesh> mallasMosaico = new Dictionary<(int, int), Mesh>();
        /// <summary>El nivel del mar (m): el umbral de agua del generador (lo usan las probabilidades de las matas).</summary>
        public float NivelDelAgua = 375;

        readonly Dictionary<(int, int), (List<TrozoDeDibujo> trozos, int plantas)> teselas = new Dictionary<(int, int), (List<TrozoDeDibujo>, int)>();
        readonly Dictionary<(int, int), int> generacion = new Dictionary<(int, int), int>();
        readonly HashSet<(int, int)> sucias = new HashSet<(int, int)>();
        readonly HashSet<(int, int)> enCurso = new HashSet<(int, int)>();
        readonly List<Muestras> muestreando = new List<Muestras>();
        sealed class InfoMosaico { public bool Hecho, Caido; public int Intentos; public double Ms; public Vector3[] Vertices; public Color[] Colores; }
        readonly ConcurrentQueue<((int, int) t, int gen, List<TrozoDeDibujo> trozos, int plantas, double ms, InfoMosaico mosaico)> hechas =
            new ConcurrentQueue<((int, int), int, List<TrozoDeDibujo>, int, double, InfoMosaico)>();
        List<(double x, double y, double r)> claros = new List<(double, double, double)>();
        HashSet<string> clavesClaros = new HashSet<string>();
        string claveClaros = "";
        long firmaClaros = long.MinValue;
        readonly List<(int, int)> fuera = new List<(int, int)>();
        readonly List<(int tx, int ty, float d)> pendientes = new List<(int, int, float)>();
        readonly List<Matrix4x4>[,] lote; // [especie * 3 + variante, nivel]
        readonly List<Matrix4x4> lotesSombras = new List<Matrix4x4>();
        readonly Plane[] planos = new Plane[6];
        readonly Dictionary<string, Material[,][]> materialesPorMalla = new Dictionary<string, Material[,][]>();
        readonly List<Material> materiales = new List<Material>();
        int faltan;

        /// <summary>Para medir: teselas repartidas y el tiempo de reparto (ms, en el hilo), plantas cargadas, y lo dibujado el último fotograma.</summary>
        public int Repartidas { get; private set; }
        public double MsRepartiendo { get; private set; }
        public double MsMuestreando { get; private set; }
        public double PeorMsMuestreando { get; private set; }
        public int Plantas { get; private set; }
        public int Dibujadas { get; private set; }
        public long Triangulos { get; private set; }
        public int Llamadas { get; private set; }
        /// <summary>De las llamadas, las que arrojan sombra (la pasada de sombra de URP las repite en cada cascada).</summary>
        public int LlamadasQueArrojan { get; private set; }
        /// <summary>El peor tiempo (ms) de `Actualizar` y de `Dibujar` en un fotograma.</summary>
        public double PeorMsActualizar { get; private set; }
        public double PeorMsDibujar { get; private set; }
        /// <summary>Lo que tarda `Dibujar` en la CPU (media móvil, ms).</summary>
        public double MsDibujar { get; private set; }
        /// <summary>El mosaico: teselas resueltas, las que cayeron al prototipo, intentos y milisegundos (en el hilo).</summary>
        public int Mosaicos { get; private set; }
        public int CaidasAlPrototipo { get; private set; }
        public int IntentosDeMosaico { get; private set; }
        public double MsMosaico { get; private set; }
        public double PeorMsMosaico { get; private set; }

        public VegetacionDelMapa(Relieve relieve, RasterDeMundo fertilidad, RasterDeMundo humedad, RasterDeMundo bosque, Vector4 umbrales, float nieve)
        {
            this.relieve = relieve;
            this.fertilidad = fertilidad;
            this.humedad = humedad;
            this.bosque = bosque;
            this.umbrales = umbrales;
            this.nieve = nieve;

            var texto = Resources.Load<TextAsset>("MapaMundo/Vegetacion/mosaico");
            tabla = texto != null ? JsonConvert.DeserializeObject<TablaDelMosaico>(texto.text, new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace })
                : new TablaDelMosaico();
            unchecked
            {
                int h = (int)2166136261;
                foreach (var ch in relieve?.Hash ?? "") h = (h ^ ch) * 16777619;
                semillaMundo = h;
            }
            materialMosaico = new Material(Resources.Load<Shader>("MapaMundo/MapaColor")) { name = "mosaico (depuración)" };
            materialMosaico.SetFloat("_Luz", 0);
            var manifiesto = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(Resources.Load<TextAsset>("MapaMundo/Vegetacion/vegetacion").text);
            var sombreado = Resources.Load<Shader>("MapaMundo/MapaVegetacion");
            var lista = new List<Especie>();
            // (nombre: el de sus texturas; modelo: el FBX)
            foreach (var (nombre, modelo, variantes, radio) in new[] { ("encina", "encina", 3, 4.2f), ("olivo", "olivo", 3, 3.2f), ("lentisco", "lentisco", 2, 1.1f),
                         ("pino", "pino", 3, 5.0f), ("alamo", "alamo", 3, 2.6f), ("cipres", "cipres", 3, 1.3f), ("abeto", "abeto", 3, 3.4f), ("palmera", "palmera", 3, 5.0f),
                         ("adelfa", "adelfa", 3, 1.0f), ("retama", "retama", 3, 0.9f), ("canas", "canas", 3, 0.7f),
                         ("hierba", "hierba", 3, 0.4f), ("flor", "flor", 3, 0.4f), ("piedra", "piedra", 3, 0.5f) })
            {
                var raiz = Resources.Load<GameObject>("MapaMundo/Vegetacion/" + modelo);
                if (raiz == null) continue;
                var corteza = Array.IndexOf(new[] { "olivo", "pino", "alamo", "cipres", "abeto", "palmera", "piedra" }, nombre) >= 0 ? nombre : "encina";
                var hojas = Resources.Load<Texture2D>("MapaMundo/Vegetacion/" + nombre + "_hojas_color") != null ? nombre : "encina"; // (la piedra no tiene)
                // la palmera y las cañas no tienen grumos de copa (la submalla «masa» no sale): la de la encina, por si acaso
                var masa = Resources.Load<Texture2D>("MapaMundo/Vegetacion/" + nombre + "_masa_color") != null ? nombre : "encina";
                var mats = new Dictionary<string, Material>
                {
                    ["corteza"] = Material(sombreado, nombre + " corteza", corteza + "_corteza", false, 0.0f, 2010),
                    ["hojas"] = Material(sombreado, nombre + " hojas", hojas + "_hojas", true, 0.35f, 2450),
                    ["masa"] = Material(sombreado, nombre + " masa", masa + "_masa", false, 0.15f, 2010),
                    ["lejos"] = Material(sombreado, nombre + " lejos", nombre + "_lejos", false, 0.1f, 2010, soloColor: true),
                };
                // la hierba toma el tinte del suelo de debajo y la flor, el color de su mancha; los árboles, la hondura del bosque (en el sombreado)
                bool arbol = Array.IndexOf(new[] { "encina", "olivo", "pino", "alamo", "cipres", "abeto", "palmera" }, nombre) >= 0;
                foreach (var m in mats.Values)
                {
                    m.SetFloat("_Modo", nombre == "hierba" ? 1 : nombre == "flor" ? 2 : 0);
                    m.SetFloat("_ConHondura", arbol ? 1 : 0);
                }
                var e = new Especie { Nombre = nombre, Variantes = variantes, RadioCopa = radio, Mallas = new Mesh[variantes, 3] };
                var porMalla = new Material[variantes, 3][];
                for (int v = 0; v < variantes; v++)
                    for (int n = 0; n < 3; n++)
                    {
                        var clave = $"{modelo}_{v + 1}_{Niveles[n]}";
                        var hijo = raiz.transform.Find(clave);
                        e.Mallas[v, n] = hijo != null ? hijo.GetComponent<MeshFilter>()?.sharedMesh : null;
                        // Qué es cada submalla: por el nombre del material importado (<especie>_corteza, _hojas, _masa, _lejos); si no, el manifiesto.
                        var importados = hijo != null ? hijo.GetComponent<MeshRenderer>()?.sharedMaterials : null;
                        var tipos = importados != null && importados.Length > 0 && Array.TrueForAll(importados, x => x != null && mats.ContainsKey(Tipo(x.name)))
                            ? new List<string>(Array.ConvertAll(importados, x => Tipo(x.name)))
                            : manifiesto.TryGetValue(clave, out var k) ? k : new List<string>();
                        porMalla[v, n] = tipos.ConvertAll(x => mats[x]).ToArray();
                    }
                materialesPorMalla[nombre] = porMalla;
                lista.Add(e);
            }
            especies = lista.ToArray();
            lote = new List<Matrix4x4>[especies.Length * 3, 3];
            for (int i = 0; i < lote.GetLength(0); i++) for (int n = 0; n < 3; n++) lote[i, n] = new List<Matrix4x4>();


            materialSombra = new Material(Resources.Load<Shader>("MapaMundo/MapaSombraArbol")) { name = "sombra bajo los árboles", enableInstancing = true, renderQueue = 2005 };
            cuadro = new Mesh { name = "cuadro de sombra" };
            cuadro.SetVertices(new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) });
            cuadro.SetUVs(0, new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
            cuadro.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            cuadro.RecalculateNormals();
            cuadro.bounds = new Bounds(Vector3.zero, new Vector3(1, 1, 1));
        }

        static string Tipo(string material) => material.Substring(material.LastIndexOf('_') + 1);

        Material Material(Shader s, string nombre, string textura, bool recorte, float translucidez, int cola, bool soloColor = false)
        {
            var m = new Material(s) { name = nombre, enableInstancing = true, renderQueue = cola };
            m.SetTexture("_MainTex", Resources.Load<Texture2D>("MapaMundo/Vegetacion/" + textura + "_color"));
            if (!soloColor)
            {
                m.SetTexture("_Normal", Resources.Load<Texture2D>("MapaMundo/Vegetacion/" + textura + "_normal"));
                m.SetTexture("_Mascara", Resources.Load<Texture2D>("MapaMundo/Vegetacion/" + textura + "_mascara"));
            }
            bool corteza = textura.EndsWith("_corteza");
            m.SetFloat("_Recorte", recorte ? 1 : 0);
            m.SetFloat("_Cull", recorte ? (float)CullMode.Off : (float)CullMode.Back);
            m.SetFloat("_Translucidez", translucidez);
            m.SetFloat("_Viento", corteza ? 0.04f : 0.12f);
            m.SetFloat("_Variacion", corteza ? 0.25f : 1);
            materiales.Add(m);
            return m;
        }

        /// <summary>Una pieza para mirarla suelta (pruebas, comparación con Blender): la malla y sus materiales.</summary>
        public (Mesh malla, Material[] materiales) Pieza(string especie, int variante, int nivel)
        {
            var i = Array.FindIndex(especies, e => e.Nombre == especie);
            return i < 0 ? (null, null) : (especies[i].Mallas[variante, nivel], materialesPorMalla[especie][variante, nivel]);
        }

        /// <summary>Los sitios sin árboles alrededor (u). Si cambian, se rehacen solo las teselas que tocan los que cambiaron; las de antes se
        /// siguen dibujando hasta tener las nuevas.</summary>
        public void Claros(List<(double x, double y, double r)> nuevos)
        {
            // Primero, sin crear nada (se llama cada fotograma): ¿han cambiado?
            long firma = nuevos.Count;
            unchecked { foreach (var c in nuevos) firma = firma * 1000003 + (long)Math.Round(c.x * 10) * 7919 + (long)Math.Round(c.y * 10) * 104729 + (long)Math.Round(c.r * 10); }
            if (firma == firmaClaros) return;
            firmaClaros = firma;
            nuevos = new List<(double, double, double)>(nuevos);
            var sb = new System.Text.StringBuilder();
            var claves = new HashSet<string>();
            foreach (var c in nuevos) { var k = $"{c.x:0.0},{c.y:0.0},{c.r:0.0}"; claves.Add(k); sb.Append(k).Append(';'); }
            var clave = sb.ToString();
            if (clave == claveClaros) return;
            if (relieve != null)
            {
                float lado = (float)relieve.LadoEnUnidades;
                void Marcar(double x, double y, double r)
                {
                    for (int ty = (int)Math.Floor((y - r) / lado); ty <= (int)Math.Floor((y + r) / lado); ty++)
                        for (int tx = (int)Math.Floor((x - r) / lado); tx <= (int)Math.Floor((x + r) / lado); tx++)
                            if (teselas.ContainsKey((tx, ty)) || enCurso.Contains((tx, ty))) sucias.Add((tx, ty));
                }
                foreach (var c in nuevos) if (!clavesClaros.Contains($"{c.x:0.0},{c.y:0.0},{c.r:0.0}")) Marcar(c.x, c.y, c.r);
                foreach (var c in claros) if (!claves.Contains($"{c.x:0.0},{c.y:0.0},{c.r:0.0}")) Marcar(c.x, c.y, c.r);
            }
            claveClaros = clave;
            clavesClaros = claves;
            claros = nuevos;
        }

        /// <summary>Reparte las teselas a menos de `alcance` u de (x, y) que falten (las más cercanas primero) y suelta las lejanas.</summary>
        public void Actualizar(double x, double y, float alcance)
        {
            var total = Stopwatch.StartNew();
            ActualizarSinMedir(x, y, alcance);
            PeorMsActualizar = Math.Max(PeorMsActualizar, total.Elapsed.TotalMilliseconds);
        }

        void ActualizarSinMedir(double x, double y, float alcance)
        {
            float lado = (float)relieve.LadoEnUnidades;
            fuera.Clear();
            foreach (var k in teselas.Keys)
                if (DistanciaTesela(k.Item1, k.Item2, x, y, lado) > alcance + lado) fuera.Add(k);
            foreach (var k in fuera) { teselas.Remove(k); sucias.Remove(k); }

            while (hechas.TryDequeue(out var h))
            {
                enCurso.Remove(h.t);
                if (!generacion.TryGetValue(h.t, out var g) || g != h.gen) continue; // la pidieron de nuevo entretanto
                teselas[h.t] = (h.trozos, h.plantas);
                Repartidas++;
                MsRepartiendo += h.ms;
                if (h.mosaico != null && h.mosaico.Hecho)
                {
                    Mosaicos++;
                    IntentosDeMosaico += h.mosaico.Intentos;
                    MsMosaico += h.mosaico.Ms;
                    PeorMsMosaico = Math.Max(PeorMsMosaico, h.mosaico.Ms);
                    if (h.mosaico.Caido) CaidasAlPrototipo++;
                    if (h.mosaico.Vertices != null)
                    {
                        if (mallasMosaico.TryGetValue(h.t, out var vieja)) UnityEngine.Object.Destroy(vieja);
                        var malla = new Mesh { name = "mosaico", indexFormat = IndexFormat.UInt32 };
                        malla.SetVertices(h.mosaico.Vertices);
                        malla.SetColors(h.mosaico.Colores);
                        var tri = new int[h.mosaico.Vertices.Length / 4 * 6];
                        // (en la escena z = −y: el orden va al revés para que las caras miren arriba)
                        for (int q = 0, v = 0; v < h.mosaico.Vertices.Length; v += 4) { tri[q++] = v; tri[q++] = v + 2; tri[q++] = v + 1; tri[q++] = v; tri[q++] = v + 3; tri[q++] = v + 2; }
                        malla.SetTriangles(tri, 0);
                        malla.RecalculateNormals();
                        mallasMosaico[h.t] = malla;
                    }
                }
            }
            Plantas = 0;
            foreach (var t in teselas.Values) Plantas += t.plantas;

            // Las que faltan o están sucias, las más cercanas primero.
            pendientes.Clear();
            int t0x = Mathf.Max(0, (int)((x - alcance) / lado)), t1x = Mathf.Min(relieve.TeselasX - 1, (int)((x + alcance) / lado));
            int t0y = Mathf.Max(0, (int)((y - alcance) / lado)), t1y = Mathf.Min(relieve.TeselasY - 1, (int)((y + alcance) / lado));
            for (int ty = t0y; ty <= t1y; ty++)
                for (int tx = t0x; tx <= t1x; tx++)
                {
                    var d = DistanciaTesela(tx, ty, x, y, lado);
                    if (d > alcance || enCurso.Contains((tx, ty)) || Muestreando(tx, ty)) continue;
                    if (teselas.ContainsKey((tx, ty)) && !sucias.Contains((tx, ty))) continue;
                    pendientes.Add((tx, ty, d));
                }
            if (pendientes.Count > 1) pendientes.Sort(PorDistancia);
            faltan = pendientes.Count;
            for (int i = 0; i < pendientes.Count && muestreando.Count + enCurso.Count < EnMarchaMax; i++)
            {
                var (tx, ty, _) = pendientes[i];
                sucias.Remove((tx, ty));
                int gen = (generacion.TryGetValue((tx, ty), out var g) ? g : 0) + 1;
                generacion[(tx, ty)] = gen;
                muestreando.Add(NuevasMuestras(tx, ty, lado, gen));
                faltan--;
            }

            // El muestreo, con tope de tiempo; al acabar una tesela, el reparto va al hilo.
            var reloj = Stopwatch.StartNew();
            while (muestreando.Count > 0 && reloj.Elapsed.TotalMilliseconds < TopeMs)
            {
                var m = muestreando[0];
                MuestrearFila(m);
                if (m.Fila < m.N) continue;
                muestreando.RemoveAt(0);
                enCurso.Add((m.Tx, m.Ty));
                var misClaros = claros;
                Task.Run(() =>
                {
                    var r = Stopwatch.StartNew();
                    List<TrozoDeDibujo> trozos;
                    int n = 0;
                    var info = new InfoMosaico();
                    try
                    {
                        var plantas = Repartir(m.Tx, m.Ty, lado, m, misClaros, info);
                        n = plantas.Count;
                        trozos = Trozos((m.Tx, m.Ty), plantas, lado);
                    }
                    catch (Exception ex) { Debug.LogException(ex); trozos = new List<TrozoDeDibujo>(); }
                    hechas.Enqueue(((m.Tx, m.Ty), m.Gen, trozos, n, r.Elapsed.TotalMilliseconds, info));
                });
            }
            var ms = reloj.Elapsed.TotalMilliseconds;
            MsMuestreando += ms;
            PeorMsMuestreando = Math.Max(PeorMsMuestreando, ms);
        }

        static readonly Comparison<(int tx, int ty, float d)> PorDistancia = (a, b) => a.d.CompareTo(b.d);

        bool Muestreando(int tx, int ty)
        {
            foreach (var m in muestreando) if (m.Tx == tx && m.Ty == ty) return true;
            return false;
        }

        /// <summary>¿Quedan teselas por repartir?</summary>
        public bool Completo => enCurso.Count == 0 && muestreando.Count == 0 && faltan == 0 && hechas.IsEmpty;

        static float DistanciaTesela(int tx, int ty, double x, double y, float lado)
        {
            double dx = Math.Max(0, Math.Max(tx * lado - x, x - (tx + 1) * lado));
            double dy = Math.Max(0, Math.Max(ty * lado - y, y - (ty + 1) * lado));
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        // ── el reparto ──

        const float PasoMuestras = 0.5f;
        const int Margen = 6; // 3 u: la distancia al agua de la ribera (2,5 u) sale de la ventana

        Muestras NuevasMuestras(int tx, int ty, float lado, int gen)
        {
            int n = Mathf.RoundToInt(lado / PasoMuestras) + 1 + 2 * Margen;
            return new Muestras { N = n, X0 = tx * lado - Margen * PasoMuestras, Y0 = ty * lado - Margen * PasoMuestras, H = new float[n * n], Agua = new bool[n * n], Tx = tx, Ty = ty, Gen = gen };
        }

        /// <summary>Una fila de alturas y agua de la tesela, cada 0,5 u (en el hilo principal).</summary>
        void MuestrearFila(Muestras m)
        {
            int j = m.Fila;
            for (int i = 0, k = j * m.N; i < m.N; i++, k++)
            {
                double x = m.X0 + i * PasoMuestras, y = m.Y0 + j * PasoMuestras;
                m.H[k] = (float)relieve.AlturaEn(x, y);
                m.Agua[k] = relieve.AguaEn(x, y);
            }
            m.Fila++;
        }

        /// <summary>Para las pruebas: reparte la tesela ahora, en este hilo, con los claros de ahora.</summary>
        public List<Planta> RepartirAhora(int tx, int ty)
        {
            float lado = (float)relieve.LadoEnUnidades;
            var m = NuevasMuestras(tx, ty, lado, 0);
            while (m.Fila < m.N) MuestrearFila(m);
            return Repartir(tx, ty, lado, m, claros, new InfoMosaico());
        }

        /// <summary>Para las pruebas: el mosaico de la tesela (estados de 2 u y rodales de 8 u) o null si cayó al prototipo.</summary>
        public (EstadoDeCelda[] estructura, int[] rodales) MosaicoAhora(int tx, int ty)
        {
            float lado = (float)relieve.LadoEnUnidades;
            var m = NuevasMuestras(tx, ty, lado, 0);
            while (m.Fila < m.N) MuestrearFila(m);
            var info = new InfoMosaico();
            var r = Resolver(tx, ty, lado, m, claros, info);
            return r;
        }

        public string NombreDeEspecie(int i) => especies[i].Nombre;

        List<Planta> Repartir(int tx, int ty, float lado, Muestras m, List<(double x, double y, double r)> claros, InfoMosaico info)
        {
            var e = EscalaDelMapa.MetrosPorUnidad;
            var plantas = new List<Planta>();
            if (especies.Length == 0) return plantas;
            int Modelo(string n) => Array.FindIndex(especies, s => s.Nombre == n);
            int iEncina = Modelo("encina"), iOlivo = Modelo("olivo"), iPino = Modelo("pino"), iLentisco = Modelo("lentisco"), iAlamo = Modelo("alamo");
            int iRetama = Modelo("retama"), iAdelfa = Modelo("adelfa"), iCanas = Modelo("canas"), iPiedra = Modelo("piedra");
            int iHierba = Modelo("hierba"), iFlor = Modelo("flor");
            // El modelo de cada especie de las reglas; las que aún no tienen el suyo, con el de la encina.
            var modelo = new int[7];
            for (int i = 0; i < 7; i++) modelo[i] = iEncina;
            if (iOlivo >= 0) modelo[(int)EspecieDeArbol.Olivo] = iOlivo;
            if (iPino >= 0) modelo[(int)EspecieDeArbol.Pino] = iPino;
            if (iAlamo >= 0) modelo[(int)EspecieDeArbol.Alamo] = iAlamo;
            foreach (var (esp, nombre) in new[] { (EspecieDeArbol.Cipres, "cipres"), (EspecieDeArbol.Abeto, "abeto"), (EspecieDeArbol.Palmera, "palmera") })
                if (Modelo(nombre) >= 0) modelo[(int)esp] = Modelo(nombre);
            double costa = umbrales.x, colina = umbrales.y, montana = umbrales.z;
            var misClaros = claros.FindAll(c => c.x + c.r > tx * lado && c.x - c.r < (tx + 1) * lado && c.y + c.r > ty * lado && c.y - c.r < (ty + 1) * lado);
            bool rioCerca = Cauces != null && Cauces.HayRioCerca(tx * lado, ty * lado, (tx + 1) * lado, (ty + 1) * lado);
            var aAgua = ReglasDeVegetacion.DistanciaAlAgua(m.Agua, m.N, m.N);
            double Bosque(double x, double y) => bosque == null ? 0 : ReglasDeVegetacion.RasterEn(bosque.Columnas, bosque.Filas, bosque.Celda, bosque.Datos, x, y);
            double Fertil(double x, double y) => fertilidad == null ? 0.3 : ReglasDeVegetacion.RasterEn(fertilidad.Columnas, fertilidad.Filas, fertilidad.Celda, fertilidad.Datos, x, y);

            // La muestra de (x, y) (la de abajo a la izquierda de su celda) y la altura interpolada (`enMuestras`).
            int Muestra(double x, double y, out float alt)
            {
                double fi = Math.Min(m.N - 1.001, Math.Max(0, (x - m.X0) / PasoMuestras)), fj = Math.Min(m.N - 1.001, Math.Max(0, (y - m.Y0) / PasoMuestras));
                int i = (int)Math.Floor(fi), j = (int)Math.Floor(fj), k = j * m.N + i;
                float a = (float)(fi - i), b = (float)(fj - j);
                alt = (m.H[k] * (1 - a) + m.H[k + 1] * a) * (1 - b) + (m.H[k + m.N] * (1 - a) + m.H[k + m.N + 1] * a) * b;
                return k;
            }
            bool Agua(int c) => m.Agua[c] || m.Agua[c + 1] || m.Agua[c + m.N] || m.Agua[c + m.N + 1];
            float Pendiente(int c) => (float)(Math.Sqrt((m.H[c + 1] - m.H[c]) * (m.H[c + 1] - m.H[c]) + (m.H[c + m.N] - m.H[c]) * (m.H[c + m.N] - m.H[c])) / (PasoMuestras * e));
            Vector3 Suelo(int c) => new Vector3(-(m.H[c + 1] - m.H[c]) / (PasoMuestras * e), 1, (m.H[c + m.N] - m.H[c]) / (PasoMuestras * e)).normalized; // z = −y
            bool EnClaro(double x, double y)
            {
                foreach (var q in misClaros) if ((x - q.x) * (x - q.x) + (y - q.y) * (y - q.y) < q.r * q.r) return true;
                return false;
            }
            double ACauce(double x, double y) => rioCerca ? Cauces.ACauce(x, y) : double.PositiveInfinity;

            // El mosaico (si va): la estructura de cada celda de 2 u y la especie que manda en cada rodal de 8 u.
            var (estructura, rodales) = Mosaico ? Resolver(tx, ty, lado, m, claros, info) : (null, null);
            int nE = (int)Math.Round(lado / MosaicoDeVegetacion.Celda), nR = (int)Math.Round(lado / MosaicoDeVegetacion.CeldaRodal);
            // Los bordes entre celdas ondulan: el estado y el rodal se miran con la posición movida ±0,9 u por un ruido de 3 u.
            double Onda(double x, double y, int s) => (ReglasDeVegetacion.Manchas(x, y, 3, s) - 0.5) * 1.8;
            EstadoDeCelda EstadoEn(double x0, double y0)
            {
                double x = x0 + Onda(x0, y0, 901), y = y0 + Onda(x0, y0, 907);
                int i = Math.Min(nE - 1, Math.Max(0, (int)((x - tx * lado) / MosaicoDeVegetacion.Celda)));
                int j = Math.Min(nE - 1, Math.Max(0, (int)((y - ty * lado) / MosaicoDeVegetacion.Celda)));
                return estructura[j * nE + i];
            }

            // Los árboles (`arbolesDelTrozo`): celdas de 1 u.
            double porU2 = ReglasDeVegetacion.Porcion / (Copa * Copa);
            int c0x = (int)Math.Round(tx * lado), c0y = (int)Math.Round(ty * lado);
            // Con el mosaico, cada estado lleva su parte de la densidad del prototipo (`TablaDelMosaico.Arboles`): el bosque cerrado, con las
            // copas tocándose (más que el prototipo, que se queda en un 43 % de cobertura); el abierto, la del prototipo.
            const double ajuste = 1;
            for (int j = c0y; j < c0y + (int)lado; j++)
                for (int i = c0x; i < c0x + (int)lado; i++)
                {
                    double d = Bosque(i + 0.5, j + 0.5);
                    if (d < ReglasDeVegetacion.DensidadMinima) continue;
                    double l = porU2 * d;
                    EstadoDeCelda estado = estructura != null ? EstadoEn(i + 0.5, j + 0.5) : EstadoDeCelda.Cerrado;
                    byte hondura = estructura == null ? (byte)0 : estado == EstadoDeCelda.Cerrado ? (byte)2 : estado == EstadoDeCelda.Abierto || estado == EstadoDeCelda.Ribera ? (byte)1 : (byte)0;
                    if (estructura != null) l *= tabla.ParteDeArboles(estado) * ajuste;
                    int cuantos = (int)Math.Floor(l) + (ReglasDeVegetacion.Azar(i, j, 7) < l % 1 ? 1 : 0);
                    for (int k = 0; k < cuantos; k++)
                    {
                        double x = i + ReglasDeVegetacion.Azar(i, j, 11 + k), y = j + ReglasDeVegetacion.Azar(i, j, 29 + k);
                        int c = Muestra(x, y, out var alt);
                        if (Agua(c)) continue;
                        if (Pendiente(c) > ReglasDeVegetacion.PendienteMaxima || ACauce(x, y) < 0.2 || EnClaro(x, y)) continue;
                        // Lo que el suelo del mapa no tiene en el prototipo: ni en la cima ni en la nieve.
                        if (alt > umbrales.w || alt > nieve - 40) continue;
                        double dAgua = Math.Min(aAgua[c] * PasoMuestras, ACauce(x, y));
                        double r = ReglasDeVegetacion.Azar(i, j, 41 + k);
                        EspecieDeArbol esp;
                        double t = ReglasDeVegetacion.Azar(i, j, 67 + k);
                        double xr = x + Onda(x, y, 911), yr = y + Onda(x, y, 919);
                        int rodal = rodales != null ? rodales[Math.Min(nR - 1, Math.Max(0, (int)((yr - ty * lado) / MosaicoDeVegetacion.CeldaRodal))) * nR
                                                              + Math.Min(nR - 1, Math.Max(0, (int)((xr - tx * lado) / MosaicoDeVegetacion.CeldaRodal)))] : -1;
                        if (rodal >= 0)
                        {
                            var zona = ReglasDeVegetacion.ZonaDe(x, y, alt, dAgua, costa, colina, montana);
                            var dominante = (EspecieDeArbol)rodal;
                            // El olivo no manda en el bosque cerrado (un rodal de olivos apretados parece un vivero): ahí, la siguiente de la zona.
                            if (dominante == EspecieDeArbol.Olivo && estado == EstadoDeCelda.Cerrado) dominante = ReglasDeVegetacion.SiguienteSin(zona, EspecieDeArbol.Olivo);
                            esp = ReglasDeVegetacion.EspecieConDominante(zona, dominante, t);
                        }
                        else
                            esp = ReglasDeVegetacion.EspecieDe(x, y, alt, dAgua, t, costa, colina, montana);
                        int mod = modelo[(int)esp];
                        if (mod < 0) continue;
                        var s = especies[mod];
                        double r2 = ReglasDeVegetacion.Azar((int)Math.Round(x * 64), (int)Math.Round(y * 64), 5);
                        plantas.Add(new Planta
                        {
                            Clase = (byte)esp,
                            Esp = (byte)mod,
                            Var = (byte)Math.Min(s.Variantes - 1, (int)(ReglasDeVegetacion.Azar(i, j, 101 + k) * s.Variantes)),
                            Pos = EscalaDelMapa.AEscena(x, y, alt - Hundir),
                            Suelo = Suelo(c),
                            Giro = (float)(r * 360),
                            // en la linde, árboles jóvenes (0,6 a 0,8)
                            Escala = estado == EstadoDeCelda.Linde && estructura != null ? (float)(0.6 + 0.2 * r) : (float)(0.75 + 0.5 * r),
                            Hondura = hondura,
                            Alto = (float)(0.9 + 0.3 * r2),
                            Azar = (float)ReglasDeVegetacion.Azar(i, j, 53 + k),
                        });
                    }
                }

            // Las matas (`matasDelTrozo`): una candidata cada 0,5 u. La mata del prototipo, con su especie según el sitio (lo pone esto, no el
            // prototipo): adelfa junto al agua en lo bajo (ramblas y riberas), retama en el matorral y algo en la linde, y lentisco en lo demás.
            // El junco, cañas (uno de cada dos; en el cañaveral, todos, y más de la caña alta). La hierba, las flores y las piedras, aún no.
            if (iLentisco >= 0)
            {
                int celdas = (int)Math.Round(lado / ReglasDeVegetacion.PasoMatas);
                int g0x = (int)Math.Round(tx * lado / ReglasDeVegetacion.PasoMatas), g0y = (int)Math.Round(ty * lado / ReglasDeVegetacion.PasoMatas);
                for (int j = 0; j < celdas; j++)
                    for (int i = 0; i < celdas; i++)
                    {
                        int gi = g0x + i, gj = g0y + j;
                        double x = tx * lado + (i + ReglasDeVegetacion.Azar(gi, gj, 5)) * ReglasDeVegetacion.PasoMatas;
                        double y = ty * lado + (j + ReglasDeVegetacion.Azar(gi, gj, 7)) * ReglasDeVegetacion.PasoMatas;
                        int c = Muestra(x, y, out var alt);
                        if (Agua(c) || ACauce(x, y) < 0.1 || EnClaro(x, y)) continue;
                        if (alt > umbrales.w || alt > nieve - 40) continue; // ni en la cima ni en la nieve (el suelo del mapa las pinta de roca y nieve)
                        float pend = Pendiente(c);
                        double dAgua = Math.Min(aAgua[c] * PasoMuestras, ACauce(x, y));
                        var pr = ReglasDeVegetacion.Probabilidades(alt, pend, Fertil(x, y), Bosque(x, y), dAgua, ReglasDeVegetacion.Manchas(x, y, 6, 77),
                            NivelDelAgua, costa, colina, montana);
                        var estadoM = estructura != null ? EstadoEn(x, y) : EstadoDeCelda.Vacio;
                        if (estructura != null)
                        {
                            pr[(int)TipoDeMata.Mata] = Math.Min(1, pr[(int)TipoDeMata.Mata] * tabla.RefuerzoDeMatas(estadoM));
                            // la capa baja, encima de la fórmula del prototipo: pradera más hierba, claro más flores, matorral menos hierba (y pajiza)
                            // y más piedra, bosque cerrado poca hierba, pedregal ×4 de piedra
                            switch (estadoM)
                            {
                                case EstadoDeCelda.Pradera: pr[(int)TipoDeMata.Hierba] *= 1.3; break;
                                case EstadoDeCelda.Claro: pr[(int)TipoDeMata.Flor] *= 1.5; break;
                                case EstadoDeCelda.Matorral: pr[(int)TipoDeMata.Hierba] *= 0.6; pr[(int)TipoDeMata.Piedra] *= 1.5; break;
                                case EstadoDeCelda.Cerrado: pr[(int)TipoDeMata.Hierba] *= 0.3; break;
                                case EstadoDeCelda.Pedregal: pr[(int)TipoDeMata.Piedra] *= 4; break;
                            }
                            double suma = 0;
                            foreach (var pv in pr) suma += pv;
                            if (suma > 1) for (int t = 0; t < pr.Length; t++) pr[t] /= suma;
                        }
                        // La hierba y las piedras, en corros con calvas (no un matojo por celda, todos igual de separados): una mancha de unas 3 u
                        // (2 u la piedra) multiplica su probabilidad entre ×0,3 y ×1,6. En la arena de la costa, casi nada de hierba (×0,15: barrón
                        // de duna, en corros). Ninguna piedra donde el suelo ya es roca viva (pendiente de más de 1 o la roca de la altura); se
                        // juntan al pie de las laderas (canchal).
                        pr[(int)TipoDeMata.Hierba] *= (0.3 + 1.3 * ReglasDeVegetacion.Suave(0.3, 0.7, ReglasDeVegetacion.Manchas(x, y, 3, 709))) * (alt < costa ? 0.15 : 1);
                        pr[(int)TipoDeMata.Piedra] *= pend > 1.0 ? 0
                            : (0.2 + 1.6 * ReglasDeVegetacion.Suave(0.3, 0.7, ReglasDeVegetacion.Manchas(x, y, 2, 719)))
                              * (1 + 2 * ReglasDeVegetacion.Suave(0.3, 0.6, pend) * (1 - ReglasDeVegetacion.Suave(0.85, 1.0, pend)))
                              * (1 - ReglasDeVegetacion.Suave(montana + 20, umbrales.w, alt));
                        double u = ReglasDeVegetacion.Azar(gi, gj, 11);
                        int tipo = -1;
                        for (int t = 0; t < pr.Length; t++) if ((u -= pr[t]) < 0) { tipo = t; break; }
                        double r = ReglasDeVegetacion.Azar(gi, gj, 13), q = ReglasDeVegetacion.Azar(gi, gj, 29);
                        byte clase = Lentisco;
                        int iMata = iLentisco;
                        if (tipo == (int)TipoDeMata.Junco)
                        {
                            bool canaveral = estadoM == EstadoDeCelda.Canaveral;
                            if (iCanas < 0 || (!canaveral && q >= 0.5)) continue;
                            plantas.Add(new Planta
                            {
                                Clase = Canas,
                                Esp = (byte)iCanas,
                                // la variante 3 es la caña alta (3,5 m): en el cañaveral, la mitad; fuera, una de cada seis
                                Var = (byte)(ReglasDeVegetacion.Azar(gi, gj, 31) < (canaveral ? 0.5 : 0.16) ? 2 : (int)(r * 2)),
                                Mata = true,
                                Pos = EscalaDelMapa.AEscena(x, y, alt - 0.2f),
                                Suelo = Suelo(c),
                                Giro = (float)(ReglasDeVegetacion.Azar(gi, gj, 33) * 360),
                                Escala = (float)(1.2 + 0.6 * r), // algo mayores que de verdad: en el mapa, una mata de cañas de 2 m no se lee
                                Alto = 1,
                                Azar = (float)ReglasDeVegetacion.Azar(gi, gj, 3),
                            });
                            continue;
                        }
                        if (tipo == (int)TipoDeMata.Hierba || tipo == (int)TipoDeMata.Flor || tipo == (int)TipoDeMata.Piedra)
                        {
                            if (!CapaBaja) continue;
                            // (el tinte de la hierba, del suelo de debajo, y el color de la flor, de su mancha de 9 u: los pone el sombreado)
                            bool piedra = tipo == (int)TipoDeMata.Piedra;
                            int iCapa = tipo == (int)TipoDeMata.Hierba ? iHierba : tipo == (int)TipoDeMata.Flor ? iFlor : iPiedra;
                            byte claseCapa = tipo == (int)TipoDeMata.Hierba ? Hierba : tipo == (int)TipoDeMata.Flor ? Flor : Piedra;
                            double rr = ReglasDeVegetacion.Azar(gi, gj, 45);
                            // las piedras, de ×0,5 a ×2,2 (más pequeñas que grandes); en el pedregal, alguna grande (hasta 0,35 u)
                            double esc = TamanoDelPrototipo[tipo] * e * EscalaCapa * (piedra ? 0.5 + 1.7 * rr * rr : 0.7 + 0.6 * r);
                            if (piedra && estadoM == EstadoDeCelda.Pedregal && ReglasDeVegetacion.Azar(gi, gj, 37) < 0.12) esc = e * (0.18 + 0.17 * r);
                            if (iCapa < 0) continue;
                            var sc = especies[iCapa];
                            plantas.Add(new Planta
                            {
                                Clase = claseCapa,
                                Esp = (byte)iCapa,
                                Var = (byte)Math.Min(sc.Variantes - 1, (int)(ReglasDeVegetacion.Azar(gi, gj, 41) * sc.Variantes)),
                                Mata = true,
                                Pos = EscalaDelMapa.AEscena(x, y, alt - (piedra ? 0.15f : 0.05f) * (float)esc), // (la piedra, medio enterrada: un 30 %)
                                Suelo = Suelo(c),
                                Giro = (float)(ReglasDeVegetacion.Azar(gi, gj, 43) * 360),
                                Escala = (float)esc,
                                Alto = 1,
                                Azar = (float)ReglasDeVegetacion.Azar(gi, gj, 3),
                            });
                            continue;
                        }
                        if (tipo != (int)TipoDeMata.Mata) continue;
                        bool matorral = estadoM == EstadoDeCelda.Matorral;
                        // la adelfa, en la orilla de ríos y ramblas (no en la playa: por los cauces y sobre la línea de costa)
                        if (iAdelfa >= 0 && ACauce(x, y) < 2.5 && alt > costa && alt < colina && q < 0.6) { clase = Adelfa; iMata = iAdelfa; }
                        else if (matorral)
                        {
                            // el matorral, mezclado: retama la mitad, lentisco un 35 % y el resto hueco (para las piedras)
                            if (q >= 0.85) continue;
                            if (iRetama >= 0 && q < 0.5 && alt < montana) { clase = Retama; iMata = iRetama; }
                        }
                        else if (iRetama >= 0 && alt < montana && (estadoM == EstadoDeCelda.Linde ? q < 0.25 : estructura == null && Fertil(x, y) < 0.35 && q < 0.4))
                        { clase = Retama; iMata = iRetama; }
                        var s = especies[iMata];
                        void Mata(double mx, double my, float malt, int mc, double escala, int semilla)
                        {
                            plantas.Add(new Planta
                            {
                                Clase = clase,
                                Esp = (byte)iMata,
                                Var = (byte)Math.Min(s.Variantes - 1, (int)(ReglasDeVegetacion.Azar(gi, gj, semilla) * s.Variantes)),
                                Mata = true,
                                Pos = EscalaDelMapa.AEscena(mx, my, malt - 0.2f),
                                Suelo = Suelo(mc),
                                Giro = (float)(ReglasDeVegetacion.Azar(gi, gj, semilla + 1) * 360),
                                Escala = (float)(escala * EscalaMatas),
                                Alto = 1,
                                Azar = (float)ReglasDeVegetacion.Azar(gi, gj, 3),
                            });
                        }
                        if (!matorral) { Mata(x, y, alt, c, 0.8 + 0.4 * r, 13); continue; }
                        // En el matorral, en corros: un punto madre (uno de cada 2,5) y de 4 a 9 hijas a menos de 0,6 u, de 0,6 a 1,6 de tamaño,
                        // con suelo entre corros.
                        if (ReglasDeVegetacion.Azar(gi, gj, 17) >= 0.4) continue;
                        int hijas = 4 + (int)(ReglasDeVegetacion.Azar(gi, gj, 19) * 6);
                        Mata(x, y, alt, c, 0.6 + 1.0 * r, 13);
                        for (int h = 0; h < hijas; h++)
                        {
                            double a = ReglasDeVegetacion.Azar(gi, gj, 23 + 2 * h) * 6.2832, d = 0.15 + 0.45 * ReglasDeVegetacion.Azar(gi, gj, 24 + 2 * h);
                            double hx = x + Math.Cos(a) * d, hy = y + Math.Sin(a) * d;
                            int hc = Muestra(hx, hy, out var halt);
                            if (Agua(hc) || EnClaro(hx, hy) || ACauce(hx, hy) < 0.1) continue;
                            Mata(hx, hy, halt, hc, 0.6 + 1.0 * ReglasDeVegetacion.Azar(gi, gj, 60 + h), 40 + 2 * h);
                        }
                    }
            }
            return plantas;
        }

        /// <summary>El mosaico de la tesela: los datos de cada celda de 2 u (de las muestras y los rasters) y de cada rodal de 8 u, y el WFC.
        /// Rellena `info` (milisegundos, intentos, si cayó al prototipo y, con `VerMosaico`, la malla de colores).</summary>
        (EstadoDeCelda[] estructura, int[] rodales) Resolver(int tx, int ty, float lado, Muestras m, List<(double x, double y, double r)> claros, InfoMosaico info)
        {
            var reloj = System.Diagnostics.Stopwatch.StartNew();
            var e = EscalaDelMapa.MetrosPorUnidad;
            double costa = umbrales.x, colina = umbrales.y, montana = umbrales.z;
            var aAgua = ReglasDeVegetacion.DistanciaAlAgua(m.Agua, m.N, m.N);
            bool rioCerca = Cauces != null && Cauces.HayRioCerca(tx * lado, ty * lado, (tx + 1) * lado, (ty + 1) * lado);
            int K(double x, double y) => Math.Min(m.N - 1, Math.Max(0, (int)Math.Round((y - m.Y0) / PasoMuestras))) * m.N
                                         + Math.Min(m.N - 1, Math.Max(0, (int)Math.Round((x - m.X0) / PasoMuestras)));
            bool AguaEn(double x, double y) => m.Agua[K(x, y)] || (rioCerca && Cauces.ACauce(x, y) < 0.2);
            int n = (int)Math.Round(lado / MosaicoDeVegetacion.Celda);
            var datos = new DatosDeCelda[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    double x = tx * lado + (i + 0.5) * MosaicoDeVegetacion.Celda, y = ty * lado + (j + 0.5) * MosaicoDeVegetacion.Celda;
                    int c = K(x, y);
                    int c1 = Math.Min(c + 1, m.H.Length - 1), cn = Math.Min(c + m.N, m.H.Length - 1);
                    float pend = (float)(Math.Sqrt((m.H[c1] - m.H[c]) * (m.H[c1] - m.H[c]) + (m.H[cn] - m.H[c]) * (m.H[cn] - m.H[c])) / (PasoMuestras * e));
                    bool claro = false;
                    foreach (var q in claros) if ((x - q.x) * (x - q.x) + (y - q.y) * (y - q.y) < q.r * q.r) { claro = true; break; }
                    double ac = rioCerca ? Cauces.ACauce(x, y) : double.PositiveInfinity;
                    datos[j * n + i] = new DatosDeCelda
                    {
                        X = x, Y = y,
                        Densidad = bosque == null ? 0 : ReglasDeVegetacion.RasterEn(bosque.Columnas, bosque.Filas, bosque.Celda, bosque.Datos, x, y),
                        Fertilidad = fertilidad == null ? 0.3 : ReglasDeVegetacion.RasterEn(fertilidad.Columnas, fertilidad.Filas, fertilidad.Celda, fertilidad.Datos, x, y),
                        Pendiente = pend,
                        DAgua = Math.Min(aAgua[c] * PasoMuestras, ac),
                        Altura = m.H[c],
                        Vacia = AguaEn(x, y) || claro,
                        AguaAlLado = !AguaEn(x, y) && (AguaEn(x + 2, y) || AguaEn(x - 2, y) || AguaEn(x, y + 2) || AguaEn(x, y - 2)),
                    };
                }
            int semilla = unchecked(semillaMundo * 31 + tx * 7349 + ty * 9973);
            var estructura = MosaicoDeVegetacion.Estructura(datos, n, semilla, colina, tabla, out var intentos);
            int nR = (int)Math.Round(lado / MosaicoDeVegetacion.CeldaRodal);
            var zonas = new ZonaDeVegetacion[nR * nR];
            var bordes = new EspecieDeArbol[nR * nR];
            for (int j = 0; j < nR; j++)
                for (int i = 0; i < nR; i++)
                {
                    double x = tx * lado + (i + 0.5) * MosaicoDeVegetacion.CeldaRodal, y = ty * lado + (j + 0.5) * MosaicoDeVegetacion.CeldaRodal;
                    int c = K(x, y);
                    double dAgua = Math.Min(aAgua[c] * PasoMuestras, rioCerca ? Cauces.ACauce(x, y) : double.PositiveInfinity);
                    zonas[j * nR + i] = ReglasDeVegetacion.ZonaDe(x, y, m.H[c], dAgua, costa, colina, montana);
                    bordes[j * nR + i] = ReglasDeVegetacion.DominanteDe(x, y, zonas[j * nR + i]);
                }
            var rodales = estructura == null ? null : MosaicoDeVegetacion.Rodales(zonas, bordes, nR, semilla ^ 0x5bd1e995, tabla, out var intentosRodales);
            info.Hecho = true;
            info.Caido = estructura == null;
            info.Intentos = intentos;
            info.Ms = reloj.Elapsed.TotalMilliseconds;
            if (VerMosaico && estructura != null)
            {
                var v = new List<Vector3>(n * n * 4);
                var col = new List<Color>(n * n * 4);
                for (int j = 0; j < n; j++)
                    for (int i = 0; i < n; i++)
                    {
                        var d = datos[j * n + i];
                        float h = (float)d.Altura + 1.5f;
                        double x0 = tx * lado + i * MosaicoDeVegetacion.Celda, y0 = ty * lado + j * MosaicoDeVegetacion.Celda, w = MosaicoDeVegetacion.Celda * 0.92;
                        v.Add(EscalaDelMapa.AEscena(x0, y0, h)); v.Add(EscalaDelMapa.AEscena(x0, y0 + w, h));
                        v.Add(EscalaDelMapa.AEscena(x0 + w, y0 + w, h)); v.Add(EscalaDelMapa.AEscena(x0 + w, y0, h));
                        var cc = ColorDeEstado(estructura[j * n + i]).linear;
                        for (int q = 0; q < 4; q++) col.Add(cc);
                    }
                info.Vertices = v.ToArray();
                info.Colores = col.ToArray();
            }
            return (estructura, rodales);
        }

        public static Color ColorDeEstado(EstadoDeCelda e)
        {
            switch (e)
            {
                case EstadoDeCelda.Cerrado: return new Color32(20, 70, 25, 255);
                case EstadoDeCelda.Abierto: return new Color32(70, 140, 60, 255);
                case EstadoDeCelda.Linde: return new Color32(200, 190, 60, 255);
                case EstadoDeCelda.Claro: return new Color32(240, 120, 200, 255);
                case EstadoDeCelda.Matorral: return new Color32(140, 90, 40, 255);
                case EstadoDeCelda.Pradera: return new Color32(170, 220, 120, 255);
                case EstadoDeCelda.Ribera: return new Color32(40, 170, 170, 255);
                case EstadoDeCelda.Canaveral: return new Color32(60, 90, 220, 255);
                case EstadoDeCelda.Pedregal: return new Color32(150, 150, 150, 255);
                default: return new Color32(0, 0, 0, 255);
            }
        }

        /// <summary>Giro en y, escala uniforme y traslación, sin pasar por la API de Unity (va en el hilo).</summary>
        static Matrix4x4 Arbol(Vector3 p, float giroGrados, float s, float alto)
        {
            float a = giroGrados * Mathf.Deg2Rad, c = (float)Math.Cos(a) * s, n = (float)Math.Sin(a) * s;
            var m = new Matrix4x4();
            m.m00 = c; m.m01 = 0; m.m02 = n; m.m03 = p.x;
            m.m10 = 0; m.m11 = s * alto; m.m12 = 0; m.m13 = p.y;
            m.m20 = -n; m.m21 = 0; m.m22 = c; m.m23 = p.z;
            m.m33 = 1;
            return m;
        }

        /// <summary>La sombra: el cuadro tumbado sobre el suelo (normal `n`), alargado `largo` hacia `hacia` y `ancho` de través.</summary>
        static Matrix4x4 Sombra(Vector3 p, Vector3 n, Vector3 hacia, float desplazar, float ancho, float largo)
        {
            var f = (hacia - n * Vector3.Dot(hacia, n)).normalized;
            var r = Vector3.Cross(n, f).normalized;
            p += f * desplazar;
            var m = new Matrix4x4();
            m.m00 = r.x * ancho; m.m01 = n.x; m.m02 = f.x * largo; m.m03 = p.x;
            m.m10 = r.y * ancho; m.m11 = n.y; m.m12 = f.y * largo; m.m13 = p.y;
            m.m20 = r.z * ancho; m.m21 = n.z; m.m22 = f.z * largo; m.m23 = p.z;
            m.m33 = 1;
            return m;
        }

        List<TrozoDeDibujo> Trozos((int tx, int ty) t, List<Planta> plantas, float lado)
        {
            var e = EscalaDelMapa.MetrosPorUnidad;
            int porLado = Mathf.CeilToInt(lado / Trozo), claves = especies.Length * 3;
            var trozos = new TrozoDeDibujo[porLado * porLado];
            var haciaFuera = new Vector3(-PinturaDelMapa.Sol.x, 0, -PinturaDelMapa.Sol.z).normalized;
            // el largo de la mancha, con la elevación del sol (calibrada para el del prototipo, unos 56°)
            float largoSol = (PinturaDelMapa.SolDelPrototipo.y / new Vector2(PinturaDelMapa.SolDelPrototipo.x, PinturaDelMapa.SolDelPrototipo.z).magnitude)
                / (PinturaDelMapa.Sol.y / new Vector2(PinturaDelMapa.Sol.x, PinturaDelMapa.Sol.z).magnitude);
            float tamano = Tamano;
            List<Matrix4x4>[][] Bandas(int n)
            {
                var r = new List<Matrix4x4>[n][];
                for (int b = 0; b < n; b++) { r[b] = new List<Matrix4x4>[claves]; for (int c = 0; c < claves; c++) r[b][c] = new List<Matrix4x4>(); }
                return r;
            }
            foreach (var p in plantas)
            {
                double xu = p.Pos.x / e, yu = -p.Pos.z / e;
                int i = Mathf.Clamp((int)((xu - t.tx * lado) / Trozo), 0, porLado - 1), j = Mathf.Clamp((int)((yu - t.ty * lado) / Trozo), 0, porLado - 1);
                var tr = trozos[j * porLado + i];
                if (tr == null)
                {
                    tr = trozos[j * porLado + i] = new TrozoDeDibujo
                    {
                        CentroU = new Vector2((float)(t.tx * lado + (i + 0.5f) * Trozo), (float)(t.ty * lado + (j + 0.5f) * Trozo)),
                        Caja = new Bounds(p.Pos, Vector3.zero),
                        Arboles = Bandas(ReglasDeVegetacion.BandasParte.Length),
                        Matas = Bandas(ReglasDeVegetacion.BandasMatasParte.Length),
                        Capa = Bandas(ReglasDeVegetacion.BandasMatasParte.Length),
                        Sombras = new List<Matrix4x4>[ReglasDeVegetacion.BandasParte.Length],
                    };
                    for (int b = 0; b < tr.Sombras.Length; b++) tr.Sombras[b] = new List<Matrix4x4>();
                }
                var s = especies[p.Esp];
                if (p.Mata)
                {
                    for (int b = 0; b < ReglasDeVegetacion.BandasMatasParte.Length; b++)
                    {
                        if (p.Azar >= ReglasDeVegetacion.BandasMatasParte[b]) continue;
                        (p.Clase >= Hierba ? tr.Capa : tr.Matas)[b][p.Esp * 3 + p.Var]
                            .Add(Arbol(p.Pos, p.Giro, p.Escala * (float)ReglasDeVegetacion.BandasMatasCrece[b], 1));
                    }
                }
                else
                {
                    for (int b = 0; b < ReglasDeVegetacion.BandasParte.Length; b++)
                    {
                        double parte = ReglasDeVegetacion.BandasParte[b];
                        if (p.Azar >= parte) continue; // los que quedan son siempre los mismos
                        float esc = tamano * p.Escala * (float)(1 / Math.Sqrt(parte));
                        tr.Arboles[b][p.Esp * 3 + p.Var].Add(Arbol(p.Pos, p.Giro, esc, p.Alto));
                        float r = s.RadioCopa * esc;
                        // Sombra arrojada falsa: grande, blanda, alargada hacia el lado contrario del sol y tumbada sobre la pendiente; en el bosque
                        // cerrado, más grande, para que se fundan y el suelo entre troncos quede en sombra.
                        float funde = p.Hondura == 2 ? 1.35f : 1;
                        tr.Sombras[b].Add(Sombra(p.Pos + p.Suelo * (Hundir + 0.5f), p.Suelo, haciaFuera, r * 0.8f * largoSol, r * 2.5f * funde, r * 3.6f * funde * largoSol));
                    }
                }
                float alto = 12 * tamano * p.Escala * 3;
                tr.Caja.Encapsulate(new Bounds(p.Pos + Vector3.up * alto * 0.5f, new Vector3(s.RadioCopa * 6 * tamano, alto, s.RadioCopa * 6 * tamano)));
            }
            var lista = new List<TrozoDeDibujo>();
            int[][] Con(List<Matrix4x4>[][] bandas) => Array.ConvertAll(bandas, l => Enumerable.Range(0, l.Length).Where(c => l[c].Count > 0).ToArray());
            foreach (var tr in trozos)
                if (tr != null)
                {
                    (tr.ConArboles, tr.ConMatas, tr.ConCapa) = (Con(tr.Arboles), Con(tr.Matas), Con(tr.Capa));
                    lista.Add(tr);
                }
            return lista;
        }

        // ── el dibujo ──

        static readonly int Aclarar = Shader.PropertyToID("_Aclarar"), AclararCuantos = Shader.PropertyToID("_AclararCuantos");
        readonly Vector4[] aclarar = new Vector4[8];

        /// <summary>Las copas se aclaran alrededor del héroe y de las columnas que se ven (hasta 8, en la escena, con su radio en metros) y entre
        /// ellas y la cámara, para que el bosque cerrado no las tape; los troncos se quedan.</summary>
        public void Aclarado(List<Vector4> puntos)
        {
            int n = Math.Min(8, puntos.Count);
            for (int i = 0; i < 8; i++) aclarar[i] = i < n ? puntos[i] : Vector4.zero;
            Shader.SetGlobalVectorArray(Aclarar, aclarar);
            Shader.SetGlobalFloat(AclararCuantos, n);
        }

        /// <summary>Dibuja lo repartido para la cámara `camara`, con el héroe en (x, y) u.</summary>
        public void Dibujar(Camera camara, double x, double y)
        {
            var total = Stopwatch.StartNew();
            DibujarSinMedir(camara, x, y);
            PeorMsDibujar = Math.Max(PeorMsDibujar, total.Elapsed.TotalMilliseconds);
            MsDibujar = MsDibujar * 0.95 + total.Elapsed.TotalMilliseconds * 0.05;
        }

        void DibujarSinMedir(Camera camara, double x, double y)
        {
            for (int i = 0; i < lote.GetLength(0); i++) for (int n = 0; n < 3; n++) lote[i, n].Clear();
            lotesSombras.Clear();
            GeometryUtility.CalculateFrustumPlanes(camara, planos);
            var ojo = camara.transform.position;
            foreach (var t in teselas.Values)
                foreach (var tr in t.trozos)
                {
                    if (!GeometryUtility.TestPlanesAABB(planos, tr.Caja)) continue;
                    double dh = Math.Sqrt((tr.CentroU.x - x) * (tr.CentroU.x - x) + (tr.CentroU.y - y) * (tr.CentroU.y - y)) - Trozo * 0.7;
                    int banda = 0, bandaMatas = 0;
                    while (dh >= ReglasDeVegetacion.BandasHasta[banda]) banda++;
                    while (dh >= ReglasDeVegetacion.BandasMatasHasta[bandaMatas]) bandaMatas++;
                    // el nivel, por la distancia al centro del trozo (al punto más cercano de su caja, con copas de hasta 100 m, el nivel de cerca
                    // se colaba a zoom habitual: 1,1 millones de triángulos en el bosque denso)
                    float dc = Vector3.Distance(tr.Caja.center, ojo);
                    int nivel = dc < Cerca ? 0 : dc < Medio ? 1 : 2;
                    foreach (int c in tr.ConArboles[banda]) lote[c, nivel].AddRange(tr.Arboles[banda][c]);
                    foreach (int c in tr.ConMatas[bandaMatas]) lote[c, nivel].AddRange(tr.Matas[bandaMatas][c]);
                    if (dh < CapaHasta) foreach (int c in tr.ConCapa[bandaMatas]) lote[c, nivel].AddRange(tr.Capa[bandaMatas][c]);
                    if (nivel < 2) lotesSombras.AddRange(tr.Sombras[banda]);
                }

            Dibujadas = 0;
            Triangulos = 0;
            Llamadas = 0;
            LlamadasQueArrojan = 0;
            for (int esp = 0; esp < especies.Length; esp++)
            {
                var s = especies[esp];
                var mats = materialesPorMalla[s.Nombre];
                // Quién arroja sombra: la capa baja (hierba, flores, piedras) nunca; las matas, solo de cerca (la primera cascada); los árboles,
                // de cerca y a media distancia, siempre con la misma malla que se dibuja (una que sobresalga, como la cúpula de lejos, haría
                // sombra propia falsa y cuñas angulosas en las copas vecinas); de lejos, que queda más allá de la distancia de sombra, nada.
                bool capa = s.Nombre == "hierba" || s.Nombre == "flor" || s.Nombre == "piedra";
                bool mata = s.Nombre == "lentisco" || s.Nombre == "retama" || s.Nombre == "adelfa" || s.Nombre == "canas";
                for (int v = 0; v < s.Variantes; v++)
                    for (int n = 0; n < 3; n++)
                    {
                        var lista = lote[esp * 3 + v, n];
                        var malla = s.Mallas[v, n];
                        if (lista.Count == 0 || malla == null) continue;
                        Dibujadas += lista.Count;
                        var arroja = PinturaDelMapa.SombrasReales && !capa && (n == 0 || (n == 1 && !mata)) ? ShadowCastingMode.On : ShadowCastingMode.Off;
                        var ms = mats[v, n];
                        for (int sub = 0; sub < Mathf.Min(malla.subMeshCount, ms.Length); sub++)
                        {
                            var rp = new RenderParams(ms[sub]) { shadowCastingMode = arroja, receiveShadows = true };
                            for (int desde = 0; desde < lista.Count; desde += 1023)
                            {
                                Graphics.RenderMeshInstanced(rp, malla, sub, lista, Mathf.Min(1023, lista.Count - desde), desde);
                                Llamadas++;
                                if (arroja == ShadowCastingMode.On) LlamadasQueArrojan++;
                            }
                            Triangulos += (long)malla.GetIndexCount(sub) / 3 * lista.Count;
                        }
                    }
            }
            if (VerMosaico)
                foreach (var kv in mallasMosaico)
                    if (teselas.ContainsKey(kv.Key))
                    {
                        Graphics.RenderMesh(new RenderParams(materialMosaico), kv.Value, 0, Matrix4x4.identity);
                        Llamadas++;
                    }
            if (lotesSombras.Count > 0)
            {
                var rp = new RenderParams(materialSombra) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
                for (int desde = 0; desde < lotesSombras.Count; desde += 1023)
                {
                    Graphics.RenderMeshInstanced(rp, cuadro, 0, lotesSombras, Mathf.Min(1023, lotesSombras.Count - desde), desde);
                    Llamadas++;
                }
                Triangulos += 2L * lotesSombras.Count;
            }
        }

        public void Destruir()
        {
            teselas.Clear();
            foreach (var m in materiales) UnityEngine.Object.Destroy(m);
            if (materialSombra != null) UnityEngine.Object.Destroy(materialSombra);
            if (materialMosaico != null) UnityEngine.Object.Destroy(materialMosaico);
            foreach (var m in mallasMosaico.Values) UnityEngine.Object.Destroy(m);
            if (cuadro != null) UnityEngine.Object.Destroy(cuadro);
        }
    }
}

// El suelo del mapa de mundo por teselas de 64 u alrededor del héroe (BronzeAgeClient: `src/mundo3d/terreno.ts`): una malla por tesela con la
// altura del relieve y, donde hay agua, una lámina transparente aparte (`Conquest/MapaAgua`) al nivel del mar (el umbral del generador de
// `/v1/datos`) o del lago (ya viene plano), con el fondo bajando desde la orilla y la tierra de junto al agua bajando hacia ella. Lo pinta `Conquest/MapaSuelo` con las texturas de `Resources/MapaMundo/Suelo` según la altura, la
// pendiente y los datos del motor (fertilidad, humedad y bosque, que van al sombreado como una textura de una celda por texel); el agua y la
// capa de fertilidad van con su color plano en el color de vértice (alfa 0). Se cargan las más cercanas primero, dos por fotograma, con paso de
// 0,5 u cerca, 1 u a media distancia y 2 u lejos; las que se alejan se sueltan. Sin juntas: la normal sale de las alturas con el anillo del
// vecino (la misma a los dos lados del borde) y cada tesela lleva un faldón que tapa la grieta con una vecina de otro paso.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ConquestTactics.Mundo.Mapa
{
    public sealed class TerrenoDelMapa
    {
        const int PorFotograma = 2;

        readonly Relieve relieve;
        readonly RasterDeMundo fertilidad;
        bool conFertilidad;
        /// <summary>Los cauces de los ríos: el suelo baja hasta ellos (si no, el agua del río queda enterrada).</summary>
        public CaucesDelMapa Cauces;
        readonly float nivelDelMar;
        readonly Material material, materialAgua;
        readonly Texture2D datos;
        readonly Transform padre;
        readonly Dictionary<(int, int), (GameObject go, float paso)> teselas = new Dictionary<(int, int), (GameObject, float)>();
        readonly List<(int tx, int ty, float d)> pendientes = new List<(int, int, float)>();
        readonly System.Diagnostics.Stopwatch reloj = new System.Diagnostics.Stopwatch();

        public int Cargadas => teselas.Count;
        /// <summary>Teselas construidas desde el principio y el tiempo que ha llevado (ms), para medir.</summary>
        public int Construidas { get; private set; }
        public double MsConstruyendo => reloj.Elapsed.TotalMilliseconds;
        /// <summary>La tesela que más ha tardado en construirse (ms).</summary>
        public double PeorMsTesela { get; private set; }

        /// <summary>`nivelDelMar`: metros a los que va la lámina del mar (el umbral de agua del generador). `umbrales`: metros de la costa, la colina,
        /// la montaña y la cima (los del generador).</summary>
        public TerrenoDelMapa(Relieve relieve, RasterDeMundo fertilidad, RasterDeMundo humedad, RasterDeMundo bosque, float nivelDelMar, Vector4 umbrales, Transform padre)
        {
            this.relieve = relieve;
            this.fertilidad = fertilidad;
            this.nivelDelMar = nivelDelMar;
            this.padre = padre;
            material = new Material(Resources.Load<Shader>("MapaMundo/MapaSuelo")) { name = "suelo del mapa" };
            materialAgua = RiosDelMapa.MaterialDeAgua(RiosDelMapa.ModoAgua.Lamina);
            material.SetTexture("_SueloColor", Resources.Load<Texture2DArray>("MapaMundo/Suelo/suelo_color"));
            material.SetTexture("_SueloNormal", Resources.Load<Texture2DArray>("MapaMundo/Suelo/suelo_normal"));
            material.SetTexture("_SueloMascara", Resources.Load<Texture2DArray>("MapaMundo/Suelo/suelo_mascara"));
            material.SetTexture("_SueloRuido", Resources.Load<Texture2D>("MapaMundo/Suelo/suelo_ruido"));
            material.SetVector("_Umbrales", umbrales);
            // La vegetación mira lo mismo que pinta el suelo (`MapaComun.hlsl`, `SueloBajo`): la hierba toma el tinte del suelo de debajo y la
            // hondura del dosel sale del bosque.
            Shader.SetGlobalTexture("_VegRuido", material.GetTexture("_SueloRuido"));
            Shader.SetGlobalVector("_VegUmbrales", umbrales);
            Shader.SetGlobalVector("_VegVerdor", material.GetVector("_Verdor"));
            var nieve = material.GetVector("_Nieve");
            nieve.x = umbrales.w + PinturaDelMapa.NieveSobreCima * ((float)Relieve.MetrosAltura - umbrales.w);
            material.SetVector("_Nieve", nieve);

            // Los datos del motor, una celda por texel: R fertilidad, G humedad, B bosque (sin dato, 0). La fila 0 es el norte, como en el raster.
            var r = fertilidad ?? humedad ?? bosque;
            if (r != null)
            {
                datos = new Texture2D(r.Columnas, r.Filas, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "datos del suelo" };
                var px = new byte[r.Columnas * r.Filas * 4];
                for (int k = 0; k < r.Columnas * r.Filas; k++)
                {
                    px[4 * k] = fertilidad?.Datos[k] ?? 0;
                    px[4 * k + 1] = humedad?.Datos[k] ?? 0;
                    px[4 * k + 2] = bosque?.Datos[k] ?? 0;
                    px[4 * k + 3] = 255;
                }
                datos.SetPixelData(px, 0);
                datos.Apply(false, true);
                material.SetTexture("_SueloDatos", datos);
                var e = EscalaDelMapa.MetrosPorUnidad;
                material.SetVector("_DatosTamano", new Vector4((float)(r.Columnas * r.Celda * e), (float)(r.Filas * r.Celda * e), (float)(r.Celda * e), 0));
                Shader.SetGlobalTexture("_VegDatos", datos);
                Shader.SetGlobalVector("_VegDatosTamano", new Vector4((float)(r.Columnas * r.Celda * e), (float)(r.Filas * r.Celda * e), (float)(r.Celda * e), (float)e));
            }
        }

        /// <summary>La capa de fertilidad (botón de capas): el suelo se pinta con la rampa de fertilidad en vez de con las texturas. Rehace las teselas.</summary>
        public bool ConFertilidad
        {
            get => conFertilidad;
            set
            {
                if (value == conFertilidad) return;
                conFertilidad = value;
                foreach (var t in teselas.Values) Soltar(t.go);
                teselas.Clear();
            }
        }

        /// <summary>Lo que se ve en (x, y): la altura del suelo o, si hay agua, la de su lámina (metros).</summary>
        public float AlturaVisible(double x, double y)
        {
            var h = (float)relieve.AlturaEn(x, y);
            return relieve.AguaEn(x, y) ? Mathf.Max(h, nivelDelMar) : h;
        }

        /// <summary>Pone al día las teselas alrededor de (x, y): `alcance` u (lo que se ve desde la cámara, con su tope).</summary>
        public void Actualizar(double x, double y, float alcance)
        {
            float lado = (float)relieve.LadoEnUnidades;
            alcance = Mathf.Min(alcance, PinturaDelMapa.DistanciaCarga);
            // Se sueltan las que quedan fuera (con un margen, para no hacer y deshacer en el borde).
            var fuera = new List<(int, int)>();
            foreach (var kv in teselas)
                if (Distancia(kv.Key.Item1, kv.Key.Item2, x, y, lado) > alcance + lado) fuera.Add(kv.Key);
            foreach (var k in fuera) { Soltar(teselas[k].go); teselas.Remove(k); }

            pendientes.Clear();
            int t0x = Mathf.Max(0, (int)((x - alcance) / lado)), t1x = Mathf.Min(relieve.TeselasX - 1, (int)((x + alcance) / lado));
            int t0y = Mathf.Max(0, (int)((y - alcance) / lado)), t1y = Mathf.Min(relieve.TeselasY - 1, (int)((y + alcance) / lado));
            for (int ty = t0y; ty <= t1y; ty++)
                for (int tx = t0x; tx <= t1x; tx++)
                {
                    var d = Distancia(tx, ty, x, y, lado);
                    if (d > alcance) continue;
                    var paso = PinturaDelMapa.PasoSegunDistancia(d);
                    if (teselas.TryGetValue((tx, ty), out var t) && t.paso == paso) continue;
                    pendientes.Add((tx, ty, d));
                }
            pendientes.Sort((a, b) => a.d.CompareTo(b.d));
            for (int i = 0; i < Mathf.Min(PorFotograma, pendientes.Count); i++)
            {
                var (tx, ty, d) = pendientes[i];
                var paso = PinturaDelMapa.PasoSegunDistancia(d);
                if (teselas.TryGetValue((tx, ty), out var vieja)) Soltar(vieja.go);
                var antes = reloj.Elapsed.TotalMilliseconds;
                reloj.Start();
                teselas[(tx, ty)] = (Construir(tx, ty, paso, lado), paso);
                reloj.Stop();
                PeorMsTesela = System.Math.Max(PeorMsTesela, reloj.Elapsed.TotalMilliseconds - antes);
                Construidas++;
            }
        }

        /// <summary>Lo más alto de la malla de suelo ya pintada alrededor de (x, y): las cuatro esquinas de la celda de la tesela (metros); null si esa
        /// tesela aún no está. Para comprobar que lo que va encima del suelo (ríos, rayas) no queda enterrado.</summary>
        public float? AlturaDeMalla(double x, double y)
        {
            float lado = (float)relieve.LadoEnUnidades;
            int tx = (int)(x / lado), ty = (int)(y / lado);
            if (!teselas.TryGetValue((tx, ty), out var t) || t.go == null) return null;
            var vertices = t.go.GetComponent<MeshFilter>().sharedMesh.vertices;
            int n = Mathf.RoundToInt(lado / t.paso) + 1;
            int i = Mathf.Clamp((int)((x - tx * lado) / t.paso), 0, n - 2), j = Mathf.Clamp((int)((y - ty * lado) / t.paso), 0, n - 2);
            return Mathf.Max(Mathf.Max(vertices[j * n + i].y, vertices[j * n + i + 1].y), Mathf.Max(vertices[(j + 1) * n + i].y, vertices[(j + 1) * n + i + 1].y));
        }

        /// <summary>¿Quedan teselas por hacer en lo que se ve?</summary>
        public bool Completo => pendientes.Count <= PorFotograma;

        /// <summary>Distancia (u) del punto al rectángulo de la tesela.</summary>
        static float Distancia(int tx, int ty, double x, double y, float lado)
        {
            double dx = System.Math.Max(0, System.Math.Max(tx * lado - x, x - (tx + 1) * lado));
            double dy = System.Math.Max(0, System.Math.Max(ty * lado - y, y - (ty + 1) * lado));
            return (float)System.Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>El nivel de la lámina sobre una muestra de agua de altura `h` (metros): el mar a su nivel, cada lago a su altura plana, un
        /// metro por debajo (BronzeAgeClient: `nivelDelAgua`).</summary>
        float Nivel(float h) => Mathf.Max(nivelDelMar, h) - 1;

        /// <summary>El color de vértice del suelo: con texturas (alfa 1), datos para `MapaSuelo` (255 = nada): el rojo, lo hondo que queda bajo la
        /// lámina (0 a 2,6 u o más) para pintar el fondo; el verde, la arena mojada junto al agua; el azul, la ribera de un río. La capa de
        /// fertilidad, su color plano (alfa 0); bajo el agua, en esa capa, el color del agua.</summary>
        Color32 ColorEn(double x, double y, bool agua, float hondo, float mojado, float ribera)
        {
            if (conFertilidad && agua) return Plano(hondo > PinturaDelMapa.MarHondo ? PinturaDelMapa.AguaHonda : PinturaDelMapa.AguaSomera);
            if (conFertilidad && fertilidad != null) return Plano(ColorDeFertilidad(fertilidad.En(x, y) / 255f));
            byte Uno(float v) => (byte)Mathf.RoundToInt(255 * (1 - Mathf.Clamp01(v)));
            return new Color32(Uno(hondo / EscalaDelMapa.MetrosPorUnidad / PinturaDelMapa.AguaMaxima), Uno(mojado), Uno(ribera), 255);
        }

        // El proyecto va en espacio lineal: el color de vértice no se convierte solo.
        static Color32 Plano(Color c)
        {
            Color32 k = c.linear;
            k.a = 0;
            return k;
        }

        GameObject Construir(int tx, int ty, float paso, float lado)
        {
            var e = EscalaDelMapa.MetrosPorUnidad;
            // `ancho`: muestras del radio de la pendiente ancha (unos `RadioPendienteAncha` metros, al menos una). `M`: el margen de la ventana
            // de muestras alrededor de la tesela, lo que piden la pendiente ancha y la orilla (6 u).
            int n = Mathf.RoundToInt(lado / paso) + 1, ancho = Mathf.Max(1, Mathf.RoundToInt(PinturaDelMapa.RadioPendienteAncha / (paso * e)));
            int M = Mathf.Max(ancho + 1, Mathf.CeilToInt(PinturaDelMapa.AguaOrilla / paso)), W = n + 2 * M;
            double x0 = tx * lado, y0 = ty * lado;
            // La ventana: altura del relieve y bit de agua de cada muestra.
            var crudo = new float[W * W];
            var agua = new bool[W * W];
            bool hayAgua = false;
            for (int j = 0, k = 0; j < W; j++)
                for (int i = 0; i < W; i++, k++)
                {
                    double x = x0 + (i - M) * paso, y = y0 + (j - M) * paso;
                    crudo[k] = (float)relieve.AlturaEn(x, y);
                    agua[k] = relieve.AguaEn(x, y);
                    hayAgua |= agua[k];
                }
            // La orilla (BronzeAgeClient, `terreno.ts` y `agua.ts`): la máscara de agua emborronada (`b`, la costa es b = 0,5) y, en el agua, la
            // distancia a tierra. El fondo baja de 0 a 2,6 u en los primeros 6 u desde la orilla; la tierra de junto al agua baja hacia la lámina
            // con 0,8 u de playa, sin quedar a menos de 0,03 u sobre ella. Así la orilla es la línea donde el suelo corta la lámina, sin escalones.
            float[] mascara = hayAgua ? MascaraSuave(agua, W) : null, distancia = hayAgua ? DistanciaATierra(agua, W) : null;
            // En tierra, la distancia al agua más cercana (en muestras) y el nivel de su lámina.
            float[] alAgua = null, nivelAlAgua = null;
            if (hayAgua) (alAgua, nivelAlAgua) = DistanciaAlAgua(agua, crudo, W);
            var cauces = Cauces != null && Cauces.HayRioCerca(x0 - M * paso, y0 - M * paso, x0 + lado + M * paso, y0 + lado + M * paso) ? Cauces : null;
            var suelo = new float[W * W];
            var nivelCerca = new float[W * W];
            var ribera = new float[W * W];
            for (int j = 0, k = 0; j < W; j++)
                for (int i = 0; i < W; i++, k++)
                {
                    double x = x0 + (i - M) * paso, y = y0 + (j - M) * paso;
                    float h = crudo[k];
                    nivelCerca[k] = float.NaN;
                    if (hayAgua)
                    {
                        float b = mascara[k];
                        if (agua[k])
                        {
                            float nivel = Nivel(h), playa = nivel + (0.5f - b) * PinturaDelMapa.AguaPlaya * e;
                            float hondo = PinturaDelMapa.AguaMaxima * Mathf.SmoothStep(0, 1, Mathf.Clamp01(distancia[k] * paso / PinturaDelMapa.AguaOrilla));
                            suelo[k] = Mathf.Min(h, Mathf.Min(playa, nivel - hondo * e));
                            nivelCerca[k] = nivel;
                            continue;
                        }
                        // La tierra de junto al agua baja hacia la lámina con un techo continuo: 0,03 u sobre ella en la orilla, la playa de 0,8 u
                        // por la máscara y, más allá, una pendiente de 0,8 u por u de distancia al agua. (Antes solo bajaba donde la máscara era > 0,
                        // unas 2 muestras: la frontera seguía la escalera de la malla y, en un acantilado junto al agua, salían dientes.)
                        nivelCerca[k] = nivelAlAgua[k];
                        if (!float.IsNaN(nivelCerca[k]))
                        {
                            float d = alAgua[k] * paso;
                            float techo = nivelCerca[k] + e * (PinturaDelMapa.AguaSeco + PinturaDelMapa.AguaPlaya * Mathf.Max(0, 0.5f - b) + PinturaDelMapa.AguaPlaya * Mathf.Max(0, d - 1));
                            h = Mathf.Min(h, techo);
                        }
                    }
                    if (cauces != null && cauces.Consultar(x, y, paso, out ribera[k]) is float fondo) h = Mathf.Min(h, fondo);
                    suelo[k] = h;
                }
            // La pendiente ancha: la del relieve suavizado con una caja de ±`ancho` muestras. Va al sombreado (UV0) para decidir la roca de
            // pendiente y la nieve: una arista estrecha no llega a roca y el borde no sigue las diagonales de los triángulos.
            var suave = Caja(Caja(suelo, W, ancho, 1), W, ancho, W);
            // La malla y, detrás, el faldón: una tira que cuelga de cada borde y tapa la grieta con una vecina de otro paso.
            int borde = 4 * n;
            var vertices = new Vector3[n * n + borde];
            var normales = new Vector3[n * n + borde];
            var colores = new Color32[n * n + borde];
            var anchas = new Vector3[n * n + borde];
            float dos = 2 * paso * e;
            for (int j = 0, k = 0; j < n; j++)
                for (int i = 0; i < n; i++, k++)
                {
                    int r = (j + M) * W + i + M;
                    // Relativo a la esquina de la tesela: así los números de la malla son pequeños. z = −y del mapa.
                    vertices[k] = new Vector3((float)(i * paso * e), suelo[r], (float)(-j * paso * e));
                    normales[k] = new Vector3(-(suelo[r + 1] - suelo[r - 1]) / dos, 1, (suelo[r + W] - suelo[r - W]) / dos).normalized;
                    // La arena mojada: la tierra de la orilla (máscara entre 0 y 0,5).
                    float mojado = hayAgua && !agua[r] && !float.IsNaN(nivelAlAgua[r]) ? 1 - Mathf.SmoothStep(0, 1, alAgua[r] * paso / 1.2f) : 0;
                    colores[k] = ColorEn(x0 + i * paso, y0 + j * paso, agua[r], agua[r] ? Mathf.Max(0, Nivel(crudo[r]) - suelo[r]) : 0, mojado, ribera[r]);
                    anchas[k] = new Vector3(-(suave[r + 1] - suave[r - 1]) / dos, 1, (suave[r + W] - suave[r - W]) / dos).normalized;
                }
            // El contorno, en cuatro lados de n vértices (las esquinas, repetidas).
            var contorno = new int[borde];
            for (int s = 0; s < n; s++)
            {
                contorno[s] = s;
                contorno[n + s] = s * n + n - 1;
                contorno[2 * n + s] = (n - 1) * n + (n - 1 - s);
                contorno[3 * n + s] = (n - 1 - s) * n;
            }
            float caida = paso * e + 2;
            for (int s = 0; s < borde; s++)
            {
                int v = contorno[s], f = n * n + s;
                vertices[f] = vertices[v] - new Vector3(0, caida, 0);
                normales[f] = normales[v];
                colores[f] = colores[v];
                anchas[f] = anchas[v];
            }
            var triangulos = new int[(n - 1) * (n - 1) * 6 + 4 * (n - 1) * 6];
            int t = 0;
            for (int j = 0; j < n - 1; j++)
                for (int i = 0; i < n - 1; i++)
                {
                    int a = j * n + i, b = a + 1, c = a + n, d = c + 1;
                    // z = −y: el orden de los vértices se da la vuelta para que la cara mire arriba.
                    triangulos[t++] = a; triangulos[t++] = b; triangulos[t++] = c;
                    triangulos[t++] = b; triangulos[t++] = d; triangulos[t++] = c;
                }
            for (int lado4 = 0; lado4 < 4; lado4++)
                for (int s = 0; s < n - 1; s++)
                {
                    int q = lado4 * n + s;
                    int a = contorno[q], b = contorno[q + 1], fa = n * n + q, fb = fa + 1;
                    triangulos[t++] = a; triangulos[t++] = b; triangulos[t++] = fa;
                    triangulos[t++] = b; triangulos[t++] = fb; triangulos[t++] = fa;
                }
            var malla = new Mesh { name = $"tesela {tx},{ty}", indexFormat = vertices.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            malla.SetVertices(vertices);
            malla.SetNormals(normales);
            malla.SetColors(colores);
            malla.SetUVs(0, anchas);
            malla.SetTriangles(triangulos, 0);
            malla.RecalculateBounds();
            var go = new GameObject(malla.name);
            go.transform.SetParent(padre, false);
            go.transform.localPosition = EscalaDelMapa.AEscena(x0, y0, 0);
            go.AddComponent<MeshFilter>().sharedMesh = malla;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            if (hayAgua) Lamina(go.transform, n, M, W, paso, agua, crudo, suelo, nivelCerca);
            return go;
        }

        /// <summary>La lámina de agua de la tesela (BronzeAgeClient: `laminaDeAgua`): toda celda con alguna esquina de agua. Las esquinas de agua van a
        /// su nivel; las de tierra, al de la muestra de agua más cercana (quedan bajo el suelo, que corta la lámina en la orilla). Cada vértice lleva
        /// en UV0.x lo hondo que queda el fondo (u), para el color, la transparencia, las olas y la espuma del sombreado.</summary>
        void Lamina(Transform tesela, int n, int M, int W, float paso, bool[] agua, float[] crudo, float[] suelo, float[] nivelCerca)
        {
            var e = EscalaDelMapa.MetrosPorUnidad;
            var indice = new int[n * n];
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangulos = new List<int>();
            for (int k = 0; k < indice.Length; k++) indice[k] = -1;
            int Vertice(int i, int j)
            {
                int k = j * n + i;
                if (indice[k] >= 0) return indice[k];
                int r = (j + M) * W + i + M;
                float nivel = agua[r] ? Nivel(crudo[r]) : nivelCerca[r];
                if (float.IsNaN(nivel)) nivel = NivelCerca(agua, crudo, W, i + M, j + M, 4);
                if (float.IsNaN(nivel)) nivel = suelo[r];
                indice[k] = vertices.Count;
                vertices.Add(new Vector3((float)(i * paso * e), nivel, (float)(-j * paso * e)));
                // UV0: x lo hondo (u); y, 1 si es un lago (la lámina por encima del mar), que el sombreado pinta más calmado.
                uvs.Add(new Vector2(Mathf.Max(0, nivel - suelo[r]) / e, nivel > nivelDelMar - 0.5f ? 1 : 0));
                return indice[k];
            }
            for (int j = 0; j < n - 1; j++)
                for (int i = 0; i < n - 1; i++)
                {
                    int r = (j + M) * W + i + M;
                    if (!(agua[r] || agua[r + 1] || agua[r + W] || agua[r + W + 1])) continue;
                    int a = Vertice(i, j), b = Vertice(i + 1, j), c = Vertice(i, j + 1), d = Vertice(i + 1, j + 1);
                    triangulos.Add(a); triangulos.Add(b); triangulos.Add(c);
                    triangulos.Add(b); triangulos.Add(d); triangulos.Add(c);
                }
            if (triangulos.Count == 0) return;
            var malla = new Mesh { name = "agua", indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            malla.SetVertices(vertices);
            var arriba = new Vector3[vertices.Count];
            for (int k = 0; k < arriba.Length; k++) arriba[k] = Vector3.up;
            malla.SetNormals(arriba);
            malla.SetUVs(0, uvs);
            malla.SetTriangles(triangulos, 0);
            malla.RecalculateBounds();
            var go = new GameObject("agua");
            go.transform.SetParent(tesela, false);
            go.AddComponent<MeshFilter>().sharedMesh = malla;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = materialAgua;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>El nivel de la lámina de la muestra de agua más cercana a (i, j) a menos de `radio` muestras (por anillos); NaN si no hay.</summary>
        float NivelCerca(bool[] agua, float[] crudo, int W, int i, int j, int radio)
        {
            for (int r = 1; r <= radio; r++)
                for (int dj = -r; dj <= r; dj++)
                    for (int di = -r; di <= r; di++)
                    {
                        if (Mathf.Max(Mathf.Abs(di), Mathf.Abs(dj)) != r) continue;
                        int a = i + di, c = j + dj;
                        if (a >= 0 && c >= 0 && a < W && c < W && agua[c * W + a]) return Nivel(crudo[c * W + a]);
                    }
            return float.NaN;
        }

        /// <summary>En cada muestra de tierra, la distancia (en muestras, por chaflán) al agua más cercana y el nivel de su lámina (NaN donde no hay
        /// agua en la ventana); en el agua, 0 y su nivel.</summary>
        (float[] distancia, float[] nivel) DistanciaAlAgua(bool[] agua, float[] crudo, int W)
        {
            const float Recto = 1, Diagonal = 1.41421356f;
            var d = new float[W * W];
            var n = new float[W * W];
            for (int k = 0; k < d.Length; k++) { d[k] = agua[k] ? 0 : 1e9f; n[k] = agua[k] ? Nivel(crudo[k]) : float.NaN; }
            void Mirar(int k, int q, float paso)
            {
                if (d[q] + paso < d[k]) { d[k] = d[q] + paso; n[k] = n[q]; }
            }
            for (int j = 0; j < W; j++)
                for (int i = 0; i < W; i++)
                {
                    int k = j * W + i;
                    if (i > 0) Mirar(k, k - 1, Recto);
                    if (j > 0) Mirar(k, k - W, Recto);
                    if (i > 0 && j > 0) Mirar(k, k - W - 1, Diagonal);
                    if (i < W - 1 && j > 0) Mirar(k, k - W + 1, Diagonal);
                }
            for (int j = W - 1; j >= 0; j--)
                for (int i = W - 1; i >= 0; i--)
                {
                    int k = j * W + i;
                    if (i < W - 1) Mirar(k, k + 1, Recto);
                    if (j < W - 1) Mirar(k, k + W, Recto);
                    if (i < W - 1 && j < W - 1) Mirar(k, k + W + 1, Diagonal);
                    if (i > 0 && j < W - 1) Mirar(k, k + W - 1, Diagonal);
                }
            return (d, n);
        }

        /// <summary>El bit de agua emborronado (dos pasadas de caja de 3 × 3; BronzeAgeClient: `mascaraSuave`): la costa queda donde vale 0,5.</summary>
        static float[] MascaraSuave(bool[] agua, int W)
        {
            var a = new float[W * W];
            for (int k = 0; k < a.Length; k++) a[k] = agua[k] ? 1 : 0;
            for (int pasada = 0; pasada < 2; pasada++) a = Caja(Caja(a, W, 1, 1), W, 1, W);
            return a;
        }

        /// <summary>En cada muestra de agua, la distancia (en muestras) a la tierra más cercana, con chaflán (pasos de 1 y √2, dos barridos;
        /// BronzeAgeClient: `distanciaATierra`). En tierra, 0.</summary>
        static float[] DistanciaATierra(bool[] agua, int W)
        {
            const float Recto = 1, Diagonal = 1.41421356f;
            var d = new float[W * W];
            for (int k = 0; k < d.Length; k++) d[k] = agua[k] ? 1e9f : 0;
            for (int j = 0; j < W; j++)
                for (int i = 0; i < W; i++)
                {
                    int k = j * W + i;
                    if (d[k] == 0) continue;
                    if (i > 0) d[k] = Mathf.Min(d[k], d[k - 1] + Recto);
                    if (j > 0) d[k] = Mathf.Min(d[k], d[k - W] + Recto);
                    if (i > 0 && j > 0) d[k] = Mathf.Min(d[k], d[k - W - 1] + Diagonal);
                    if (i < W - 1 && j > 0) d[k] = Mathf.Min(d[k], d[k - W + 1] + Diagonal);
                }
            for (int j = W - 1; j >= 0; j--)
                for (int i = W - 1; i >= 0; i--)
                {
                    int k = j * W + i;
                    if (d[k] == 0) continue;
                    if (i < W - 1) d[k] = Mathf.Min(d[k], d[k + 1] + Recto);
                    if (j < W - 1) d[k] = Mathf.Min(d[k], d[k + W] + Recto);
                    if (i < W - 1 && j < W - 1) d[k] = Mathf.Min(d[k], d[k + W + 1] + Diagonal);
                    if (i > 0 && j < W - 1) d[k] = Mathf.Min(d[k], d[k + W - 1] + Diagonal);
                }
            return d;
        }

        /// <summary>Media móvil de ±`r` muestras a lo largo de un eje (`paso` 1: filas; `m`: columnas) de una rejilla de `m` × `m`; en los
        /// bordes, lo que haya.</summary>
        static float[] Caja(float[] a, int m, int r, int paso)
        {
            var b = new float[a.Length];
            for (int fila = 0; fila < m; fila++)
                for (int c = 0; c < m; c++)
                {
                    int k = paso == 1 ? fila * m + c : c * m + fila, cuantos = 0;
                    float suma = 0;
                    for (int d = -r; d <= r; d++)
                    {
                        int q = c + d;
                        if (q < 0 || q >= m) continue;
                        suma += a[k + d * paso];
                        cuantos++;
                    }
                    b[k] = suma / cuantos;
                }
            return b;
        }

        /// <summary>La rampa de la capa de fertilidad (BronzeAgeClient: `mundo/capasDelMapa.ts`): bajo el mínimo, de rojizo a ocre; desde él, de
        /// amarillo a verde oscuro.</summary>
        public static Color ColorDeFertilidad(float f)
        {
            const float minimo = 0.3f;
            if (f < minimo) return Color.Lerp(new Color32(140, 66, 44, 255), new Color32(204, 132, 66, 255), f / minimo);
            var t = (f - minimo) / (1 - minimo);
            return t < 0.5f ? Color.Lerp(new Color32(232, 214, 104, 255), new Color32(118, 178, 66, 255), t * 2)
                : Color.Lerp(new Color32(118, 178, 66, 255), new Color32(30, 110, 50, 255), (t - 0.5f) * 2);
        }

        public void Destruir()
        {
            foreach (var t in teselas.Values) Soltar(t.go);
            teselas.Clear();
            Object.Destroy(material);
            Object.Destroy(materialAgua);
            if (datos != null) Object.Destroy(datos);
        }

        static void Soltar(GameObject go)
        {
            if (go == null) return;
            foreach (var f in go.GetComponentsInChildren<MeshFilter>()) Object.Destroy(f.sharedMesh); // el suelo y su lámina de agua
            Object.Destroy(go);
        }
    }
}

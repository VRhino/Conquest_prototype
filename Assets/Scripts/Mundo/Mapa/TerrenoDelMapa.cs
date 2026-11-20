// El suelo del mapa de mundo por teselas de 64 u alrededor del héroe (BronzeAgeClient: `src/mundo3d/terreno.ts`): una malla por tesela con la
// altura del relieve tal cual y el agua plana encima de lo que el relieve marca como agua (el mar a su nivel, el umbral del generador de
// `/v1/datos`; los lagos ya vienen planos). Lo pinta `Conquest/MapaSuelo` con las texturas de `Resources/MapaMundo/Suelo` según la altura, la
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
        readonly Material material;
        readonly Texture2D datos;
        readonly Transform padre;
        readonly Dictionary<(int, int), (GameObject go, float paso)> teselas = new Dictionary<(int, int), (GameObject, float)>();
        readonly List<(int tx, int ty, float d)> pendientes = new List<(int, int, float)>();
        readonly System.Diagnostics.Stopwatch reloj = new System.Diagnostics.Stopwatch();

        public int Cargadas => teselas.Count;
        /// <summary>Teselas construidas desde el principio y el tiempo que ha llevado (ms), para medir.</summary>
        public int Construidas { get; private set; }
        public double MsConstruyendo => reloj.Elapsed.TotalMilliseconds;

        /// <summary>`nivelDelMar`: metros a los que va la lámina del mar (el umbral de agua del generador). `umbrales`: metros de la costa, la colina,
        /// la montaña y la cima (los del generador).</summary>
        public TerrenoDelMapa(Relieve relieve, RasterDeMundo fertilidad, RasterDeMundo humedad, RasterDeMundo bosque, float nivelDelMar, Vector4 umbrales, Transform padre)
        {
            this.relieve = relieve;
            this.fertilidad = fertilidad;
            this.nivelDelMar = nivelDelMar;
            this.padre = padre;
            material = new Material(Resources.Load<Shader>("MapaMundo/MapaSuelo")) { name = "suelo del mapa" };
            material.SetTexture("_SueloColor", Resources.Load<Texture2DArray>("MapaMundo/Suelo/suelo_color"));
            material.SetTexture("_SueloNormal", Resources.Load<Texture2DArray>("MapaMundo/Suelo/suelo_normal"));
            material.SetTexture("_SueloMascara", Resources.Load<Texture2DArray>("MapaMundo/Suelo/suelo_mascara"));
            material.SetTexture("_SueloRuido", Resources.Load<Texture2D>("MapaMundo/Suelo/suelo_ruido"));
            material.SetVector("_Umbrales", umbrales);
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
                reloj.Start();
                teselas[(tx, ty)] = (Construir(tx, ty, paso, lado), paso);
                reloj.Stop();
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

        /// <summary>La altura de la malla en (x, y): la lámina si hay agua; si no, el suelo, bajado hasta el fondo del cauce junto a los ríos.</summary>
        float Suelo(double x, double y, float paso)
        {
            var h = (float)relieve.AlturaEn(x, y);
            if (relieve.AguaEn(x, y)) return Mathf.Max(h, nivelDelMar);
            if (Cauces?.Fondo(x, y, paso) is float fondo) h = Mathf.Min(h, fondo);
            return h;
        }

        /// <summary>El color de vértice: blanco con alfa 1 donde van las texturas; el agua y la capa de fertilidad, su color plano con alfa 0.</summary>
        Color32 ColorEn(double x, double y)
        {
            if (relieve.AguaEn(x, y))
            {
                var h = (float)relieve.AlturaEn(x, y);
                return Plano(Mathf.Max(h, nivelDelMar) - h > PinturaDelMapa.MarHondo ? PinturaDelMapa.AguaHonda : PinturaDelMapa.AguaSomera);
            }
            if (conFertilidad && fertilidad != null) return Plano(ColorDeFertilidad(fertilidad.En(x, y) / 255f));
            return new Color32(255, 255, 255, 255);
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
            // `ancho`: muestras del radio de la pendiente ancha (unos `RadioPendienteAncha` metros, al menos una).
            int n = Mathf.RoundToInt(lado / paso) + 1, ancho = Mathf.Max(1, Mathf.RoundToInt(PinturaDelMapa.RadioPendienteAncha / (paso * e))), anillo = ancho + 1, m = n + 2 * anillo;
            double x0 = tx * lado, y0 = ty * lado;
            // Las alturas con un anillo de más (las del vecino): la normal del borde es la misma a los dos lados de la junta.
            var alto = new float[m * m];
            for (int j = 0; j < m; j++)
                for (int i = 0; i < m; i++)
                    alto[j * m + i] = Suelo(x0 + (i - anillo) * paso, y0 + (j - anillo) * paso, paso);
            // La pendiente ancha: la del relieve suavizado con una caja de ±`ancho` muestras. Va al sombreado (UV0) para decidir la roca de
            // pendiente y la nieve: una arista estrecha no llega a roca y el borde no sigue las diagonales de los triángulos.
            var suave = Caja(Caja(alto, m, ancho, 1), m, ancho, m);
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
                    int r = (j + anillo) * m + i + anillo;
                    // Relativo a la esquina de la tesela: así los números de la malla son pequeños. z = −y del mapa.
                    vertices[k] = new Vector3((float)(i * paso * e), alto[r], (float)(-j * paso * e));
                    normales[k] = new Vector3(-(alto[r + 1] - alto[r - 1]) / dos, 1, (alto[r + m] - alto[r - m]) / dos).normalized;
                    colores[k] = ColorEn(x0 + i * paso, y0 + j * paso);
                    anchas[k] = new Vector3(-(suave[r + 1] - suave[r - 1]) / dos, 1, (suave[r + m] - suave[r - m]) / dos).normalized;
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
            return go;
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
            if (datos != null) Object.Destroy(datos);
        }

        static void Soltar(GameObject go)
        {
            if (go == null) return;
            Object.Destroy(go.GetComponent<MeshFilter>().sharedMesh);
            Object.Destroy(go);
        }
    }
}

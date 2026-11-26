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
        readonly RasterDeMundo fertilidad, humedad;
        readonly Vector4 umbrales;
        bool conFertilidad;
        /// <summary>Los cauces de los ríos: el suelo baja hasta ellos (si no, el agua del río queda enterrada).</summary>
        public CaucesDelMapa Cauces;
        /// <summary>El campo de los ríos (26-11): talla su cauce en el suelo y pone su agua en la lámina de la tesela, como el mar y los lagos.
        /// Sin él, el cauce de `Cauces` de antes (y la cinta de agua aparte).</summary>
        public CampoDeRios Campo;
        /// <summary>Lo que tarda (u) el agua de un río en bajar a la del mar o el lago en que entra.</summary>
        const float Boca = 4;
        /// <summary>Lo más que sube o baja la lámina en una celda con río junto al mar o un lago (m, por 1 + 2 × el paso en u); más, es una
        /// juntura que no casa y la celda no se pinta.</summary>
        const float SaltoDeLamina = 2;
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
        /// <summary>Lo más que ha tardado la sombra del relieve en un fotograma (ms; tiene presupuesto).</summary>
        public double PeorMsRelieve { get; private set; }
        /// <summary>Teselas a la espera de su sombra del relieve.</summary>
        public int HorneandoRelieve => horneando.Count;

        sealed class Horneado
        {
            public GameObject Go;
            public double X0, Y0;
            public int N, Nr, Hechos;
            public float Paso;
            public float[] Rejilla;
            public List<Vector2Int> Agua;
        }
        readonly List<Horneado> horneando = new List<Horneado>();
        readonly System.Diagnostics.Stopwatch relojRelieve = new System.Diagnostics.Stopwatch();

        /// <summary>La sombra del relieve de las teselas pendientes, como mucho `presupuestoMs` por fotograma (el relieve no se lee desde otro hilo):
        /// punto a punto de la rejilla de 2 u; al acabar una, sus vértices la reciben (UV1.x del suelo y del faldón, UV0.z del agua).</summary>
        public void HornearRelieve(double presupuestoMs)
        {
            if (horneando.Count == 0) return;
            relojRelieve.Restart();
            var e = EscalaDelMapa.MetrosPorUnidad;
            var (dx, dy, tan) = SombraDelRelieve.DelSol(PinturaDelMapa.Sol.x, PinturaDelMapa.Sol.y, PinturaDelMapa.Sol.z);
            double Altura(double x, double y) => relieve.AlturaEn(x, y);
            while (horneando.Count > 0 && relojRelieve.Elapsed.TotalMilliseconds < presupuestoMs)
            {
                var h = horneando[0];
                if (h.Go == null) { horneando.RemoveAt(0); continue; } // ya se soltó
                h.Rejilla ??= new float[h.Nr * h.Nr];
                while (h.Hechos < h.Rejilla.Length && relojRelieve.Elapsed.TotalMilliseconds < presupuestoMs)
                {
                    int i = h.Hechos % h.Nr, j = h.Hechos / h.Nr;
                    h.Rejilla[h.Hechos++] = SombraDelRelieve.Visibilidad(Altura, h.X0 + i * SombraDelRelieve.PasoRejilla, h.Y0 + j * SombraDelRelieve.PasoRejilla, dx, dy, tan, e);
                }
                if (h.Hechos < h.Rejilla.Length) break;
                AplicarRelieve(h);
                horneando.RemoveAt(0);
            }
            PeorMsRelieve = System.Math.Max(PeorMsRelieve, relojRelieve.Elapsed.TotalMilliseconds);
        }

        void AplicarRelieve(Horneado h)
        {
            float Tapa(int i, int j) => 1 - SombraDelRelieve.En(h.Rejilla, h.Nr, SombraDelRelieve.PasoRejilla, i * h.Paso, j * h.Paso);
            int n = h.N, borde = 4 * n;
            var tapas = new Vector2[n * n + borde];
            // (UV1.y, lo somero de los trechos de vado: se deja como estaba)
            var antes = new List<Vector2>();
            h.Go.GetComponent<MeshFilter>().sharedMesh.GetUVs(1, antes);
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                    tapas[j * n + i] = new Vector2(Tapa(i, j), antes.Count == tapas.Length ? antes[j * n + i].y : 0);
            // el faldón, como el contorno (el mismo orden que en Construir)
            for (int s = 0; s < n; s++)
            {
                tapas[n * n + s] = tapas[s];
                tapas[n * n + n + s] = tapas[s * n + n - 1];
                tapas[n * n + 2 * n + s] = tapas[(n - 1) * n + (n - 1 - s)];
                tapas[n * n + 3 * n + s] = tapas[(n - 1 - s) * n];
            }
            h.Go.GetComponent<MeshFilter>().sharedMesh.SetUVs(1, tapas);
            if (h.Agua == null) return;
            var agua = h.Go.transform.Find("agua")?.GetComponent<MeshFilter>()?.sharedMesh;
            if (agua == null) return;
            var uvs = new List<Vector3>();
            agua.GetUVs(0, uvs);
            for (int k = 0; k < uvs.Count && k < h.Agua.Count; k++) uvs[k] = new Vector3(uvs[k].x, uvs[k].y, Tapa(h.Agua[k].x, h.Agua[k].y));
            agua.SetUVs(0, uvs);
        }

        /// <summary>`nivelDelMar`: metros a los que va la lámina del mar (el umbral de agua del generador). `umbrales`: metros de la costa, la colina,
        /// la montaña y la cima (los del generador).</summary>
        public TerrenoDelMapa(Relieve relieve, RasterDeMundo fertilidad, RasterDeMundo humedad, RasterDeMundo bosque, float nivelDelMar, Vector4 umbrales, Transform padre)
        {
            this.relieve = relieve;
            this.fertilidad = fertilidad;
            this.humedad = humedad;
            this.umbrales = umbrales;
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

        /// <summary>El material del suelo (sus texturas, para que el camino «Mejorado» sea la misma tierra).</summary>
        public Material Material => material;

        /// <summary>Qué suelo hay en (x, y), con las reglas de `MapaSuelo` sin su ruido: cuánto es arena (por la altura, junto a la costa), roca
        /// (por la altura o por la pendiente ancha, la de ±2 u) y pradera verde (por la humedad y la fertilidad); de 0 a 1.</summary>
        /// <summary>¿Hay agua del relieve (mar o lago) en (x, y)?</summary>
        public bool AguaEn(double x, double y) => relieve.AguaEn(x, y);

        public (float arena, float roca, float verde) SueloEn(double x, double y)
        {
            float h = (float)relieve.AlturaEn(x, y);
            float arena = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(umbrales.x - 12, umbrales.x + 8, h));
            var e = EscalaDelMapa.MetrosPorUnidad;
            double gx = (relieve.AlturaEn(x + 2, y) - relieve.AlturaEn(x - 2, y)) / (4 * e), gy = (relieve.AlturaEn(x, y + 2) - relieve.AlturaEn(x, y - 2)) / (4 * e);
            float pendiente = 1 - 1 / Mathf.Sqrt(1 + (float)(gx * gx + gy * gy));
            var r = material.GetVector("_Roca");
            float roca = Mathf.Max(Mathf.SmoothStep(0, 1, Mathf.InverseLerp(umbrales.z + 20, umbrales.w + 10, h)), Mathf.SmoothStep(0, 1, Mathf.InverseLerp(r.x, r.y, pendiente)));
            var v = material.GetVector("_Verdor");
            float hum = humedad == null ? 0.3f : humedad.En(x, y) / 255f, fer = fertilidad == null ? 0.3f : fertilidad.En(x, y) / 255f;
            float verde = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(v.x, v.y, 0.5f * hum + 0.5f * fer));
            return (arena, roca, verde);
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
            foreach (var k in fuera) { Soltar(teselas[k].go); teselas.Remove(k); Version++; }

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
                Version++;
                reloj.Stop();
                PeorMsTesela = System.Math.Max(PeorMsTesela, reloj.Elapsed.TotalMilliseconds - antes);
                Construidas++;
            }
        }

        /// <summary>Lo más alto de la malla de suelo ya pintada alrededor de (x, y): las cuatro esquinas de la celda de la tesela (metros); null si esa
        /// tesela aún no está. Para comprobar que lo que va encima del suelo (ríos, rayas) no queda enterrado.</summary>
        /// <summary>Cambia cada vez que se hace o se suelta una tesela (lo que va pegado al suelo mira si se tiene que rehacer).</summary>
        public int Version { get; private set; }

        /// <summary>Lo que resume el suelo pintado bajo la caja [x0, x1] × [y0, y1] (u): qué teselas hay y con qué malla.</summary>
        public int Firma(double x0, double y0, double x1, double y1)
        {
            float lado = (float)relieve.LadoEnUnidades;
            int f = 17;
            unchecked
            {
                for (int ty = (int)System.Math.Floor(y0 / lado); ty <= (int)System.Math.Floor(y1 / lado); ty++)
                    for (int tx = (int)System.Math.Floor(x0 / lado); tx <= (int)System.Math.Floor(x1 / lado); tx++)
                        f = f * 31 + (teselas.TryGetValue((tx, ty), out var t) && t.go != null ? t.go.GetEntityId().GetHashCode() : 0);
            }
            return f;
        }

        readonly Dictionary<(int, int), (GameObject go, float[] alturas, int n)> pintadas = new Dictionary<(int, int), (GameObject, float[], int)>();

        /// <summary>La altura (m) de la malla de suelo ya pintada en (x, y), interpolada en su triángulo como la pinta la GPU (lo que va pegado al
        /// suelo, como los caminos, no se hunde ni flota en las teselas de paso grande); sobre el agua, al menos su lámina; sin tesela, el relieve.</summary>
        public float AlturaPintada(double x, double y)
        {
            float lado = (float)relieve.LadoEnUnidades;
            int tx = (int)System.Math.Floor(x / lado), ty = (int)System.Math.Floor(y / lado);
            if (!teselas.TryGetValue((tx, ty), out var t) || t.go == null) return AlturaVisible(x, y);
            int n = Mathf.RoundToInt(lado / t.paso) + 1;
            if (!pintadas.TryGetValue((tx, ty), out var p) || p.go != t.go)
            {
                var v = t.go.GetComponent<MeshFilter>().sharedMesh.vertices;
                var h = new float[n * n];
                for (int k = 0; k < h.Length; k++) h[k] = v[k].y;
                pintadas[(tx, ty)] = p = (t.go, h, n);
            }
            double fx = (x - tx * lado) / t.paso, fy = (y - ty * lado) / t.paso;
            int i = Mathf.Clamp((int)fx, 0, n - 2), j = Mathf.Clamp((int)fy, 0, n - 2);
            double u = fx - i, w = fy - j;
            float a = p.alturas[j * n + i], b = p.alturas[j * n + i + 1], c = p.alturas[(j + 1) * n + i], d = p.alturas[(j + 1) * n + i + 1];
            // los triángulos de la tesela: (a, b, c) y (b, d, c)
            double altura = u + w <= 1 ? a + (b - a) * u + (c - a) * w : d + (c - d) * (1 - u) + (b - d) * (1 - w);
            return relieve.AguaEn(x, y) ? Mathf.Max((float)altura, nivelDelMar) : (float)altura;
        }

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
            // (la ribera, lineal de −1 a 1 en 0..1: interpolada entre vértices, el sombreado la corta por píxel en una curva, sin dientes)
            return new Color32(Uno(hondo / EscalaDelMapa.MetrosPorUnidad / PinturaDelMapa.AguaMaxima), Uno(mojado), Uno(0.5f + 0.5f * ribera), 255);
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
            var cauces = Cauces != null && Cauces.HayRioCerca(x0 - M * paso, y0 - M * paso, x0 + lado + M * paso, y0 + lado + M * paso) ? Cauces : null;
            var campo = Campo != null && Campo.HayRioEn(x0 - M * paso, y0 - M * paso, x0 + lado + M * paso, y0 + lado + M * paso) ? Campo : null;
            // del río, en cada muestra: el nivel de su agua (NaN fuera), lo mojado, la corriente (rumbo) y lo rápido que baja (aguas blancas, 0..1)
            // (y el de su orilla, para las esquinas secas de la lámina)
            float[] nivelRio = null, nivelOrilla = null, mojadoRio = null, rapido = null, centroRio = null, pesoRio = null;
            Vector2[] ejeRio = null;
            Vector2[] corriente = null;
            if (campo != null) { ejeRio = new Vector2[W * W]; pesoRio = new float[W * W]; centroRio = new float[W * W]; nivelRio = new float[W * W]; nivelOrilla = new float[W * W]; mojadoRio = new float[W * W]; rapido = new float[W * W]; corriente = new Vector2[W * W]; }
            bool hayRio = false;
            var ribera = new float[W * W];
            var somero = new float[W * W];
            // El campo de los ríos, antes que la orilla del mar y los lagos: así, donde un río entra en ellos, su agua no cuenta como tierra (sin
            // orilla ni espuma atravesadas en la boca).
            var enRio = campo != null ? new CampoDeRios.EnRio?[W * W] : null;
            var aguaOrio = agua;
            if (campo != null)
            {
                aguaOrio = (bool[])agua.Clone();
                for (int j = 0, k = 0; j < W; j++)
                    for (int i = 0; i < W; i++, k++)
                    {
                        if (agua[k]) continue;
                        double x = x0 + (i - M) * paso, y = y0 + (j - M) * paso;
                        float ensanche = cauces != null ? cauces.Ensanche(x, y, out somero[k]) : 1;
                        enRio[k] = campo.En(x, y, crudo[k], CampoDeRios.MedioMinimo * paso, ensanche);
                        aguaOrio[k] = enRio[k] is CampoDeRios.EnRio en && !float.IsNaN(en.Nivel);
                        // lo somero del vado, con lo andado continuo del río más cercano (no a saltos de muestra en muestra)
                        if (cauces != null && enRio[k] is CampoDeRios.EnRio er && er.Rio >= 0) somero[k] = cauces.SomeroDe(er.Rio, er.S);
                    }
            }
            // La orilla (BronzeAgeClient, `terreno.ts` y `agua.ts`): la máscara de agua emborronada (`b`, la costa es b = 0,5) y, en el agua, la
            // distancia a tierra. El fondo baja de 0 a 2,6 u en los primeros 6 u desde la orilla; la tierra de junto al agua baja hacia la lámina
            // con 0,8 u de playa, sin quedar a menos de 0,03 u sobre ella. Así la orilla es la línea donde el suelo corta la lámina, sin escalones.
            float[] mascara = hayAgua ? MascaraSuave(agua, W) : null, distancia = hayAgua ? DistanciaATierra(aguaOrio, W) : null;
            // En tierra, la distancia al agua más cercana (en muestras) y el nivel de su lámina.
            float[] alAgua = null, nivelAlAgua = null;
            if (hayAgua) (alAgua, nivelAlAgua) = DistanciaAlAgua(agua, crudo, W);
            var suelo = new float[W * W];
            var nivelCerca = new float[W * W];
            for (int j = 0, k = 0; j < W; j++)
                for (int i = 0; i < W; i++, k++)
                {
                    double x = x0 + (i - M) * paso, y = y0 + (j - M) * paso;
                    float h = crudo[k];
                    nivelCerca[k] = float.NaN;
                    if (nivelRio != null) { nivelRio[k] = nivelOrilla[k] = float.NaN; centroRio[k] = -1; pesoRio[k] = 1; }
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
                    if (campo != null)
                    {
                        // El cauce del campo (ya consultado), con el medio ancho mínimo de este paso; en el trecho de vado, menos hondo. La ribera,
                        // más ancha en el lado de dentro de las curvas y en el vado (`Ensanche`, que da también lo somero).
                        if (enRio[k] is CampoDeRios.EnRio en)
                        {
                            // La boca: en los últimos `Boca` u antes del mar o de un lago, el agua del río (con su cauce y su orilla) baja hasta su
                            // lámina, para que se junten sin escalón ni dientes de orilla.
                            // Y el río se funde con ellos: su aspecto (`pesoRio`, al sombreado) y lo hondo de su cauce pasan a los suyos en esos `Boca` u.
                            float cercaDelAgua = hayAgua && !float.IsNaN(nivelAlAgua[k]) ? Mathf.SmoothStep(0, 1, alAgua[k] * paso / Boca) : 1;
                            pesoRio[k] = cercaDelAgua;
                            if (hayAgua && !float.IsNaN(nivelAlAgua[k]) && en.NivelOrilla > nivelAlAgua[k])
                            {
                                float baja = (1 - cercaDelAgua) * (en.NivelOrilla - nivelAlAgua[k]);
                                en.Suelo -= baja; en.Nivel -= baja; en.NivelOrilla -= baja;
                            }
                            if (!float.IsNaN(en.Nivel)) en.Suelo = en.Nivel - (en.Nivel - en.Suelo) * Mathf.Lerp(0.3f, 1, cercaDelAgua);
                            h = en.Suelo;
                            ribera[k] = en.Ribera;
                            mojadoRio[k] = en.Mojado;
                            corriente[k] = new Vector2(en.Cx, en.Cy);
                            centroRio[k] = en.Centro;
                            ejeRio[k] = new Vector2(en.S, en.Lado);
                            rapido[k] = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.1f, 0.3f, en.Pendiente / e));
                            nivelOrilla[k] = en.NivelOrilla;
                            if (!float.IsNaN(en.Nivel))
                            {
                                h = en.Nivel - (en.Nivel - h) * Mathf.Lerp(1, 0.3f, somero[k]);
                                nivelRio[k] = en.Nivel;
                                hayRio = true;
                            }
                        }
                    }
                    else if (cauces != null && cauces.Consultar(x, y, paso, out ribera[k], out somero[k]) is float fondo) h = Mathf.Min(h, fondo);
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
            // UV1: x la sombra del relieve (la pone `AplicarRelieve`), y lo somero de los trechos de vado (la playa de grava, en `MapaSuelo`)
            var uv1 = new Vector2[n * n + borde];
            // UV2: del río más cercano, la distancia a su eje, su medio ancho pintado y el ancho de su ribera (u; 0 lejos de todo río)
            var uv2 = new Vector4[n * n + borde];
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
                    // (en el río: lo hondo bajo su lámina y su suelo mojado, a la medida de su ancho)
                    bool deRio = !agua[r] && nivelRio != null && !float.IsNaN(nivelRio[r]);
                    if (mojadoRio != null && !agua[r]) mojado = Mathf.Max(mojado, mojadoRio[r]);
                    float hondoAqui = agua[r] ? Mathf.Max(0, Nivel(crudo[r]) - suelo[r]) : deRio ? Mathf.Max(0, nivelRio[r] - suelo[r]) : 0;
                    colores[k] = ColorEn(x0 + i * paso, y0 + j * paso, agua[r] || deRio, hondoAqui, mojado, ribera[r]);
                    anchas[k] = new Vector3(-(suave[r + 1] - suave[r - 1]) / dos, 1, (suave[r + W] - suave[r - W]) / dos).normalized;
                    uv1[k] = new Vector2(0, somero[r]);
                    if (enRio != null && enRio[r] is CampoDeRios.EnRio er) uv2[k] = new Vector4(Mathf.Abs(er.Lado), er.Medio, er.AnchoRibera, 0);
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
                uv1[f] = uv1[v];
                uv2[f] = uv2[v];
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
            malla.SetUVs(1, uv1);
            malla.SetUVs(2, uv2);
            malla.SetTriangles(triangulos, 0);
            malla.RecalculateBounds();
            var go = new GameObject(malla.name);
            go.transform.SetParent(padre, false);
            go.transform.localPosition = EscalaDelMapa.AEscena(x0, y0, 0);
            go.AddComponent<MeshFilter>().sharedMesh = malla;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            var verticesDelAgua = hayAgua || hayRio ? Lamina(go.transform, n, M, W, paso, agua, crudo, suelo, nivelCerca, hayRio ? new Rio(nivelRio, nivelOrilla, corriente, rapido, somero, centroRio, pesoRio, ejeRio) : null) : null;
            // La sombra del relieve (con sombras reales): se hornea poco a poco, con presupuesto por fotograma (`HornearRelieve`); la tesela sale
            // sin ella y la recibe al acabar.
            if (PinturaDelMapa.SombrasReales && PinturaDelMapa.SombraDelRelieve)
                horneando.Add(new Horneado { Go = go, X0 = x0, Y0 = y0, N = n, Paso = paso, Agua = verticesDelAgua,
                    Nr = Mathf.RoundToInt(lado / (float)SombraDelRelieve.PasoRejilla) + 1 });
            return go;
        }

        /// <summary>Lo del río en cada muestra de la ventana, para la lámina: el nivel de su agua (NaN fuera), la corriente, lo rápido y lo somero.</summary>
        sealed class Rio
        {
            public readonly float[] Nivel, Orilla, Rapido, Somero, Centro, Peso;
            public readonly Vector2[] Corriente, Eje;
            public Rio(float[] nivel, float[] orilla, Vector2[] corriente, float[] rapido, float[] somero, float[] centro, float[] peso, Vector2[] eje)
            { Nivel = nivel; Orilla = orilla; Corriente = corriente; Rapido = rapido; Somero = somero; Centro = centro; Peso = peso; Eje = eje; }
            public bool En(int r) => !float.IsNaN(Nivel[r]);
        }

        /// <summary>La lámina de agua de la tesela (BronzeAgeClient: `laminaDeAgua`): toda celda con alguna esquina de agua. Las esquinas de agua van a
        /// su nivel; las de tierra, al de la muestra de agua más cercana (quedan bajo el suelo, que corta la lámina en la orilla). Cada vértice lleva
        /// en UV0.x lo hondo que queda el fondo (u), para el color, la transparencia, las olas y la espuma del sombreado.
        /// Con `rio`, también las celdas con alguna esquina de río (donde ya hay mar o lago, manda ese): sus esquinas, al nivel del río; las de
        /// tierra, al de la muestra de río más cercana. UV1: x lo somero (el trecho de vado), y 1 en el río (bajando a 0 en la boca, que se funde con el mar o el lago); UV2: el rumbo de la corriente (xy),
        /// lo rápido que baja (z) y lo cerca del eje (w: `CampoDeRios.EnRio.Centro`); UV3, lo andado a lo largo del río y la
        /// distancia con signo a su eje (u).</summary>
        // (devuelve, de cada vértice de la lámina, su (i, j) en la tesela, para la sombra del relieve; null si no hay lámina)
        List<Vector2Int> Lamina(Transform tesela, int n, int M, int W, float paso, bool[] agua, float[] crudo, float[] suelo, float[] nivelCerca, Rio rio)
        {
            var e = EscalaDelMapa.MetrosPorUnidad;
            var indice = new int[n * n];
            var vertices = new List<Vector3>();
            var uvs = new List<Vector3>();
            var uv1 = new List<Vector2>();
            var uv2 = new List<Vector4>();
            var uv3 = new List<Vector2>();
            var coordenadas = new List<Vector2Int>();
            var triangulos = new List<int>();
            for (int k = 0; k < indice.Length; k++) indice[k] = -1;
            int Vertice(int i, int j)
            {
                int k = j * n + i;
                if (indice[k] >= 0) return indice[k];
                int r = (j + M) * W + i + M;
                // (de qué muestra de río toma el nivel: la suya, en el cauce o en su orilla (el mismo campo); si no, la más cercana; -1 si no es de río)
                // (junto al mar o un lago, una muestra vecina de agua, la esquina seca va a su lámina, como antes)
                bool junto = agua[r - 1] || agua[r + 1] || agua[r - W] || agua[r + W] || agua[r - W - 1] || agua[r - W + 1] || agua[r + W - 1] || agua[r + W + 1];
                // (salvo si esa lámina queda más alta que el río: un lago colgado sobre él no se derrama por sus esquinas)
                int deRio = rio == null || agua[r] ? -1 : rio.En(r) ? r : !float.IsNaN(rio.Orilla[r]) && (!junto || !(rio.Orilla[r] >= nivelCerca[r])) ? r
                    : junto ? -1 : float.IsNaN(nivelCerca[r]) ? RioCerca(rio, W, i + M, j + M, 2) : -1;
                float nivel = agua[r] ? Nivel(crudo[r]) : deRio < 0 ? nivelCerca[r] : rio.En(deRio) ? rio.Nivel[deRio] : rio.Orilla[deRio];
                if (float.IsNaN(nivel)) nivel = NivelCerca(agua, crudo, W, i + M, j + M, 4);
                if (float.IsNaN(nivel)) nivel = suelo[r];
                indice[k] = vertices.Count;
                vertices.Add(new Vector3((float)(i * paso * e), nivel, (float)(-j * paso * e)));
                // UV0: x lo hondo (u); y, 1 si es un lago (la lámina por encima del mar), que el sombreado pinta más calmado; z, la sombra del relieve.
                // (de río toda esquina que no toque el mar o un lago: si no, el borde del río se pintaría como orilla de mar, con su espuma)
                bool esRio = rio != null && !agua[r] && (deRio >= 0 || !junto);
                int q = deRio >= 0 ? deRio : r;
                uvs.Add(new Vector3(Mathf.Max(0, nivel - suelo[r]) / e, !esRio && nivel > nivelDelMar - 0.5f ? 1 : 0, 0));
                uv1.Add(esRio ? new Vector2(rio.Somero[q], rio.Peso[q]) : Vector2.zero);
                uv2.Add(esRio ? new Vector4(rio.Corriente[q].x, rio.Corriente[q].y, rio.Rapido[q], rio.Centro[r]) : Vector4.zero);
                uv3.Add(esRio ? (rio.Eje[r] != Vector2.zero ? rio.Eje[r] : rio.Eje[q]) : Vector2.zero);
                coordenadas.Add(new Vector2Int(i, j));
                return indice[k];
            }
            for (int j = 0; j < n - 1; j++)
                for (int i = 0; i < n - 1; i++)
                {
                    int r = (j + M) * W + i + M;
                    if (!(agua[r] || agua[r + 1] || agua[r + W] || agua[r + W + 1]) && !(rio != null && (rio.En(r) || rio.En(r + 1) || rio.En(r + W) || rio.En(r + W + 1)))) continue;
                    int a = Vertice(i, j), b = Vertice(i + 1, j), c = Vertice(i, j + 1), d = Vertice(i + 1, j + 1);
                    // (con ríos: una celda entre el mar o un lago y tierra cuyas esquinas van a láminas de alturas muy distintas, un lago colgado
                    // sobre el río, sería una cuña de agua; el suelo tapa ese salto. Los rápidos de un río, sí se pintan)
                    bool mezcla = (agua[r] || agua[r + 1] || agua[r + W] || agua[r + W + 1]) && !(agua[r] && agua[r + 1] && agua[r + W] && agua[r + W + 1]);
                    if (rio != null && mezcla && Mathf.Max(Mathf.Max(vertices[a].y, vertices[b].y), Mathf.Max(vertices[c].y, vertices[d].y))
                                     - Mathf.Min(Mathf.Min(vertices[a].y, vertices[b].y), Mathf.Min(vertices[c].y, vertices[d].y)) > SaltoDeLamina * (1 + 2 * paso)) continue;
                    triangulos.Add(a); triangulos.Add(b); triangulos.Add(c);
                    triangulos.Add(b); triangulos.Add(d); triangulos.Add(c);
                }
            if (triangulos.Count == 0) return null;
            var malla = new Mesh { name = "agua", indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            malla.SetVertices(vertices);
            var arriba = new Vector3[vertices.Count];
            for (int k = 0; k < arriba.Length; k++) arriba[k] = Vector3.up;
            malla.SetNormals(arriba);
            malla.SetUVs(0, uvs);
            // (siempre, también sin río: sin esos canales, el sombreado del agua leía valores sin definir y la lámina de las teselas sin río no se
            // veía; en el mundo de pruebas, lagos enteros de arena)
            malla.SetUVs(1, uv1); malla.SetUVs(2, uv2); malla.SetUVs(3, uv3);
            malla.SetTriangles(triangulos, 0);
            malla.RecalculateBounds();
            var go = new GameObject("agua");
            go.transform.SetParent(tesela, false);
            go.AddComponent<MeshFilter>().sharedMesh = malla;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = materialAgua;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return coordenadas;
        }

        /// <summary>La muestra de río más cercana a (i, j) a menos de `radio` muestras (por anillos); -1 si no hay.</summary>
        static int RioCerca(Rio rio, int W, int i, int j, int radio)
        {
            for (int r = 1; r <= radio; r++)
                for (int dj = -r; dj <= r; dj++)
                    for (int di = -r; di <= r; di++)
                    {
                        if (Mathf.Max(Mathf.Abs(di), Mathf.Abs(dj)) != r) continue;
                        int a = i + di, c = j + dj;
                        if (a >= 0 && c >= 0 && a < W && c < W && rio.En(c * W + a)) return c * W + a;
                    }
            return -1;
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

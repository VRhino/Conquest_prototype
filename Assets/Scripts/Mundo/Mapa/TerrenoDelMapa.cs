// El suelo del mapa de mundo por teselas de 64 u alrededor del héroe (BronzeAgeClient: `src/mundo3d/terreno.ts`, sin su pintura de relleno):
// una malla por tesela con la altura del relieve tal cual, color plano por la clase de terreno del artefacto, y el agua plana encima de lo que el
// relieve marca como agua (el mar a su nivel, el umbral del generador de `/v1/datos`; los lagos ya vienen planos). Se cargan las más cercanas
// primero, dos por fotograma, con paso de 0,5 u cerca, 1 u a media distancia y 2 u lejos; las que se alejan se sueltan.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ConquestTactics.Mundo.Mapa
{
    public sealed class TerrenoDelMapa
    {
        const int PorFotograma = 2;

        readonly Relieve relieve;
        readonly RasterDeMundo clases, bosque, fertilidad;
        bool conFertilidad;
        /// <summary>Los cauces de los ríos: el suelo baja hasta ellos (si no, el agua del río queda enterrada).</summary>
        public CaucesDelMapa Cauces;
        readonly float nivelDelMar;
        readonly Material material;
        readonly Transform padre;
        readonly Dictionary<(int, int), (GameObject go, float paso)> teselas = new Dictionary<(int, int), (GameObject, float)>();
        readonly List<(int tx, int ty, float d)> pendientes = new List<(int, int, float)>();

        public int Cargadas => teselas.Count;

        /// <summary>`nivelDelMar`: metros a los que va la lámina del mar (el umbral de agua del generador).</summary>
        public TerrenoDelMapa(Relieve relieve, RasterDeMundo clases, RasterDeMundo bosque, RasterDeMundo fertilidad, float nivelDelMar, Material material, Transform padre)
        {
            this.relieve = relieve;
            this.clases = clases;
            this.bosque = bosque;
            this.fertilidad = fertilidad;
            this.nivelDelMar = nivelDelMar;
            this.material = material;
            this.padre = padre;
        }

        /// <summary>La capa de fertilidad (botón de capas): el suelo se pinta con la rampa de fertilidad en vez de por clase. Rehace las teselas.</summary>
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
                teselas[(tx, ty)] = (Construir(tx, ty, paso, lado), paso);
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

        GameObject Construir(int tx, int ty, float paso, float lado)
        {
            int n = Mathf.RoundToInt(lado / paso) + 1;
            double x0 = tx * lado, y0 = ty * lado;
            var e = EscalaDelMapa.MetrosPorUnidad;
            var vertices = new Vector3[n * n];
            var colores = new Color32[n * n];
            for (int j = 0, k = 0; j < n; j++)
                for (int i = 0; i < n; i++, k++)
                {
                    double x = x0 + i * paso, y = y0 + j * paso;
                    var h = (float)relieve.AlturaEn(x, y);
                    Color c;
                    if (relieve.AguaEn(x, y))
                    {
                        var lamina = Mathf.Max(h, nivelDelMar);
                        c = lamina - h > PinturaDelMapa.MarHondo ? PinturaDelMapa.AguaHonda : PinturaDelMapa.AguaSomera;
                        h = lamina;
                    }
                    else
                    {
                        if (Cauces?.Fondo(x, y, paso) is float fondo) h = Mathf.Min(h, fondo);
                        c = conFertilidad && fertilidad != null ? ColorDeFertilidad(fertilidad.En(x, y) / 255f) : ConBosque(ColorDeClase(clases.En(x, y)), bosque == null ? 0 : bosque.En(x, y) / 255f);
                    }
                    // Relativo a la esquina de la tesela: así los números de la malla son pequeños.
                    vertices[k] = new Vector3((float)(i * paso * e), h, (float)(-j * paso * e));
                    colores[k] = c.linear; // el proyecto va en espacio lineal: el color de vértice no se convierte solo
                }
            var triangulos = new int[(n - 1) * (n - 1) * 6];
            for (int j = 0, t = 0; j < n - 1; j++)
                for (int i = 0; i < n - 1; i++)
                {
                    int a = j * n + i, b = a + 1, c = a + n, d = c + 1;
                    // z = −y: el orden de los vértices se da la vuelta para que la cara mire arriba.
                    triangulos[t++] = a; triangulos[t++] = b; triangulos[t++] = c;
                    triangulos[t++] = b; triangulos[t++] = d; triangulos[t++] = c;
                }
            var malla = new Mesh { name = $"tesela {tx},{ty}", indexFormat = n * n > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            malla.SetVertices(vertices);
            malla.SetColors(colores);
            malla.SetTriangles(triangulos, 0);
            malla.RecalculateNormals();
            malla.RecalculateBounds();
            var go = new GameObject(malla.name);
            go.transform.SetParent(padre, false);
            go.transform.localPosition = EscalaDelMapa.AEscena(x0, y0, 0);
            go.AddComponent<MeshFilter>().sharedMesh = malla;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        /// <summary>El bosque como relleno plano: sotobosque sobre el color del suelo según la densidad (BronzeAgeClient: `colorDelSuelo`).</summary>
        public static Color ConBosque(Color suelo, float densidad) =>
            Color.Lerp(suelo, PinturaDelMapa.Sotobosque, 0.55f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.15f, 0.6f, densidad)));

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

        public static Color ColorDeClase(byte clase)
        {
            switch ((BronzeAge.Contract.V1.ClaseTerreno)clase)
            {
                case BronzeAge.Contract.V1.ClaseTerreno.TerrenoAgua:
                case BronzeAge.Contract.V1.ClaseTerreno.TerrenoCosta: return PinturaDelMapa.Costa;
                case BronzeAge.Contract.V1.ClaseTerreno.TerrenoColina: return PinturaDelMapa.Colina;
                case BronzeAge.Contract.V1.ClaseTerreno.TerrenoMontana: return PinturaDelMapa.Montana;
                case BronzeAge.Contract.V1.ClaseTerreno.TerrenoCima: return PinturaDelMapa.Cima;
                default: return PinturaDelMapa.Llano;
            }
        }

        public void Destruir()
        {
            foreach (var t in teselas.Values) Soltar(t.go);
            teselas.Clear();
        }

        static void Soltar(GameObject go)
        {
            if (go == null) return;
            Object.Destroy(go.GetComponent<MeshFilter>().sharedMesh);
            Object.Destroy(go);
        }
    }
}

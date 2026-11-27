// El montaje del campo del lugar en BattleScene (Campo_De_Batalla_Del_Lugar.md §4, pieza 3): lo que generó `GeneradorDelCampo` (C# puro,
// entero) se vuelve escena. Solo visualización y navegación: el campo no se toca desde aquí.
// - El Terrain jugable: el área jugable más 100 m a cada lado, en un heightmap de 513, con su collider; lo muestrea `calculateTerraindHeight`.
// - El anillo: todo el campo en una malla de 257² sin collider, un metro por debajo (donde se solapa no se ve), bajo la niebla. Es malla y no
//   un segundo Terrain para que `Terrain.activeTerrain` sea siempre el jugable.
// - El agua: una lámina por vértice de 2 m donde el campo tiene agua; los árboles, dibujados por instancias, con tronco (collider y hueco en el
//   NavMesh) los del área jugable hasta 1500.
// - El NavMesh del Terrain jugable, con el agua honda fuera y los troncos como huecos. El suelo plano de la escena (`Map`) se apaga.
// Sin campo (un ticket sin recorte, o la escena abierta a mano), la escena se queda como estaba.
using System.Collections.Generic;
using ConquestTactics.Mundo.Campo;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public class MontajeDelCampo : MonoBehaviour
{
    const float MargenTerreno = 100f; // m a cada lado del área jugable
    const int ResolucionJugable = 513, ResolucionAnillo = 257;
    const float HundidoAnillo = 1f;
    const int MaxColliders = 1500;
    const int CapaSuelo = 3, CapaAgua = 4;
    static readonly Color Niebla = new Color(0x9f / 255f, 0xb6 / 255f, 0xcc / 255f);

    /// <summary>Las posiciones (escena) de los puntos de spawn 1 a 6 del campo montado, o null si no hay campo. Las pasa a ECS <c>SpawnsDelCampoSystem</c>.</summary>
    public static float3[] Spawns;
    /// <summary>El campo montado ahora (lo lee el minimapa), o null.</summary>
    public static CampoGenerado Actual;

    CampoGenerado campo;
    Rejilla alturas;
    NavMeshDataInstance navMesh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Enganchar()
    {
        SceneManager.sceneLoaded -= AlCargar;
        SceneManager.sceneLoaded += AlCargar;
    }

    static void AlCargar(Scene escena, LoadSceneMode modo)
    {
        if (escena.name != SceneTransitionService.SceneNames.Battle) return;
        Spawns = null;
        var reloj = System.Diagnostics.Stopwatch.StartNew();
        var campo = CampoDeLaBatalla.Esperar(null);
        if (campo == null) return;
        long espera = reloj.ElapsedMilliseconds;
        var go = new GameObject("CampoDelLugar");
        SceneManager.MoveGameObjectToScene(go, escena);
        go.AddComponent<MontajeDelCampo>().Montar(campo, escena);
        Debug.Log($"[Campo] montado en {reloj.ElapsedMilliseconds} ms (esperando la generación {espera} ms)");
    }

    // del campo (mm, y al sur) a la escena (m, z al norte), y al revés
    static Vector3 AEscena(long x, long y, long h) => new Vector3(x / 1000f, h / 1000f, -y / 1000f);
    float AlturaEn(long x, long y) => alturas.Muestra(x, y) / 1000f;

    void Montar(CampoGenerado c, Scene escena)
    {
        campo = c;
        Actual = c;
        alturas = new Rejilla(-c.Lado / 2, -c.Lado / 2, c.PasoAlturas, c.NAlturas, c.NAlturas, c.Alturas);
        // el suelo plano y las piezas del castillo, fuera (con su NavMesh horneado)
        Material baseMat = null;
        foreach (var raiz in escena.GetRootGameObjects())
            if (raiz.name == "Map")
            {
                var suelo = raiz.transform.Find("Ground");
                if (suelo != null && suelo.TryGetComponent<MeshRenderer>(out var r)) baseMat = r.sharedMaterial;
                raiz.SetActive(false);
            }
        if (baseMat == null) baseMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        var terreno = TerrenoJugable(out float minH, out float maxH);
        Suelo();
        Agua(baseMat);
        var troncos = Troncos();
        HornearNavMesh(terreno, troncos, minH, maxH);
        ColocarSpawns(terreno);
        var placas = c.Spawns != null ? new[] { AEscena(c.Spawns.Atacante.X, c.Spawns.Atacante.Y, 0), AEscena(c.Spawns.Defensor.X, c.Spawns.Defensor.Y, 0) } : new Vector3[0];
        gameObject.AddComponent<VegetacionDelCampo>().Montar(c, AlturaEn, aAgua, placas, 25f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Niebla;
        RenderSettings.fogStartDistance = c.Jugable / 2000f + (c.Lado - c.Jugable) / 8000f;
        RenderSettings.fogEndDistance = c.Lado / 2000f;
    }

    Terrain TerrenoJugable(out float minH, out float maxH)
    {
        long S = campo.Jugable + (long)(2 * MargenTerreno * 1000);
        int n = ResolucionJugable;
        var h = new float[n, n];
        minH = float.MaxValue; maxH = float.MinValue;
        for (int zi = 0; zi < n; zi++)
            for (int xi = 0; xi < n; xi++)
            {
                float v = AlturaEn(-S / 2 + xi * S / (n - 1), S / 2 - zi * S / (n - 1));
                h[zi, xi] = v;
                minH = Mathf.Min(minH, v);
                maxH = Mathf.Max(maxH, v);
            }
        float alto = Mathf.Max(1f, maxH - minH);
        for (int zi = 0; zi < n; zi++)
            for (int xi = 0; xi < n; xi++) h[zi, xi] = (h[zi, xi] - minH) / alto;
        float lado = S / 1000f;
        var datos = new TerrainData { heightmapResolution = n };
        datos.size = new Vector3(lado, alto, lado);
        datos.SetHeights(0, 0, h);
        var go = Terrain.CreateTerrainGameObject(datos);
        go.name = "Terreno jugable";
        go.layer = CapaSuelo;
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(-lado / 2, minH, -lado / 2);
        var t = go.GetComponent<Terrain>();
        // (el Terrain solo da el collider, la altura y el NavMesh: el suelo lo dibuja `Suelo`)
        t.drawHeightmap = false;
        t.drawTreesAndFoliage = false;
        return t;
    }

    int CeldaDe(long x, long y)
    {
        int n = campo.NZonas;
        int i = Mathf.Clamp((int)((x + campo.Lado / 2) / campo.CeldaZonas), 0, n - 1), j = Mathf.Clamp((int)((y + campo.Lado / 2) / campo.CeldaZonas), 0, n - 1);
        return j * n + i;
    }

    Zona ZonaEn(long x, long y) => campo.Zonas[CeldaDe(x, y)];

    static Material Tenido(Material baseMat, Color color, string nombre)
    {
        var m = new Material(baseMat) { name = nombre, enableInstancing = true };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null);
        return m;
    }

    GameObject ConMalla(string nombre, Mesh malla, Material material, int capa)
    {
        var go = new GameObject(nombre) { layer = capa };
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = malla;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = ShadowCastingMode.Off;
        return go;
    }

    // El suelo: una malla con `Conquest/CampoSuelo` (las capas del mapa de mundo), la jugable a 2 m y el anillo a ~8 m, un metro por debajo
    // y más oscuro. Lo que decide la capa va por vértice (ver el sombreado).
    void Suelo()
    {
        var sombreado = Resources.Load<Shader>("Campo/CampoSuelo");
        if (sombreado == null) { Debug.LogError("[Campo] falta el sombreado Campo/CampoSuelo"); return; }
        var material = new Material(sombreado) { name = "suelo del campo" };
        material.SetTexture("_SueloColor", Resources.Load<Texture2DArray>("MapaMundo/Suelo/suelo_color"));
        material.SetTexture("_SueloNormal", Resources.Load<Texture2DArray>("MapaMundo/Suelo/suelo_normal"));
        material.SetTexture("_SueloMascara", Resources.Load<Texture2DArray>("MapaMundo/Suelo/suelo_mascara"));
        material.SetTexture("_SueloRuido", Resources.Load<Texture2D>("MapaMundo/Suelo/suelo_ruido"));
        var fuera = new Material(material) { name = "suelo del anillo" };
        fuera.SetFloat("_Oscuro", 0.8f);
        aAgua = DistanciaAlAgua();
        long S = campo.Jugable + (long)(2 * MargenTerreno * 1000);
        var jugable = ConMalla("Suelo jugable", MallaDeSuelo(S, (int)(S / campo.PasoAlturas) + 1, 0), material, CapaSuelo);
        jugable.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
        ConMalla("Suelo del anillo", MallaDeSuelo(campo.Lado, ResolucionAnillo, HundidoAnillo), fuera, 0);
    }

    float[] aAgua;

    // La distancia (m) de cada celda de zonas al agua (chaflán, dos pasadas): para la orilla mojada.
    float[] DistanciaAlAgua()
    {
        int n = campo.NZonas;
        float paso = campo.CeldaZonas / 1000f, diag = paso * 1.41421f;
        var d = new float[n * n];
        for (int k = 0; k < d.Length; k++) d[k] = campo.Zonas[k] == Zona.Somero || campo.Zonas[k] == Zona.Honda ? 0 : 1e9f;
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int k = j * n + i;
                if (i > 0) d[k] = Mathf.Min(d[k], d[k - 1] + paso);
                if (j > 0) { d[k] = Mathf.Min(d[k], d[k - n] + paso); if (i > 0) d[k] = Mathf.Min(d[k], d[k - n - 1] + diag); if (i + 1 < n) d[k] = Mathf.Min(d[k], d[k - n + 1] + diag); }
            }
        for (int j = n - 1; j >= 0; j--)
            for (int i = n - 1; i >= 0; i--)
            {
                int k = j * n + i;
                if (i + 1 < n) d[k] = Mathf.Min(d[k], d[k + 1] + paso);
                if (j + 1 < n) { d[k] = Mathf.Min(d[k], d[k + n] + paso); if (i + 1 < n) d[k] = Mathf.Min(d[k], d[k + n + 1] + diag); if (i > 0) d[k] = Mathf.Min(d[k], d[k + n - 1] + diag); }
            }
        return d;
    }

    Mesh MallaDeSuelo(long lado, int n, float hundido)
    {
        var lugar = campo.Lugar;
        var v = new Vector3[n * n];
        var normales = new Vector3[n * n];
        var anchas = new Vector3[n * n];
        var colores = new Color[n * n];
        var suelo = new Vector4[n * n];
        Vector3 Normal(long x, long y, long e)
        {
            float gx = AlturaEn(x + e, y) - AlturaEn(x - e, y), gz = AlturaEn(x, y - e) - AlturaEn(x, y + e);
            return new Vector3(-gx, 2 * e / 1000f, -gz).normalized;
        }
        int nA = campo.NAlturas;
        for (int j = 0, k = 0; j < n; j++)
            for (int i = 0; i < n; i++, k++)
            {
                long x = -lado / 2 + i * lado / (n - 1), y = lado / 2 - j * lado / (n - 1);
                float h = AlturaEn(x, y);
                v[k] = new Vector3(x / 1000f, h - hundido, -y / 1000f);
                normales[k] = Normal(x, y, campo.PasoAlturas);
                anchas[k] = Normal(x, y, 12_000);
                int celda = CeldaDe(x, y);
                colores[k] = new Color(lugar.Fertilidad.Muestra(x, y) / 65536f, lugar.Humedad.Muestra(x, y) / 65536f, Mathf.Clamp01(campo.Bosque[celda] / 65536f), 1);
                // lo hondo bajo la lámina, en el vértice de las alturas más cercano
                int vi = Mathf.Clamp((int)((x + campo.Lado / 2 + campo.PasoAlturas / 2) / campo.PasoAlturas), 0, nA - 1), vj = Mathf.Clamp((int)((y + campo.Lado / 2 + campo.PasoAlturas / 2) / campo.PasoAlturas), 0, nA - 1);
                int kv = vj * nA + vi;
                float hondo = campo.Lamina[kv] != CampoGenerado.SinAgua ? Mathf.Max(0, campo.Lamina[kv] / 1000f - h) : 0;
                float mojado = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2, 40, aAgua[celda]));
                suelo[k] = new Vector4((campo.AlturaBase + campo.Alturas[kv] * 4L / 25) / 1000f, hondo, mojado, campo.Pisado[kv] / 65536f);
            }
        var tri = new int[(n - 1) * (n - 1) * 6];
        for (int j = 0, t = 0; j + 1 < n; j++)
            for (int i = 0; i + 1 < n; i++)
            {
                int a = j * n + i;
                tri[t++] = a; tri[t++] = a + n; tri[t++] = a + 1;
                tri[t++] = a + 1; tri[t++] = a + n; tri[t++] = a + n + 1;
            }
        var malla = new Mesh { name = "suelo", indexFormat = IndexFormat.UInt32 };
        malla.vertices = v;
        malla.normals = normales;
        malla.colors = colores;
        malla.SetUVs(0, anchas);
        malla.SetUVs(1, suelo);
        malla.triangles = tri;
        malla.RecalculateBounds();
        return malla;
    }

    // la lámina del agua: un vértice seco junto al agua toma la de su vecino (y el suelo lo tapa), así la orilla no sale en escalera
    void Agua(Material baseMat)
    {
        int n = campo.NAlturas;
        var lamina = campo.Lamina;
        var indice = new int[n * n];
        var verts = new List<Vector3>();
        var datos = new List<Vector2>();
        var tri = new List<int>();
        float LaminaEn(int i, int j)
        {
            int k = j * n + i;
            if (lamina[k] != CampoGenerado.SinAgua) return lamina[k] / 1000f;
            float s = 0; int c = 0;
            for (int dj = -1; dj <= 1; dj++)
                for (int di = -1; di <= 1; di++)
                {
                    int ii = i + di, jj = j + dj;
                    if (ii < 0 || jj < 0 || ii >= n || jj >= n || lamina[jj * n + ii] == CampoGenerado.SinAgua) continue;
                    s += lamina[jj * n + ii] / 1000f; c++;
                }
            return c > 0 ? s / c : float.NaN;
        }
        int Vertice(int i, int j)
        {
            int k = j * n + i;
            if (indice[k] == 0)
            {
                float h = LaminaEn(i, j);
                verts.Add(AEscena(-campo.Lado / 2 + i * campo.PasoAlturas, -campo.Lado / 2 + j * campo.PasoAlturas, 0) + Vector3.up * h);
                // lo que cubre (m) y la espuma: donde la lámina baja más de un 3 % (del todo desde un 15 %), los rápidos
                float cubre = Mathf.Max(0, h - campo.Alturas[k] / 1000f);
                float caida = 0;
                foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int ii = i + di, jj = j + dj;
                    if (ii < 0 || jj < 0 || ii >= n || jj >= n || lamina[jj * n + ii] == CampoGenerado.SinAgua) continue;
                    caida = Mathf.Max(caida, Mathf.Abs(lamina[jj * n + ii] / 1000f - h) / (campo.PasoAlturas / 1000f));
                }
                datos.Add(new Vector2(cubre, Mathf.InverseLerp(0.03f, 0.15f, caida)));
                indice[k] = verts.Count;
            }
            return indice[k] - 1;
        }
        for (int j = 0; j + 1 < n; j++)
            for (int i = 0; i + 1 < n; i++)
            {
                int k = j * n + i;
                if (lamina[k] == CampoGenerado.SinAgua && lamina[k + 1] == CampoGenerado.SinAgua && lamina[k + n] == CampoGenerado.SinAgua && lamina[k + n + 1] == CampoGenerado.SinAgua) continue;
                int a = Vertice(i, j), b = Vertice(i + 1, j), cc = Vertice(i, j + 1), d = Vertice(i + 1, j + 1);
                // (y al sur es z al norte: el orden de los vértices se invierte para que la cara mire arriba)
                tri.Add(a); tri.Add(b); tri.Add(cc);
                tri.Add(b); tri.Add(d); tri.Add(cc);
            }
        if (verts.Count == 0) return;
        var malla = new Mesh { name = "agua", indexFormat = IndexFormat.UInt32 };
        malla.SetVertices(verts);
        malla.SetUVs(0, datos);
        malla.SetTriangles(tri, 0);
        malla.RecalculateNormals();
        var sombreado = Resources.Load<Shader>("Campo/CampoAgua");
        ConMalla("Agua", malla, sombreado != null ? new Material(sombreado) { name = "agua del campo" } : Tenido(baseMat, new Color(0.25f, 0.55f, 0.62f), "agua"), CapaAgua);
    }

    // Los troncos de los árboles del área jugable, hasta `MaxColliders`: collider (para el héroe fuera de red) y hueco en el NavMesh. Los árboles
    // se dibujan en `VegetacionDelCampo`.
    List<Vector3> Troncos()
    {
        var troncos = new List<Vector3>();
        var padre = new GameObject("Troncos");
        padre.transform.SetParent(transform, false);
        long J = campo.Jugable / 2;
        for (int k = 0; k + 1 < campo.Arboles.Length && troncos.Count < MaxColliders; k += 2)
        {
            long x = campo.Arboles[k], y = campo.Arboles[k + 1];
            if (System.Math.Abs(x) > J || System.Math.Abs(y) > J) continue;
            var p = AEscena(x, y, 0) + Vector3.up * AlturaEn(x, y);
            troncos.Add(p);
            var t = new GameObject("tronco");
            t.transform.SetParent(padre.transform, false);
            t.transform.position = p + Vector3.up * 3f;
            var col = t.AddComponent<CapsuleCollider>();
            col.radius = 0.4f;
            col.height = 6f;
        }
        return troncos;
    }

    // el NavMesh del Terrain jugable: el agua honda, fuera, y cada tronco, un hueco
    void HornearNavMesh(Terrain terreno, List<Vector3> troncos, float minH, float maxH)
    {
        var reloj = System.Diagnostics.Stopwatch.StartNew();
        var fuentes = new List<NavMeshBuildSource>
        {
            new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Terrain, sourceObject = terreno.terrainData, transform = Matrix4x4.TRS(terreno.transform.position, Quaternion.identity, Vector3.one), area = 0 },
        };
        int noAndable = NavMesh.GetAreaFromName("Not Walkable");
        float altoCaja = maxH - minH + 40f, medioY = (minH + maxH) / 2f;
        long S = campo.Jugable + (long)(2 * MargenTerreno * 1000);
        int n = campo.NZonas;
        float celda = campo.CeldaZonas / 1000f;
        // el agua honda, por tiras de celdas seguidas de cada fila
        for (int j = 0; j < n; j++)
        {
            long y = -campo.Lado / 2 + j * campo.CeldaZonas + campo.CeldaZonas / 2;
            if (System.Math.Abs(y) > S / 2) continue;
            for (int i = 0; i < n;)
            {
                if (campo.Zonas[j * n + i] != Zona.Honda) { i++; continue; }
                int i0 = i;
                while (i < n && campo.Zonas[j * n + i] == Zona.Honda) i++;
                float x0 = (-campo.Lado / 2 + (long)i0 * campo.CeldaZonas) / 1000f, x1 = (-campo.Lado / 2 + (long)i * campo.CeldaZonas) / 1000f;
                fuentes.Add(new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.ModifierBox, area = noAndable, size = new Vector3(x1 - x0, altoCaja, celda),
                    transform = Matrix4x4.Translate(new Vector3((x0 + x1) / 2f, medioY, -y / 1000f)),
                });
            }
        }
        foreach (var p in troncos)
            fuentes.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.ModifierBox, area = noAndable, size = new Vector3(1f, 8f, 1f), transform = Matrix4x4.Translate(p) });
        var ajustes = NavMesh.GetSettingsByID(0);
        var limites = new Bounds(new Vector3(0, medioY, 0), new Vector3(S / 1000f, altoCaja, S / 1000f));
        var datos = NavMeshBuilder.BuildNavMeshData(ajustes, fuentes, limites, Vector3.zero, Quaternion.identity);
        if (datos == null) { Debug.LogError("[Campo] no se pudo hornear el NavMesh del campo"); return; }
        navMesh = NavMesh.AddNavMeshData(datos);
        Debug.Log($"[Campo] NavMesh horneado en {reloj.ElapsedMilliseconds} ms ({fuentes.Count} fuentes)");
    }

    // los puntos 1 a 3 (atacante) y 4 a 6 (defensor), sobre el suelo
    void ColocarSpawns(Terrain terreno)
    {
        if (campo.Spawns == null) { Debug.LogWarning("[Campo] el campo no tiene spawns: se quedan los de la escena"); return; }
        Spawns = new float3[6];
        for (int i = 0; i < 6; i++)
        {
            var q = campo.Spawns.Puntos[i];
            var p = AEscena(q.X, q.Y, 0);
            p.y = terreno.SampleHeight(p) + terreno.transform.position.y;
            Spawns[i] = p;
        }
    }

    void OnDestroy()
    {
        if (navMesh.valid) navMesh.Remove();
        Spawns = null;
        if (Actual == campo) Actual = null;
    }
}

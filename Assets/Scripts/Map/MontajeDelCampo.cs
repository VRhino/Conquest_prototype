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

    CampoGenerado campo;
    Rejilla alturas;
    NavMeshDataInstance navMesh;
    readonly List<(Mesh malla, Material[] materiales, List<Matrix4x4> matrices)> arboles = new List<(Mesh, Material[], List<Matrix4x4>)>();

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
        Anillo(baseMat);
        Agua(baseMat);
        var troncos = Arboles(baseMat);
        HornearNavMesh(terreno, troncos, minH, maxH);
        ColocarSpawns(terreno);

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
        Pintar(datos, S);
        var go = Terrain.CreateTerrainGameObject(datos);
        go.name = "Terreno jugable";
        go.layer = CapaSuelo;
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(-lado / 2, minH, -lado / 2);
        var t = go.GetComponent<Terrain>();
        t.drawTreesAndFoliage = false;
        return t;
    }

    // las capas del suelo por la zona: hierba, bosque, arena, roca, camino y lecho
    void Pintar(TerrainData datos, long S)
    {
        var colores = new[] { new Color(0.42f, 0.50f, 0.27f), new Color(0.30f, 0.38f, 0.20f), new Color(0.76f, 0.70f, 0.50f), new Color(0.50f, 0.48f, 0.45f), new Color(0.72f, 0.65f, 0.49f), new Color(0.43f, 0.40f, 0.31f) };
        var capas = new TerrainLayer[colores.Length];
        for (int i = 0; i < capas.Length; i++)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, colores[i]);
            tex.Apply();
            capas[i] = new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(4, 4) };
        }
        datos.terrainLayers = capas;
        int n = datos.alphamapResolution = 512;
        var alfa = new float[n, n, capas.Length];
        for (int zi = 0; zi < n; zi++)
            for (int xi = 0; xi < n; xi++)
            {
                long x = -S / 2 + (xi * 2 + 1) * S / (2 * n), y = S / 2 - (zi * 2 + 1) * S / (2 * n);
                alfa[zi, xi, Capa(ZonaEn(x, y))] = 1;
            }
        datos.SetAlphamaps(0, 0, alfa);
    }

    static int Capa(Zona z) => z switch
    {
        Zona.BosqueClaro or Zona.BosqueCerrado => 1,
        Zona.Arena => 2,
        Zona.Acantilado => 3,
        Zona.Camino => 4,
        Zona.Somero or Zona.Honda => 5,
        _ => 0,
    };

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

    void Anillo(Material baseMat)
    {
        int n = ResolucionAnillo;
        long L = campo.Lado;
        var v = new Vector3[n * n];
        for (int j = 0, k = 0; j < n; j++)
            for (int i = 0; i < n; i++, k++)
            {
                long x = -L / 2 + i * L / (n - 1), y = L / 2 - j * L / (n - 1);
                v[k] = new Vector3(x / 1000f, AlturaEn(x, y) - HundidoAnillo, -y / 1000f);
            }
        var tri = new int[(n - 1) * (n - 1) * 6];
        for (int j = 0, t = 0; j + 1 < n; j++)
            for (int i = 0; i + 1 < n; i++)
            {
                int a = j * n + i;
                tri[t++] = a; tri[t++] = a + n; tri[t++] = a + 1;
                tri[t++] = a + 1; tri[t++] = a + n; tri[t++] = a + n + 1;
            }
        var malla = new Mesh { name = "anillo", indexFormat = IndexFormat.UInt32, vertices = v, triangles = tri };
        malla.RecalculateNormals();
        ConMalla("Anillo", malla, Tenido(baseMat, new Color(0.36f, 0.44f, 0.24f), "anillo"), 0);
    }

    // la lámina del agua: un vértice seco junto al agua toma la de su vecino (y el suelo lo tapa), así la orilla no sale en escalera
    void Agua(Material baseMat)
    {
        int n = campo.NAlturas;
        var lamina = campo.Lamina;
        var indice = new int[n * n];
        var verts = new List<Vector3>();
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
        malla.SetTriangles(tri, 0);
        malla.RecalculateNormals();
        ConMalla("Agua", malla, Tenido(baseMat, new Color(0.25f, 0.55f, 0.62f), "agua"), CapaAgua);
    }

    // los árboles: el modelo de la encina del mapa de mundo, dibujado por instancias; los del área jugable, hasta `MaxColliders`, con tronco
    List<Vector3> Arboles(Material baseMat)
    {
        var troncos = new List<Vector3>();
        var raiz = Resources.Load<GameObject>("MapaMundo/Vegetacion/encina");
        var filtro = raiz != null ? raiz.GetComponentInChildren<MeshFilter>() : null;
        if (filtro == null || filtro.sharedMesh == null) return troncos;
        var malla = filtro.sharedMesh;
        // (la malla, con el giro y la escala que trae del FBX)
        var local = raiz.transform.worldToLocalMatrix * filtro.transform.localToWorldMatrix;
        float bajo = float.MaxValue, arriba = float.MinValue;
        var bb = malla.bounds;
        for (int e = 0; e < 8; e++)
        {
            var esquina = local.MultiplyPoint3x4(bb.center + Vector3.Scale(bb.extents, new Vector3((e & 1) == 0 ? -1 : 1, (e & 2) == 0 ? -1 : 1, (e & 4) == 0 ? -1 : 1)));
            bajo = Mathf.Min(bajo, esquina.y);
            arriba = Mathf.Max(arriba, esquina.y);
        }
        float alto = Mathf.Max(0.01f, arriba - bajo);
        var mats = new Material[malla.subMeshCount];
        for (int s = 0; s < mats.Length; s++)
            mats[s] = Tenido(baseMat, s == 0 ? new Color(0.36f, 0.27f, 0.18f) : new Color(0.25f, 0.40f, 0.18f), "árbol " + s);
        var matrices = new List<Matrix4x4>();
        var padre = new GameObject("Troncos");
        padre.transform.SetParent(transform, false);
        long J = campo.Jugable / 2;
        for (int k = 0; k + 1 < campo.Arboles.Length; k += 2)
        {
            long x = campo.Arboles[k], y = campo.Arboles[k + 1];
            var p = AEscena(x, y, 0) + Vector3.up * AlturaEn(x, y);
            // (lo que varía de un árbol a otro solo se ve: no decide nada)
            uint h = Entero.Azar(x / 100, y / 100, 41);
            float escala = (7f + 5f * (h & 0xFFFF) / 65535f) / alto, giro = (h >> 16) * 360f / 65535f;
            matrices.Add(Matrix4x4.TRS(p - Vector3.up * (bajo * escala), Quaternion.Euler(0, giro, 0), Vector3.one * escala) * local);
            if (troncos.Count < MaxColliders && System.Math.Abs(x) <= J && System.Math.Abs(y) <= J)
            {
                troncos.Add(p);
                var t = new GameObject("tronco");
                t.transform.SetParent(padre.transform, false);
                t.transform.position = p + Vector3.up * 3f;
                var col = t.AddComponent<CapsuleCollider>();
                col.radius = 0.4f;
                col.height = 6f;
            }
        }
        arboles.Add((malla, mats, matrices));
        return troncos;
    }

    void Update()
    {
        // (el servidor de batalla va sin gráficos: nada que dibujar)
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
        foreach (var (malla, mats, matrices) in arboles)
            for (int s = 0; s < mats.Length; s++)
            {
                var rp = new RenderParams(mats[s]) { shadowCastingMode = ShadowCastingMode.On, receiveShadows = true };
                for (int desde = 0; desde < matrices.Count; desde += 1023)
                    Graphics.RenderMeshInstanced(rp, malla, s, matrices, Mathf.Min(1023, matrices.Count - desde), desde);
            }
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
    }
}

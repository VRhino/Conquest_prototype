// El carro de caravana del mapa del mundo (paso 10, tanda 2): Assets/Resources/MapaMundo/CarroDeCaravana.prefab, el carro de Synty
// (SM_Veh_Cart_01) con su eje largo a lo largo de +Z y un punto «Tiro» delante, donde el mapa pone el animal (el caballo del héroe, Mounts/HeroHorse:
// no hay modelo de buey ni de camello). Lo arma el editor; el mapa solo lo carga con Resources.Load. Una captura de comprobación va a
// Logs/mundo/carro.png. En lote: -executeMethod CarroDeCaravanaSetup.Construir
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CarroDeCaravanaSetup
{
    const string Carro = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Vehicles/SM_Veh_Cart_01.prefab";
    const string Destino = "Assets/Resources/MapaMundo/CarroDeCaravana.prefab";

    [MenuItem("Conquest/Mundo/Carro de caravana")]
    public static void Construir()
    {
        var raiz = new GameObject("CarroDeCaravana");
        var carro = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Carro));
        carro.transform.SetParent(raiz.transform, false);
        var b = Limites(carro);
        // El eje largo, a lo largo de Z; y el carro, centrado y sobre el suelo.
        if (b.size.x > b.size.z) carro.transform.localRotation = Quaternion.Euler(0, 90, 0);
        b = Limites(carro);
        carro.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
        b = Limites(carro);
        var tiro = new GameObject("Tiro").transform;
        tiro.SetParent(raiz.transform, false);
        tiro.localPosition = new Vector3(0, 0, b.max.z + 0.8f);
        Directory.CreateDirectory(Path.GetDirectoryName(Destino));
        PrefabUtility.SaveAsPrefabAsset(raiz, Destino);
        Debug.Log($"[CarroDeCaravana] guardado: tamaño {b.size}, tiro en z={tiro.localPosition.z:0.00}");
        Captura(raiz, tiro.position);
        Object.DestroyImmediate(raiz);
    }

    static Bounds Limites(GameObject g)
    {
        var rs = g.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    /// <summary>Una vista de lado y de arriba con el caballo en su sitio, para ver hacia dónde mira el carro.</summary>
    static void Captura(GameObject raiz, Vector3 tiro)
    {
        var caballo = Object.Instantiate(Resources.Load<GameObject>("Mounts/HeroHorse"));
        caballo.transform.position = tiro;
        var cam = new GameObject("cam").AddComponent<Camera>();
        cam.transform.position = new Vector3(9, 6, 2);
        cam.transform.LookAt(new Vector3(0, 0.8f, 1.5f));
        cam.backgroundColor = new Color(0.55f, 0.6f, 0.65f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        var luz = new GameObject("luz").AddComponent<Light>();
        luz.type = LightType.Directional;
        luz.transform.rotation = Quaternion.Euler(50, -30, 0);
        var rt = new RenderTexture(800, 500, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var t = new Texture2D(800, 500, TextureFormat.RGB24, false);
        t.ReadPixels(new Rect(0, 0, 800, 500), 0, 0);
        RenderTexture.active = null;
        Directory.CreateDirectory("Logs/mundo");
        File.WriteAllBytes("Logs/mundo/carro.png", t.EncodeToPNG());
        Object.DestroyImmediate(caballo);
        Object.DestroyImmediate(cam.gameObject);
        Object.DestroyImmediate(luz.gameObject);
    }
}

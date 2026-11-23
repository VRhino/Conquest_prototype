using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Prepara los animales y los carros de las caravanas (tema 7) a partir de lo exportado de Blender (`BronzeAgeArte/mapa/figuras/exportar_caravana.py`),
/// como ficheros nuevos: los modelos (los animales Generic, con su esqueleto de 34 huesos del `Horse_Mount` deformado; los carros sin rig, con las
/// ruedas como hijos), las paletas (Point, sin compresión), un material por paleta (URP Lit) y un prefab por pieza en
/// `Resources/MapaMundo/Caravana`, que es lo que cuece `HornoDeFiguras`.
///   Unity.exe -batchmode -quit -projectPath . -executeMethod CaravanaSetup.Construir
/// </summary>
public static class CaravanaSetup
{
    const string Carpeta = "Assets/Art/Caravana";
    const string Prefabs = "Assets/Resources/MapaMundo/Caravana";
    static readonly (string modelo, string paleta, bool animal)[] Piezas =
    {
        ("Ox_Draught", "Ox_Palette", true), ("Donkey_Pack", "Donkey_Palette", true), ("Camel_Draught", "Camel_Palette", true),
        ("Cart_Basic", "Cart_Palette", false), ("Cart_Reinforced", "Cart_Palette", false), ("Cart_Foundation", "Cart_Palette", false),
        ("Cart_Hero", "Cart_Palette", false),
    };

    [MenuItem("Conquest/Caravana: animales y carros")]
    public static void Construir()
    {
        Directory.CreateDirectory(Prefabs);
        foreach (var paleta in Piezas.Select(p => p.paleta).Distinct())
        {
            var ruta = $"{Carpeta}/Textures/{paleta}.png";
            var ti = (TextureImporter)AssetImporter.GetAtPath(ruta);
            if (ti == null) { Debug.LogError($"[Caravana] falta {ruta}"); return; }
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
            var rutaMat = $"{Carpeta}/Materials/{paleta.Replace("_Palette", "")}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(rutaMat);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = Path.GetFileNameWithoutExtension(rutaMat) };
                AssetDatabase.CreateAsset(mat, rutaMat);
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ruta));
            mat.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(mat);
        }
        var resumen = new System.Text.StringBuilder();
        foreach (var (modelo, paleta, animal) in Piezas)
        {
            var ruta = $"{Carpeta}/Models/{modelo}.fbx";
            var mi = (ModelImporter)AssetImporter.GetAtPath(ruta);
            if (mi == null) { Debug.LogError($"[Caravana] falta {ruta}"); return; }
            mi.animationType = animal ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            if (animal) mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.isReadable = true;   // (el horno mide los aperos y la punta de las varas)
            mi.SaveAndReimport();
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{Carpeta}/Materials/{paleta.Replace("_Palette", "")}.mat");
            var copia = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ruta));
            int tris = 0;
            foreach (var r in copia.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = mat;
                var m = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                if (m != null) tris += m.triangles.Length / 3;
            }
            PrefabUtility.SaveAsPrefabAsset(copia, $"{Prefabs}/{modelo}.prefab");
            resumen.Append($"{modelo} {tris} triángulos, {copia.GetComponentsInChildren<Renderer>(true).Length} piezas; ");
            Object.DestroyImmediate(copia);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Caravana] hecho: {resumen}");
    }
}

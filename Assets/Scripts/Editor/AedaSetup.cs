using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Prepara el Aeda (tema 7) a partir de lo exportado de Blender (`BronzeAgeArte/mapa/figuras/aeda/exportar_aeda.py`), como ficheros nuevos
/// en `Assets/Art/Aeda` y sin tocar nada de las levas: el modelo (Generic, como los de las levas, con el esqueleto de Synty en `Rig_Leva`),
/// los dos clips de las levas que usa copiados tal cual (`Leva_Andar` y `Leva_Reposo_Mirar`, solo marcados en bucle), un controlador con el
/// andar y el reposo por `Speed`, la piel (copia del `FantasyHero_Tono1` de las levas), el material de colores planos (sin máscara de color de
/// bando: el Aeda es neutral) y el prefab en `Resources/MapaMundo/Aeda`, que es lo que cuece `HornoDeFiguras`.
///   Unity.exe -batchmode -quit -projectPath . -executeMethod AedaSetup.Construir
/// </summary>
public static class AedaSetup
{
    const string Carpeta = "Assets/Art/Aeda";
    const string Prefab = "Assets/Resources/MapaMundo/Aeda/Aeda.prefab";
    const string PielDeLasLevas = "Assets/BronzeAgeArte/Comun/Materials/FantasyHero_Tono1.mat";

    [MenuItem("Conquest/Aeda")]
    public static void Construir()
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath($"{Carpeta}/Textures/Aeda_Atlas.png");
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.SaveAndReimport();
        var mi = (ModelImporter)AssetImporter.GetAtPath($"{Carpeta}/Models/Aeda.fbx");
        mi.animationType = ModelImporterAnimationType.Generic;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.importAnimation = false;
        mi.materialImportMode = ModelImporterMaterialImportMode.None;
        mi.SaveAndReimport();
        AnimationClip Clip(string nombre)
        {
            var ruta = $"{Carpeta}/Animations/{nombre}.fbx";
            var ci = (ModelImporter)AssetImporter.GetAtPath(ruta);
            ci.animationType = ModelImporterAnimationType.Generic;
            ci.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            ci.materialImportMode = ModelImporterMaterialImportMode.None;
            var clips = ci.defaultClipAnimations;
            foreach (var c in clips) { c.loopTime = true; c.name = nombre; }
            ci.clipAnimations = clips;
            ci.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
        }
        var andar = Clip("Leva_Andar");
        var reposo = Clip("Leva_Reposo_Mirar");
        // el controlador: reposo (Speed 0) y andar (Speed 1), como la locomoción de las tropas
        var rutaAc = $"{Carpeta}/Animations/AC_Aeda.controller";
        AssetDatabase.DeleteAsset(rutaAc);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(rutaAc);
        ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
        var estado = ac.CreateBlendTreeInController("Locomocion", out var arbol, 0);
        arbol.blendParameter = "Speed";
        arbol.AddChild(reposo, 0);
        arbol.AddChild(andar, 1);
        ac.layers[0].stateMachine.defaultState = estado;
        // los materiales: la piel (copia de la de las levas) y los colores planos
        var rutaPiel = $"{Carpeta}/Materials/Aeda_Piel.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(rutaPiel) == null) AssetDatabase.CopyAsset(PielDeLasLevas, rutaPiel);
        var piel = AssetDatabase.LoadAssetAtPath<Material>(rutaPiel);
        var rutaColores = $"{Carpeta}/Materials/Aeda_Colores.mat";
        var colores = AssetDatabase.LoadAssetAtPath<Material>(rutaColores);
        if (colores == null)
        {
            colores = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Aeda_Colores" };
            AssetDatabase.CreateAsset(colores, rutaColores);
        }
        colores.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Carpeta}/Textures/Aeda_Atlas.png"));
        colores.SetFloat("_Smoothness", 0.1f);
        EditorUtility.SetDirty(colores);
        // el prefab: el modelo con su controlador y sus materiales (la malla: piel y colores, por este orden, como en Blender)
        var copia = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{Carpeta}/Models/Aeda.fbx"));
        // el Animator, en el nodo del esqueleto (`Rig_Leva`) y no en la raíz: los clips de las levas tienen ese nodo como raíz (rutas
        // «Root/Hips/…») y el FBX del Aeda lo trae debajo de la raíz, junto a la malla
        var viejo = copia.GetComponent<Animator>();
        if (viejo != null) Object.DestroyImmediate(viejo);
        var nodo = copia.transform.Find("Rig_Leva");
        var animator = nodo.gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = ac;
        animator.applyRootMotion = false;
        foreach (var r in copia.GetComponentsInChildren<Renderer>(true))
            r.sharedMaterials = r is SkinnedMeshRenderer ? new[] { piel, colores } : new[] { colores };
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Prefab));
        PrefabUtility.SaveAsPrefabAsset(copia, Prefab);
        var tris = copia.GetComponentsInChildren<SkinnedMeshRenderer>(true).Sum(s => s.sharedMesh.triangles.Length / 3);
        Object.DestroyImmediate(copia);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Aeda] hecho: {tris} triángulos de cuerpo, clips {andar.name} ({andar.length:0.00} s, {andar.isLooping}) y {reposo.name} ({reposo.length:0.00} s), " +
                  $"curvas del andar {AnimationUtility.GetCurveBindings(andar).Length}");
    }

    /// <summary>Diagnóstico: las rutas de las curvas del andar frente a las del modelo.</summary>
    public static void Rutas()
    {
        var clip = AssetDatabase.LoadAllAssetsAtPath($"{Carpeta}/Animations/Leva_Andar.fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
        var b = AnimationUtility.GetCurveBindings(clip);
        Debug.Log("[AedaRutas] clip: " + string.Join(" | ", b.Select(x => x.path + "." + x.propertyName).Distinct().Take(12)));
        var m = AssetDatabase.LoadAssetAtPath<GameObject>($"{Carpeta}/Models/Aeda.fbx");
        string Ruta(Transform t) => t == m.transform ? "" : (t.parent == m.transform ? t.name : Ruta(t.parent) + "/" + t.name);
        Debug.Log("[AedaRutas] modelo: " + string.Join(" | ", m.GetComponentsInChildren<Transform>(true).Take(10).Select(t => Ruta(t) + " s" + t.localScale.x)));
    }
}

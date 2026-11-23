using System.Collections.Generic;
using System.IO;
using System.Linq;
using ConquestTactics.Visual;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Prepara los caballos de guerra del tema 7 a partir de lo exportado de Blender (`BronzeAgeArte/mapa/figuras/exportar_guerra.py`), como ficheros
/// nuevos junto al `Horse_Mount`, que no se toca: los tres modelos (Generic, con el avatar del `Horse_Mount` para que valgan sus clips), las
/// paletas de las cinco capas (Point, sin compresión y legibles, para pintar las señas encima), el material de base (URP Lit), un prefab por
/// cuerpo y el registro `Resources/Mounts/CaballosDeGuerra.asset` que usan el horno del mapa y `HeroHorseVisual`.
///   Unity.exe -batchmode -quit -projectPath . -executeMethod CaballosDeGuerraSetup.Construir
/// </summary>
public static class CaballosDeGuerraSetup
{
    const string Carpeta = "Assets/Art/Mounts/Horse";
    static readonly (string nombre, string fichero)[] Cuerpos = { ("iberico", "Iberian"), ("tracio", "Thracian"), ("niseo", "Nisaean") };
    static readonly string[] Capas = { "castana", "torda", "alazana", "negra", "baya" };
    /// <summary>El cuerpo que también tira de carros, y el FBX con sus arreos de tiro (`exportar_caravana.py -- caballo_tiro`).</summary>
    static readonly (string cuerpo, string fichero) TiroDe = ("tracio", "Thracian_Draught");

    [MenuItem("Conquest/Caballos de guerra")]
    public static void Construir()
    {
        var avatar = AssetDatabase.LoadAllAssetsAtPath(CavalrySetup.HorseModel).OfType<Avatar>().FirstOrDefault();
        foreach (var fichero in Cuerpos.Select(c => c.fichero).Append(TiroDe.fichero))
        {
            var ruta = $"{Carpeta}/Models/Horse_War_{fichero}.fbx";
            var mi = (ModelImporter)AssetImporter.GetAtPath(ruta);
            if (mi == null) { Debug.LogError($"[CaballosDeGuerra] falta {ruta}"); return; }
            mi.animationType = ModelImporterAnimationType.Generic;
            if (avatar != null) { mi.avatarSetup = ModelImporterAvatarSetup.CopyFromOther; mi.sourceAvatar = avatar; }
            mi.importAnimation = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.isReadable = false;
            mi.SaveAndReimport();
        }
        var paletas = Capas.Select(capa =>
        {
            var ruta = $"{Carpeta}/Textures/Horse_War_Palette_{capa}.png";
            var ti = (TextureImporter)AssetImporter.GetAtPath(ruta);
            ti.isReadable = true;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
        }).ToArray();
        var rutaMat = $"{Carpeta}/Materials/Horse_War.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(rutaMat);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Horse_War" };
            AssetDatabase.CreateAsset(mat, rutaMat);
        }
        mat.SetTexture("_BaseMap", paletas[0]);
        mat.SetFloat("_Smoothness", 0.15f);
        EditorUtility.SetDirty(mat);
        var registro = ScriptableObject.CreateInstance<CaballosDeGuerra>();
        registro.Material = mat;
        registro.Capas = Capas.Select((c, i) => new CaballosDeGuerra.Capa { Nombre = c, Paleta = paletas[i] }).ToArray();
        Directory.CreateDirectory($"{Carpeta}/Prefabs");
        // (las mallas, llevadas al espacio de las del `Horse_Mount`: su malla y sus huesos están colocados distinto que en el FBX nuevo, así que
        // se cuece la nueva en reposo, se pasa al espacio de la malla vieja y se le dan las bindposes de los huesos viejos; así se cuelga sin más
        // de cualquier copia del `Horse_Mount`)
        var viejo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CavalrySetup.HorseModel));
        var cuerpoViejo = viejo.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name == "Horse");
        var huesosViejos = viejo.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
        Directory.CreateDirectory($"{Carpeta}/Meshes");
        // (las bindposes del caballo de antes, por hueso: con ellas está hecho su clip. Ojo: no son la pose de reposo de sus huesos en el FBX; el
        // cuello, sobre todo, está en otra postura. Por eso no basta con colgar la malla nueva de los huesos en reposo)
        var mallaVieja = cuerpoViejo.sharedMesh;
        var bpViejas = new Dictionary<string, Matrix4x4>();
        for (int i = 0; i < cuerpoViejo.bones.Length; i++) bpViejas[cuerpoViejo.bones[i].name] = mallaVieja.bindposes[i];
        foreach (var b in cuerpoViejo.bones)
        {
            var reposo = b.worldToLocalMatrix * cuerpoViejo.transform.localToWorldMatrix;
            var d = 0f;
            for (int k = 0; k < 16; k++) d = Mathf.Max(d, Mathf.Abs(reposo[k] - bpViejas[b.name][k]));
            if (d > 1e-4f) Debug.Log($"[CaballosDeGuerra] {b.name}: la bindpose de Horse_Mount no es su reposo ({d:0.0000})");

        }
        Mesh Adaptar(SkinnedMeshRenderer nuevo, string nombre)
        {
            // cada vértice, cocido en el reposo del FBX nuevo, se lleva al caballo de antes hueso a hueso: lo que cuelga del hueso i se queda donde
            // está respecto a ese hueso (con su posición y su giro, sin escala: los dos FBX no tienen la misma escala en los huesos), y se mezcla por
            // pesos. Si el reposo de los dos esqueletos coincide, es lo mismo que dejarlo donde está; si no (un giro de más en el cuello, por
            // ejemplo), la malla sigue al hueso del caballo de antes, que es el que mueve el clip. Las bindposes, las del caballo de antes
            var src = nuevo.sharedMesh;
            var cocida = new Mesh();
            nuevo.BakeMesh(cocida, true);
            var aMundo = nuevo.transform.localToWorldMatrix;
            var g = new Matrix4x4[nuevo.bones.Length];
            var bps = new Matrix4x4[nuevo.bones.Length];
            for (int i = 0; i < g.Length; i++)
            {
                var n = nuevo.bones[i].name;
                var hn = nuevo.bones[i];
                var hv = huesosViejos[n];
                bps[i] = bpViejas.TryGetValue(n, out var bp) ? bp : hv.worldToLocalMatrix * cuerpoViejo.transform.localToWorldMatrix;
                var rigidoNuevo = Matrix4x4.TRS(hn.position, hn.rotation, Vector3.one);
                var rigidoViejo = Matrix4x4.TRS(hv.position, hv.rotation, Vector3.one);
                g[i] = cuerpoViejo.transform.worldToLocalMatrix * rigidoViejo * rigidoNuevo.inverse * aMundo;
                var dp = (hn.position - hv.position).magnitude;
                var da = Quaternion.Angle(hn.rotation, hv.rotation);
                if (dp > 0.002f || da > 0.5f) Debug.Log($"[CaballosDeGuerra] {nombre}: el hueso {n} no está en el mismo reposo en los dos FBX: {dp * 100:0.0} cm y {da:0.0}°");
            }
            var vsCocidos = cocida.vertices;
            var nsCocidas = cocida.normals;
            Object.DestroyImmediate(cocida);
            var pesos = src.boneWeights;
            var vs = vsCocidos;
            var ns = nsCocidas;
            var v2 = new Vector3[vs.Length];
            var n2 = new Vector3[vs.Length];
            for (int k = 0; k < vs.Length; k++)
            {
                var w = pesos[k];
                Vector3 p = Vector3.zero, q = Vector3.zero;
                foreach (var (hi, wi) in new[] { (w.boneIndex0, w.weight0), (w.boneIndex1, w.weight1), (w.boneIndex2, w.weight2), (w.boneIndex3, w.weight3) })
                {
                    if (wi <= 0) continue;
                    p += g[hi].MultiplyPoint3x4(vs[k]) * wi;
                    q += g[hi].MultiplyVector(ns[k]) * wi;
                }
                v2[k] = p;
                n2[k] = q.normalized;
            }
            var m = Object.Instantiate(src);
            m.name = nombre;
            m.vertices = v2;
            m.normals = n2;
            m.bindposes = bps;
            m.RecalculateBounds();
            var ruta = $"{Carpeta}/Meshes/{nombre}.asset";
            AssetDatabase.DeleteAsset(ruta);
            AssetDatabase.CreateAsset(m, ruta);
            return m;
        }
        registro.Cuerpos = Cuerpos.Select(c =>
        {
            var modelo = AssetDatabase.LoadAssetAtPath<GameObject>($"{Carpeta}/Models/Horse_War_{c.fichero}.fbx");
            var copia = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
            var malla = copia.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name == "Horse");
            var arreos = copia.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == "Horse_Tack");
            var cuerpo = new CaballosDeGuerra.Cuerpo
            {
                Nombre = c.nombre, Malla = Adaptar(malla, $"Horse_War_{c.fichero}"), Huesos = malla.bones.Select(b => b.name).ToArray(),
                Arreos = arreos != null ? Adaptar(arreos, $"Horse_War_{c.fichero}_Tack") : null,
                HuesosArreos = arreos != null ? arreos.bones.Select(b => b.name).ToArray() : null,
            };
            if (c.nombre == TiroDe.cuerpo)
            {
                var tiro = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{Carpeta}/Models/Horse_War_{TiroDe.fichero}.fbx"));
                var arreosTiro = tiro.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name == "Horse_Tack");
                cuerpo.ArreosDeTiro = Adaptar(arreosTiro, $"Horse_War_{TiroDe.fichero}_Tack");
                cuerpo.HuesosArreosDeTiro = arreosTiro.bones.Select(b => b.name).ToArray();
                Object.DestroyImmediate(tiro);
            }
            foreach (var r in copia.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = mat;
            PrefabUtility.SaveAsPrefabAsset(copia, $"{Carpeta}/Prefabs/Horse_War_{c.fichero}.prefab");
            Object.DestroyImmediate(copia);
            return cuerpo;
        }).ToArray();
        Object.DestroyImmediate(viejo);
        Directory.CreateDirectory("Assets/Resources/Mounts");
        var rutaRegistro = "Assets/Resources/" + CaballosDeGuerra.ResourcePath + ".asset";
        AssetDatabase.DeleteAsset(rutaRegistro);
        AssetDatabase.CreateAsset(registro, rutaRegistro);
        AssetDatabase.SaveAssets();
        Debug.Log($"[CaballosDeGuerra] hecho: {registro.Cuerpos.Length} cuerpos ({string.Join(", ", registro.Cuerpos.Select(c => $"{c.Nombre} {c.Malla.triangles.Length / 3} triángulos, {c.Huesos.Length} huesos"))}), {paletas.Count(p => p != null)} capas, avatar {(avatar != null ? avatar.name : "—")}");
    }
}

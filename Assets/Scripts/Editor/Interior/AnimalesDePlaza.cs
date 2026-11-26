using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ConquestTactics.Interior.Editor
{
    /// <summary>
    /// Prepara los animales de la plaza (la cabra, el perro, la gallina y el gallo; `BronzeAgeArte/interior/animales`), como `CaravanaSetup` los
    /// de las caravanas: los modelos Generic con su avatar (la cabra y el perro llevan el esqueleto de 34 huesos del `Horse_Mount`, así que andan
    /// con su controlador; la gallina, uno propio que mueve `Gallina` por código), las paletas (Point, sin compresión), un material por capa
    /// (URP Lit) y un prefab por animal y capa en `Resources/Ciudad/Animales`.
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod ConquestTactics.Interior.Editor.AnimalesDePlaza.Construir
    /// </summary>
    public static class AnimalesDePlaza
    {
        const string Carpeta = "Assets/Art/Plaza/Animales";
        const string Prefabs = "Assets/Resources/Ciudad/Animales";
        static readonly (string modelo, string[] capas)[] Piezas =
        {
            ("Goat", new[] { "", "_blanca", "_negra" }), ("Dog", new[] { "", "_negro", "_blanco" }), ("Hen", new[] { "", "_blanca", "_negra" }), ("Rooster", new[] { "" }),
        };

        const string Caballo = "Assets/Art/Mounts/Horse/";
        /// <summary>Los que andan con el paso del caballo: su modelo y el nombre de su controlador.</summary>
        static readonly (string modelo, string nombre)[] Bestias =
        {
            (Carpeta + "/Models/Goat.fbx", "Goat"), (Carpeta + "/Models/Dog.fbx", "Dog"),
            ("Assets/Art/Caravana/Models/Donkey_Pack.fbx", "Donkey_Pack"), ("Assets/Art/Caravana/Models/Ox_Draught.fbx", "Ox_Draught"),
        };

        /// <summary>
        /// El paso y el reposo de cada animal, sacados de los del caballo. Las animaciones del caballo no les valen tal cual: llevan la posición
        /// y la escala de cada hueso (las medidas del caballo, y en metros, cuando estos esqueletos van a escala 100) y sus giros son absolutos
        /// (los del caballo en reposo, no los de un perro con el cuello tendido y la cola alta). Aquí se hace, para cada animal, una copia con
        /// solo los giros, y cada giro llevado a su reposo: lo que el caballo gira ese hueso respecto de su reposo, aplicado al reposo del animal.
        /// Con ellas, un controlador igual que el del caballo (`MountGaitBlend` 0 reposo, 1 paso; `MountGaitScale`) en `Resources/Ciudad/Animales`.
        /// </summary>
        static void Pasos()
        {
            Directory.CreateDirectory(Carpeta + "/Animations");
            var caballo = AssetDatabase.LoadAssetAtPath<GameObject>(Caballo + "Models/Horse_Mount.fbx");
            var reposoCaballo = caballo.GetComponentsInChildren<Transform>().GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First().localRotation);
            AnimationClip Del(string fichero) => AssetDatabase.LoadAllAssetsAtPath(Caballo + "Animations/Horse/" + fichero).OfType<AnimationClip>().First(c => !c.name.StartsWith("__"));
            var fuentes = new[] { ("Reposo", Del("Horse_Idle.fbx")), ("Paso", Del("Horse_Walk.fbx")) };
            foreach (var (modelo, nombre) in Bestias)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(modelo);
                if (go == null) { Debug.LogError($"[Animales] falta {modelo}"); continue; }
                var reposo = go.GetComponentsInChildren<Transform>().GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First().localRotation);
                var clips = new AnimationClip[2];
                for (int k = 0; k < 2; k++)
                {
                    var (que, fuente) = fuentes[k];
                    var clip = new AnimationClip { name = $"{nombre}_{que}", frameRate = fuente.frameRate };
                    foreach (var grupo in AnimationUtility.GetCurveBindings(fuente).Where(b => b.propertyName.StartsWith("m_LocalRotation")).GroupBy(b => b.path))
                    {
                        var hueso = grupo.Key.Substring(grupo.Key.LastIndexOf('/') + 1);
                        if (!reposo.TryGetValue(hueso, out var suyo) || !reposoCaballo.TryGetValue(hueso, out var delCaballo)) continue;
                        var c = "xyzw".Select(e => AnimationUtility.GetEditorCurve(fuente, grupo.First(b => b.propertyName.EndsWith("." + e)))).ToArray();
                        var nuevas = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
                        var antes = Quaternion.identity;
                        bool primero = true;
                        foreach (var t in c[0].keys.Select(x => x.time))
                        {
                            var q = new Quaternion(c[0].Evaluate(t), c[1].Evaluate(t), c[2].Evaluate(t), c[3].Evaluate(t)).normalized;
                            var n = suyo * (Quaternion.Inverse(delCaballo) * q);
                            if (!primero && Quaternion.Dot(antes, n) < 0) n = new Quaternion(-n.x, -n.y, -n.z, -n.w);
                            antes = n;
                            primero = false;
                            for (int e = 0; e < 4; e++) nuevas[e].AddKey(t, n[e]);
                        }
                        for (int e = 0; e < 4; e++) clip.SetCurve(grupo.Key, typeof(Transform), "localRotation." + "xyzw"[e], nuevas[e]);
                    }
                    clip.EnsureQuaternionContinuity();
                    AnimationUtility.SetAnimationClipSettings(clip, AnimationUtility.GetAnimationClipSettings(fuente));
                    var ruta = $"{Carpeta}/Animations/{clip.name}.anim";
                    AssetDatabase.DeleteAsset(ruta);
                    AssetDatabase.CreateAsset(clip, ruta);
                    clips[k] = clip;
                }
                var rutaControl = $"{Prefabs}/AC_{nombre}.controller";
                AssetDatabase.DeleteAsset(rutaControl);
                var control = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(rutaControl);
                control.AddParameter("MountGaitBlend", AnimatorControllerParameterType.Float);
                control.AddParameter(new AnimatorControllerParameter { name = "MountGaitScale", type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
                var estado = control.CreateBlendTreeInController("Gait", out var arbol);
                arbol.blendParameter = "MountGaitBlend";
                arbol.AddChild(clips[0], 0);
                arbol.AddChild(clips[1], 1);
                estado.speedParameter = "MountGaitScale";
                estado.speedParameterActive = true;
                EditorUtility.SetDirty(control);
                Debug.Log($"[Animales] {nombre}: su reposo y su paso, de {AnimationUtility.GetCurveBindings(clips[1]).Length / 4} huesos");
            }
        }

        [MenuItem("Conquest/Interior/Animales de la plaza", false, 310)]
        public static void Construir()
        {
            Directory.CreateDirectory(Prefabs);
            Directory.CreateDirectory(Carpeta + "/Materials");
            AssetDatabase.Refresh();
            foreach (var (modelo, capas) in Piezas)
            {
                var ruta = $"{Carpeta}/Models/{modelo}.fbx";
                var mi = (ModelImporter)AssetImporter.GetAtPath(ruta);
                if (mi == null) { Debug.LogError($"[Animales] falta {ruta}"); return; }
                mi.animationType = ModelImporterAnimationType.Generic;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.importAnimation = false;
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
                mi.SaveAndReimport();
                foreach (var capa in capas)
                {
                    var png = $"{Carpeta}/Textures/{modelo}_Palette{capa}.png";
                    var ti = (TextureImporter)AssetImporter.GetAtPath(png);
                    if (ti == null) { Debug.LogError($"[Animales] falta {png}"); return; }
                    ti.filterMode = FilterMode.Point;
                    ti.textureCompression = TextureImporterCompression.Uncompressed;
                    ti.mipmapEnabled = false;
                    ti.wrapMode = TextureWrapMode.Clamp;
                    ti.SaveAndReimport();
                    var rutaMat = $"{Carpeta}/Materials/{modelo}{capa}.mat";
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(rutaMat);
                    if (mat == null)
                    {
                        mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = modelo + capa };
                        AssetDatabase.CreateAsset(mat, rutaMat);
                    }
                    mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(png));
                    mat.SetFloat("_Smoothness", 0.1f);
                    EditorUtility.SetDirty(mat);

                    var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ruta));
                    foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
                    var caja = new Bounds(go.transform.position, Vector3.zero);
                    foreach (var r in go.GetComponentsInChildren<Renderer>()) caja.Encapsulate(r.bounds);
                    PrefabUtility.SaveAsPrefabAsset(go, $"{Prefabs}/{modelo}{capa}.prefab");
                    Debug.Log($"[Animales] {modelo}{capa}: {caja.size.x:0.00} × {caja.size.y:0.00} × {caja.size.z:0.00} m, suelo {caja.min.y:0.00}, giro {go.transform.eulerAngles}");
                    Object.DestroyImmediate(go);
                }
            }
            Pasos();
            AssetDatabase.SaveAssets();
        }
    }
}

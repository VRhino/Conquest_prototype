// Monta la escena de demostración de animaciones de BronzeAge (Assets/Scenes/BronzeAge/DemoAnimaciones.unity).
// Menú: BronzeAge/Demo de animaciones/Crear o actualizar escena. En batchmode: -executeMethod
// BronzeAge.Demo.DemoAnimacionesEditor.CrearEscenaBatch (además saca una captura en la carpeta temporal del sistema).
// Solo toca ficheros nuevos de Assets/BronzeAgeArte/Demo y la escena de la demo.
// Los puestos NO van en la escena: los monta DemoAnimaciones al darle a Play, desde Demo/puestos.json (ver ese script).
// Este menú solo hace falta si se rompe la escena (suelo, luz, cámara y el componente de la galería).
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace BronzeAge.Demo
{
    public static class DemoAnimacionesEditor
    {
        const string RAIZ = "Assets/BronzeAgeArte/Demo";
        const string ESCENA = "Assets/Scenes/BronzeAge/DemoAnimaciones.unity";

        // materiales de las figuras: nombre en el FBX -> material de la demo
        static readonly Dictionary<string, string> REMAPEO = new Dictionary<string, string>
        {
            { "Tez_Rig_C_lambert2.002", "FantasyHero_Tono3" },
            { "Lenadores_Atlas", "Lenadores_Atlas" },
            { "Tez_Rig_C_madera", "Lenadores_Atlas" },
            { "Mineros_Atlas", "Mineros_Atlas" },
            { "Granjeros_Atlas", "Granjeros_Atlas" },
            { "Lanceros_Atlas", "Lanceros_Atlas" },
        };

        const float SEPARACION = 3f;

        /// <summary>Reimporta los clips para que tomen el avatar del modelo (si el modelo se importó después que ellos).</summary>
        static void ReimportarClips()
        {
            // primero los modelos (los clips de diagnóstico copian su avatar), luego los clips
            var todos = Directory.GetFiles(RAIZ, "*.fbx", SearchOption.AllDirectories).Select(r => r.Replace('\\', '/'))
                .Concat(RutasBronzeAge.Modelos()).Concat(RutasBronzeAge.Clips())
                .OrderBy(r => RutasBronzeAge.EsModelo(r) || r.Contains("/Modelo/") ? 0 : 1).ToList();
            foreach (var ruta in todos)
                AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>Quita de la escena de la demo los puestos guardados de versiones viejas (ahora salen de puestos.json:
        /// en Play, y fuera de Play como vista previa que no se guarda) y la guarda. Para batchmode.</summary>
        public static void LimpiarEscena()
        {
            var escena = EditorSceneManager.OpenScene(ESCENA, OpenSceneMode.Single);
            int n = 0;
            foreach (var r in escena.GetRootGameObjects())
                if (r.name.StartsWith("Puesto ") && (r.hideFlags & HideFlags.DontSave) == 0) { Object.DestroyImmediate(r); n++; }
            foreach (var d in Object.FindObjectsByType<DemoAnimaciones>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                d.puestos.Clear();                   // (la lista de los puestos viejos guardada en el componente)
                EditorUtility.SetDirty(d);
            }
            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
            Debug.Log($"[DemoAnimaciones] Escena limpia: {n} puestos guardados quitados");
        }

        [MenuItem("BronzeAge/Demo de animaciones/Crear o actualizar escena")]
        public static void CrearEscena()
        {
            ReimportarClips();
            Materiales();
            var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var luz = new GameObject("Sol").AddComponent<Light>();
            luz.type = LightType.Directional;
            luz.intensity = 1.2f;
            luz.shadows = LightShadows.Soft;
            luz.transform.rotation = Quaternion.Euler(45, -35, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);

            var suelo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            suelo.name = "Suelo";                                   // (DemoAnimaciones lo ajusta a los puestos al arrancar)
            suelo.transform.localScale = new Vector3(4f, 1, 4f);
            suelo.GetComponent<Renderer>().sharedMaterial = Mat("Suelo");

            var cam = new GameObject("Camara").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.62f, 0.66f, 0.70f);
            cam.transform.position = new Vector3(SEPARACION, 1.6f, -6f);
            cam.transform.rotation = Quaternion.Euler(8, 0, 0);
            cam.gameObject.AddComponent<AudioListener>();
            cam.gameObject.AddComponent<CamaraPaseo>();

            var demo = new GameObject("DemoAnimaciones").AddComponent<DemoAnimaciones>();

            Directory.CreateDirectory(Path.GetDirectoryName(ESCENA));
            EditorSceneManager.SaveScene(escena, ESCENA);
            Debug.Log($"[DemoAnimaciones] Escena guardada: {ESCENA} (los puestos salen de {DemoAnimaciones.LISTA} al darle a Play)");
        }

        public static void CrearEscenaBatch()
        {
            AssetDatabase.Refresh();
            CrearEscena();
            Informe();
            MedirTodo();
            Captura();
        }

        // ------------------------------------------------------------------ materiales
        static Material Mat(string nombre) => AssetDatabase.LoadAssetAtPath<Material>(RutasBronzeAge.Buscar(nombre, ".mat") ?? "");

        static void Materiales()
        {
            Directory.CreateDirectory($"{RAIZ}/Materiales");
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            Material Crear(string nombre, Shader sh)
            {
                var ruta = RutasBronzeAge.Buscar(nombre, ".mat") ?? $"{RAIZ}/Materiales/{nombre}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
                if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, ruta); }
                return m;
            }
            // piel: copia de FantasyHero.mat de Synty con el tono 3 (y la barba en la misma proporción)
            var synty = AssetDatabase.LoadAssetAtPath<Material>("Assets/Synty/PolygonFantasyHeroCharacters/Materials/FantasyHero.mat");
            var tono = Crear("FantasyHero_Tono3", synty.shader);
            tono.CopyPropertiesFromMaterial(synty);
            var piel0 = synty.GetColor("_Color_Skin");
            var piel = new Color(0.730f, 0.518f, 0.401f);
            tono.SetColor("_Color_Skin", piel);
            var barba = synty.GetColor("_Color_Stubble");
            tono.SetColor("_Color_Stubble", new Color(barba.r * piel.r / piel0.r, barba.g * piel.g / piel0.g, barba.b * piel.b / piel0.b));
            EditorUtility.SetDirty(tono);

            // atlas de cada tropa: URP Lit, filtro Point, sin mipmaps (el polvo de los mineros va en degradados de 32 px), poco brillo
            foreach (var nombre in new[] { "Lenadores_Atlas", "Mineros_Atlas", "Granjeros_Atlas", "Lanceros_Atlas" })
            {
                var rutaAtlas = RutasBronzeAge.Buscar(nombre, ".png") ?? "";
                var ti = (TextureImporter)AssetImporter.GetAtPath(rutaAtlas);
                if (ti == null) continue;
                if (ti.filterMode != FilterMode.Point || ti.mipmapEnabled)
                {
                    ti.filterMode = FilterMode.Point; ti.mipmapEnabled = false; ti.SaveAndReimport();
                }
                var atlas = Crear(nombre, lit);
                atlas.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(rutaAtlas));
                atlas.SetFloat("_Smoothness", 0.15f);
                EditorUtility.SetDirty(atlas);
            }

            var suelo = Crear("Suelo", lit); suelo.SetColor("_BaseColor", new Color(0.55f, 0.53f, 0.48f)); suelo.SetFloat("_Smoothness", 0.05f);
            var peana = Crear("Peana", lit); peana.SetColor("_BaseColor", new Color(0.40f, 0.36f, 0.30f)); peana.SetFloat("_Smoothness", 0.05f);
            AssetDatabase.SaveAssets();

            // remapeo de los materiales de los modelos
            foreach (var ruta in RutasBronzeAge.Modelos())
            {
                var mi = (ModelImporter)AssetImporter.GetAtPath(ruta.Replace('\\', '/'));
                bool cambio = false;
                foreach (var kv in REMAPEO)
                {
                    var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key);
                    var m = Mat(kv.Value);
                    if (m != null && (!mi.GetExternalObjectMap().TryGetValue(id, out var ya) || ya != m))
                    {
                        mi.AddRemap(id, m); cambio = true;
                    }
                }
                if (cambio) mi.SaveAndReimport();
            }
        }

        // ------------------------------------------------------------------ comprobación en batchmode
        static void Informe()
        {
            foreach (var ruta in RutasBronzeAge.Clips())
            {
                foreach (var c in AssetDatabase.LoadAllAssetsAtPath(ruta.Replace('\\', '/')).OfType<AnimationClip>())
                {
                    if (c.name.StartsWith("__preview__")) continue;
                    var b = AnimationUtility.GetCurveBindings(c);
                    Debug.Log($"[DemoAnimaciones] Clip {c.name}: {c.length:0.000} s, humanoide {c.humanMotion}, bucle {c.isLooping}, " +
                              $"{b.Length} curvas, Weapon_Carrier: {b.Any(x => x.path.EndsWith("Weapon_Carrier"))}");
                }
            }
            foreach (var ruta in RutasBronzeAge.Modelos())
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(ruta.Replace('\\', '/'));
                var av = go.GetComponent<Animator>()?.avatar;
                Debug.Log($"[DemoAnimaciones] Modelo {go.name}: avatar {(av != null ? av.name : "ninguno")}, humano {av != null && av.isHuman}, válido {av != null && av.isValid}");
            }
        }

        // medidas frente a Blender: cadera-cabeza del hacha y altura de la cabeza en el contacto (f15) y abajo (f20)
        static string Medir(GameObject prefab, AnimationClip clip)
        {
            var go = (GameObject)Object.Instantiate(prefab);
            go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity;
            var anim = go.GetComponent<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();
            anim.applyRootMotion = false;
            var g = UnityEngine.Playables.PlayableGraph.Create("medir");
            var c = UnityEngine.Animations.AnimationClipPlayable.Create(g, clip);
            c.SetApplyFootIK(false);
            UnityEngine.Animations.AnimationPlayableOutput.Create(g, "a", anim).SetSourcePlayable(c);
            g.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            g.Play();
            Transform T(string n) => go.GetComponentsInChildren<Transform>().First(t => t.name == n);
            var cab = go.GetComponentsInChildren<Renderer>().First(r => r.name.Contains("cabeza"));
            string txt = "";
            foreach (var f in new[] { 15, 20 })
            {
                clip.SampleAnimation(go, (f - 1) / 30f);
                var h = cab.bounds.center;
                txt += $" f{f}: cadera-cabeza {(h - T("Hips").position).magnitude:0.00} m, altura cabeza {h.y:0.00} m, cadera-mano {(T("Hand_R").position - T("Hips").position).magnitude:0.00} m;";
            }
            g.Destroy();
            Object.DestroyImmediate(go);
            return txt;
        }

        static void MedirTodo()
        {
            foreach (var (carpeta, modelo) in new[] { ("Animaciones", "Modelos"), ("Diagnostico/AvatarPropio", "Diagnostico/Modelo"), ("Diagnostico/AvatarSynty", "Diagnostico/Modelo") })
            {
                var pref = AssetDatabase.LoadAssetAtPath<GameObject>($"{RAIZ}/{modelo}/Lenador_C_1m.fbx");
                if (pref == null || !Directory.Exists($"{RAIZ}/{carpeta}")) continue;
                foreach (var ruta in Directory.GetFiles($"{RAIZ}/{carpeta}", "*.fbx"))
                {
                    var clip = AssetDatabase.LoadAllAssetsAtPath(ruta.Replace('\\', '/')).OfType<AnimationClip>().FirstOrDefault(x => !x.name.StartsWith("__preview__"));
                    if (clip == null) { Debug.Log($"[DemoAnimaciones] MEDIDA {carpeta}/{Path.GetFileName(ruta)}: sin clip (importación fallida)"); continue; }
                    Debug.Log($"[DemoAnimaciones] MEDIDA {carpeta}/{Path.GetFileNameWithoutExtension(ruta)}:{Medir(pref, clip)}");
                }
            }
            // longitudes de hueso: nuestra figura frente a ModularCharacters de Synty (en reposo)
            var nuestra = AssetDatabase.LoadAssetAtPath<GameObject>($"{RAIZ}/Diagnostico/Modelo/Lenador_C_1m.fbx");
            var synty = AssetDatabase.LoadAssetAtPath<GameObject>(DemoAnimacionesImportacion.AVATAR_SYNTY);
            if (nuestra != null && synty != null)
            {
                var a = (GameObject)Object.Instantiate(nuestra); var b = (GameObject)Object.Instantiate(synty);
                var A = a.GetComponent<Animator>(); var B = b.GetComponent<Animator>();
                var dif = new List<string>();
                foreach (HumanBodyBones hb in System.Enum.GetValues(typeof(HumanBodyBones)))
                {
                    if (hb == HumanBodyBones.LastBone) continue;
                    var ta = A.GetBoneTransform(hb); var tb = B.GetBoneTransform(hb);
                    if (ta == null || tb == null || ta.parent == null || tb.parent == null) continue;
                    float la = (ta.position - ta.parent.position).magnitude, lb = (tb.position - tb.parent.position).magnitude;
                    if (Mathf.Abs(la - lb) > 0.01f) dif.Add($"{hb} {la:0.000}/{lb:0.000}");
                }
                Debug.Log($"[DemoAnimaciones] HUESOS (nuestra/Synty, m, si difieren > 1 cm): {(dif.Count == 0 ? "ninguno" : string.Join("; ", dif))}" +
                          $" · escala raíz {a.transform.lossyScale.x:0.###}/{b.transform.lossyScale.x:0.###}");
                Object.DestroyImmediate(a); Object.DestroyImmediate(b);
            }
        }

        static void Captura()
        {
            var demo = Object.FindFirstObjectByType<DemoAnimaciones>();
            var cam = Camera.main;
            if (demo == null || cam == null || demo.puestos.Count == 0) { Debug.Log("[DemoAnimaciones] Captura: sin puestos en modo edición"); return; }
            // pose del fotograma 15 (contacto) en todas las figuras, para la captura
            foreach (var p in demo.puestos)
                if (p.figura != null && p.clip != null) p.clip.SampleAnimation(p.figura.gameObject, 14f / 30f);
            var rt = new RenderTexture(1600, 900, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            tex.Apply();
            var ruta = Path.Combine(Path.GetTempPath(), "DemoAnimaciones.png");
            File.WriteAllBytes(ruta, tex.EncodeToPNG());
            cam.targetTexture = null;
            RenderTexture.active = null;
            Debug.Log("[DemoAnimaciones] Captura: " + ruta);
            // segunda captura sin sombras (para ver si lo oscuro del suelo es del mapa de sombras en batchmode)
            var sol = Object.FindFirstObjectByType<Light>();
            var antes = sol.shadows; sol.shadows = LightShadows.None;
            cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
            File.WriteAllBytes(Path.Combine(Path.GetTempPath(), "DemoAnimaciones_sin_sombras.png"), tex.EncodeToPNG());
            cam.targetTexture = null; RenderTexture.active = null; sol.shadows = antes;
        }
    }

    /// <summary>Importación de los FBX de la demo.
    /// Modelos/ y Animaciones/: Generic, sin retarget: la figura reproduce el clip con su propio esqueleto, como en Blender.
    /// Diagnostico/: copias en Humanoid para medir el retarget (avatar propio, o el de ModularCharacters de Synty como
    /// hace Conquest con sus clips).</summary>
    public class DemoAnimacionesImportacion : AssetPostprocessor
    {
        const string RAIZ = "Assets/BronzeAgeArte/Demo/";
        const string DIAG = RAIZ + "Diagnostico/";
        public const string AVATAR_SYNTY = "Assets/Synty/PolygonFantasyHeroCharacters/Models/ModularCharacters.fbx";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(RAIZ) && !RutasBronzeAge.EsModelo(assetPath) && !RutasBronzeAge.EsClip(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            bool esModelo = RutasBronzeAge.EsModelo(assetPath) || assetPath.StartsWith(DIAG + "Modelo/");
            mi.importAnimation = !esModelo;
            mi.materialImportMode = esModelo ? ModelImporterMaterialImportMode.ImportStandard : ModelImporterMaterialImportMode.None;
            mi.importCameras = mi.importLights = false;
            mi.preserveHierarchy = true;      // que el clip (solo esqueleto) conserve el nodo Rig_C, como el modelo
            if (!assetPath.StartsWith(DIAG))
            {
                mi.animationType = ModelImporterAnimationType.Generic;
                mi.avatarSetup = esModelo ? ModelImporterAvatarSetup.CreateFromThisModel : ModelImporterAvatarSetup.NoAvatar;
                return;
            }
            mi.animationType = ModelImporterAnimationType.Human;
            Avatar fuente = null;
            if (assetPath.StartsWith(DIAG + "AvatarPropio/"))
                fuente = AssetDatabase.LoadAllAssetsAtPath(DIAG + "Modelo/Lenador_C_1m.fbx").OfType<Avatar>().FirstOrDefault();
            else if (assetPath.StartsWith(DIAG + "AvatarSynty/"))
                fuente = AssetDatabase.LoadAllAssetsAtPath(AVATAR_SYNTY).OfType<Avatar>().FirstOrDefault();
            if (fuente != null) { mi.avatarSetup = ModelImporterAvatarSetup.CopyFromOther; mi.sourceAvatar = fuente; }
            else mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        }

        void OnPreprocessAnimation()
        {
            if (!RutasBronzeAge.EsClip(assetPath) && !assetPath.StartsWith(DIAG)) return;
            if (assetPath.StartsWith(DIAG + "Modelo/")) return;
            var mi = (ModelImporter)assetImporter;
            var clips = mi.clipAnimations.Length > 0 ? mi.clipAnimations : mi.defaultClipAnimations;
            foreach (var c in clips)
            {
                // en bucle solo lo que se repite (reposo, andar, correr); los ataques, el golpe y la muerte, no (Animator de batalla)
                c.loopTime = assetPath.Contains("Reposo") || assetPath.Contains("Andar") || assetPath.Contains("Correr") || (assetPath.Contains("Rodilla") && !assetPath.Contains("Estocada")) || assetPath.Contains("Fila_De_Pie");      // (Lancero_Estocada_Fila no: no lleva «Fila_De_Pie»)
                c.lockRootRotation = true; c.keepOriginalOrientation = true;
                c.lockRootHeightY = true; c.keepOriginalPositionY = true;
                c.lockRootPositionXZ = true; c.keepOriginalPositionXZ = true;
            }
            mi.clipAnimations = clips;
        }
    }

    /// <summary>Rehace la vista previa de la demo (fuera de Play) cuando cambia puestos.json o entra un FBX de la demo.</summary>
    [InitializeOnLoad]
    public class DemoAnimacionesVigilante : AssetPostprocessor
    {
        static DemoAnimacionesVigilante()
        {
            // al abrir una escena (o al volver de Play) la vista previa se monta en cuanto la escena está cargada
            EditorSceneManager.sceneOpened += (escena, modo) => DemoAnimaciones.VistaPreviaAhoraOPedir();
            EditorApplication.playModeStateChanged += estado =>
            {
                if (estado == PlayModeStateChange.EnteredEditMode) DemoAnimaciones.VistaPreviaAhoraOPedir();
            };
        }

        static void OnPostprocessAllAssets(string[] importados, string[] borrados, string[] movidos, string[] desde)
        {
            bool toca = importados.Concat(borrados).Concat(movidos)
                .Any(r => (r.StartsWith(DemoAnimaciones.RAIZ + "/") || RutasBronzeAge.EsModelo(r) || RutasBronzeAge.EsClip(r) || (r.StartsWith(RutasBronzeAge.ESCUADRAS + "/") && r.EndsWith(".mat")))
                          && (r.EndsWith(".json") || r.EndsWith(".fbx") || r.EndsWith(".mat")));
            // cualquier fichero de nuestras carpetas que entre, se mueva o se borre: el índice de RutasBronzeAge se rehace
            if (importados.Concat(borrados).Concat(movidos).Concat(desde).Any(r => r.StartsWith(RutasBronzeAge.ESCUADRAS + "/")
                    || r.StartsWith(RutasBronzeAge.COMUN + "/") || r.StartsWith(RutasBronzeAge.DEMO + "/")))
                RutasBronzeAge.Olvidar();
            if (toca)
                DemoAnimaciones.PedirVistaPrevia();         // una sola vez por tanda, al acabar de importar
        }
    }
}

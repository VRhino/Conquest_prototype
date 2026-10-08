using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using BronzeAge.RegionalMapTool;

namespace Conquest.WorldMap.Editor
{
    public static class WorldMapPreviewBuilder
    {
        public const string ScenePath = "Assets/Scenes/WorldMapTools/WorldMapPreview.unity";

        [InitializeOnLoadMethod]
        static void RefreshLoadedPreviewAfterCompilation()
        {
            EditorApplication.delayCall+=RefreshLoadedPreview;
        }

        static void RefreshLoadedPreview()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)return;
            var preview=UnityEngine.Object.FindFirstObjectByType<WorldMapPreviewController>();
            if(!preview || !preview.worldRoot || preview.gameObject.scene.path!=ScenePath)return;
            var renderer=preview.worldRoot.GetComponent<WorldMapSyntyRenderer>();
            if(preview.artProfile && renderer && renderer.instances && renderer.instances.revision==WorldMapSyntyBaker.Revision)return;
            WorldCampaignArt.Apply(preview);
            EditorSceneManager.MarkSceneDirty(preview.gameObject.scene);
            Debug.Log("HISTORICAL_PREVIEW_REFRESHED: perfil del Mediterráneo oriental aplicado.");
        }
        [MenuItem("Tools/BronzeAge/Abrir preview del mundo regional")]
        public static void Open()
        {
            if (Application.isPlaying) { Debug.LogWarning("Sal de Play antes de abrir la preview."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) { Debug.LogError("No existe la escena. Usa Crear preview desde paquete."); return; }
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var preview = UnityEngine.Object.FindFirstObjectByType<WorldMapPreviewController>();
            if (preview && preview.worldRoot) { ConfigureMaterials(preview.worldRoot); WorldCampaignArt.Apply(preview); }
            EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("Tools/BronzeAge/Crear preview desde paquete regional")]
        public static void Choose()
        {
            if (Application.isPlaying) return;
            var file = EditorUtility.OpenFilePanel("Manifest regional", "", "json");
            if (!string.IsNullOrEmpty(file) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Build(file, false);
        }

        public static string Build(string manifest, bool additive = true, string destinationPath = null)
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, additive ? NewSceneMode.Additive : NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            try {
                BronzeAgeRegionalImporter.Import(manifest);
                var root = Selection.activeGameObject;
                if(destinationPath != null) root.name += "_" + Path.GetFileNameWithoutExtension(destinationPath);
                var controller = new GameObject("World Map Preview").AddComponent<WorldMapPreviewController>();
                controller.worldRoot = root.transform;
                var camera = new GameObject("World Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.08f, .14f, .18f);
                camera.nearClipPlane = 1; camera.farClipPlane = 60000;
                controller.mapCamera = camera;
                var data = JsonUtility.FromJson<PreviewDimensions>(File.ReadAllText(manifest));
                controller.worldSize = new Vector2(data.ancho, data.alto);
                controller.FitWorld();
                var sun = new GameObject("World Sun").AddComponent<Light>();
                sun.type = LightType.Directional; sun.intensity = 1.2f;
                sun.transform.rotation = Quaternion.Euler(55, -25, 0);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.7f, .7f, .7f);
                ConfigureMaterials(root.transform);
                WorldCampaignArt.Apply(controller,destinationPath==null?null:Path.GetDirectoryName(destinationPath).Replace("\\","/"));
                if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
                string destination = destinationPath ?? AssetDatabase.GenerateUniqueAssetPath(ScenePath);
                EditorSceneManager.SaveScene(scene, destination);
                AssetDatabase.SaveAssets();
                Debug.Log("Preview creada: " + destination + ". Abrir sola y pulsar Play.");
                return destination;
            } finally { if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
        }

        private static void ConfigureMaterials(Transform root)
        {
            var terrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (!terrainShader) return; // Disposable validation project may use the built-in pipeline.
            const string path = "Assets/WorldMap/WorldTerrain.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(terrainShader); AssetDatabase.CreateAsset(material, path); }
            foreach (var terrain in root.GetComponentsInChildren<Terrain>()) { terrain.materialTemplate = material; EditorUtility.SetDirty(terrain); }
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit) foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) {
                var mat = renderer.sharedMaterial;
                if (mat && mat.shader && mat.shader.name == "Unlit/Color") {
                    var color = mat.color; mat.shader = unlit; mat.color = color; EditorUtility.SetDirty(mat);
                }
            }
            AssetDatabase.SaveAssets();
        }

        // Reproducible validation in a disposable project, without opening the user's working scene.
        public static void BatchBuild()
        {
            try {
                string manifest = Environment.GetEnvironmentVariable("BRONZEAGE_PREVIEW_MANIFEST");
                string path = Build(manifest, false);
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var terrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
                var markers = UnityEngine.Object.FindObjectsByType<BronzeAgeRegionalMarker>(FindObjectsSortMode.None);
                if (terrains.Length != 48 || markers.Length != 1354) throw new Exception("Unexpected fixture counts");
                var camera = UnityEngine.Object.FindFirstObjectByType<WorldMapPreviewController>();
                if (!camera || !camera.mapCamera || !camera.worldRoot) throw new Exception("Missing preview references");
                camera.FitWorld();
                if (camera.mapCamera.orthographic || camera.mapCamera.transform.forward.y >= 0) throw new Exception("Preview must start in downward-facing 3D perspective");
                Debug.Log("WORLD_PREVIEW_VALIDATED: 48 terrains, 1354 resources, camera and serialized scene references.");
                EditorApplication.Exit(0);
            } catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        [Serializable] private class PreviewDimensions { public float ancho, alto; }
    }
}

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Conquest.SettlementPreview.Editor
{
    public static class SettlementPreviewBuilder
    {
        public const string ScenePath = "Assets/Scenes/SettlementPreview.unity";
        [MenuItem("Tools/BronzeAge/Crear o abrir asentamiento de prueba")]
        public static void Create()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Salir de Play antes de crear la escena.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                return;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var root = new GameObject("BronzeAge Settlement Preview");
            var preview = root.AddComponent<SettlementPreview>();
            preview.source = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/SettlementPreview/Fixtures/settlement2.json");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP Lit no disponible.");
            const string materialPath = "Assets/SettlementPreview/Surface.mat";
            preview.surfaceMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!preview.surfaceMaterial) { preview.surfaceMaterial = new Material(shader); AssetDatabase.CreateAsset(preview.surfaceMaterial, materialPath); }
            var cam = new GameObject("Settlement Camera").AddComponent<Camera>();
            cam.tag = "MainCamera"; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.12f,.17f,.12f);
            cam.gameObject.AddComponent<AudioListener>(); preview.viewCamera = cam;
            var sun = new GameObject("Settlement Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(50, -35, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.65f,.65f,.65f);
            preview.Rebuild();
            // Runtime generation keeps the saved scene small and avoids nonserialized property-block colors.
            UnityEngine.Object.DestroyImmediate(preview.generated.gameObject); preview.generated = null;
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("SettlementPreview creada. Abrir sola y pulsar Play para generar el fixture.");
        }
        [MenuItem("Tools/BronzeAge/Validar fixture de asentamiento")]
        public static void Validate()
        {
            int fixtureCount = 0, settlementCount = 0, footprintCount = 0, wallPieceCount = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/SettlementPreview/Fixtures" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
                var source = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                var json = Newtonsoft.Json.Linq.JObject.Parse(source.text);
                var settlements = json["asentamientos"] as Newtonsoft.Json.Linq.JArray
                    ?? throw new Exception(path + ": falta asentamientos.");
                var layouts = json["trazadoPorAsentamiento"] as Newtonsoft.Json.Linq.JObject
                    ?? throw new Exception(path + ": falta trazadoPorAsentamiento.");
                foreach (var city in settlements.OfType<Newtonsoft.Json.Linq.JObject>())
                {
                    string id = (string)city["id"];
                    var layout = layouts[id] as Newtonsoft.Json.Linq.JObject
                        ?? throw new Exception(path + ": falta trazado para " + id + ".");
                    var footprints = layout["huellas"] as Newtonsoft.Json.Linq.JObject
                        ?? throw new Exception(path + ": faltan huellas.");
                    int localBuildings = ((Newtonsoft.Json.Linq.JArray)city["edificios"])
                        .OfType<Newtonsoft.Json.Linq.JObject>().Count(e => (string)e["ambito"] != "mapa");
                    foreach (var p in footprints.Properties()) { SettlementPreview.ReadRect(p.Value); footprintCount++; }
                    if (footprints.Properties().Count() != localBuildings)
                        throw new Exception(path + ": huellas " + footprints.Properties().Count() + " != edificios locales " + localBuildings + ".");
                    foreach (string key in new[] { "calles", "caminos" })
                        foreach (var rect in layout[key] as Newtonsoft.Json.Linq.JArray ?? throw new Exception(path + ": falta " + key + ".")) SettlementPreview.ReadRect(rect);
                    foreach (var enclosure in (layout["murallas"] as Newtonsoft.Json.Linq.JArray ?? new Newtonsoft.Json.Linq.JArray()).OfType<Newtonsoft.Json.Linq.JObject>())
                        foreach (string key in new[] { "muro", "puertas", "torres" })
                            foreach (var rect in enclosure[key] as Newtonsoft.Json.Linq.JArray ?? new Newtonsoft.Json.Linq.JArray()) { SettlementPreview.ReadRect(rect); wallPieceCount++; }
                    settlementCount++;
                }
                fixtureCount++;
            }
            foreach (string invalid in new[] { "{}", "{\"x\":0,\"y\":0,\"ancho\":0,\"alto\":1}" })
            {
                bool rejected = false;
                try { SettlementPreview.ReadRect(Newtonsoft.Json.Linq.JObject.Parse(invalid)); } catch (InvalidOperationException) { rejected = true; }
                if (!rejected) throw new Exception("Se aceptó un rectángulo inválido.");
            }
            if (fixtureCount == 0) throw new Exception("No hay fixtures JSON para validar.");
            Debug.Log("Settlement fixtures PASS: " + fixtureCount + " archivos, " + settlementCount + " asentamientos, " + footprintCount + " huellas y " + wallPieceCount + " piezas de muralla; datos inválidos rechazados.");
        }
    }
}

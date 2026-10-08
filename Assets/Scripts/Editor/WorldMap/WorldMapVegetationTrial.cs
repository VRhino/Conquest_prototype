using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using sc.terrain.vegetationspawner;

namespace Conquest.WorldMap.Editor
{
    public static class WorldMapVegetationTrial
    {
        const string Folder = WorldMapLandscapeLab.Folder;
        const string BaselinePath = Folder + "/BaselineVegetation.asset";
        const string TrialPath = Folder + "/TrialSurroundings.asset";
        public const string ProfilePath = Folder + "/ForestDensityProfile.asset";

        [MenuItem("Tools/BronzeAge/Pruebas/Editar densidad de bosques")]
        public static void EditProfile() { Selection.activeObject = Profile(); EditorGUIUtility.PingObject(Selection.activeObject); }

        static WorldMapForestTrialProfile Profile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<WorldMapForestTrialProfile>(ProfilePath);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<WorldMapForestTrialProfile>(); profile.SetDefaults();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            return profile;
        }

        [MenuItem("Tools/BronzeAge/Pruebas/Generar bosques con Vegetation Spawner")]
        public static void Generate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Sal de Play.");
            WorldMapLandscapeLab.Create();
            var previous = SceneManager.GetActiveScene();
            var source = SceneManager.GetSceneByPath(WorldMapPreviewBuilder.ScenePath);
            bool closeSource = !source.IsValid() || !source.isLoaded;
            if (closeSource) source = EditorSceneManager.OpenScene(WorldMapPreviewBuilder.ScenePath, OpenSceneMode.Additive);
            var lab = SceneManager.GetSceneByPath(WorldMapLandscapeLab.ScenePath);
            bool closeLab = !lab.IsValid() || !lab.isLoaded;
            if (closeLab) lab = EditorSceneManager.OpenScene(WorldMapLandscapeLab.ScenePath, OpenSceneMode.Additive);
            var randomState = UnityEngine.Random.state;
            try
            {
                var timer = System.Diagnostics.Stopwatch.StartNew(); var profile = Profile();
                var originals = source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Terrain>(true))
                    .ToDictionary(t => t.transform.position);
                var preview = lab.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WorldMapPreviewController>(true)).First();
                var renderer = preview.worldRoot.GetComponent<WorldMapSyntyRenderer>();
                var baseline = AssetDatabase.LoadAssetAtPath<WorldMapSyntyInstances>(BaselinePath);
                if (!baseline) { baseline = UnityEngine.Object.Instantiate(renderer.instances); AssetDatabase.CreateAsset(baseline, BaselinePath); }
                var settings = baseline.settings;
                var terrains = preview.worldRoot.GetComponentsInChildren<Terrain>(true)
                    .OrderBy(t => t.transform.position.z).ThenBy(t => t.transform.position.x).ToList();
                foreach (var terrain in terrains)
                    if (!AssetDatabase.GetAssetPath(terrain.terrainData).StartsWith(Folder + "/")) throw new Exception("TerrainData compartido.");
                var spawner = preview.worldRoot.GetComponent<VegetationSpawner>() ?? preview.worldRoot.gameObject.AddComponent<VegetationSpawner>();
                spawner.seed = profile.seed; spawner.autoRespawnTrees = false; spawner.terrains = terrains;
                spawner.waterHeight = settings.seaElevation + 4; spawner.visibleTerrainsOnly = false;
                spawner.treeTypes.Clear(); spawner.grassPrefabs.Clear();
                var families = new[] { settings.broadleaf, settings.riverside, settings.mountain };
                var names = new[] { "Frondosas mediterráneas", "Bosque de ribera", "Coníferas de montaña" };
                for (int family = 0; family < families.Length; family++)
                {
                    var type = SpawnerBase.TreeType.New(); type.name = names[family]; type.seed = 100 + family;
                    type.probability = 100; type.distance = profile.Spacing(1); type.scaleRange = profile.scaleRange;
                    type.slopeRange = new Vector2(0, settings.maximumForestSlope); type.heightRange = new Vector2(spawner.waterHeight, 10000);
                    type.rejectUnderwater = true;
                    foreach (var prefab in families[family]) type.prefabs.Add(new SpawnerBase.TreePrefab
                        { prefab = CampaignPrefab(prefab, settings.treeHeight, profile), probability = 100 });
                    spawner.treeTypes.Add(type);
                }
                // Only use the package's Terrain prototype integration. Its fixed-radius sampler and native
                // mask probability filter cannot represent canonical variable-density forests correctly.
                spawner.RefreshTreePrefabs();
                var rivers = preview.worldRoot.GetComponentsInChildren<LineRenderer>(true);
                var water = WorldCampaignWater.Settings();
                var fields = new List<WorldMapAdaptiveForest.Field>();
                foreach (var terrain in terrains)
                {
                    var original = originals[terrain.transform.position];
                    var fieldPath = Path.Combine(Path.GetDirectoryName(AssetDatabase.GetAssetPath(original.terrainData)), terrain.name + ".layers.bytes");
                    var raw = File.ReadAllBytes(fieldPath); int n = Mathf.RoundToInt(Mathf.Sqrt(raw.Length / 7f));
                    if (n * n * 7 != raw.Length) throw new InvalidDataException(fieldPath);
                    fields.Add(new WorldMapAdaptiveForest.Field { terrain = terrain, raw = raw, n = n });
                }
                var generated = WorldMapAdaptiveForest.Generate(fields, spawner, profile, settings.maximumForestSlope, rivers, water);
                foreach (var terrain in terrains)
                {
                    var data = terrain.terrainData;
                    data.SetTreeInstances(generated.instances[terrain].ToArray(), false);
                    terrain.ignoreQualitySettings = true; // Authoring preview distances must not inherit gameplay quality limits.
                    terrain.drawTreesAndFoliage = true; terrain.treeDistance = profile.forestViewDistance;
                    // Mesh LODs keep the campaign silhouette; these Synty prototypes have no authored billboards.
                    terrain.treeBillboardDistance = profile.forestViewDistance;
                    terrain.treeMaximumFullLODCount = Mathf.Max(1, data.treeInstanceCount);
                    EditorUtility.SetDirty(data);
                }
                var boxes = terrains.Select(t => new Bounds(t.transform.position + t.terrainData.size * .5f, t.terrainData.size)).ToArray();
                var trial = AssetDatabase.LoadAssetAtPath<WorldMapSyntyInstances>(TrialPath);
                if (!trial) { trial = UnityEngine.Object.Instantiate(baseline); AssetDatabase.CreateAsset(trial, TrialPath); }
                trial.batches = baseline.batches.Where(b => b.always || !boxes.Any(box => box.Intersects(b.bounds))).ToArray();
                // Fine batches maintain the same exclusion at every zoom level.
                trial.overviewBatches = Array.Empty<WorldMapInstanceBatch>();
                EditorUtility.SetDirty(trial); renderer.instances = trial;
                preview.FocusCampaign(); EditorSceneManager.SaveScene(lab); AssetDatabase.SaveAssets();
                File.WriteAllText(Folder + "/VegetationTrial.json", JsonUtility.ToJson(new Report
                    { package = VegetationSpawner.Version, sectors = terrains.Count, trees = generated.trees,
                        minimumSpacing = profile.minimumSpacing, maximumSpacing = profile.maximumSpacing,
                        densityBands = generated.densityBands, candidates = generated.candidates,
                        generationSeconds = timer.Elapsed.TotalSeconds, seed = profile.seed }, true));
                Debug.Log("VEGETATION_TRIAL_READY: " + generated.trees + " árboles Synty en " + terrains.Count + " sectores; densidad adaptativa.");
            }
            finally
            {
                UnityEngine.Random.state = randomState; EditorUtility.ClearProgressBar();
                if (closeLab) EditorSceneManager.CloseScene(lab, true);
                if (closeSource) EditorSceneManager.CloseScene(source, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        [MenuItem("Tools/BronzeAge/Pruebas/Alternar bosque actual y Vegetation Spawner")]
        public static void Toggle()
        {
            var scene = SceneManager.GetSceneByPath(WorldMapLandscapeLab.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Abre el laboratorio de paisaje.");
            var preview = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WorldMapPreviewController>(true)).First();
            var renderer = preview.worldRoot.GetComponent<WorldMapSyntyRenderer>();
            var spawner = preview.worldRoot.GetComponent<VegetationSpawner>();
            if (!spawner) throw new InvalidOperationException("Genera primero la prueba.");
            bool native = AssetDatabase.GetAssetPath(renderer.instances) != TrialPath;
            renderer.instances = AssetDatabase.LoadAssetAtPath<WorldMapSyntyInstances>(native ? TrialPath : BaselinePath);
            foreach (var terrain in spawner.terrains) terrain.drawTreesAndFoliage = native;
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(native ? "Comparación: Vegetation Spawner" : "Comparación: bosque actual");
        }

        public static GameObject CampaignPrefab(GameObject source, float height, WorldMapForestTrialProfile profile)
        {
            string path = Folder + "/" + source.name + "_Terrain.prefab";
            var root = new GameObject(source.name + "_Terrain");
            try
            {
                var child = UnityEngine.Object.Instantiate(source, root.transform); child.transform.localPosition = Vector3.zero;
                var meshes = child.GetComponentsInChildren<Renderer>(); var bounds = meshes[0].bounds;
                foreach (var mesh in meshes.Skip(1)) bounds.Encapsulate(mesh.bounds);
                float scale = height / Mathf.Max(.01f, bounds.size.y);
                child.transform.localScale *= scale; child.transform.localPosition = Vector3.up * (-bounds.min.y * scale);
                var middle = WorldMapForestLod.Create(child, root.transform, source.name, "Middle", profile.middleMeshCell);
                var far = WorldMapForestLod.Create(child, root.transform, source.name, "Far", profile.farMeshCell);
                var lod = root.AddComponent<LODGroup>(); lod.SetLODs(new[] {
                    new LOD(profile.lodNearTransition, meshes), new LOD(profile.lodMiddleTransition, middle),
                    new LOD(profile.lodCullTransition, far) }); lod.RecalculateBounds();
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Serializable] class Report
        {
            public string package; public int sectors, trees, candidates, seed; public float minimumSpacing, maximumSpacing;
            public int[] densityBands; public double generationSeconds;
        }
    }
}

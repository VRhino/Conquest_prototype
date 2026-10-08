using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Conquest.WorldMap;
using Conquest.WorldMap.Editor;
using BronzeAge.RegionalMapTool;

public static class WorldMapLabSetup
{
    public static object Run()
    {
        var previous = SceneManager.GetActiveScene();
        var source = Enumerable.Range(0, SceneManager.sceneCount)
            .Select(SceneManager.GetSceneAt).First(s => s.path == WorldMapPreviewBuilder.ScenePath);
        var originals = source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Terrain>(true))
            .ToDictionary(t => t.transform.position);
        var originalResources = source.GetRootGameObjects()
            .Sum(r => r.GetComponentsInChildren<BronzeAgeRegionalMarker>(true).Length);
        var lab = EditorSceneManager.OpenScene(WorldMapLandscapeLab.ScenePath, OpenSceneMode.Additive);
        int terrains = 0, samples = 0;
        try
        {
            foreach (var root in lab.GetRootGameObjects()) root.SetActive(false);
            foreach (var terrain in lab.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Terrain>(true)))
            {
                var original = originals[terrain.transform.position];
                var data = terrain.terrainData;
                if (data == original.terrainData || !AssetDatabase.GetAssetPath(data).StartsWith(WorldMapLandscapeLab.Folder + "/"))
                    throw new Exception("Shared TerrainData: " + terrain.name);
                if (terrain.GetComponent<TerrainCollider>().terrainData != data) throw new Exception("Wrong collider data");
                int resolution = data.heightmapResolution;
                if (resolution != original.terrainData.heightmapResolution || data.size != original.terrainData.size)
                    throw new Exception("Terrain dimensions changed");
                var heights = data.GetHeights(0, 0, resolution, resolution);
                var expected = original.terrainData.GetHeights(0, 0, resolution, resolution);
                for (int y = 0; y < resolution; y++) for (int x = 0; x < resolution; x++)
                {
                    if (heights[y, x] != expected[y, x]) throw new Exception("Height changed");
                    samples++;
                }
                terrains++;
            }
            if (terrains != originals.Count) throw new Exception("Missing terrains");
            int resources = lab.GetRootGameObjects().Sum(r => r.GetComponentsInChildren<BronzeAgeRegionalMarker>(true).Length);
            if (resources != originalResources) throw new Exception("Resources changed");
            foreach (var preview in lab.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WorldMapPreviewController>(true)))
                if (preview.mapCamera.gameObject.scene != lab || preview.worldRoot.gameObject.scene != lab)
                    throw new Exception("Preview references point outside lab");

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = settings.FindGroup("WorldMap-Lab") ?? settings.CreateGroup("WorldMap-Lab", false, false, false, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(WorldMapLandscapeLab.ScenePath), group);
            entry.address = "worldmap/lab";
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            var report = new Report { terrains = terrains, heightSamples = samples, resources = resources,
                heightsUnchanged = true, independentTerrainData = true, address = entry.address,
                vegetationSpawnerImported = AssetDatabase.FindAssets("VegetationSpawner t:MonoScript").Length > 0 };
            File.WriteAllText(WorldMapLandscapeLab.Folder + "/Validation.json", JsonUtility.ToJson(report, true));
            return report;
        }
        finally
        {
            EditorSceneManager.CloseScene(lab, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    [Serializable] public class Report
    {
        public int terrains, heightSamples, resources;
        public bool heightsUnchanged, independentTerrainData, vegetationSpawnerImported;
        public string address;
    }
}

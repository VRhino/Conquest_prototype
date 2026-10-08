using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Conquest.WorldMap.Editor
{
    /// <summary>Isolated authoring fixture; never edits the source scene or its TerrainData.</summary>
    public static class WorldMapLandscapeLab
    {
        public const string Folder = "Assets/Scenes/WorldMapTools/Experiments";
        public const string ScenePath = Folder + "/LandscapeLab.unity";

        [MenuItem("Tools/BronzeAge/Pruebas/Crear laboratorio de paisaje")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Sal de Play antes de crear el laboratorio.");
            if (File.Exists(ScenePath))
            {
                Debug.Log("El laboratorio ya existe: " + ScenePath);
                return;
            }
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/WorldMap", "Experiments");
            if (!AssetDatabase.CopyAsset(WorldMapPreviewBuilder.ScenePath, ScenePath))
                throw new IOException("No se pudo copiar la preview guardada.");

            var previous = SceneManager.GetActiveScene();
            var lab = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var clonedCount = 0;
            try
            {
                foreach (var root in lab.GetRootGameObjects())
                {
                    foreach (var terrain in root.GetComponentsInChildren<Terrain>(true))
                    {
                        var data = UnityEngine.Object.Instantiate(terrain.terrainData);
                        data.name = "Lab_" + terrain.terrainData.name;
                        AssetDatabase.CreateAsset(data, AssetDatabase.GenerateUniqueAssetPath(Folder + "/" + data.name + ".asset"));
                        terrain.terrainData = data;
                        var collider = terrain.GetComponent<TerrainCollider>();
                        if (collider) collider.terrainData = data;
                        if (terrain.materialTemplate)
                        {
                            var material = UnityEngine.Object.Instantiate(terrain.materialTemplate);
                            AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(Folder + "/TerrainMaterial.asset"));
                            terrain.materialTemplate = material;
                        }
                        clonedCount++;
                    }
                    foreach (var preview in root.GetComponentsInChildren<WorldMapPreviewController>(true))
                    {
                        if (!preview.artProfile) continue;
                        var profile = UnityEngine.Object.Instantiate(preview.artProfile);
                        AssetDatabase.CreateAsset(profile, AssetDatabase.GenerateUniqueAssetPath(Folder + "/ArtProfile.asset"));
                        preview.artProfile = profile;
                        var renderer = preview.worldRoot.GetComponent<WorldMapSyntyRenderer>();
                        if (renderer) renderer.artProfile = profile;
                    }
                }
                if (clonedCount == 0) throw new InvalidOperationException("La preview no contiene Terrain.");
                EditorSceneManager.SaveScene(lab);
                AssetDatabase.SaveAssets();
                Debug.Log("LANDSCAPE_LAB_READY: " + clonedCount + " TerrainData independientes. " + ScenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(lab, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        [MenuItem("Tools/BronzeAge/Pruebas/Abrir laboratorio de paisaje")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Create();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
    }
}

using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Scenes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Conquest.SettlementPreview.Editor
{
    public static class SettlementWalkSetup
    {
        [MenuItem("Tools/BronzeAge/Preparar modo a pie")]
        public static void Setup()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Salir de Play primero.");
            var preview = Object.FindFirstObjectByType<SettlementPreview>();
            if (!preview) throw new System.InvalidOperationException("Abre SettlementPreview primero.");
            var scene = preview.gameObject.scene;
            var walk = preview.GetComponent<SettlementWalkMode>();
            if (!walk) walk = preview.gameObject.AddComponent<SettlementWalkMode>();
            walk.heroCameraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Camera/Main Camera Follow Hero.prefab");
            bool hasRegistry = false;
            foreach (var root in scene.GetRootGameObjects()) if (root.GetComponentInChildren<VisualPrefabRegistry>(true)) hasRegistry = true;
            if (!hasRegistry) PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Map/visualRegistry.prefab"), scene);
            const string path = "Assets/Scenes/SettlementHeroWorld.unity";
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(path))
            {
                var data = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(data);
                var config = new GameObject("Settlement hero configuration");
                config.AddComponent<SettlementHeroAuthoring>().heroPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hero/HeroEntity_Pure.prefab");
                config.AddComponent<HeroGameplayConfigAuthoring>();
                config.AddComponent<ShieldConfigAuthoring>();
                EditorSceneManager.SaveScene(data, path);
                EditorSceneManager.CloseScene(data, true);
            }
            SceneManager.SetActiveScene(scene);
            bool hasSubscene = false;
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponent<SubScene>() is SubScene sub && AssetDatabase.GetAssetPath(sub.SceneAsset) == path) hasSubscene = true;
            if (!hasSubscene)
            {
                var sub = new GameObject("SettlementHeroWorld").AddComponent<SubScene>();
                sub.SceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path); sub.AutoLoadScene = true;
            }
            EditorUtility.SetDirty(walk);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Modo a pie configurado: prefab ECS aislado, visualRegistry y cámara existente. Sin tropas ni batalla.");
        }
    }
}

using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Conquest.WorldMap;
public static class ConfigureWorldMapTravel
{
    public static string Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
        var original=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        const string materialPath="Assets/WorldMap/Campaign/Symbols/TravelRoute.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.SetColor("_BaseColor",new Color(1,.77f,.3f));AssetDatabase.CreateAsset(material,materialPath);}
        var navigation=AssetDatabase.LoadAssetAtPath<WorldMapLocalNavigation>("Assets/WorldMap/Campaign/LocalNavigation.asset");
        foreach(var path in new[]{"Assets/Scenes/GameScenes/WorldMapCampaign.unity","Assets/Scenes/WorldMapTools/Experiments/CalibrationFactor12/CalibrationFactor12.unity"})
        {
            var scene=EditorSceneManager.OpenScene(path);
            var campaign=UnityEngine.Object.FindAnyObjectByType<WorldMapCampaignController>();
            campaign.navigation=navigation;
            campaign.streamer.trackedActor=campaign.actor;
            var presentation=campaign.GetComponent<WorldMapLocalRouteVisual>();
            if(!presentation)presentation=campaign.gameObject.AddComponent<WorldMapLocalRouteVisual>();
            foreach(var old in campaign.GetComponents<MonoBehaviour>())if(old.GetType().Name=="WorldMapTravelPresentation")UnityEngine.Object.DestroyImmediate(old);presentation.lineMaterial=material;presentation.width=.6f;presentation.lift=1.2f;presentation.sampleSpacing=15;
            if(campaign.actor.localScale!=Vector3.one*6)throw new Exception("Hero scale changed");
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(original);
        return "Travel enabled in campaign and calibration; shared 31.25u navigation; hero scale6 preserved.";
    }
}

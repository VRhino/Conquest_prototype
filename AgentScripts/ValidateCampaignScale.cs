using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor.SceneManagement;
using Conquest.WorldMap;
using Conquest.WorldMap.Editor;

public static class ValidateCampaignScale
{
    public static string Run()
    {
        var l=UnityEngine.Object.FindAnyObjectByType<WorldMapScaleLab>();
        if(!l||l.gameObject.scene.path!=WorldMapScaleLabBuilder.ScenePath)throw new Exception("Open scale trial");
        if(l.terrains.Length!=48||Math.Abs(l.Distance-1200)>.1||Math.Abs(l.TravelSeconds-100)>.1)throw new Exception("Wrong route calibration");
        double maxSlope=0,maxRoot=0;
        foreach(var p in l.route){
            if(Math.Abs(l.Height(p)-p.y)>.02||p.y<400)throw new Exception("Route outside terrain");
            var t=l.terrains.Single(t=>p.x>=t.transform.position.x&&p.x<t.transform.position.x+t.terrainData.size.x&&p.z>=t.transform.position.z&&p.z<t.transform.position.z+t.terrainData.size.z);
            maxSlope=Math.Max(maxSlope,t.terrainData.GetSteepness((p.x-t.transform.position.x)/4000,(p.z-t.transform.position.z)/4000));
        }
        foreach(var f in l.forests){
            if(f.native==f.campaign||f.native.heightmapResolution!=f.campaign.heightmapResolution||!f.native.GetHeights(0,0,257,257).Cast<float>().SequenceEqual(f.campaign.GetHeights(0,0,257,257).Cast<float>()))throw new Exception("Forest comparison changed relief");
            foreach(var tree in f.campaign.treeInstances)maxRoot=Math.Max(maxRoot,Math.Abs(tree.position.y*1000-f.campaign.GetInterpolatedHeight(tree.position.x,tree.position.z)));
        }
        l.ResetJourney();if(l.EntryAllowed)throw new Exception("Entry allowed remotely");
        l.travelMultiplier=1;l.BeginJourney();l.Advance(50);
        if(Math.Abs(l.travelled-600)>.1||Math.Abs(l.elapsed-50)>.1||l.EntryAllowed)throw new Exception("Movement or access incorrect halfway");
        l.Advance(50);
        if(!l.EntryAllowed||l.moving||Math.Abs(l.travelled-1200)>.1)throw new Exception("Arrival incorrect");
        var endpoint=l.route.Last();l.actor.position=endpoint+Vector3.right*19;if(l.EntryAllowed)throw new Exception("Gate accepts outside radius");
        l.actor.position=endpoint+Vector3.right*17;if(!l.EntryAllowed)throw new Exception("Gate rejects inside radius");
        l.ResetJourney();l.travelMultiplier=10;l.BeginJourney();l.Advance(10);
        if(!l.EntryAllowed||Math.Abs(l.elapsed-100)>.1)throw new Exception("Accelerated visual trial changes game duration");
        l.travelMultiplier=1;l.ResetJourney();l.PlayerView();
        int canonical=l.terrains.Sum(t=>{var f=l.forests.FirstOrDefault(f=>f.terrain==t);return (f==null?t.terrainData:f.native).treeInstanceCount;});
        if(canonical!=178665||maxSlope>20||maxRoot>.01)throw new Exception("Canonical forest or local path mismatch");
        var s=$"Route=1200 units\nTravel=100 game seconds\nHalfway=600 units in 50 seconds\nEntry radius=18 units: denied at 19, allowed at 17\nAcceleration=10x retains 100 game seconds\nTerrain height arrays unchanged=4\nNative reference trees={canonical}\nMax route slope={maxSlope}\nMax forest root error={maxRoot}\n";
        File.WriteAllText(WorldMapScaleLabBuilder.Folder+"/Validation.txt",s);
        if(!Application.isPlaying)EditorSceneManager.SaveScene(l.gameObject.scene);
        return s;
    }
}

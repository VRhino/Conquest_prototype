using System;
using System.IO;
using System.Text;
using UnityEngine;
using Conquest.WorldMap;
using Conquest.WorldMap.Editor;

public static class ValidateCampaignCalibration
{
    static void Near(float actual,float expected,float tolerance,string message)
    { if(Mathf.Abs(actual-expected)>tolerance)throw new Exception(message+": "+actual+" != "+expected); }
    public static string Run()
    {
        var t=UnityEngine.Object.FindAnyObjectByType<WorldMapCalibrationTrial>();
        if(!Application.isPlaying||!t)throw new Exception("Run the factor12 trial in Play first.");
        var c=t.campaign;var p=t.Contract;var result=new StringBuilder();
        if(c.streamer.PendingCount!=0||c.streamer.LoadedCount==0||!string.IsNullOrEmpty(c.streamer.Error))throw new Exception("Wait for sector streaming to finish.");
        Near(p.unityPerStrategicUnit,12,.0001f,"Factor");Near(p.secondsPerEconomicTick,60,.0001f,"Economic tick");
        Near(p.StrategicWorldSize(c.streamer.manifest.worldSize).x,2666.6667f,.001f,"World strategic width");Near(p.StrategicWorldSize(c.streamer.manifest.worldSize).y,2000,.001f,"World strategic height");
        float[] radii={72,120,180,240,300,360};for(int i=0;i<radii.Length;i++)Near(p.InfluenceUnity(i),radii[i],.001f,"Influence "+i);
        Near(t.BaselineDistance,360,.01f,"Route");
        foreach(var city in new[]{c.cityA,c.cityB})
        {
            float maxRadius=0;
            foreach(var mesh in city.GetComponentsInChildren<MeshFilter>(true))foreach(var vertex in mesh.sharedMesh.vertices)
            {var offset=mesh.transform.TransformPoint(vertex)-city.position;offset.y=0;maxRadius=Mathf.Max(maxRadius,offset.magnitude);}
            if(maxRadius>p.ToUnity(p.citySymbolRadius)+.02f||maxRadius<1)throw new Exception("City symbol footprint: "+maxRadius);
            result.AppendLine(city.name+" actual geometry radius: "+maxRadius.ToString("F3")+" u (limit "+p.ToUnity(p.citySymbolRadius)+")");
        }
        if(c.actor.GetComponentInChildren<Animator>())
        {
            Near(c.actor.localScale.x,p.heroSymbolScale,.001f,"Hero scale X");Near(c.actor.localScale.y,p.heroSymbolScale,.001f,"Hero scale Y");Near(c.actor.localScale.z,p.heroSymbolScale,.001f,"Hero scale Z");
            result.AppendLine("Animated hero scale: "+p.heroSymbolScale+" (user-approved; not legacy symbol height10)");
        }
        else
        {
            float minY=float.MaxValue,maxY=float.MinValue;
            foreach(var mesh in c.actor.GetComponentsInChildren<MeshFilter>(true))foreach(var vertex in mesh.sharedMesh.vertices)
            {float y=mesh.transform.TransformPoint(vertex).y;minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);}
            Near(maxY-minY,p.armySymbolHeightUnity,.02f,"Army symbol height");
        }
        var delta=c.cityB.position-c.cityA.position;delta.y=0;Near(delta.magnitude,360,.01f,"City separation");
        if(!c.navigation.SegmentClear(t.baselineRoute[0],t.baselineRoute[1]))throw new Exception("Fixture blocked");
        float[] speeds={6,6.6f,8.4f,7.2f,5.6f,4.8f,3.6f,3};
        foreach(CampaignTravelKind kind in Enum.GetValues(typeof(CampaignTravelKind)))
        {
            Near(p.SpeedUnity(kind),speeds[(int)kind],.001f,"Speed "+kind);
            c.SetTravelKind(kind);t.BeginTrial();float duration=360/c.movementSpeed;
            c.Advance(duration*.5f);Near(c.travelled,180,.03f,"Half distance "+kind);if(!c.moving)throw new Exception("Stopped early");
            c.Advance(duration*.5f+.01f);Near(c.travelled,360,.03f,"Arrival "+kind);if(c.moving||!c.EntryAllowed)throw new Exception("Arrival/access incorrect");
            result.AppendLine(kind+": "+c.movementSpeed.ToString("F1")+" u/s; 360 u in "+duration.ToString("F3")+" s");
        }
        // Many short updates must equal the same elapsed interval, not the tick count.
        c.SetTravelKind(CampaignTravelKind.Light);t.BeginTrial();for(int i=0;i<300;i++)c.Advance(.1f);Near(c.travelled,180,.15f,"Substep movement");
        c.Advance(30.1f);Near(c.travelled,360,.15f,"Substep arrival");
        c.actor.position=t.baselineRoute[1]+Vector3.right*25;if(c.EntryAllowed)throw new Exception("Gate allows 25");
        c.actor.position=t.baselineRoute[1]+Vector3.right*23;if(!c.EntryAllowed)throw new Exception("Gate rejects 23");
        t.ResetTrial();float oldAspect=c.mapCamera.aspect;
        foreach(float aspect in new[]{16f/9f,4f/3f,21f/9f})
        {
            c.mapCamera.aspect=aspect;c.SetVisibleGroundWidth(180);Near(c.VisibleGroundWidth(),180,.05f,"Visible width/aspect "+aspect);
            var r=c.mapCamera.transform.right;r.y=0;r.Normalize();
            var a=c.focus-r*90;var b=c.focus+r*90;a.y=b.y=c.focus.y;
            Near(c.mapCamera.WorldToViewportPoint(a).x,0,.002f,"Screen left");Near(c.mapCamera.WorldToViewportPoint(b).x,1,.002f,"Screen right");
        }
        c.mapCamera.aspect=oldAspect;c.SetVisibleGroundWidth(180);var pose=c.Capture();c.visibleWidthTarget=900;c.Restore(pose);c.ApplyCamera();Near(c.VisibleGroundWidth(),180,.05f,"Return camera width");
        var axis=c.cityB.position-c.cityA.position;axis.y=0;float half=axis.magnitude*.5f;axis.Normalize();
        for(int level=0;level<=5;level++)
        {
            t.SetLevel(level);for(int i=0;i<t.influenceA.positionCount;i++)if(Vector3.Dot(t.influenceA.GetPosition(i)-c.cityA.position,axis)>half+.01f)throw new Exception("Rival rings overlap");
        }
        t.SetLevel(2);c.SetTravelKind(CampaignTravelKind.Light);t.ResetTrial();
        result.AppendLine("PASS: camera width180 at 16:9,4:3,21:9; horizontal screen crossing30s; return pose retains zoom.");
        result.AppendLine("PASS: radii72/120/180/240/300/360; equal-level rival clipping; gate denied25/allowed23; substeps match.");
        result.AppendLine("Geography SHA256: "+c.streamer.manifest.sourceSHA256);
        result.AppendLine("Loaded sectors: "+c.streamer.LoadedCount+"; streaming error: "+c.streamer.Error);
        File.WriteAllText(WorldMapCalibrationBuilder.Folder+"/Validation.txt",result.ToString());
        return result.ToString();
    }
}

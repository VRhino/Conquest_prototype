using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Conquest.WorldMap;
public static class ValidateWorldMapTravel
{
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    static async Task Wait(Func<bool> ready,int seconds=45){var end=DateTime.UtcNow.AddSeconds(seconds);while(!ready()){if(DateTime.UtcNow>end)throw new Exception("Timed out awaiting navigation/streaming");await Task.Delay(50);}}
    static float Distance(Vector3 a,Vector3 b)=>Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z));
    public static async Task<string> Run()
    {
        Require(Application.isPlaying,"Enter Play");
        var c=UnityEngine.Object.FindAnyObjectByType<WorldMapCampaignController>();var trial=c.GetComponent<WorldMapCalibrationTrial>();
        Require(trial,"Use calibration scene");var report=new StringBuilder();
        await Wait(()=>c.streamer.PendingCount==0&&c.streamer.TryHeight(trial.baselineRoute[0],out _));
        try
        {
            c.SetTravelKind(CampaignTravelKind.Light);trial.ResetTrial();
            var goal=c.actor.position+Vector3.right*60;goal.y=c.navigation.Height(goal);
            var screen=c.mapCamera.WorldToScreenPoint(goal);Require(screen.z>0,"Destination behind camera");
            Require(c.TravelAtScreen(new Vector2(screen.x,screen.y)),"Screen click rejected");await Wait(()=>!c.IsPlanning);
            Require(c.moving&&c.HasDestination,"Accepted destination not moving");c.StopTravel();
            Require(Distance(c.demoRoute.Last(),goal)<.5f,"Terrain picking mismatch");
            var position=c.actor.position;await Task.Delay(250);Require(Distance(position,c.actor.position)<.01f,"Paused hero moved");
            c.ResumeTravel();c.Advance(2);c.StopTravel();Require(Distance(position,c.actor.position)>11.8f,"Resume/speed incorrect");
            position=c.actor.position;var replacement=position+Vector3.right*90;
            Require(c.TravelTo(replacement),"Replacement rejected");await Wait(()=>!c.IsPlanning);c.StopTravel();
            Require(Distance(c.demoRoute[0],position)<.01f,"New order restarted from original spawn");
            Require(Distance(c.demoRoute.Last(),replacement)<.01f,"Wrong replacement destination");
            int water=Array.FindIndex(c.navigation.blocked,b=>b==1);Require(water>=0,"No sea mask");
            var oldRoute=c.demoRoute;Require(!c.TravelTo(c.navigation.Center(water))&&ReferenceEquals(oldRoute,c.demoRoute),"Water order overwrote valid route");
            Require(!c.TravelTo(new Vector3(-1,0,1)),"Outside world accepted");
            report.AppendLine("PASS: ground click; current-position replacement; pause/resume; 6u/s; water/outside rejection.");
            var hud=c.GetComponent<WorldMapCampaignHud>();var cityCollider=c.cityA.GetComponent<Collider>();var cityScreen=c.mapCamera.WorldToScreenPoint(cityCollider.bounds.center);
            Require(hud.SelectAt(new Vector2(cityScreen.x,cityScreen.y))&&c.selectedSettlement==c.cityA.GetComponent<WorldMapSettlementTarget>(),"City click no longer selects its interaction menu");c.selectedSettlement=null;
            oldRoute=c.demoRoute;Require(!c.TravelAtGuiPoint(new Vector2(40,30),trial.PanelRect),"UI panel click accepted");
            Require(ReferenceEquals(oldRoute,c.demoRoute)&&!c.moving,"UI panel click issued a ground order");
            report.AppendLine("PASS: city picking preserved; the shared GUI click handler excludes the trial panel.");

            // Remote ground picking and camera residency must work independently of actor residency.
            var remote=trial.baselineRoute[0]+new Vector3(-9000,0,7000);remote.y=c.navigation.Height(remote);
            var ray=new Ray(remote+Vector3.up*1000,Vector3.down);
            Require(c.navigation.Raycast(ray,2000,out var picked)&&Distance(picked,remote)<.1f,"Unloaded height-field picking failed");
            c.FocusAt(remote);await Wait(()=>c.streamer.PendingCount==0&&c.streamer.LoadedCount>=12);
            Require(c.streamer.TryHeight(c.actor.position,out _)&&c.streamer.TryHeight(remote,out _),"Camera evicted hero terrain");
            Require(c.streamer.LoadedCount<=19,"Streaming loaded too much world");
            report.AppendLine("PASS: remote height-field click and separate camera/hero streaming ("+c.streamer.LoadedCount+" sectors).");

            // Find a safe route across a real sector boundary; the original calibration neighborhood is resident.
            Vector3 a=default,b=default;bool found=false;
            for(float z=6000;z<7500&&!found;z+=31.25f){a=new Vector3(15940,0,z);b=new Vector3(16060,0,z);found=c.navigation.SegmentClear(a,b);}
            Require(found,"No fixture at real sector boundary");
            c.actor.position=a+Vector3.up*(c.navigation.Height(a)+c.actorLift);c.follow=true;c.focus=c.actor.position;
            await Wait(()=>c.streamer.TryHeight(a,out _)&&c.streamer.TryHeight(b,out _));
            c.demoRoute=new[]{a,b};c.segment=1;c.travelled=0;c.ResumeTravel();c.Advance(20.1f);
            Require(!c.moving&&Distance(c.actor.position,b)<.02f,"Could not cross sector boundary");
            Require(c.streamer.TryHeight(c.actor.position,out var ground)&&Mathf.Abs(c.actor.position.y-ground-c.actorLift)<.02f,"Hero lost ground at sector border");
            report.AppendLine("PASS: real sector boundary crossed, no fall/jump; calibrated hero root scale6 unchanged.");
            Require(c.actor.localScale==Vector3.one*6,"Hero scale changed");
            var synthetic=ScriptableObject.CreateInstance<WorldMapLocalNavigation>();
            try{
                synthetic.columns=synthetic.rows=3;synthetic.cellSize=10;synthetic.blocked=new byte[9];synthetic.heights=new float[]{0,100,100,0,100,100,0,100,100};Require(!synthetic.SegmentClear(new Vector3(5,0,15),new Vector3(25,0,15)),"Smoothing bypasses slope restriction");
                synthetic.columns=synthetic.rows=7;synthetic.blocked=new byte[49];synthetic.heights=new float[49];for(int z=0;z<7;z++)synthetic.blocked[z*7+3]=3;synthetic.blocked[5*7+3]=0;
                var realNavigation=c.navigation;c.navigation=synthetic;synthetic.sourceSHA256=c.streamer.manifest.sourceSHA256;var savedPosition=c.actor.position;c.actor.position=new Vector3(15,0,15);int budget=c.searchNodesPerFrame;c.searchNodesPerFrame=1;oldRoute=c.demoRoute;
                try{Require(c.TravelTo(new Vector3(55,0,15)),"Async detour request rejected");Require(c.IsPlanning,"Detour search should yield before completion");c.StopTravel();await Task.Delay(150);Require(!c.IsPlanning&&!c.moving&&ReferenceEquals(oldRoute,c.demoRoute),"Cancelled search later applied a stale route");}
                finally{c.navigation=realNavigation;c.actor.position=savedPosition;c.searchNodesPerFrame=budget;}
            }
            finally{UnityEngine.Object.Destroy(synthetic);}
            report.AppendLine("PASS: smoothing rejects steep slopes; cancelling a yielding search never applies a stale order.");
            System.IO.File.WriteAllText("Docs/WorldMap_ClickTravel_Validation.txt",report.ToString());return report.ToString();
        }
        finally{trial.ResetTrial();c.SetTravelKind(CampaignTravelKind.Light);}
    }
}

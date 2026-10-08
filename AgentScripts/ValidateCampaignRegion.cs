using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Conquest.WorldMap;
public static class ValidateCampaignRegion {
 public static string Run(){
  var l=UnityEngine.Object.FindAnyObjectByType<WorldMapRegionLab>();if(!l)throw new Exception("Open region trial");var d=l.Data;
  if(d.sites.Length!=12||d.kingdomCount!=5||d.sites.GroupBy(s=>s.kingdom).Any(g=>g.Count()<2))throw new Exception("Kingdom population mismatch");
  if(l.scale.movementSpeed!=12||l.scale.actorVisualScale!=1.8f||l.scale.cityVisualScale!=1.4f)throw new Exception("Accepted defaults mismatch");
  var available=d.resources.Select(r=>r.id).ToHashSet();
  for(int k=0;k<6;k++){var ids=d.sites.SelectMany(s=>s.levels[k].resourceIds).ToArray();if(ids.Distinct().Count()!=ids.Length||ids.Any(id=>!available.Contains(id)))throw new Exception("Shared or invisible node");}
  float min=float.PositiveInfinity;
  for(int i=0;i<d.sites.Length;i++){
   var s=d.sites[i];if(!s.levels[0].resourceIds.Contains(s.stoneId)||Vector2.Distance(new Vector2(s.x,s.z),new Vector2(s.woodX,s.woodZ))>90)throw new Exception("Invalid starting witnesses");
   if(Math.Abs(l.cities[i].position.y-l.scale.Height(l.cities[i].position))>.02)throw new Exception("Floating city");
   foreach(var t in d.sites.Skip(i+1))min=Mathf.Min(min,Vector2.Distance(new Vector2(s.x,s.z),new Vector2(t.x,t.z)));
  }
  if(min<1080)throw new Exception("Level 5 overlap");
  foreach(var f in l.scale.forests)if(!f.native.GetHeights(0,0,257,257).Cast<float>().SequenceEqual(f.campaign.GetHeights(0,0,257,257).Cast<float>()))throw new Exception("Relief modified");
  l.level=5;l.overview=true;l.Apply();if(l.borders.Any(b=>!b.enabled||b.positionCount<4))throw new Exception("Influence missing");
  l.level=0;l.overview=false;l.Apply();
  var report=$"12 representative settlements / 5 kingdoms (at least 2 each)\n12/12 existing stone and forest start witnesses\nMinimum centre spacing={min:F2}; level 5 non-overlap verified\nNo shared resource node across all six levels\nEvery counted node has a visible marker\nSpeed=12; army scale=1.8; city scale=1.4\nForest proxy tiles={l.scale.forests.Length}; height arrays unchanged\nGeographic feasibility only, not economic viability or global capacity.\n";
  File.WriteAllText("Assets/Scenes/WorldMapTools/Experiments/CampaignRegion/Validation.txt",report);return report;
 }
}

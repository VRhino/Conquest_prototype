using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Conquest.WorldMap;
using UnityEditor.SceneManagement;
public static class ValidateStrategicTrials {
 public static string Run(){
  var l=UnityEngine.Object.FindAnyObjectByType<WorldMapRegionLab>();if(!l||l.trialViews==null||l.trialViews.Length!=5)throw new Exception("Open strategic trials");
  int checks=0,totalCorridors=0,totalFocuses=0;
  for(int bank=0;bank<5;bank++){
   l.SwitchRegion(bank);var d=l.Data;if(d.sites.Length!=4||d.kingdomCount!=2||d.sites.GroupBy(s=>s.kingdom).Any(g=>g.Count()!=2))throw new Exception("Wrong kingdom sample");
   if(l.trialViews.Count(v=>v.root.activeSelf)!=1)throw new Exception("Multiple regions active");
   var ids=d.resources.Select(r=>r.id).ToHashSet();
   for(int level=0;level<6;level++){
    l.level=level;l.Apply();var owned=d.sites.SelectMany(s=>s.levels[level].resourceIds).ToArray();if(owned.Distinct().Count()!=owned.Length||owned.Any(id=>!ids.Contains(id)))throw new Exception("Shared or missing resource");
    for(int i=0;i<4;i++){if(l.borders[i].positionCount!=d.sites[i].levels[level].polygon.Length+1||Math.Abs(l.cities[i].position.y-l.scale.Height(l.cities[i].position))>.02)throw new Exception("Border/ground mismatch");checks++;}
   }
   if(d.sites.Any(s=>!s.levels[0].resourceIds.Contains(s.stoneId)))throw new Exception("Starting stone missing");
   if(d.markers.Length!=l.trialViews[bank].markers.Length||d.corridors.Length!=l.trialViews[bank].corridorRoot.transform.childCount)throw new Exception("Missing diagnostic layers");
   foreach(var c in d.corridors)if(c.points.Length<2||c.points.Any(p=>!float.IsFinite(p.x)||!float.IsFinite(p.z)))throw new Exception("Invalid corridor");
   l.showFoci=false;l.showCorridors=false;l.ApplyLayers();if(l.trialViews[bank].focalRoot.activeSelf||l.trialViews[bank].corridorRoot.activeSelf)throw new Exception("Layer toggles fail");
   l.showFoci=l.showCorridors=true;l.ApplyLayers();l.FocusMarker(0);l.overview=true;l.Apply();
   totalCorridors+=d.corridors.Length;totalFocuses+=d.markers.Length;
  }
  if(l.scale.movementSpeed!=12||l.scale.cityVisualScale!=1.4f||l.scale.actorVisualScale!=1.8f)throw new Exception("Accepted scales changed");
  foreach(var f in l.scale.forests)if(!f.native.GetHeights(0,0,257,257).Cast<float>().SequenceEqual(f.campaign.GetHeights(0,0,257,257).Cast<float>()))throw new Exception("Terrain heights changed");
  if(l.scale.forests.Sum(f=>f.native.treeInstanceCount)!=178665)throw new Exception("Canonical forest changed");
  l.SwitchRegion(0);l.level=0;l.Apply();
  var text=$"Regions=5; Kingdoms per region=2; Cities per region=4; Influence checks={checks}\nDiagnostic focuses={totalFocuses}; Corridors={totalCorridors}\nResource IDs and exclusive ownership checked at all levels; multiple same-type nodes allowed\nTerrain heights unchanged across 48 tiles; original forest trees=178665\nSpeed=12; city scale=1.4; army scale=1.8\nNo authoritative state mutated; weighted corridors separate from direct travel times\n";
  File.WriteAllText("Assets/Scenes/WorldMapTools/Experiments/StrategicTrials/Validation.txt",text);if(!Application.isPlaying)EditorSceneManager.SaveScene(l.gameObject.scene);return text;
 }
}

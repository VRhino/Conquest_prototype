using System;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using Conquest.WorldMap;
using BronzeAge.RegionalMapTool;
public static class ValidateWorldMapCampaign {
 public static string Run(){
  if(Application.isPlaying)throw new Exception("Edit mode structural test");
  var c=UnityEngine.Object.FindAnyObjectByType<WorldMapCampaignController>();if(!c)throw new Exception("Open campaign");var s=c.streamer;var m=s.manifest;
  if(m.sectors.Length!=48||s.overview.Length!=48||c.movementSpeed!=12||c.entryDistance!=18||c.actor.localScale!=Vector3.one*1.8f||c.cityA.localScale!=Vector3.one*1.4f)throw new Exception("Wrong manifest or scale");
  if(m.sectors.Select(a=>a.address).Distinct().Count()!=48)throw new Exception("Duplicate addresses");
  var ids=new System.Collections.Generic.HashSet<string>();int crowns=0;
  var settings=AddressableAssetSettingsDefaultObject.Settings;var group=settings.FindGroup("WorldMap-Campaign-Sectors");if(group.GetSchema<BundledAssetGroupSchema>().BundleMode!=BundledAssetGroupSchema.BundlePackingMode.PackSeparately)throw new Exception("Sectors bundled together");
  foreach(var address in m.sectors){
   var entry=group.entries.Single(e=>e.address==address.address);var asset=AssetDatabase.LoadAssetAtPath<GameObject>(entry.AssetPath);var t=asset.GetComponent<WorldMapCampaignSector>().terrain;
   var native=AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/Scenes/WorldMapTools/Experiments/StrategicTrials/"+address.id+"-canopies.asset");
   if(string.IsNullOrEmpty(m.coastRevision)){if(!t.terrainData.GetHeights(0,0,257,257).Cast<float>().SequenceEqual(native.GetHeights(0,0,257,257).Cast<float>()))throw new Exception("Detailed terrain changed");}
   else {var package=UnityEngine.JsonUtility.FromJson<Conquest.WorldMap.Editor.WorldMapCampaignCoastBuilder.Package>(File.ReadAllText(m.sourcePackage));var tile=package.bloques.Single(b=>b.id==address.id);var raw=File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(m.sourcePackage),tile.alturas));var h=t.terrainData.GetHeights(0,0,257,257);for(int z=0;z<257;z++)for(int x=0;x<257;x++){int q=(z*257+x)*2;if(Mathf.Abs(h[z,x]-(raw[q]|raw[q+1]<<8)/65535f)>1f/65535f+1e-7f)throw new Exception("Canonical coast height mismatch");}}
   if(t.transform.position!=address.origin||asset.GetComponentInChildren<TerrainCollider>().terrainData!=t.terrainData)throw new Exception("Collider or transform mismatch");
   crowns+=t.terrainData.treeInstanceCount;
   foreach(var marker in asset.GetComponentsInChildren<BronzeAgeRegionalMarker>(true))if(!ids.Add(marker.sourceId))throw new Exception("Resource duplicated");
  }
  if(ids.Count!=1360||crowns<=0||crowns>67580)throw new Exception("Lost resource or tree");
  foreach(var o in s.overview)if(o.GetComponent<TerrainCollider>()||o.GetComponent<Terrain>().terrainData.heightmapResolution!=33||o.GetComponent<Terrain>().terrainData.treeInstanceCount!=0)throw new Exception("Detailed overview");
  var deps=AssetDatabase.GetDependencies("Assets/Scenes/GameScenes/WorldMapCampaign.unity",true);if(deps.Any(p=>p.EndsWith("-canopies.asset")||p.Contains("/Campaign/Sectors/")))throw new Exception("Scene pins detailed sectors");
  for(int z=0;z<6;z++)for(int x=0;x<8;x++){var wanted=s.DesiredIndices(new Vector3(x*4000+2000,0,z*4000+2000));if(wanted.Length>9||!wanted.Any(i=>m.sectors[i].column==x&&m.sectors[i].row==z))throw new Exception("Streaming neighborhood");}
  var pose=c.Capture();c.FocusAt(new Vector3(6800,0,11860));c.Restore(pose);if(c.focus!=pose.focus||c.follow!=pose.follow)throw new Exception("Camera pose restore failed");
  string text=$"48 independent sectors; all 48 height arrays match source within Terrain quantization; {ids.Count} unique resources; {crowns} campaign crowns\nScene does not reference detailed TerrainData or sector prefabs\n48 low-resolution terrains with no collider or trees; at most 9 nearby sectors\nReturn camera pose preserved; accepted scales unchanged\n";
  File.WriteAllText("Assets/WorldMap/Campaign/Validation.txt",text);return text;
 }
}

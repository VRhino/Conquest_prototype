using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Conquest.WorldMap;

public static class RestoreCampaignGround {
 const string Source = "Assets/Scenes/WorldMapTools/Experiments/MountainsV9/";
 public static string Run() {
  if(EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Exit Play first");
  var controller=UnityEngine.Object.FindAnyObjectByType<WorldMapCampaignController>();
  if(!controller) throw new Exception("Open WorldMapCampaign first");
  // Resolve and validate every source before changing an asset.
  foreach(var sector in controller.streamer.manifest.sectors) {
   var src=AssetDatabase.LoadAssetAtPath<TerrainData>(Source+sector.id+".asset");
   if(!src || src.alphamapLayers!=8) throw new Exception("Missing accepted ground: "+sector.id);
  }
  int count=0,trees=0;
  foreach(var sector in controller.streamer.manifest.sectors) {
   var src=AssetDatabase.LoadAssetAtPath<TerrainData>(Source+sector.id+".asset");
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WorldMap/Campaign/Sectors/"+sector.id+".prefab");
   var dst=prefab.GetComponent<WorldMapCampaignSector>().terrain.terrainData;
   var heights=dst.GetHeights(0,0,dst.heightmapResolution,dst.heightmapResolution);
   var crowns=dst.treeInstances;
   CopyPaint(src,dst);
   if(!heights.Cast<float>().SequenceEqual(dst.GetHeights(0,0,dst.heightmapResolution,dst.heightmapResolution).Cast<float>()) || !crowns.SequenceEqual(dst.treeInstances)) throw new Exception("Terrain or forest changed: "+sector.id);
   var low=controller.streamer.overview.Single(o=>o.GetComponent<Terrain>().transform.position==sector.origin).GetComponent<Terrain>().terrainData;
   CopyPaint(src,low);
   trees+=crowns.Length;count++;
  }
  AssetDatabase.SaveAssets();
  var report=$"Restored V9 regional ground on {count} detailed and {count} overview terrains. Exact detailed alphamaps; overview resampled. Heights and {trees} tree instances unchanged. Existing Yoge textures and V9 mountain materials retained.";
  File.WriteAllText("Docs/CampaignGroundRestoration.txt",report);
  return report;
 }
 static void CopyPaint(TerrainData src,TerrainData dst) {
  dst.terrainLayers=src.terrainLayers;
  var original=src.GetAlphamaps(0,0,src.alphamapWidth,src.alphamapHeight);
  if(dst.heightmapResolution>33) {
   dst.alphamapResolution=src.alphamapResolution;
   dst.SetAlphamaps(0,0,original);
   if(!original.Cast<float>().SequenceEqual(dst.GetAlphamaps(0,0,dst.alphamapWidth,dst.alphamapHeight).Cast<float>())) throw new Exception("Paint mismatch");
  } else {
   var paint=new float[dst.alphamapHeight,dst.alphamapWidth,src.alphamapLayers];
   for(int z=0;z<dst.alphamapHeight;z++)for(int x=0;x<dst.alphamapWidth;x++)for(int l=0;l<src.alphamapLayers;l++)
    paint[z,x,l]=original[Mathf.Min(src.alphamapHeight-1,(int)((z+.5f)/dst.alphamapHeight*src.alphamapHeight)),Mathf.Min(src.alphamapWidth-1,(int)((x+.5f)/dst.alphamapWidth*src.alphamapWidth)),l];
   dst.SetAlphamaps(0,0,paint);
  }
  foreach(var splat in dst.alphamapTextures)EditorUtility.SetDirty(splat);
  EditorUtility.SetDirty(dst);
 }
}

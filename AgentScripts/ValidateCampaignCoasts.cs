using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Conquest.WorldMap;
using BronzeAge.RegionalMapTool;
public static class ValidateCampaignCoasts {
 public static string Run(){
  var c=UnityEngine.Object.FindAnyObjectByType<WorldMapCampaignController>();var m=c.streamer.manifest;
  var world=JsonUtility.FromJson<CoastalWorld>(File.ReadAllText(Path.Combine(Path.GetDirectoryName(m.sourcePackage),"world.json")));
  var package=JsonUtility.FromJson<Conquest.WorldMap.Editor.WorldMapCampaignCoastBuilder.Package>(File.ReadAllText(m.sourcePackage));
  var sampleHeight=Conquest.WorldMap.Editor.WorldMapCampaignCoastBuilder.PackageHeightSampler(package,Path.GetDirectoryName(m.sourcePackage));
  int triangles=0,seams=0;var data=new System.Collections.Generic.Dictionary<string,TerrainData>();
  foreach(var address in m.sectors){var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WorldMap/Campaign/Sectors/"+address.id+".prefab");var terrain=asset.GetComponent<WorldMapCampaignSector>().terrain;data.Add(address.id,terrain.terrainData);
   var expected=BronzeAgeCoastalImporter.BuildMesh(world.costas,address.origin,address.size,sampleHeight);var face=terrain.transform.Find("Canonical coastal faces");
   if(expected){if(!face)throw new Exception("Missing faces "+address.id);var mesh=face.GetComponent<MeshFilter>().sharedMesh;if(!expected.vertices.SequenceEqual(mesh.vertices)||!expected.triangles.SequenceEqual(mesh.triangles))throw new Exception("Geometry differs from export");if(face.localPosition!=Vector3.zero)throw new Exception("Offset coast geometry");triangles+=mesh.triangles.Length/3;UnityEngine.Object.DestroyImmediate(expected);}
   else if(face)throw new Exception("Unexpected faces");
   foreach(var tree in terrain.terrainData.treeInstances){float h=terrain.terrainData.GetInterpolatedHeight(tree.position.x,tree.position.z);if(Mathf.Abs(tree.position.y*1000-h)>.1f)throw new Exception("Floating tree");}
  }
  foreach(var address in m.sectors){var heights=data[address.id].GetHeights(0,0,257,257);
   foreach(var direction in new[]{Vector2Int.right,Vector2Int.up}){var other=m.sectors.FirstOrDefault(q=>q.column==address.column+direction.x&&q.row==address.row+direction.y);if(other==null)continue;var h=data[other.id].GetHeights(0,0,257,257);for(int k=0;k<257;k++)if((direction.x==1?heights[k,256]:heights[256,k])!=(direction.x==1?h[k,0]:h[0,k]))throw new Exception("Height seam");seams++;}
  }
  string report=$"Coastal faces match canonical export: {triangles} triangles; {seams} exact height seams; trees anchored; sector-local meshes; revision={m.coastRevision}";File.WriteAllText("Docs/CampaignCoastsValidation.txt",report);return report;
 }
}

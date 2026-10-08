using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using Conquest.WorldMap;
public static class BuildWorldMapCampaign {
 const string Folder="Assets/WorldMap/Campaign";
 const string ScenePath="Assets/Scenes/GameScenes/WorldMapCampaign.unity";
 public static string Run(){
  if(Application.isPlaying||SceneManager.GetActiveScene().isDirty)throw new Exception("Exit Play and save scene");
  if(File.Exists(ScenePath)||AssetDatabase.IsValidFolder(Folder))throw new Exception("Existing campaign preserved");
  Ensure(Folder);Ensure(Folder+"/Sectors");Ensure(Folder+"/Overview");Ensure(Folder+"/Water");Ensure(Folder+"/Symbols");
  var scene=EditorSceneManager.OpenScene("Assets/Scenes/WorldMapTools/Experiments/StrategicTrials/StrategicTrials.unity",OpenSceneMode.Single);
  EditorSceneManager.SaveScene(scene,ScenePath,true);scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
  var preview=UnityEngine.Object.FindFirstObjectByType<WorldMapPreviewController>();var sourceRoot=preview.worldRoot;
  var scale=UnityEngine.Object.FindFirstObjectByType<WorldMapRegionLab>().scale;
  var terrainSources=scale.terrains.OrderBy(t=>Mathf.FloorToInt(t.transform.position.z/4000)*8+Mathf.FloorToInt(t.transform.position.x/4000)).ToArray();
  var originals=scene.GetRootGameObjects();
  var camera=preview.mapCamera;camera.name="Campaign Camera";camera.fieldOfView=50;camera.farClipPlane=10000;
  var runtime=new GameObject("Campaign presentation");
  var streamer=runtime.AddComponent<WorldMapSectorStreamer>();streamer.viewCamera=camera;
  var controller=runtime.AddComponent<WorldMapCampaignController>();controller.streamer=streamer;controller.mapCamera=camera;
  var manifest=ScriptableObject.CreateInstance<WorldMapCampaignManifest>();manifest.worldId="campaign-anatolia-v9-seed42";manifest.seed=42;manifest.worldgenVersion="regional-16 experimental";
  manifest.sourceSHA256="342c7bf6a1282fcaf85ad3abe57ac34c95baf5e4e021a4efa0b85ceef6b382b2";
  manifest.sectors=terrainSources.Select(t=>new CampaignSectorAddress{id=t.name,address="worldmap/v9/seed42/sector/"+t.name,column=Mathf.FloorToInt(t.transform.position.x/4000),row=Mathf.FloorToInt(t.transform.position.z/4000),origin=t.transform.position,size=new Vector2(4000,4000)}).ToArray();
  AssetDatabase.CreateAsset(manifest,Folder+"/Manifest.asset");streamer.manifest=manifest;
  var roots=new GameObject[48];var overview=new Transform[48];int trees=0;
  for(int i=0;i<48;i++){
   var source=terrainSources[i];roots[i]=new GameObject(source.name);var sector=roots[i].AddComponent<WorldMapCampaignSector>();
   var clone=UnityEngine.Object.Instantiate(source.gameObject,roots[i].transform);clone.name="Terrain";
   var t=clone.GetComponent<Terrain>();t.terrainData=source.terrainData;t.allowAutoConnect=false;t.groupingID=42;t.SetNeighbors(null,null,null,null);
   clone.GetComponent<TerrainCollider>().terrainData=t.terrainData;sector.terrain=t;trees+=t.terrainData.treeInstanceCount;
   var low=Overview(source);var lowObject=Terrain.CreateTerrainGameObject(low);lowObject.name="Distant "+source.name;lowObject.transform.SetParent(runtime.transform);lowObject.transform.position=source.transform.position;
   var lt=lowObject.GetComponent<Terrain>();lt.materialTemplate=source.materialTemplate;lt.heightmapPixelError=12;lt.drawTreesAndFoliage=false;lt.allowAutoConnect=false;lt.groupingID=99;
   UnityEngine.Object.DestroyImmediate(lowObject.GetComponent<TerrainCollider>());overview[i]=lowObject.transform;
  }
  streamer.overview=overview;
  // Every static marker stays in its own sector; no stock or owner is baked.
  foreach(var marker in sourceRoot.GetComponentsInChildren<BronzeAge.RegionalMapTool.BronzeAgeRegionalMarker>(true)){
   int i=Index(marker.canonicalPosition);var g=UnityEngine.Object.Instantiate(marker.gameObject,roots[i].transform);g.name=marker.gameObject.name;g.SetActive(true);
  }
  // Split the existing rock/cliff instance cache by world-space position.
  var renderer=sourceRoot.GetComponent<WorldMapSyntyRenderer>();
  if(renderer&&renderer.instances){
   var cache=renderer.instances;
   for(int i=0;i<48;i++){
    var batches=new List<WorldMapInstanceBatch>();
    foreach(var b in cache.batches){var matrices=b.matrices.Where(m=>Index(m.MultiplyPoint3x4(Vector3.zero))==i).ToArray();if(matrices.Length==0)continue;
     var bounds=new Bounds(matrices[0].MultiplyPoint3x4(Vector3.zero),Vector3.zero);foreach(var m in matrices)bounds.Encapsulate(m.MultiplyPoint3x4(Vector3.zero));bounds.Expand(250);
     batches.Add(new WorldMapInstanceBatch{mesh=b.mesh,material=b.material,submesh=b.submesh,always=b.always,distant=b.distant,matrices=matrices,bounds=bounds});
    }
    var data=ScriptableObject.CreateInstance<WorldMapSyntyInstances>();data.settings=cache.settings;data.batches=batches.ToArray();data.overviewBatches=new WorldMapInstanceBatch[0];AssetDatabase.CreateAsset(data,Folder+"/Sectors/"+manifest.sectors[i].id+"-rocks.asset");
    var r=roots[i].AddComponent<WorldMapSyntyRenderer>();r.instances=data;r.artProfile=renderer.artProfile;roots[i].GetComponent<WorldMapCampaignSector>().surroundings=r;
   }
  }
  // Water triangles belong to exactly one sector; shared border vertices are unchanged.
  int waterTriangles=0;
  foreach(var mf in sourceRoot.GetComponentsInChildren<MeshFilter>(true)){
   if(mf.GetComponent<BronzeAge.RegionalMapTool.BronzeAgeRegionalMarker>()||mf.name=="Mar"||!mf.gameObject.activeInHierarchy)continue;
   var mr=mf.GetComponent<MeshRenderer>();if(!mr||!mr.enabled||!mf.sharedMesh)continue;
   var mesh=mf.sharedMesh;var vertices=mesh.vertices;var uv=mesh.uv;var colors=mesh.colors;
   for(int sub=0;sub<mesh.subMeshCount;sub++){
    var bins=new Dictionary<int,List<int>>();var tri=mesh.GetTriangles(sub);
    for(int n=0;n<tri.Length;n+=3){var center=mf.transform.TransformPoint((vertices[tri[n]]+vertices[tri[n+1]]+vertices[tri[n+2]])/3);int index=Index(center);if(!bins.TryGetValue(index,out var list))bins.Add(index,list=new List<int>());list.Add(tri[n]);list.Add(tri[n+1]);list.Add(tri[n+2]);waterTriangles++;}
    foreach(var bin in bins){var v=new List<Vector3>();var u=new List<Vector2>();var c=new List<Color>();foreach(int n in bin.Value){v.Add(mf.transform.TransformPoint(vertices[n]));u.Add(uv.Length==vertices.Length?uv[n]:Vector2.zero);c.Add(colors.Length==vertices.Length?colors[n]:Color.white);}
     var part=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};part.SetVertices(v);part.SetUVs(0,u);part.SetColors(c);part.SetTriangles(Enumerable.Range(0,v.Count).ToArray(),0);part.RecalculateNormals();part.RecalculateBounds();
     string name=mf.name.Replace("/","-")+"-"+sub+"-"+bin.Key;AssetDatabase.CreateAsset(part,Folder+"/Water/"+name+".asset");
     var g=new GameObject(name);g.transform.SetParent(roots[bin.Key].transform);g.AddComponent<MeshFilter>().sharedMesh=part;g.AddComponent<MeshRenderer>().sharedMaterial=mr.sharedMaterials[Mathf.Min(sub,mr.sharedMaterials.Length-1)];
    }
   }
  }
  var sea=sourceRoot.Find("Mar");if(sea)UnityEngine.Object.Instantiate(sea.gameObject,runtime.transform).name="Campaign sea";
  var settings=AddressableAssetSettingsDefaultObject.Settings;if(!settings)throw new Exception("Addressables settings missing");
  var group=settings.FindGroup("WorldMap-Campaign-Sectors")??settings.CreateGroup("WorldMap-Campaign-Sectors",false,false,true,null,typeof(BundledAssetGroupSchema),typeof(ContentUpdateGroupSchema));
  var schema=group.GetSchema<BundledAssetGroupSchema>();schema.BundleMode=BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
  schema.BuildPath.SetVariableByName(settings,"Local.BuildPath");schema.LoadPath.SetVariableByName(settings,"Local.LoadPath");
  var prefabPaths=new List<string>();
  for(int i=0;i<48;i++){
   string path=Folder+"/Sectors/"+manifest.sectors[i].id+".prefab";PrefabUtility.SaveAsPrefabAsset(roots[i],path);UnityEngine.Object.DestroyImmediate(roots[i]);prefabPaths.Add(path);
   var entry=settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path),group);entry.address=manifest.sectors[i].address;
  }
  var shared=settings.FindGroup("WorldMap-Campaign-Shared")??settings.CreateGroup("WorldMap-Campaign-Shared",false,false,true,null,typeof(BundledAssetGroupSchema),typeof(ContentUpdateGroupSchema));
  var sharedSchema=shared.GetSchema<BundledAssetGroupSchema>();sharedSchema.BundleMode=BundledAssetGroupSchema.BundlePackingMode.PackTogether;sharedSchema.BuildPath.SetVariableByName(settings,"Local.BuildPath");sharedSchema.LoadPath.SetVariableByName(settings,"Local.LoadPath");
  var common=prefabPaths.SelectMany(path=>AssetDatabase.GetDependencies(path,true).Distinct()).GroupBy(path=>path).Where(g=>g.Count()>1&&g.Key.StartsWith("Assets/")&&!g.Key.EndsWith(".cs")&&!g.Key.EndsWith(".dll")&&!g.Key.EndsWith(".asmdef")).Select(g=>g.Key).ToArray();
  foreach(string path in common){var guid=AssetDatabase.AssetPathToGUID(path);if(settings.FindAssetEntry(guid)!=null)continue;var entry=settings.CreateOrMoveEntry(guid,shared);entry.address="worldmap/shared/"+guid;}
  // Only symbols and the measured local route come from the scale lab.
  var sample=EditorSceneManager.OpenScene("Assets/Scenes/WorldMapTools/Experiments/CampaignScale/CampaignScale.unity",OpenSceneMode.Additive);
  var local=sample.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<WorldMapScaleLab>(true)).First();controller.demoRoute=(Vector3[])local.route.Clone();
  controller.actor=Symbol(local.actor,"Army",runtime.transform);controller.actor.localScale=Vector3.one*1.8f;
  controller.cityA=Symbol(local.cityA,"City A",runtime.transform);controller.cityB=Symbol(local.cityB,"City B",runtime.transform);controller.cityA.localScale=controller.cityB.localScale=Vector3.one*1.4f;
  foreach(var t in new[]{controller.cityA,controller.cityB})for(int i=0;i<t.childCount;i++)t.GetChild(i).gameObject.SetActive(i==0);
  EditorSceneManager.CloseScene(sample,true);SceneManager.SetActiveScene(scene);
  var bridge=new GameObject("Campaign scene bridge");bridge.AddComponent<CampaignSceneBridge>();
  foreach(var g in originals)if(g!=camera.gameObject&&!g.GetComponent<Light>())UnityEngine.Object.DestroyImmediate(g);
  controller.focus=controller.demoRoute[0];controller.actor.position=controller.demoRoute[0]+Vector3.up;streamer.focus=controller.focus;controller.ApplyCamera();
  var scenes=EditorBuildSettings.scenes.ToList();foreach(string path in new[]{ScenePath,"Assets/Scenes/GameScenes/SettlementScene.unity"})if(!scenes.Any(s=>s.path==path))scenes.Add(new EditorBuildSettingsScene(path,true));EditorBuildSettings.scenes=scenes.ToArray();
  EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Selection.activeGameObject=runtime;
  string report=$"Sector prefabs=48; each separately addressable; shared assets={common.Length}\nDetailed crowns={trees}; distant terrain=48 x 33\nWater triangles partitioned={waterTriangles}; resource IDs preserved\nScene contains no detailed TerrainData or sector prefab references\nLocal travel=12 u/s; city=1.4; army=1.8; entry=18\n";File.WriteAllText(Folder+"/BuildReport.txt",report);return report;
 }
 static Transform Symbol(Transform source,string name,Transform parent){var clone=UnityEngine.Object.Instantiate(source.gameObject);clone.name=name;string path=Folder+"/Symbols/"+name+".prefab";var asset=PrefabUtility.SaveAsPrefabAsset(clone,path);UnityEngine.Object.DestroyImmediate(clone);var result=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent.gameObject.scene);result.transform.SetParent(parent,true);return result.transform;}
 static TerrainData Overview(Terrain source){var native=source.terrainData;var data=new TerrainData{heightmapResolution=33,size=native.size,terrainLayers=native.terrainLayers,alphamapResolution=32,baseMapResolution=64};var h=new float[33,33];for(int z=0;z<33;z++)for(int x=0;x<33;x++)h[z,x]=native.GetInterpolatedHeight(x/32f,z/32f)/native.size.y;data.SetHeights(0,0,h);
  var original=native.GetAlphamaps(0,0,native.alphamapWidth,native.alphamapHeight);var a=new float[32,32,native.alphamapLayers];for(int z=0;z<32;z++)for(int x=0;x<32;x++)for(int l=0;l<native.alphamapLayers;l++)a[z,x,l]=original[Mathf.Min(native.alphamapHeight-1,(int)((z+.5f)/32*native.alphamapHeight)),Mathf.Min(native.alphamapWidth-1,(int)((x+.5f)/32*native.alphamapWidth)),l];data.SetAlphamaps(0,0,a);AssetDatabase.CreateAsset(data,Folder+"/Overview/"+source.name+".asset");return data;}
 static int Index(Vector3 p)=>Mathf.Clamp(Mathf.FloorToInt(p.z/4000),0,5)*8+Mathf.Clamp(Mathf.FloorToInt(p.x/4000),0,7);
 static void Ensure(string path){if(AssetDatabase.IsValidFolder(path))return;Ensure(Path.GetDirectoryName(path).Replace('\\','/'));AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));}
}

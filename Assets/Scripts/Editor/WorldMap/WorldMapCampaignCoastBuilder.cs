using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Conquest.WorldMap;
using BronzeAge.RegionalMapTool;

namespace Conquest.WorldMap.Editor {
 public static class WorldMapCampaignCoastBuilder {
  const string Folder="Assets/WorldMap/Campaign/Coasts",V9="Assets/Scenes/WorldMapTools/Experiments/MountainsV9/";
  [MenuItem("Tools/BronzeAge/Campaña/Importar costas del worldgen")]
  public static void Choose(){var path=EditorUtility.OpenFilePanel("Paquete regional con costas","","json");if(!string.IsNullOrEmpty(path))Debug.Log(Build(path));}
  [Serializable] public class Package { public string formato,world,worldgenVersion; public int formatoVersion,seed; public float ancho,alto,nivelMar;public Tile[] bloques;public FileEntry[] archivos; }
  [Serializable] public class FileEntry {public string archivo,sha256;public long bytes;}
  [Serializable] public class Tile {public string id,alturas;public int resolucion;public float x,z,ancho,alto;}
  static void Ensure(string folder){if(AssetDatabase.IsValidFolder(folder))return;var parent=Path.GetDirectoryName(folder).Replace('\\','/');Ensure(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(folder));}
  public static Func<Vector3,float> PackageHeightSampler(Package package,string source){
   return BronzeAgeCoastalImporter.HeightSampler(package.bloques.Select(tile=>{
    var bytes=File.ReadAllBytes(Safe(source,tile.alturas));int n=tile.resolucion;var heights=new float[n,n];
    for(int z=0;z<n;z++)for(int x=0;x<n;x++){int i=(z*n+x)*2;heights[z,x]=(bytes[i]|bytes[i+1]<<8)/65535f;}
    return new BronzeAgeCoastalImporter.HeightGrid{origin=new Vector3(tile.x,0,tile.z),size=new Vector3(tile.ancho,1000,tile.alto),heights=heights};
   }));
  }
  public static string Build(string manifestPath){
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play first");
   var c=UnityEngine.Object.FindAnyObjectByType<WorldMapCampaignController>();if(!c)throw new InvalidOperationException("Open WorldMapCampaign");
   if(c.gameObject.scene.isDirty)throw new InvalidOperationException("Save campaign scene first");
   var text=File.ReadAllText(manifestPath);var package=JsonUtility.FromJson<Package>(text);var source=Path.GetDirectoryName(Path.GetFullPath(manifestPath));
   if(package.formato!="bronzeage-regional-unity"||package.formatoVersion!=1||package.seed!=c.streamer.manifest.seed||package.ancho!=c.streamer.manifest.worldSize.x||package.alto!=c.streamer.manifest.worldSize.y||package.nivelMar!=c.streamer.manifest.seaHeight)throw new InvalidDataException("Package does not match campaign");
   foreach(var entry in package.archivos){var path=Safe(source,entry.archivo);if(new FileInfo(path).Length!=entry.bytes)throw new InvalidDataException("Incomplete "+entry.archivo);using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))if(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant()!=entry.sha256)throw new InvalidDataException("Checksum "+entry.archivo);}
   var coastal=JsonUtility.FromJson<CoastalWorld>(File.ReadAllText(Safe(source,package.world))).costas;if(coastal==null||coastal.revision!="costas-1")throw new InvalidDataException("Missing canonical coast geometry");
   // Preflight all inputs before modifying assets.
   foreach(var address in c.streamer.manifest.sectors){var tile=package.bloques.Single(t=>t.id==address.id);if(tile.resolucion!=257||File.ReadAllBytes(Safe(source,tile.alturas)).Length!=257*257*2)throw new InvalidDataException("Heights "+tile.id);if(!AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/WorldMap/Campaign/ForestMasses/"+tile.id+".asset"))throw new InvalidDataException("Missing forest baseline "+tile.id);}
   Ensure(Folder);var rock=AssetDatabase.LoadAssetAtPath<TerrainData>(V9+"tile-4-1.asset").terrainLayers[2];var material=BronzeAgeCoastalImporter.Material(Folder,rock.diffuseTexture);
   var sampleHeight=PackageHeightSampler(package,source);
   var paintChecks=new System.Collections.Generic.Dictionary<string,string>();
   int crowns=0,removed=0,triangles=0,changed=0;var heightsById=new System.Collections.Generic.Dictionary<string,TerrainData>();
   foreach(var address in c.streamer.manifest.sectors){
    var tile=package.bloques.Single(t=>t.id==address.id);var baseline=AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/WorldMap/Campaign/ForestMasses/"+tile.id+".asset");
    string terrainPath=Folder+"/"+tile.id+".asset";
    // Keep the native TerrainData and its owned splat subassets stable across rebuilds.
    // CopySerialized from a temporary clone can replace these with transient texture state.
    var data=AssetDatabase.LoadAssetAtPath<TerrainData>(terrainPath);
    if(!data){data=new TerrainData{heightmapResolution=257,size=baseline.size,baseMapResolution=baseline.baseMapResolution};AssetDatabase.CreateAsset(data,terrainPath);}
    data.treePrototypes=baseline.treePrototypes;
    var accepted=AssetDatabase.LoadAssetAtPath<TerrainData>(V9+tile.id+".asset");
    data.terrainLayers=accepted.terrainLayers;data.alphamapResolution=accepted.alphamapResolution;var paint=accepted.GetAlphamaps(0,0,accepted.alphamapWidth,accepted.alphamapHeight);
    var raw=File.ReadAllBytes(Safe(source,tile.alturas));var heights=new float[257,257];var old=baseline.GetHeights(0,0,257,257);
    for(int z=0;z<257;z++)for(int x=0;x<257;x++){int i=(z*257+x)*2;heights[z,x]=(raw[i]|raw[i+1]<<8)/65535f;if(Mathf.Abs(heights[z,x]-old[z,x])>.00002f)changed++;}
    data.SetHeights(0,0,heights);
    // Rock appears on the newly steep shore; all other accepted regional paint is retained.
    for(int z=0;z<data.alphamapHeight;z++)for(int x=0;x<data.alphamapWidth;x++){
     float u=(x+.5f)/data.alphamapWidth,v=(z+.5f)/data.alphamapHeight;
     float delta=Mathf.Abs(data.GetInterpolatedHeight(u,v)-baseline.GetInterpolatedHeight(u,v));
     float weight=Mathf.Clamp01((delta-1)/8)*Mathf.Clamp01((data.GetSteepness(u,v)-16)/24);
     for(int l=0;l<data.alphamapLayers;l++)paint[z,x,l]*=1-weight;paint[z,x,2]+=weight;
    }
    data.SetAlphamaps(0,0,paint);var trees=baseline.treeInstances.Where(t=>data.GetInterpolatedHeight(t.position.x,t.position.z)>package.nivelMar+.5f&&data.GetSteepness(t.position.x,t.position.z)<38).ToArray();removed+=baseline.treeInstanceCount-trees.Length;
    for(int i=0;i<trees.Length;i++){var t=trees[i];t.position.y=data.GetInterpolatedHeight(t.position.x,t.position.z)/data.size.y;trees[i]=t;}data.SetTreeInstances(trees,false);crowns+=trees.Length;
    // Persisting a cloned TerrainData may reset its splat textures. Paint the persistent asset.
    data.terrainLayers=accepted.terrainLayers;data.SetAlphamaps(0,0,paint);data.SetBaseMapDirty();foreach(var splat in data.alphamapTextures)EditorUtility.SetDirty(splat);EditorUtility.SetDirty(data);
    var persistedPaint=data.GetAlphamaps(0,0,data.alphamapWidth,data.alphamapHeight);float paintError=0;for(int z=0;z<data.alphamapHeight;z++)for(int x=0;x<data.alphamapWidth;x++)for(int l=0;l<data.alphamapLayers;l++)paintError=Mathf.Max(paintError,Mathf.Abs(paint[z,x,l]-persistedPaint[z,x,l]));if(paintError>.02f)throw new InvalidDataException("Regional paint lost while saving "+tile.id);
    heightsById.Add(tile.id,data);paintChecks.Add(AssetDatabase.GetAssetPath(data),PaintHash(data));
    var mesh=BronzeAgeCoastalImporter.BuildMesh(coastal,address.origin,address.size,sampleHeight);if(mesh){triangles+=mesh.triangles.Length/3;mesh=Save(mesh,Folder+"/"+tile.id+"-faces.asset");}
    string prefabPath="Assets/WorldMap/Campaign/Sectors/"+tile.id+".prefab";var root=PrefabUtility.LoadPrefabContents(prefabPath);
    try {var sector=root.GetComponent<WorldMapCampaignSector>();sector.terrain.terrainData=data;root.GetComponentInChildren<TerrainCollider>().terrainData=data;BronzeAgeCoastalImporter.Install(sector.terrain.transform,mesh,material);
     foreach(var marker in root.GetComponentsInChildren<BronzeAgeRegionalMarker>(true)){var p=marker.canonicalPosition;p.y=data.GetInterpolatedHeight((p.x-address.origin.x)/tile.ancho,(p.z-address.origin.z)/tile.alto)+address.origin.y;marker.canonicalPosition=p;marker.transform.position=p+Vector3.up*10;}
     PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
    }finally{PrefabUtility.UnloadPrefabContents(root);}
    var overview=c.streamer.overview.Single(o=>o.position==address.origin);var low=overview.GetComponent<Terrain>().terrainData;var h=new float[33,33];for(int z=0;z<33;z++)for(int x=0;x<33;x++)h[z,x]=data.GetInterpolatedHeight(x/32f,z/32f)/data.size.y;low.SetHeights(0,0,h);low.terrainLayers=data.terrainLayers;
    var a=new float[low.alphamapHeight,low.alphamapWidth,data.alphamapLayers];for(int z=0;z<low.alphamapHeight;z++)for(int x=0;x<low.alphamapWidth;x++)for(int l=0;l<data.alphamapLayers;l++)a[z,x,l]=paint[Mathf.Min(data.alphamapHeight-1,(int)((z+.5f)/low.alphamapHeight*data.alphamapHeight)),Mathf.Min(data.alphamapWidth-1,(int)((x+.5f)/low.alphamapWidth*data.alphamapWidth)),l];low.SetAlphamaps(0,0,a);low.SetBaseMapDirty();foreach(var splat in low.alphamapTextures)EditorUtility.SetDirty(splat);EditorUtility.SetDirty(low);BronzeAgeCoastalImporter.Install(overview,mesh,material);paintChecks.Add(AssetDatabase.GetAssetPath(low),PaintHash(low));
   }
   foreach(var item in new[]{c.cityA,c.cityB,c.actor}){var p=item.position;int col=Mathf.Clamp((int)(p.x/4000),0,7),row=Mathf.Clamp((int)(p.z/4000),0,5);var data=heightsById["tile-"+col+"-"+row];p.y=data.GetInterpolatedHeight((p.x-col*4000)/4000,(p.z-row*4000)/4000)+(item==c.actor?c.actorLift:0);item.position=p;}
   var manifest=c.streamer.manifest;manifest.coastRevision=coastal.revision;manifest.sourcePackage=Path.GetFullPath(manifestPath);manifest.worldgenVersion=package.worldgenVersion;using(var sha=SHA256.Create())manifest.sourceSHA256=BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();EditorUtility.SetDirty(manifest);
   EditorSceneManager.MarkSceneDirty(c.gameObject.scene);EditorSceneManager.SaveScene(c.gameObject.scene);AssetDatabase.SaveAssets();
   foreach(var check in paintChecks){AssetDatabase.ImportAsset(check.Key,ImportAssetOptions.ForceUpdate);if(PaintHash(AssetDatabase.LoadAssetAtPath<TerrainData>(check.Key))!=check.Value)throw new InvalidDataException("Paint changed after asset reload: "+check.Key);}
   var report=$"Canonical coast revision={coastal.revision}; segments={coastal.segmentos.Length}; triangles={triangles}; detailed sectors=48; overview sectors=48; changed height samples={changed}; crowns={crowns}; removed on water/steep slopes={removed}. Resource IDs and planar positions preserved. Source={manifestPath}";File.WriteAllText("Docs/CampaignCoasts.txt",report);return report;
  }
  static string PaintHash(TerrainData data){var alpha=data.GetAlphamaps(0,0,data.alphamapWidth,data.alphamapHeight);var bytes=new byte[Buffer.ByteLength(alpha)];Buffer.BlockCopy(alpha,0,bytes,0,bytes.Length);using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes));}
  static string Safe(string folder,string relative){var root=Path.GetFullPath(folder)+Path.DirectorySeparatorChar;var path=Path.GetFullPath(Path.Combine(folder,relative));if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Path outside package");return path;}
  static Mesh Save(Mesh value,string path){
   var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
   if(old){
    // CopySerialized can leave a previously uploaded GPU vertex buffer stale.
    old.Clear();old.indexFormat=value.indexFormat;old.vertices=value.vertices;old.uv=value.uv;
    old.triangles=value.triangles;old.normals=value.normals;old.bounds=value.bounds;
    old.UploadMeshData(false);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;
   }
   value.UploadMeshData(false);AssetDatabase.CreateAsset(value,path);return value;
  }
 }
}

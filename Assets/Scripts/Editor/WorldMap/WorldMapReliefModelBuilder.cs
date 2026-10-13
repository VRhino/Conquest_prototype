using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BronzeAge.RegionalMapTool;

namespace Conquest.WorldMap.Editor {
 // Presentation bake only. Never edits canonical heights, navigation or resource positions.
 public static class WorldMapReliefModelBuilder {
  public const string Folder="Assets/WorldMap/Campaign/ReliefModels";
  public const string ChildName="Synty relief landmarks";
  const string Prefabs="Assets/Synty/PolygonFantasyKingdom/Prefabs/";
  [Serializable] public class ResourcePoint {public string id;public float x,z;}
  [Serializable] public class WorldInput {public CoastalData costas;public ResourcePoint[] recursos;}
  public sealed class Context {
   public WorldMapCampaignManifest manifest;public WorldMapLocalNavigation navigation;
   public WorldInput world;public WorldMapReliefModelProfile profile;
   public Dictionary<string,TerrainData> terrains=new Dictionary<string,TerrainData>();
   public Dictionary<GameObject,WorldMapSyntyBaker.Model> models=new Dictionary<GameObject,WorldMapSyntyBaker.Model>();
   public CampaignSectorAddress Sector(Vector3 p)=>manifest.sectors.FirstOrDefault(s=>p.x>=s.origin.x&&p.z>=s.origin.z&&p.x<s.origin.x+s.size.x&&p.z<s.origin.z+s.size.y);
   public float Height(Vector3 p){var s=Sector(p);return s==null?manifest.seaHeight:terrains[s.id].GetInterpolatedHeight((p.x-s.origin.x)/s.size.x,(p.z-s.origin.z)/s.size.y)+s.origin.y;}
  }
  static void Ensure(string path){if(AssetDatabase.IsValidFolder(path))return;var parent=Path.GetDirectoryName(path).Replace('\\','/');Ensure(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
  public static WorldMapReliefModelProfile Profile(){
   Ensure(Folder);var p=AssetDatabase.LoadAssetAtPath<WorldMapReliefModelProfile>(Folder+"/Profile.asset");
   if(p)return p;p=ScriptableObject.CreateInstance<WorldMapReliefModelProfile>();
   GameObject[] Load(string family,params string[] names)=>names.Select(n=>AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+family+"/"+n+".prefab")??throw new FileNotFoundException(n)).ToArray();
   p.greenMountains=Load("Generic","SM_Generic_Mountains_Grass_01","SM_Generic_Mountains_Grass_02");
   p.snowMountains=Load("Generic","SM_Generic_Mountains_Snow_01","SM_Generic_Mountains_Snow_02");
   p.coastalCliffs=Load("Environments","SM_Env_Rock_Cliff_01","SM_Env_Rock_Cliff_02","SM_Env_Rock_Cliff_03","SM_Env_Rock_Cliff_04","SM_Env_Rock_Cliff_05");
   AssetDatabase.CreateAsset(p,Folder+"/Profile.asset");return p;
  }
  public static Context Read(WorldMapCampaignController controller){
   var m=controller.streamer.manifest;var nav=controller.navigation;
   if(!nav||nav.sourceSHA256!=m.sourceSHA256)throw new InvalidOperationException("Rebuild navigation for this geography first");
   var c=new Context{manifest=m,navigation=nav,profile=Profile(),world=JsonUtility.FromJson<WorldInput>(File.ReadAllText(Path.Combine(Path.GetDirectoryName(m.sourcePackage),"world.json")))};
   var materials=new Dictionary<Material,Material>();
   foreach(var prefab in c.profile.greenMountains.Concat(c.profile.snowMountains).Concat(c.profile.coastalCliffs).Distinct()){
    if(!prefab)throw new InvalidDataException("Missing Synty relief prefab");c.models.Add(prefab,WorldMapSyntyBaker.ReadModel(prefab,materials));
   }
   foreach(var s in m.sectors){var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WorldMap/Campaign/Sectors/"+s.id+".prefab");c.terrains.Add(s.id,asset.GetComponent<WorldMapCampaignSector>().terrain.terrainData);}
   return c;
  }
  static float Random(int seed,int x,int z,int stream){unchecked{uint h=(uint)seed^(uint)x*0x9e3779b9u^(uint)z*0x85ebca6bu^(uint)stream*0xc2b2ae35u;h^=h>>16;h*=0x7feb352du;h^=h>>15;h*=0x846ca68bu;h^=h>>16;return (h&0xffffff)/16777216f;}}
  static bool InTrial(Context c,Vector3 p,Rect trial)=>c.profile.wholeWorld||trial.Contains(new Vector2(p.x/c.manifest.worldSize.x,p.z/c.manifest.worldSize.y));
  public static bool SafeFootprint(Context c,Bounds footprint,bool mountain){
   var margin=c.profile.walkableClearance;float minX=footprint.min.x-margin,maxX=footprint.max.x+margin,minZ=footprint.min.z-margin,maxZ=footprint.max.z+margin;
   var n=c.navigation;if(minX<0||minZ<0||maxX>=c.manifest.worldSize.x||maxZ>=c.manifest.worldSize.y)return false;
   for(int z=Mathf.FloorToInt(minZ/n.cellSize);z<=Mathf.FloorToInt(maxZ/n.cellSize);z++)for(int x=Mathf.FloorToInt(minX/n.cellSize);x<=Mathf.FloorToInt(maxX/n.cellSize);x++){
    byte block=n.blocked[z*n.columns+x];if(mountain?block!=2:block!=1&&block!=2)return false;
   }
   foreach(var r in c.world.recursos??Array.Empty<ResourcePoint>())if(r.x>=minX-c.profile.resourceClearance&&r.x<=maxX+c.profile.resourceClearance&&r.z>=minZ-c.profile.resourceClearance&&r.z<=maxZ+c.profile.resourceClearance)return false;
   return true;
  }
  static WorldMapReliefPlacement Placement(Context c,string id,string kind,GameObject prefab,Vector3 anchor,Vector3 scale,float yaw){
   var bounds=c.models[prefab].bounds;
   var matrix=Matrix4x4.TRS(anchor,Quaternion.Euler(0,yaw,0),scale)*Matrix4x4.Translate(new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z));
   return new WorldMapReliefPlacement{id=id,kind=kind,prefab=prefab,sectorId=c.Sector(anchor).id,position=matrix.GetColumn(3),scale=scale,yaw=yaw,footprint=WorldMapSyntyBaker.TransformBounds(bounds,matrix)};
  }
  public static WorldMapReliefPlacement[] Generate(Context c){
   var p=c.profile;var result=new List<WorldMapReliefPlacement>();var candidates=new List<(int x,int z,Vector3 point,float score)>();
   int nx=Mathf.CeilToInt(c.manifest.worldSize.x/p.candidateSpacing),nz=Mathf.CeilToInt(c.manifest.worldSize.y/p.candidateSpacing);
   for(int z=0;z<nz;z++)for(int x=0;x<nx;x++){
    var q=new Vector3((x+.2f+Random(c.manifest.seed,x,z,1)*.6f)*p.candidateSpacing,0,(z+.2f+Random(c.manifest.seed,x,z,2)*.6f)*p.candidateSpacing);
    if(!InTrial(c,q,p.mountainTrial)||c.Sector(q)==null)continue;
    int i=c.navigation.Index(q);if(i<0||c.navigation.blocked[i]!=2)continue;q.y=c.Height(q);
    if(q.y>=p.minimumElevation)candidates.Add((x,z,q,q.y+Random(c.manifest.seed,x,z,3)*70));
   }
   var anchors=new List<Vector3>();
   foreach(var candidate in candidates.OrderByDescending(q=>q.score).ThenBy(q=>q.x).ThenBy(q=>q.z)){
    if(anchors.Count>=p.maximumMountains)break;var q=candidate.point;
    if(anchors.Any(a=>new Vector2(a.x-q.x,a.z-q.z).sqrMagnitude<p.mountainSeparation*p.mountainSeparation))continue;
    bool snow=q.y>=p.snowElevation;var family=snow?p.snowMountains:p.greenMountains;
    var prefab=family[Mathf.Min(family.Length-1,(int)(Random(c.manifest.seed,candidate.x,candidate.z,4)*family.Length))];var bounds=c.models[prefab].bounds;
    float yaw=Random(c.manifest.seed,candidate.x,candidate.z,5)*360;
    float width=Mathf.Lerp(p.mountainWidth.x,p.mountainWidth.y,Random(c.manifest.seed,candidate.x,candidate.z,6));
    for(int attempt=0;attempt<5&&width>=p.mountainWidth.x-.01f;attempt++,width=Mathf.Max(p.mountainWidth.x,width*.8f)){
     float minimum=q.y,maximum=q.y;var rotation=Quaternion.Euler(0,yaw,0);
     for(int j=0;j<12;j++){float a=j*Mathf.PI/6;float h=c.Height(q+rotation*new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*width*.5f);minimum=Mathf.Min(minimum,h);maximum=Mathf.Max(maximum,h);}
     float height=Mathf.Clamp(maximum-minimum+width*.48f,p.mountainHeight.x,p.mountainHeight.y);
     var scale=new Vector3(width/Mathf.Max(bounds.size.x,bounds.size.z),height/bounds.size.y,width/Mathf.Max(bounds.size.x,bounds.size.z));
     var anchor=new Vector3(q.x,minimum-height*p.mountainEmbed,q.z);
     var item=Placement(c,"mountain-"+candidate.x+"-"+candidate.z,snow?"snow":"green",prefab,anchor,scale,yaw);
     item.exposedHeight=height;
     if(!SafeFootprint(c,item.footprint,true))continue;result.Add(item);anchors.Add(q);break;
    }
   }
   int cliffs=0;
   foreach(var segment in c.world.costas.segmentos.Where(s=>s.tipo=="acantilado").OrderBy(s=>s.id,StringComparer.Ordinal)){
    var a=segment.puntos[0].Position;var b=segment.puntos[1].Position;
    var inward=(segment.puntos[4].Position-a)+(segment.puntos[5].Position-b);inward.y=0;inward.Normalize();
    int slots=Mathf.Max(1,Mathf.FloorToInt(Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z))/p.cliffSpacing));
    for(int slot=0;slot<slots;slot++){
     if(cliffs>=p.maximumCliffs)break;
     int keyX=Mathf.RoundToInt(a.x),keyZ=Mathf.RoundToInt(a.z)+slot;
     float u=(slot+.2f+Random(c.manifest.seed,keyX,keyZ,7)*.6f)/slots;
     var q=Vector3.Lerp(a,b,u)+inward*12;if(!InTrial(c,q,p.coastTrial)||c.Sector(q)==null)continue;
     var prefab=p.coastalCliffs[Mathf.Min(p.coastalCliffs.Length-1,(int)(Random(c.manifest.seed,keyX,keyZ,8)*p.coastalCliffs.Length))];var bounds=c.models[prefab].bounds;
     float width=Mathf.Lerp(p.cliffWidth.x,p.cliffWidth.y,Random(c.manifest.seed,keyX,keyZ,9));
     float depth=Mathf.Lerp(p.cliffDepth.x,p.cliffDepth.y,Random(c.manifest.seed,keyX,keyZ,10));
     float height=Mathf.Max(25,Vector3.Lerp(segment.puntos[4].Position,segment.puntos[5].Position,u).y-c.manifest.seaHeight+p.cliffBelowSea+4);
     var anchor=new Vector3(q.x,c.manifest.seaHeight-p.cliffBelowSea,q.z);
     float yaw=Mathf.Atan2(inward.x,inward.z)*Mathf.Rad2Deg;
     var scale=new Vector3(width/bounds.size.x,height/bounds.size.y,depth/bounds.size.z);
     var item=Placement(c,segment.id+"-rock-"+slot,"coast",prefab,anchor,scale,yaw);
     if(!SafeFootprint(c,item.footprint,false))continue;
     if(result.Where(r=>r.kind=="coast").Any(r=>new Vector2(r.footprint.center.x-q.x,r.footprint.center.z-q.z).sqrMagnitude<p.cliffSpacing*p.cliffSpacing*.7f))continue;
     result.Add(item);cliffs++;
    }
   }
   return result.OrderBy(r=>r.id,StringComparer.Ordinal).ToArray();
  }
  public static string Fingerprint(IEnumerable<WorldMapReliefPlacement> layout){
   var text=new StringBuilder();foreach(var p in layout){text.Append(p.id).Append('|').Append(p.kind).Append('|').Append(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(p.prefab))).Append('|').Append(p.exposedHeight.ToString("R",CultureInfo.InvariantCulture));var m=p.Matrix;for(int i=0;i<16;i++)text.Append('|').Append(m[i].ToString("R",CultureInfo.InvariantCulture));}
   using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-","").ToLowerInvariant();
  }
  [MenuItem("Tools/BronzeAge/Campaña/Reconstruir relieve con modelos Synty")]
  public static void Menu()=>Debug.Log(Build());
  public static string Build(){
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play first");
   var controller=UnityEngine.Object.FindAnyObjectByType<WorldMapCampaignController>();if(!controller)throw new InvalidOperationException("Open WorldMapCampaign");
   if(controller.gameObject.scene.isDirty)throw new InvalidOperationException("Save scene first");
   var c=Read(controller);var placements=Generate(c);var hash=Fingerprint(placements);
   if(hash!=Fingerprint(Generate(c)))throw new InvalidOperationException("Unstable seeded placement");
   if(!placements.Any(p=>p.kind!="coast")||!placements.Any(p=>p.kind=="coast"))throw new InvalidOperationException("Trial has no safe mountain or coast placements");
   string path=Folder+"/Layout.asset";var layout=AssetDatabase.LoadAssetAtPath<WorldMapReliefLayout>(path);if(!layout){layout=ScriptableObject.CreateInstance<WorldMapReliefLayout>();AssetDatabase.CreateAsset(layout,path);}
   layout.revision=1;layout.seed=c.manifest.seed;layout.sourceSHA256=c.manifest.sourceSHA256;layout.fingerprint=hash;layout.profile=c.profile;layout.placements=placements;EditorUtility.SetDirty(layout);
   long triangles=0;int batchCount=0;
   foreach(var sector in c.manifest.sectors){
    var groups=new Dictionary<(Mesh,Material,int,int,int),List<Matrix4x4>>();
    foreach(var p in placements.Where(q=>q.sectorId==sector.id))foreach(var part in c.models[p.prefab].parts){
     var mesh=p.kind=="coast"?part.mesh:ConformMountain(c,p,part);
     var key=(mesh,part.material,part.submesh,Mathf.FloorToInt(p.footprint.center.x/500),Mathf.FloorToInt(p.footprint.center.z/500));
     if(!groups.TryGetValue(key,out var matrices))groups.Add(key,matrices=new List<Matrix4x4>());matrices.Add(p.Matrix*part.transform);
    }
    var batches=new List<WorldMapInstanceBatch>();foreach(var group in groups){var matrices=group.Value.ToArray();var bounds=WorldMapSyntyBaker.TransformBounds(group.Key.Item1.bounds,matrices[0]);foreach(var m in matrices.Skip(1))bounds.Encapsulate(WorldMapSyntyBaker.TransformBounds(group.Key.Item1.bounds,m));batches.Add(new WorldMapInstanceBatch{mesh=group.Key.Item1,material=group.Key.Item2,submesh=group.Key.Item3,matrices=matrices,bounds=bounds,always=true});triangles+=(long)group.Key.Item1.GetIndexCount(group.Key.Item3)/3*matrices.Length;}
    var root=PrefabUtility.LoadPrefabContents("Assets/WorldMap/Campaign/Sectors/"+sector.id+".prefab");
    try{
     var profile=root.GetComponent<WorldMapCampaignSector>().surroundings?.artProfile;
     WorldMapSyntyInstances data=null;if(batches.Count>0){string dataPath=Folder+"/"+sector.id+".asset";data=AssetDatabase.LoadAssetAtPath<WorldMapSyntyInstances>(dataPath);if(!data){data=ScriptableObject.CreateInstance<WorldMapSyntyInstances>();AssetDatabase.CreateAsset(data,dataPath);}data.revision=1;data.settings=WorldMapSyntyBaker.Settings();data.batches=batches.ToArray();data.overviewBatches=data.batches;data.bounds=batches[0].bounds;foreach(var b in batches)data.bounds.Encapsulate(b.bounds);data.detailTriangles=batches.Sum(b=>(long)b.mesh.GetIndexCount(b.submesh)/3*b.matrices.Length);EditorUtility.SetDirty(data);}
     Install(root.transform,data,controller.mapCamera,profile);
     Install(controller.streamer.overview.Single(o=>o.position==sector.origin),data,controller.mapCamera,profile);
     PrefabUtility.SaveAsPrefabAsset(root,"Assets/WorldMap/Campaign/Sectors/"+sector.id+".prefab");
    }finally{PrefabUtility.UnloadPrefabContents(root);}batchCount+=batches.Count;
   }
   EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);EditorSceneManager.SaveScene(controller.gameObject.scene);AssetDatabase.SaveAssets();
   string report=$"Seed={layout.seed}; mountains={placements.Count(p=>p.kind!="coast")}; snow={placements.Count(p=>p.kind=="snow")}; coastal rocks={placements.Count(p=>p.kind=="coast")}; triangles={triangles}; batches={batchCount}; fingerprint={hash}. Heights, paint, forests, navigation and resource IDs unchanged; all footprints avoid walkable cells and resource nodes.";
   File.WriteAllText("Docs/ReliefModelBuild.txt",report);return report;
  }
  // Preserve Synty topology/UVs. Only the decorative base conforms; canonical Terrain stays intact.
  static Mesh ConformMountain(Context c,WorldMapReliefPlacement placement,WorldMapSyntyBaker.Part part){
   var vertices=part.mesh.vertices;var bounds=c.models[placement.prefab].bounds;
   var matrix=placement.Matrix*part.transform;var inverse=matrix.inverse;
   for(int i=0;i<vertices.Length;i++){
    var model=part.transform.MultiplyPoint3x4(vertices[i]);float h=Mathf.Clamp01((model.y-bounds.min.y)/bounds.size.y);
    var world=matrix.MultiplyPoint3x4(vertices[i]);
    world.y=c.Height(world)+Mathf.SmoothStep(0,1,h)*placement.exposedHeight-(1-h)*placement.exposedHeight*c.profile.mountainEmbed;
    vertices[i]=inverse.MultiplyPoint3x4(world);
   }
   var mesh=UnityEngine.Object.Instantiate(part.mesh);mesh.name=placement.id+" conforming Synty";mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();
   // Asset GUID is stable across editor sessions; never key a persistent asset by instance ID.
   string path=Folder+"/"+placement.id+"-"+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(part.mesh))+".asset";
   var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing){existing.Clear();existing.indexFormat=mesh.indexFormat;existing.vertices=mesh.vertices;existing.uv=mesh.uv;existing.subMeshCount=mesh.subMeshCount;for(int s=0;s<mesh.subMeshCount;s++)existing.SetTriangles(mesh.GetTriangles(s),s);existing.normals=mesh.normals;existing.bounds=mesh.bounds;existing.UploadMeshData(false);EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(mesh);return existing;}
   mesh.UploadMeshData(false);AssetDatabase.CreateAsset(mesh,path);return mesh;
  }
  static void Install(Transform parent,WorldMapSyntyInstances data,Camera camera,WorldMapArtSettings profile){
   var old=parent.Find(ChildName);if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);if(!data)return;
   var root=new GameObject(ChildName);root.transform.SetParent(parent,false);var renderer=root.AddComponent<WorldMapSyntyRenderer>();renderer.instances=data;renderer.viewCamera=camera;renderer.artProfile=profile;
  }
 }
}

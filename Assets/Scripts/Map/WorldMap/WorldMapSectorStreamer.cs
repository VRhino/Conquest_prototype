using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
namespace Conquest.WorldMap {
 // Owns only presentation leases. Never generates geography or queries gameplay state.
 public sealed class WorldMapSectorStreamer : MonoBehaviour {
  public WorldMapCampaignManifest manifest;public Camera viewCamera;public Transform[] overview;
  [Range(0,1)] public int preloadRadius=1;
  [Min(.05f)] public float reconcileInterval=.2f;
  [Min(1)] public float failureRetrySeconds=30;
  public Vector3 focus;
  public Transform trackedActor;
  [NonSerialized] public bool trackTravel;
  [NonSerialized] public Vector3 travelLookAhead;
  public int LoadedCount=>loaded.Count;public int PendingCount=>pending.HasValue?1:0;
  public int TotalLoads{get;private set;}public int TotalReleases{get;private set;}
  public float LastLoadMilliseconds{get;private set;}
  public string Error{get;private set;}="";
  public bool Suspended{get;private set;}
  readonly Dictionary<int,AsyncOperationHandle<GameObject>> loaded=new Dictionary<int,AsyncOperationHandle<GameObject>>();
  readonly Dictionary<int,float> failures=new Dictionary<int,float>();
  AsyncOperationHandle<GameObject>? pending;
  readonly HashSet<int> desired=new HashSet<int>();
  readonly List<int> streamIndices=new List<int>(),validationIndices=new List<int>(),nearbyIndices=new List<int>(),releaseIndices=new List<int>();
  readonly Dictionary<(int,int),Terrain> terrainByCell=new Dictionary<(int,int),Terrain>();
  Comparison<int> distanceComparison;Vector3 sortPosition;

  void OnEnable(){if(Application.isPlaying)StartCoroutine(Stream());}
  void FillDesired(Vector3 p,List<int> output){
   output.Clear();
   if(!manifest||manifest.sectors==null)return;
   int x=Mathf.Clamp(Mathf.FloorToInt(p.x/manifest.sectorSize),0,Mathf.CeilToInt(manifest.worldSize.x/manifest.sectorSize)-1);
   int z=Mathf.Clamp(Mathf.FloorToInt(p.z/manifest.sectorSize),0,Mathf.CeilToInt(manifest.worldSize.y/manifest.sectorSize)-1);
   for(int i=0;i<manifest.sectors.Length;i++)if(Mathf.Abs(manifest.sectors[i].column-x)<=preloadRadius&&Mathf.Abs(manifest.sectors[i].row-z)<=preloadRadius)output.Add(i);
   sortPosition=p;distanceComparison??=CompareDistance;output.Sort(distanceComparison);
  }
  int CompareDistance(int a,int b){
   var offset=new Vector3(manifest.sectorSize/2,0,manifest.sectorSize/2);
   int order=(manifest.sectors[a].origin+offset-sortPosition).sqrMagnitude.CompareTo((manifest.sectors[b].origin+offset-sortPosition).sqrMagnitude);
   return order!=0?order:a.CompareTo(b);
  }
  public int[] DesiredIndices(Vector3 p){var result=new List<int>();FillDesired(p,result);return result.ToArray();}
  void FillActiveDesired(List<int> result){
   result.Clear();
   if(trackedActor){
    FillDesired(trackedActor.position,nearbyIndices);result.AddRange(nearbyIndices);
    if(trackTravel)for(int i=0;i<manifest.sectors.Length;i++){var sector=manifest.sectors[i];if(travelLookAhead.x>=sector.origin.x&&travelLookAhead.z>=sector.origin.z&&travelLookAhead.x<sector.origin.x+manifest.sectorSize&&travelLookAhead.z<sector.origin.z+manifest.sectorSize){if(!result.Contains(i))result.Add(i);break;}}
   }
   FillDesired(focus,nearbyIndices);foreach(int i in nearbyIndices)if(!result.Contains(i))result.Add(i);
  }
  // Public diagnostic APIs return independent snapshots; the coroutine reuses private scratch.
  public int[] ActiveDesiredIndices(){var result=new List<int>();FillActiveDesired(result);return result.ToArray();}
  IEnumerator Stream(){
   if(!manifest||manifest.sectors==null){Error="Falta manifiesto de campaña";yield break;}
   var wait=new WaitForSecondsRealtime(reconcileInterval);
   while(true){
    if(Suspended){yield return null;continue;}
    FillActiveDesired(streamIndices);desired.Clear();foreach(int index in streamIndices)desired.Add(index);
    releaseIndices.Clear();foreach(int index in loaded.Keys)if(!desired.Contains(index))releaseIndices.Add(index);
    foreach(int index in releaseIndices)Release(index,false);
    if(releaseIndices.Count>0)ConnectNeighbors();
    foreach(int index in streamIndices){
     if(loaded.ContainsKey(index)||(failures.TryGetValue(index,out float retry)&&Time.unscaledTime<retry))continue;
     float started=Time.realtimeSinceStartup;
     var handle=Addressables.InstantiateAsync(manifest.sectors[index].address,transform,false,true);pending=handle;
     yield return handle;pending=null;
     LastLoadMilliseconds=(Time.realtimeSinceStartup-started)*1000;
     if(handle.Status!=AsyncOperationStatus.Succeeded){Error=$"Sector {manifest.sectors[index].id}: {handle.OperationException?.Message}";failures[index]=Time.unscaledTime+failureRetrySeconds;Addressables.Release(handle);break;}
     // Camera can move while an asynchronous request is in flight.
     FillActiveDesired(validationIndices);
     if(Suspended||!validationIndices.Contains(index)){ReleaseHandle(handle);break;}
     var sector=handle.Result.GetComponent<WorldMapCampaignSector>();
     if(!sector||!sector.terrain){Error="Sector sin Terrain";ReleaseHandle(handle);failures[index]=Time.unscaledTime+failureRetrySeconds;break;}
     sector.Bind(viewCamera);loaded.Add(index,handle);TotalLoads++;Error="";
     if(overview!=null&&index<overview.Length&&overview[index])overview[index].gameObject.SetActive(false);
     ConnectNeighbors();yield return null;
    }
    wait.waitTime=reconcileInterval;yield return wait;
   }
  }
  void ConnectNeighbors(){
   terrainByCell.Clear();
   foreach(var pair in loaded){if(!pair.Value.IsValid()||!pair.Value.Result)continue;var sector=manifest.sectors[pair.Key];var component=pair.Value.Result.GetComponent<WorldMapCampaignSector>();if(component&&component.terrain)terrainByCell[(sector.column,sector.row)]=component.terrain;}
   foreach(var pair in terrainByCell){var cell=pair.Key;terrainByCell.TryGetValue((cell.Item1-1,cell.Item2),out var left);terrainByCell.TryGetValue((cell.Item1,cell.Item2+1),out var top);terrainByCell.TryGetValue((cell.Item1+1,cell.Item2),out var right);terrainByCell.TryGetValue((cell.Item1,cell.Item2-1),out var bottom);pair.Value.SetNeighbors(left,top,right,bottom);}
  }
  public bool TryHeight(Vector3 p,out float height){
   foreach(var item in loaded){if(!item.Value.IsValid()||!item.Value.Result)continue;var sector=item.Value.Result.GetComponent<WorldMapCampaignSector>();var t=sector.terrain;var o=t.transform.position;var size=t.terrainData.size;
    if(p.x>=o.x&&p.z>=o.z&&p.x<o.x+size.x&&p.z<o.z+size.z){height=t.SampleHeight(p)+o.y;return true;}}
   height=manifest?manifest.seaHeight:375;return false;
  }
  public float SurfaceHeight(Vector3 p){if(TryHeight(p,out float h))return Mathf.Max(manifest.seaHeight,h);
   if(overview!=null)foreach(var root in overview){if(!root)continue;var t=root.GetComponent<Terrain>();var o=root.position;var s=t.terrainData.size;if(p.x>=o.x&&p.z>=o.z&&p.x<=o.x+s.x&&p.z<=o.z+s.z)return Mathf.Max(manifest.seaHeight,t.SampleHeight(p)+o.y);}
   return manifest.seaHeight;
  }
  void ReleaseAll(){releaseIndices.Clear();releaseIndices.AddRange(loaded.Keys);foreach(int i in releaseIndices)Release(i,false);terrainByCell.Clear();}
  public void Suspend(){Suspended=true;ReleaseAll();}
  public void Resume(){Suspended=false;}
  void Release(int index,bool connect=true){var handle=loaded[index];loaded.Remove(index);if(overview!=null&&index<overview.Length&&overview[index])overview[index].gameObject.SetActive(true);ReleaseHandle(handle);TotalReleases++;if(connect)ConnectNeighbors();}
  static void ReleaseHandle(AsyncOperationHandle<GameObject> h){if(!h.IsValid())return;if(h.Status==AsyncOperationStatus.Succeeded)Addressables.ReleaseInstance(h);else Addressables.Release(h);}
  void OnDisable(){
   StopAllCoroutines();ReleaseAll();
   if(pending.HasValue){var h=pending.Value;pending=null;if(h.IsValid()){if(h.IsDone)ReleaseHandle(h);else h.Completed+=ReleaseHandle;}}
  }
 }
}

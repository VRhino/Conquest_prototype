using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
  HashSet<int> desired=new HashSet<int>();
  void OnEnable(){if(Application.isPlaying)StartCoroutine(Stream());}
  public int[] DesiredIndices(Vector3 p){
   int x=Mathf.Clamp(Mathf.FloorToInt(p.x/manifest.sectorSize),0,Mathf.CeilToInt(manifest.worldSize.x/manifest.sectorSize)-1);
   int z=Mathf.Clamp(Mathf.FloorToInt(p.z/manifest.sectorSize),0,Mathf.CeilToInt(manifest.worldSize.y/manifest.sectorSize)-1);
   return Enumerable.Range(0,manifest.sectors.Length).Where(i=>Mathf.Abs(manifest.sectors[i].column-x)<=preloadRadius&&Mathf.Abs(manifest.sectors[i].row-z)<=preloadRadius)
    .OrderBy(i=>(manifest.sectors[i].origin+new Vector3(manifest.sectorSize/2,0,manifest.sectorSize/2)-p).sqrMagnitude).ToArray();
  }
  // Camera can explore independently. Keep actor terrain resident and preload the next travel tile.
  public int[] ActiveDesiredIndices(){var result=new List<int>();if(trackedActor){result.AddRange(DesiredIndices(trackedActor.position));if(trackTravel){int next=Array.FindIndex(manifest.sectors,s=>travelLookAhead.x>=s.origin.x&&travelLookAhead.z>=s.origin.z&&travelLookAhead.x<s.origin.x+manifest.sectorSize&&travelLookAhead.z<s.origin.z+manifest.sectorSize);if(next>=0&&!result.Contains(next))result.Add(next);}}foreach(int i in DesiredIndices(focus))if(!result.Contains(i))result.Add(i);return result.ToArray();}
  IEnumerator Stream(){
   if(!manifest||manifest.sectors==null){Error="Falta manifiesto de campaña";yield break;}
   while(true){
    if(Suspended){yield return null;continue;}
    var indices=ActiveDesiredIndices();desired=new HashSet<int>(indices);
    foreach(var index in loaded.Keys.Where(i=>!desired.Contains(i)).ToArray())Release(index);
    foreach(int index in indices){
     if(loaded.ContainsKey(index)||(failures.TryGetValue(index,out float retry)&&Time.unscaledTime<retry))continue;
     float started=Time.realtimeSinceStartup;
     var handle=Addressables.InstantiateAsync(manifest.sectors[index].address,transform,false,true);pending=handle;
     yield return handle;pending=null;
     LastLoadMilliseconds=(Time.realtimeSinceStartup-started)*1000;
     if(handle.Status!=AsyncOperationStatus.Succeeded){Error=$"Sector {manifest.sectors[index].id}: {handle.OperationException?.Message}";failures[index]=Time.unscaledTime+failureRetrySeconds;Addressables.Release(handle);break;}
     // Camera can move while an asynchronous request is in flight.
     if(Suspended||!ActiveDesiredIndices().Contains(index)){ReleaseHandle(handle);break;}
     var sector=handle.Result.GetComponent<WorldMapCampaignSector>();
     if(!sector||!sector.terrain){Error="Sector sin Terrain";ReleaseHandle(handle);failures[index]=Time.unscaledTime+failureRetrySeconds;break;}
     sector.Bind(viewCamera);loaded.Add(index,handle);TotalLoads++;Error="";
     if(overview!=null&&index<overview.Length&&overview[index])overview[index].gameObject.SetActive(false);
     ConnectNeighbors();yield return null;
    }
    yield return new WaitForSecondsRealtime(reconcileInterval);
   }
  }
  void ConnectNeighbors(){
   foreach(var pair in loaded){if(!pair.Value.IsValid()||!pair.Value.Result)continue;var s=manifest.sectors[pair.Key];var t=pair.Value.Result.GetComponent<WorldMapCampaignSector>().terrain;
    Terrain At(int x,int z){foreach(var item in loaded){if(!item.Value.IsValid()||!item.Value.Result)continue;var q=manifest.sectors[item.Key];if(q.column==x&&q.row==z)return item.Value.Result.GetComponent<WorldMapCampaignSector>().terrain;}return null;}
    t.SetNeighbors(At(s.column-1,s.row),At(s.column,s.row+1),At(s.column+1,s.row),At(s.column,s.row-1));
   }
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
  public void Suspend(){Suspended=true;foreach(int i in loaded.Keys.ToArray())Release(i);}
  public void Resume(){Suspended=false;}
  void Release(int index){var handle=loaded[index];loaded.Remove(index);if(overview!=null&&index<overview.Length&&overview[index])overview[index].gameObject.SetActive(true);ReleaseHandle(handle);TotalReleases++;ConnectNeighbors();}
  static void ReleaseHandle(AsyncOperationHandle<GameObject> h){if(!h.IsValid())return;if(h.Status==AsyncOperationStatus.Succeeded)Addressables.ReleaseInstance(h);else Addressables.Release(h);}
  void OnDisable(){
   StopAllCoroutines();foreach(int i in loaded.Keys.ToArray())Release(i);
   if(pending.HasValue){var h=pending.Value;pending=null;if(h.IsValid()){if(h.IsDone)ReleaseHandle(h);else h.Completed+=ReleaseHandle;}}
  }
 }
}

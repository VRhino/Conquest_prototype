using System;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.InputSystem;
namespace Conquest.WorldMap {
 [Serializable] public class CampaignReturnPose {public string worldId;public Vector3 actor,focus;public float viewSize,yaw,pitch,travelled,visibleWidthTarget;public int segment;public bool follow;public Vector3[] route;}
 public sealed class WorldMapCampaignController : MonoBehaviour {
  public WorldMapSectorStreamer streamer;public Camera mapCamera;public Transform actor,cityA,cityB;
  public Vector3[] demoRoute;
  public WorldMapLocalNavigation navigation;
  public string travelStatus="Elige un destino";
  public float RemainingDistance{get{float d=0;var p=actor.position;for(int i=segment;i<demoRoute.Length;i++){var q=demoRoute[i];d+=Vector2.Distance(new Vector2(p.x,p.z),new Vector2(q.x,q.z));p=q;}return d;}}
  public bool TravelTo(Vector3 destination){if(authoritativePosition)return false;if(!navigation||navigation.sourceSHA256!=streamer.manifest.sourceSHA256){travelStatus="Falta navegación para este mundo";return false;}if(!navigation.TryRoute(actor.position,destination,out var route,out var error)){travelStatus=error;return false;}demoRoute=route;segment=1;travelled=0;moving=true;follow=true;focus=actor.position;travelStatus="En camino";return true;}
  public void StopTravel(){moving=false;travelStatus="Viaje detenido";}
  public WorldMapScaleContract scaleContract;
  public CampaignTravelKind travelKind=CampaignTravelKind.Light;
  [Min(.01f)] public float movementSpeed=6;
  [Min(1)] public float entryDistance=24;
  public float viewSize=50.625f,pitch=48,yaw=155,minZoom=10,maxZoom=1400,cameraClearance=35;
  [Min(0)] public float visibleWidthTarget;
  public float seaHeight=375,actorLift=1;
  public bool moving,follow=true;
  public bool showDiagnostics;
  [NonSerialized] public bool authoritativePosition;
  public void ApplyAuthoritativePosition(Vector3 position){moving=false;actor.position=new Vector3(position.x,streamer.SurfaceHeight(position)+actorLift,position.z);if(follow)focus=actor.position;}
  public WorldMapSettlementTarget selectedSettlement;
  public bool CanEnterSelected=>!authoritativePosition&&selectedSettlement&&streamer.TryHeight(actor.position,out _)&&selectedSettlement.DistanceFrom(actor.position)<=entryDistance;
  public void RequestSelectedEntry(){if(CanEnterSelected)EnterRequested?.Invoke();}
  public float travelled;
  public int segment=1;
  public Vector3 focus;
  public event Action EnterRequested;
  GUIStyle text;float fpsElapsed;int fpsFrames;float fps;
  public float EntryRemaining=>Vector2.Distance(new Vector2(actor.position.x,actor.position.z),new Vector2(demoRoute[demoRoute.Length-1].x,demoRoute[demoRoute.Length-1].z));
  public bool EntryAllowed=>streamer.TryHeight(actor.position,out _)&&EntryRemaining<=entryDistance;
  public void ApplyScaleContract(){if(!scaleContract)return;movementSpeed=scaleContract.SpeedUnity(travelKind);entryDistance=scaleContract.ToUnity(scaleContract.doorRadius);}
  public void SetTravelKind(CampaignTravelKind kind){travelKind=kind;ApplyScaleContract();}
  void Start(){if(demoRoute==null||demoRoute.Length<2){enabled=false;return;}ApplyScaleContract();if(!WorldMapCampaignReturn.TryConsume(streamer.manifest.worldId,out var pose)){actor.position=demoRoute[0]+Vector3.up*actorLift;focus=actor.position;if(scaleContract)visibleWidthTarget=scaleContract.ToUnity(scaleContract.visibleGroundWidth);}else Restore(pose);streamer.focus=focus;ApplyCamera();}
  void Update(){
   Advance(Time.deltaTime);
   if(streamer.TryHeight(actor.position,out var h))actor.position=new Vector3(actor.position.x,Mathf.Max(seaHeight,h)+actorLift,actor.position.z);
   if(follow)focus=actor.position;streamer.focus=moving?actor.position:focus;ApplyCamera();
   fpsElapsed+=Time.unscaledDeltaTime;fpsFrames++;if(fpsElapsed>=2){fps=fpsFrames/fpsElapsed;fpsElapsed=0;fpsFrames=0;}
   var k=Keyboard.current;if(k!=null){if(k.f3Key.wasPressedThisFrame)showDiagnostics=!showDiagnostics;var v=new Vector3((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),0,(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0));if(v.sqrMagnitude>0){follow=false;focus+=Quaternion.Euler(0,yaw,0)*v*viewSize*Time.unscaledDeltaTime;}}
  }
  // Local fixture only. A future authoritative projection supplies route and pose instead.
  public void Advance(float seconds){if(authoritativePosition||!moving)return;float budget=movementSpeed*seconds;
   while(budget>0&&segment<demoRoute.Length){var delta=demoRoute[segment]-actor.position;delta.y=0;float d=delta.magnitude;float step=Mathf.Min(d,budget);var next=actor.position+(d>.001f?delta/d*step:Vector3.zero);
    if(!streamer.TryHeight(next,out float h))break;next.y=Mathf.Max(seaHeight,h)+actorLift;actor.position=next;if(delta.sqrMagnitude>.01f)actor.rotation=Quaternion.LookRotation(delta);travelled+=step;budget-=step;if(step>=d-.001f)segment++;else break;}
   if(segment>=demoRoute.Length){moving=false;travelStatus="Destino alcanzado";}
  }
  public void FocusAt(Vector3 p){follow=false;focus=p;streamer.focus=p;ApplyCamera();}
  public void ApplyCamera(){focus.x=Mathf.Clamp(focus.x,0,streamer.manifest.worldSize.x-.01f);focus.z=Mathf.Clamp(focus.z,0,streamer.manifest.worldSize.y-.01f);focus.y=streamer.SurfaceHeight(focus);PlaceCamera();
   // Measure the horizontal slice at the actor/focus depth, not the FOV parameter.
   if(visibleWidthTarget>0)for(int i=0;i<4;i++){float measured=VisibleGroundWidth();if(measured<=0||Mathf.Abs(measured-visibleWidthTarget)<.02f)break;viewSize=Mathf.Clamp(viewSize*visibleWidthTarget/measured,minZoom,maxZoom);PlaceCamera();}
  }
  void PlaceCamera(){
   mapCamera.transform.rotation=Quaternion.Euler(pitch,yaw,0);float d=viewSize/Mathf.Tan(mapCamera.fieldOfView*.5f*Mathf.Deg2Rad);var p=focus-mapCamera.transform.forward*d;p.y=Mathf.Max(p.y,streamer.SurfaceHeight(p)+cameraClearance);mapCamera.transform.position=p;mapCamera.nearClipPlane=2;mapCamera.farClipPlane=10000;
  }
  public float VisibleGroundWidth(){var plane=new Plane(Vector3.up,new Vector3(0,focus.y,0));var left=mapCamera.ViewportPointToRay(new Vector3(0,.5f,0));var right=mapCamera.ViewportPointToRay(new Vector3(1,.5f,0));if(!plane.Raycast(left,out var a)||!plane.Raycast(right,out var b))return 0;return Vector3.Distance(left.GetPoint(a),right.GetPoint(b));}
  public void SetVisibleGroundWidth(float width){visibleWidthTarget=width;ApplyCamera();}
  public void ZoomBy(float multiplier){if(scaleContract){float current=visibleWidthTarget>0?visibleWidthTarget:VisibleGroundWidth();SetVisibleGroundWidth(Mathf.Clamp(current*multiplier,scaleContract.ToUnity(scaleContract.minimumVisibleWidth),scaleContract.ToUnity(scaleContract.maximumVisibleWidth)));}else viewSize=Mathf.Clamp(viewSize*multiplier,minZoom,maxZoom);}
  public CampaignReturnPose Capture()=>new CampaignReturnPose{worldId=streamer.manifest.worldId,actor=actor.position,focus=focus,viewSize=viewSize,pitch=pitch,yaw=yaw,travelled=travelled,segment=segment,follow=follow,route=(Vector3[])demoRoute.Clone(),visibleWidthTarget=visibleWidthTarget};
  public void Restore(CampaignReturnPose p){actor.position=p.actor;focus=p.focus;viewSize=p.viewSize;pitch=p.pitch;yaw=p.yaw;travelled=p.travelled;segment=p.segment;visibleWidthTarget=p.visibleWidthTarget;if(p.route!=null&&p.route.Length>=2)demoRoute=p.route;moving=false;travelStatus="De vuelta al mapa";follow=p.follow;}
  void OnGUI(){
   if(!showDiagnostics)return;
   if(text==null)text=new GUIStyle(GUI.skin.label){wordWrap=true,fontSize=13};var panel=new Rect(18,18,320,410);GUI.color=new Color(.06f,.09f,.10f,.96f);GUI.DrawTexture(panel,Texture2D.whiteTexture);GUI.color=Color.white;GUILayout.BeginArea(new Rect(32,28,292,390));
   GUILayout.Label("CAMPAÑA · PRUEBA LOCAL",text);GUILayout.Label("Datos estáticos V9. Sin sesión de servidor ni niebla.",text);
   GUILayout.Label($"Sectores detallados: {streamer.LoadedCount}/48 · pendientes {streamer.PendingCount}",text);
   GUILayout.Label($"Cargas {streamer.TotalLoads} · liberaciones {streamer.TotalReleases} · última {streamer.LastLoadMilliseconds:F0} ms",text);
   GUILayout.Label($"FPS orientativos: {fps:F0} · memoria Unity: {Profiler.GetTotalAllocatedMemoryLong()/1048576} MiB",text);
   if(!string.IsNullOrEmpty(streamer.Error))GUILayout.Label(streamer.Error,text);
   GUILayout.BeginHorizontal();if(GUILayout.Button("Seguir ejército")){follow=true;focus=actor.position;}if(GUILayout.Button(moving?"Pausa":"Viaje local a B"))moving=!moving;GUILayout.EndHorizontal();
   GUILayout.Label($"{movementSpeed:F1} u/s · recorrido {travelled:F0} u · {travelled/movementSpeed:F1} s · acceso B {EntryRemaining:F0} u",text);
   GUI.enabled=EntryAllowed;if(GUILayout.Button("Seleccionar asentamiento B"))selectedSettlement=cityB.GetComponent<WorldMapSettlementTarget>();GUI.enabled=true;
   GUILayout.Label("El viaje usa el recorrido local validado. No envía órdenes ni sustituye navegación del servidor.",text);
   GUILayout.Label("Inspección de streaming: cambiar zona solo mueve la cámara.",text);
   GUILayout.BeginHorizontal();if(GUILayout.Button("Anatolia"))FocusAt(new Vector3(18000,0,6500));if(GUILayout.Button("Península"))FocusAt(new Vector3(6800,0,11860));if(GUILayout.Button("Delta"))FocusAt(new Vector3(16000,0,20000));GUILayout.EndHorizontal();
   GUILayout.Label("WASD: desplazar cámara · derecho: orbitar · rueda: zoom",text);GUILayout.EndArea();
   var e=Event.current;if(panel.Contains(e.mousePosition))return;
   if(e.type==EventType.ScrollWheel){ZoomBy(Mathf.Exp(e.delta.y*.08f));e.Use();}
   if(e.type==EventType.MouseDrag&&e.button==1){yaw+=e.delta.x*.25f;pitch=Mathf.Clamp(pitch+e.delta.y*.25f,35,70);e.Use();}
  }
 }
 // Only return UI context. Does not duplicate player login/session or authoritative state.
 public static class WorldMapCampaignReturn {
  static CampaignReturnPose pose;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){pose=null;}
  public static void Store(CampaignReturnPose p){pose=p;}
  public static bool TryConsume(string id,out CampaignReturnPose p){p=pose;if(p==null||p.worldId!=id)return false;pose=null;return true;}
 }
}

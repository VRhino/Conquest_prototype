using UnityEngine;
namespace Conquest.WorldMap {
 [RequireComponent(typeof(WorldMapCampaignController))]
 public sealed class WorldMapLocalRouteVisual:MonoBehaviour {
  public float width=3,lift=3,sampleSpacing=25;WorldMapCampaignController c;LineRenderer line;Material material;Vector3[] previous;Vector3 lastActor;
  void Awake(){c=GetComponent<WorldMapCampaignController>();var g=new GameObject("Local journey path");g.transform.SetParent(transform);line=g.AddComponent<LineRenderer>();material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.SetColor("_BaseColor",new Color(.93f,.72f,.31f));line.sharedMaterial=material;line.useWorldSpace=true;line.startWidth=line.endWidth=width;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;}
  void LateUpdate(){if(!line)return;line.enabled=c.moving;if(!c.moving)return;if(previous==c.demoRoute&&(c.actor.position-lastActor).sqrMagnitude<100)return;previous=c.demoRoute;lastActor=c.actor.position;var points=new System.Collections.Generic.List<Vector3>();var p=c.actor.position;for(int i=c.segment;i<c.demoRoute.Length;i++){var q=c.demoRoute[i];int count=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(new Vector2(p.x,p.z),new Vector2(q.x,q.z))/sampleSpacing));for(int k=0;k<count;k++){var v=Vector3.Lerp(p,q,(float)k/count);int index=c.navigation.Index(v);v.y=(c.streamer.TryHeight(v,out float h)?h:index>=0?c.navigation.heights[index]:v.y)+lift;points.Add(v);}p=q;}p.y=c.streamer.SurfaceHeight(p)+lift;points.Add(p);line.positionCount=points.Count;line.SetPositions(points.ToArray());}
  void OnDestroy(){if(material)Destroy(material);}
 }
}

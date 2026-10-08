using System;
using UnityEngine;
namespace Conquest.WorldMap {
 [Serializable] public class RegionTrialPoint { public float x,z; }
 [Serializable] public class RegionTrialLevel { public float radius; public RegionTrialPoint[] polygon; public string[] resourceIds; public int stone,copper,tin; }
 [Serializable] public class RegionTrialSite { public string id,name,stoneId; public int kingdom; public float x,z,woodX,woodZ; public RegionTrialLevel[] levels; }
 [Serializable] public class RegionTrialResource { public string id,type; public float x,z; }
 [Serializable] public class RegionTrialMarker {public string name,type,resourceId,note;public float x,z;}
 [Serializable] public class RegionTrialCorridor {public string name;public float cost;public RegionTrialPoint[] points;}
 [Serializable] public class RegionTrialView {public string label;public TextAsset data;public GameObject root,resourceRoot,focalRoot,corridorRoot;public Transform[] cities,woodWitnesses,markers;public LineRenderer[] borders;}
 [Serializable] public class RegionTrialData { public string region,note,primary,secondary;public RegionTrialMarker[] markers;public RegionTrialCorridor[] corridors; public int seed,area,candidates,kingdomCount; public float speed,minimumSpacing; public RegionTrialSite[] sites; public RegionTrialResource[] resources; }
 // Independent visual occupancy experiment; never creates server settlements or resources.
 public sealed class WorldMapRegionLab : MonoBehaviour {
  public TextAsset dataAsset; public WorldMapScaleLab scale;
  public Transform[] cities; public Transform[] woodWitnesses;
  public LineRenderer[] borders; public GameObject resourceRoot;
  public RegionTrialView[] trialViews;public int activeRegion,selectedFocus;
  public bool showFoci=true,showCorridors=true;
  public int selected,level,destination=0; public bool showBorders=true,showNodes=true,overview;
  RegionTrialData data; Vector2 scroll; GUIStyle body,title;
  public RegionTrialData Data { get { if(data==null)data=JsonUtility.FromJson<RegionTrialData>(dataAsset.text);return data; } }
  public void SwitchRegion(int index){
   activeRegion=index;for(int i=0;i<trialViews.Length;i++)trialViews[i].root.SetActive(i==index);
   var view=trialViews[index];dataAsset=view.data;data=null;cities=view.cities;woodWitnesses=view.woodWitnesses;borders=view.borders;resourceRoot=view.resourceRoot;
   selected=0;destination=1;level=0;selectedFocus=0;overview=false;Apply();
  }
  public void FocusMarker(int index){selectedFocus=index;overview=false;var p=Data.markers[index];scale.FocusAt(new Vector3(p.x,0,p.z),520);}
  public void ApplyLayers(){if(trialViews==null||trialViews.Length==0)return;var v=trialViews[activeRegion];v.focalRoot.SetActive(showFoci);v.corridorRoot.SetActive(showCorridors);}
  public void Apply(){
   var d=Data;
   for(int i=0;i<cities.Length;i++){
    var c=cities[i];c.localScale=Vector3.one*1.4f;
    for(int j=0;j<c.childCount;j++)c.GetChild(j).gameObject.SetActive(j==(level<2?0:level<4?1:2));
    var line=borders[i];var polygon=d.sites[i].levels[level].polygon;line.positionCount=polygon.Length+1;
    for(int j=0;j<=polygon.Length;j++){var p=polygon[j%polygon.Length];var v=new Vector3(p.x,0,p.z);v.y=scale.Height(v)+2;line.SetPosition(j,v);}
    line.enabled=showBorders;
    woodWitnesses[i].gameObject.SetActive(i==selected&&!overview);
   }
   resourceRoot.SetActive(showNodes);ApplyLayers();
   if(overview){var min=cities[0].position;var max=min;foreach(var c in cities){min=Vector3.Min(min,c.position);max=Vector3.Max(max,c.position);}scale.pitch=65;scale.yaw=0;scale.FocusAt((min+max)*.5f,Mathf.Max((max.x-min.x)/(2*scale.mapCamera.aspect),(max.z-min.z)/(2*Mathf.Sin(65*Mathf.Deg2Rad)))+350);}
   else {scale.pitch=48;scale.yaw=155;scale.FocusAt(cities[selected].position,320);}
  }
  void Start(){if(trialViews!=null&&trialViews.Length>0)SwitchRegion(activeRegion);else Apply();}
  void OnGUI(){
   if(body==null){body=new GUIStyle(GUI.skin.label){wordWrap=true,fontSize=13,fixedWidth=260};title=new GUIStyle(body){fontSize=19,fontStyle=FontStyle.Bold};}
   var panel=new Rect(18,18,330,Mathf.Min(740,Screen.height-36));GUI.Box(panel,GUIContent.none);
   GUILayout.BeginArea(new Rect(32,28,302,panel.height-20));scroll=GUILayout.BeginScrollView(scroll);
   GUILayout.Label(trialViews!=null&&trialViews.Length>0?"ATRACTIVOS REGIONALES":"PRUEBA DE UNA REGIÓN",title);
   if(trialViews!=null&&trialViews.Length>0){int bank=GUILayout.SelectionGrid(activeRegion,Array.ConvertAll(trialViews,v=>v.label),2);if(bank!=activeRegion)SwitchRegion(bank);}
   GUILayout.Label(Data.region+$" · {Data.sites.Length} ciudades · {Data.kingdomCount} reinos",body);
   GUILayout.Label("Ciudades de prueba, sin fundar entidades reales.",body);
   if(!string.IsNullOrEmpty(Data.primary))GUILayout.Label($"Perfil: {Data.primary} · {Data.secondary}",body);
   GUILayout.BeginHorizontal();if(GUILayout.Button("Entorno de ciudad")){overview=false;Apply();}if(GUILayout.Button("Comparar región")){overview=true;Apply();}GUILayout.EndHorizontal();
   string[] names=Array.ConvertAll(Data.sites,s=>$"{s.name} · R{s.kingdom+1}");
   GUILayout.Label("Ciudad seleccionada",body);int pick=GUILayout.SelectionGrid(selected,names,2);if(pick!=selected){selected=pick;overview=false;Apply();}
   int next=GUILayout.SelectionGrid(level,new[]{"Inicio","N1","N2","N3","N4","N5"},3);if(next!=level){level=next;Apply();}
   bool rings=GUILayout.Toggle(showBorders,"Influencias de todas las ciudades"),nodes=GUILayout.Toggle(showNodes,"Nodos de recursos existentes");
   if(rings!=showBorders||nodes!=showNodes){showBorders=rings;showNodes=nodes;Apply();}
   if(trialViews!=null&&trialViews.Length>0){
    bool f=GUILayout.Toggle(showFoci,"Focos de disputa y territorios productivos"),c=GUILayout.Toggle(showCorridors,"Corredores de diagnóstico");
    if(f!=showFoci||c!=showCorridors){showFoci=f;showCorridors=c;ApplyLayers();}
    if(Data.markers!=null&&Data.markers.Length>0){GUILayout.Label("Inspeccionar foco",body);int focus=GUILayout.SelectionGrid(selectedFocus,Array.ConvertAll(Data.markers,m=>m.name.Replace("Territorio ganadero","Pastos").Replace("Territorio forestal","Bosque").Replace("Territorio agrícola","Cultivos")),2);if(focus!=selectedFocus)FocusMarker(focus);
     if(GUILayout.Button("Centrar foco"))FocusMarker(selectedFocus);GUILayout.Label(Data.markers[selectedFocus].note,body);}
    GUILayout.Label($"{Data.corridors.Length} segmentos de rutas entre anclas. Coste ponderado; no tiempo de viaje ni carreteras construidas.",body);
   }
   var site=Data.sites[selected];var coverage=site.levels[level];
   GUILayout.Label($"Radio: {coverage.radius:F0} u · miniatura Ø154 u",body);
   GUILayout.Label($"Nodos dentro de influencia: piedra {coverage.stone}, cobre {coverage.copper}, estaño {coverage.tin}",body);
   GUILayout.Label("Arranque: piedra existente y punto de bosque ≥15 % dentro de radio 90. Anillo verde = testigo de madera.",body);
   GUILayout.Label("Destino para comparar tiempo directo",body);destination=GUILayout.SelectionGrid(destination,Array.ConvertAll(Data.sites,s=>s.name),3);
   var other=Data.sites[destination];float distance=Mathf.Sqrt(Mathf.Pow(site.x-other.x,2)+Mathf.Pow(site.z-other.z,2));
   GUILayout.Label($"{distance:F0} u / 12 u/s = {distance/12/60:F2} min",body);
   GUILayout.Label(trialViews!=null&&trialViews.Length>0?"Tiempo directo sin obstáculos. Las fronteras se recortan al crecer; perder un recurso no invalida el arranque. Varios nodos del mismo tipo son válidos.":"Sin obstáculos ni navegación. Los círculos no se solapan a N5; cada nodo pertenece como máximo a una ciudad de esta prueba.",body);
   GUILayout.Label("Nodos: gris piedra · naranja cobre · azul estaño · blanco ganado. No equivale a validar economía o capacidad máxima de la región.",body);
   GUILayout.EndScrollView();GUILayout.EndArea();
   foreach(var c in cities){var p=scale.mapCamera.WorldToScreenPoint(c.position+Vector3.up*60);if(p.z>0&&p.x>=350&&p.x<Screen.width&&p.y>=0&&p.y<Screen.height)GUI.Label(new Rect(p.x-45,Screen.height-p.y,160,25),c.name,body);}
   if(showFoci&&trialViews!=null&&trialViews.Length>0){var v=trialViews[activeRegion];for(int i=0;i<v.markers.Length;i++){var p=scale.mapCamera.WorldToScreenPoint(v.markers[i].position+Vector3.up*25);if(p.z>0&&p.x>=350&&p.x<Screen.width&&p.y>=0&&p.y<Screen.height)GUI.Label(new Rect(p.x-50,Screen.height-p.y,180,40),Data.markers[i].name,body);}}
   var e=Event.current;if(panel.Contains(e.mousePosition))return;
   if(e.type==EventType.ScrollWheel){scale.viewSize=Mathf.Clamp(scale.viewSize*Mathf.Exp(e.delta.y*.08f),220,6000);e.Use();scale.ApplyCamera();}
   if(e.type==EventType.MouseDrag&&e.button==1){scale.yaw+=e.delta.x*.25f;scale.pitch=Mathf.Clamp(scale.pitch+e.delta.y*.25f,35,70);e.Use();scale.ApplyCamera();}
  }
 }
}

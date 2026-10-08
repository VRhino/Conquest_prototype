using System;
using UnityEngine;

namespace Conquest.WorldMap
{
    // Local presentation experiment; no gameplay entities or authoritative state.
    public sealed class WorldMapScaleLab : MonoBehaviour
    {
        [Serializable] public class ForestComparison { public Terrain terrain; public TerrainData native, campaign; }
        public Camera mapCamera;
        public Transform actor, caravan, cityA, cityB;
        public Transform[] cityVariants;
        public Terrain[] terrains;
        public ForestComparison[] forests;
        public LineRenderer influence, routeLine;
        public Vector3[] route;
        public float caravanSpeed=5;
        public float movementSpeed=12, entryDistance=18, cityFootprintRadius=55;
        public float viewSize=320, pitch=48, yaw=155;
        public float minimumView=220, maximumView=1400;
        public float seaHeight=375, cameraClearance=35, actorOffset=1;
        public float[] influenceRadii={90,180,270,360,450,540};
        public int influenceLevel;
        public bool follow=true, showInfluence=true, campaignForest=true;
        public float travelMultiplier=1;
        public float Distance { get { float d=0;for(int i=1;i<route.Length;i++)d+=Horizontal(route[i]-route[i-1]).magnitude;return d; } }
        public float TravelSeconds=>Distance/Mathf.Max(.01f,movementSpeed);
        public float EntryRemaining=>Horizontal(actor.position-route[route.Length-1]).magnitude;
        public bool EntryAllowed=>EntryRemaining<=entryDistance;
        public float cityVisualScale=1.4f, actorVisualScale=1.8f;
        public float travelled, elapsed;
        public bool moving;
        int segment=1;
        Vector3 focus;
        string message="Acércate al acceso de la ciudad para habilitar la entrada.";
        GUIStyle title,body;
        Vector2 scroll;
        static Vector3 Horizontal(Vector3 v)=>new Vector3(v.x,0,v.z);
        public float Height(Vector3 p)
        {
            float h=seaHeight;
            foreach(var t in terrains){var o=t.transform.position;var s=t.terrainData.size;
                if(p.x>=o.x&&p.x<=o.x+s.x&&p.z>=o.z&&p.z<=o.z+s.z)h=Mathf.Max(h,t.SampleHeight(p)+o.y);}
            return h;
        }
        void Start(){ApplyForest();ResetJourney();}
        public void ApplySymbols(){cityA.localScale=cityB.localScale=Vector3.one*cityVisualScale;actor.localScale=Vector3.one*actorVisualScale;}
        public void ResetJourney()
        {
            moving=false;travelled=elapsed=0;segment=1;
            actor.position=route[0]+Vector3.up*actorOffset;
            focus=actor.position;ApplySymbols();ApplyInfluence();ApplyCamera();
        }
        public void BeginJourney(){if(segment>=route.Length)ResetJourney();moving=true;}
        void Update(){
            Advance(Time.deltaTime);
            if(caravan&&route.Length>1){
                float progress=Mathf.PingPong(Distance*.4f+Time.time*caravanSpeed,Distance)/Distance;
                float index=progress*(route.Length-1);int a=Mathf.Min((int)index,route.Length-2);
                var p=Vector3.Lerp(route[a],route[a+1],index-a);p.y=Height(p)+actorOffset;
                caravan.position=p;
            }
        }
        // Deterministic stepping also allows validating the complete trial without waiting minutes.
        public void Advance(float seconds)
        {
            if(!moving)return;
            float budget=movementSpeed*travelMultiplier*seconds;
            float used=0;
            while(budget>0&&segment<route.Length){
                var delta=Horizontal(route[segment]-actor.position);float distance=delta.magnitude;
                float step=Mathf.Min(budget,distance);
                var next=actor.position+(distance>.0001f?delta/distance*step:Vector3.zero);
                next.y=Height(next)+actorOffset;actor.position=next;
                if(delta.sqrMagnitude>.01f)actor.rotation=Quaternion.LookRotation(delta);
                budget-=step;used+=step;
                if(distance<=step+.001f)segment++;else break;
            }
            travelled+=used;elapsed+=used/Mathf.Max(.01f,movementSpeed);
            if(segment>=route.Length)moving=false;
            if(follow)focus=actor.position;
            ApplyCamera();
        }
        public void ApplyForest()
        {
            foreach(var f in forests){f.terrain.terrainData=campaignForest?f.campaign:f.native;f.terrain.Flush();}
        }
        public void ApplyInfluence()
        {
            influence.enabled=showInfluence;float radius=influenceRadii[influenceLevel];
            for(int i=0;i<influence.positionCount;i++){
                float a=i*Mathf.PI*2/(influence.positionCount-1);
                var p=cityA.position+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);
                p.y=Height(p)+2;influence.SetPosition(i,p);
            }
            for(int i=0;i<cityVariants.Length;i++)cityVariants[i].gameObject.SetActive(i==(influenceLevel<2?0:influenceLevel<4?1:2));
        }
        public void ApplyCamera()
        {
            focus.y=Height(focus);
            mapCamera.transform.rotation=Quaternion.Euler(pitch,yaw,0);
            float distance=viewSize/Mathf.Tan(mapCamera.fieldOfView*.5f*Mathf.Deg2Rad);
            var p=focus-mapCamera.transform.forward*distance;
            p.y=Mathf.Max(p.y,Height(p)+cameraClearance);
            mapCamera.transform.position=p;mapCamera.nearClipPlane=2;mapCamera.farClipPlane=Mathf.Max(12000,distance+20000);
            mapCamera.ResetWorldToCameraMatrix();mapCamera.ResetCullingMatrix();
        }
        public void FocusAt(Vector3 point,float size){follow=false;focus=point;viewSize=size;ApplyCamera();}
        public void RouteView(){follow=false;focus=(cityA.position+cityB.position)*.5f;viewSize=1050;ApplyCamera();}
        public void PlayerView(){follow=true;viewSize=320;focus=actor.position;ApplyCamera();}
        void OnGUI()
        {
            if(title==null){title=new GUIStyle(GUI.skin.label){fontSize=20,fontStyle=FontStyle.Bold};body=new GUIStyle(GUI.skin.label){wordWrap=true,fontSize=13};}
            var panel=new Rect(18,18,315,Mathf.Min(650,Screen.height-36));GUI.Box(panel,GUIContent.none);
            GUILayout.BeginArea(new Rect(32,28,285,panel.height-20));
            scroll=GUILayout.BeginScrollView(scroll);
            GUILayout.Label("ESCALA DE CAMPAÑA",title);
            GUILayout.Label("Una ciudad y su entorno · prueba local",body);
            GUILayout.Label("Tiempo directo de referencia, sin obstáculos.",body);
            GUILayout.Label($"Ruta: {Distance:F0} unidades · {TravelSeconds/60:F2} min a {movementSpeed:F1} u/s",body);
            GUILayout.BeginHorizontal();if(GUILayout.Button("Jugador"))PlayerView();if(GUILayout.Button("Ver recorrido"))RouteView();GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();if(GUILayout.Button("Viajar a B"))BeginJourney();if(GUILayout.Button(moving?"Pausa":"Continuar"))moving=!moving;if(GUILayout.Button("Reiniciar"))ResetJourney();GUILayout.EndHorizontal();
            GUILayout.Label($"Recorrido: {travelled:F0} u · tiempo de juego {elapsed:F1} s",body);
            GUILayout.Label($"Distancia al acceso B: {EntryRemaining:F0} u",body);
            GUI.enabled=EntryAllowed;if(GUILayout.Button("Comprobar entrada"))message="Proximidad válida. El enlace a la escena real se conectará después de calibrar.";GUI.enabled=true;
            GUILayout.Label(message,body);
            GUILayout.Label("Influencia de ciudad A",body);
            int level=GUILayout.SelectionGrid(influenceLevel,new[]{"Inicio","N1","N2","N3","N4","N5"},3);
            bool rings=GUILayout.Toggle(showInfluence,"Mostrar influencia");
            if(level!=influenceLevel||rings!=showInfluence){influenceLevel=level;showInfluence=rings;ApplyInfluence();}
            GUILayout.Label($"Radio {influenceRadii[influenceLevel]:F0} u · ciudad simbólica: Ø {cityFootprintRadius*2*cityVisualScale:F0} u",body);
            bool forest=GUILayout.Toggle(campaignForest,"Bosque como masa de campaña");if(forest!=campaignForest){campaignForest=forest;ApplyForest();}
            GUILayout.Label("Zoom",body);viewSize=GUILayout.HorizontalSlider(viewSize,minimumView,maximumView);
            GUILayout.Label("Tamaño de ciudad",body);cityVisualScale=GUILayout.HorizontalSlider(cityVisualScale,.8f,2.2f);
            GUILayout.Label("Tamaño de ejército",body);actorVisualScale=GUILayout.HorizontalSlider(actorVisualScale,.7f,1.8f);
            GUILayout.Label($"Velocidad {movementSpeed:F1} u/s",body);movementSpeed=GUILayout.HorizontalSlider(movementSpeed,3,12);
            ApplySymbols();
            GUILayout.Label("Prueba temporal",body);travelMultiplier=GUILayout.SelectionGrid(travelMultiplier==1?0:1,new[]{"Velocidad real","Acelerar ×10"},2)==0?1:10;
            GUILayout.EndScrollView();GUILayout.EndArea();
            foreach(var pair in new[]{cityA,cityB,caravan}){var s=mapCamera.WorldToScreenPoint(pair.position+Vector3.up*45);if(s.z>0)GUI.Label(new Rect(s.x-65,Screen.height-s.y-15,150,30),pair.name,body);}
            var e=Event.current;if(panel.Contains(e.mousePosition))return;
            if(e.type==EventType.ScrollWheel){viewSize=Mathf.Clamp(viewSize*Mathf.Exp(e.delta.y*.08f),minimumView,maximumView);e.Use();}
            if(e.type==EventType.MouseDrag&&e.button==1){yaw+=e.delta.x*.25f;pitch=Mathf.Clamp(pitch+e.delta.y*.25f,35,70);e.Use();}
            ApplyCamera();
        }
    }
}

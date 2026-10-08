using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Conquest.WorldMap.Editor
{
    public static class WorldMapScaleLabBuilder
    {
        public const string Folder="Assets/Scenes/WorldMapTools/Experiments/CampaignScale";
        public const string ScenePath=Folder+"/CampaignScale.unity";
        const string Source="Assets/Scenes/WorldMapTools/Experiments/MountainsV9/WorldMapMountainsV9.unity";
        static Material clay,roof,stone,wood,bronze,cloth,ink,routeMat,influenceMat;
        [MenuItem("Tools/BronzeAge/Pruebas/Abrir escala de campaña")]
        public static void Open(){if(EditorApplication.isPlayingOrWillChangePlaymode)return;EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);}
        public static string Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Sal de Play.");
            if(File.Exists(ScenePath))throw new InvalidOperationException("La prueba existe; no se sobrescribe.");
            if(SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("La escena actual tiene cambios pendientes.");
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder(WorldMapLandscapeLab.Folder,"CampaignScale");
            var scene=EditorSceneManager.OpenScene(Source,OpenSceneMode.Single);
            EditorSceneManager.SaveScene(scene,ScenePath,true);
            scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var preview=UnityEngine.Object.FindAnyObjectByType<WorldMapPreviewController>();
            preview.enabled=false;
            foreach(var marker in preview.worldRoot.GetComponentsInChildren<BronzeAge.RegionalMapTool.BronzeAgeRegionalMarker>(true))marker.gameObject.SetActive(false);
            var root=new GameObject("Campaign scale calibration");
            var lab=root.AddComponent<WorldMapScaleLab>();lab.mapCamera=preview.mapCamera;lab.mapCamera.fieldOfView=50;
            lab.terrains=preview.worldRoot.GetComponentsInChildren<Terrain>();
            clay=Mat("Adobe",new Color(.66f,.50f,.32f));roof=Mat("Flat clay roofs",new Color(.73f,.60f,.42f));
            stone=Mat("Limestone walls",new Color(.64f,.62f,.51f));wood=Mat("Timber",new Color(.32f,.22f,.13f));
            bronze=Mat("Bronze",new Color(.55f,.42f,.20f));cloth=Mat("Faction red",new Color(.49f,.10f,.08f));ink=Mat("Door shadow",new Color(.16f,.14f,.09f));
            routeMat=Mat("Journey",new Color(.85f,.65f,.22f));influenceMat=Mat("Influence",new Color(.20f,.75f,.70f));
            Vector3 a=new Vector3(17452.366f,0,6550.536f),b=Vector3.zero;Vector3[] path=null;
            // Select a measured dry, gently sloping local route rather than assuming
            // a straight segment through water or a mountain is playable.
            for(int i=0;i<16&&path==null;i++){
                float angle=i*Mathf.PI/8;var candidate=a+new Vector3(Mathf.Cos(angle)*1200,0,Mathf.Sin(angle)*1200);
                var points=new List<Vector3>();bool valid=true;
                for(int k=0;k<=60;k++){
                    var p=Vector3.Lerp(a+new Vector3(0,0,-78),candidate+new Vector3(0,0,-78),k/60f);
                    var t=lab.terrains.FirstOrDefault(t=>Contains(t,p));
                    if(!t){valid=false;break;}
                    var o=t.transform.position;var size=t.terrainData.size;float u=(p.x-o.x)/size.x,v=(p.z-o.z)/size.z;
                    var raw=File.ReadAllBytes("Assets/Scenes/WorldMapTools/Experiments/MountainsV9/"+t.name+".layers.bytes");
                    int x=Mathf.Clamp((int)(u*64),0,63),z=Mathf.Clamp((int)(v*64),0,63);
                    p.y=lab.Height(p);
                    if(raw[(z*64+x)*7+3]==255||p.y<400||t.terrainData.GetSteepness(u,v)>20){valid=false;break;}
                    points.Add(p);
                }
                if(valid){b=candidate;path=points.ToArray();}
            }
            if(path==null)throw new InvalidOperationException("No se encontró ruta local seca y suave.");
            a.y=lab.Height(a);b.y=lab.Height(b);lab.route=path;
            lab.cityA=City("Ciudad A",root.transform,a,false,out var variants);lab.cityVariants=variants;
            lab.cityB=City("Ciudad B · piedra",root.transform,b,true,out _);
            lab.actor=new GameObject("Ejército representativo").transform;lab.actor.SetParent(root.transform);
            Figure(lab.actor,new Vector3(0,0,0));Figure(lab.actor,new Vector3(-9,0,-8));Figure(lab.actor,new Vector3(9,0,-8));
            Cube("Estandarte",lab.actor,new Vector3(0,28,0),new Vector3(.8f,35,.8f),wood);
            Cube("Bandera",lab.actor,new Vector3(5,41,0),new Vector3(10,7,1),cloth);
            lab.caravan=new GameObject("Caravana").transform;lab.caravan.SetParent(root.transform);
            var cp=Vector3.Lerp(path[0],path[path.Length-1],.4f)+new Vector3(0,0,55);cp.y=lab.Height(cp);lab.caravan.position=cp;
            var cart=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonFantasyKingdom/Prefabs/Vehicles/SM_Veh_Cart_01.prefab");
            if(!cart)throw new InvalidOperationException("Falta la caravana Synty.");
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(cart,scene);instance.transform.SetParent(lab.caravan,false);
            var bounds=new Bounds(instance.transform.position,Vector3.zero);foreach(var r in instance.GetComponentsInChildren<Renderer>())bounds.Encapsulate(r.bounds);
            instance.transform.localScale*=24/Mathf.Max(bounds.size.x,bounds.size.z);
            foreach(var c in instance.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);
            lab.routeLine=Line("Recorrido medido",root.transform,routeMat,2.5f,path.Select(p=>p+Vector3.up*2).ToArray());
            lab.influence=Line("Influencia de A",root.transform,influenceMat,2.5f,new Vector3[129]);
            var comparisons=new List<WorldMapScaleLab.ForestComparison>();int before=0,after=0;
            foreach(var t in lab.terrains){
                var o=t.transform.position;var size=t.terrainData.size;
                if(o.x>a.x+2000||o.x+size.x<a.x-2000||o.z>a.z+2000||o.z+size.z<a.z-2000)continue;
                var native=t.terrainData;var campaign=UnityEngine.Object.Instantiate(native);var seen=new HashSet<Vector2Int>();var trees=new List<TreeInstance>();
                foreach(var tree in native.treeInstances){
                    var p=o+Vector3.Scale(tree.position,size);before++;
                    bool local=Mathf.Abs(p.x-a.x)<2000&&Mathf.Abs(p.z-a.z)<2000;
                    if(local){
                        if(Vector2.Distance(new Vector2(p.x,p.z),new Vector2(a.x,a.z))<125||Vector2.Distance(new Vector2(p.x,p.z),new Vector2(b.x,b.z))<125)continue;
                        var journey=path[path.Length-1]-path[0];journey.y=0;
                        var relative=p-path[0];relative.y=0;
                        float along=Mathf.Clamp01(Vector3.Dot(relative,journey)/journey.sqrMagnitude);
                        if((relative-journey*along).sqrMagnitude<55*55)continue;
                        var cell=new Vector2Int(Mathf.FloorToInt(p.x/50),Mathf.FloorToInt(p.z/50));if(!seen.Add(cell))continue;
                        var representative=tree;representative.widthScale*=2.2f;representative.heightScale*=.65f;trees.Add(representative);
                    }else trees.Add(tree);
                }
                campaign.SetTreeInstances(trees.ToArray(),false);after+=trees.Count;
                AssetDatabase.CreateAsset(campaign,Folder+"/"+t.name+"-campaign-forest.asset");
                campaign.SetAlphamaps(0,0,native.GetAlphamaps(0,0,native.alphamapWidth,native.alphamapHeight));foreach(var splat in campaign.alphamapTextures)EditorUtility.SetDirty(splat);EditorUtility.SetDirty(campaign);
                comparisons.Add(new WorldMapScaleLab.ForestComparison{terrain=t,native=native,campaign=campaign});
            }
            lab.forests=comparisons.ToArray();lab.ApplyForest();lab.ResetJourney();
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Selection.activeGameObject=root;
            var text=$"Route={lab.Distance:F2}\nSpeed={lab.movementSpeed}\nSeconds={lab.TravelSeconds:F2}\nEntryRadius={lab.entryDistance}\nCityDiameter={lab.cityFootprintRadius*2}\nForest terrains={comparisons.Count}\nNative trees in these terrains={before}\nCampaign trees in these terrains={after}\nA={a}\nB={b}\n";
            File.WriteAllText(Folder+"/Calibration.txt",text);return text;
        }
        static bool Contains(Terrain t,Vector3 p){var o=t.transform.position;var s=t.terrainData.size;return p.x>=o.x&&p.x<=o.x+s.x&&p.z>=o.z&&p.z<=o.z+s.z;}
        static Material Mat(string name,Color color){var m=new Material(Shader.Find("Universal Render Pipeline/Simple Lit")){color=color};AssetDatabase.CreateAsset(m,Folder+"/"+name+".mat");return m;}
        static GameObject Cube(string name,Transform parent,Vector3 p,Vector3 scale,Material m){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=m;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;}
        static void Figure(Transform parent,Vector3 p){
            Cube("Túnica",parent,p+Vector3.up*10,new Vector3(7,16,5),cloth);
            var head=GameObject.CreatePrimitive(PrimitiveType.Sphere);head.transform.SetParent(parent,false);head.transform.localPosition=p+Vector3.up*21;head.transform.localScale=Vector3.one*6;head.GetComponent<Renderer>().sharedMaterial=clay;UnityEngine.Object.DestroyImmediate(head.GetComponent<Collider>());
            Cube("Escudo",parent,p+new Vector3(-5,12,2),new Vector3(1,10,8),bronze);
            Cube("Lanza",parent,p+new Vector3(5,16,0),new Vector3(.8f,30,.8f),wood);
        }
        static Transform City(string name,Transform parent,Vector3 p,bool fixedStone,out Transform[] variants){
            var root=new GameObject(name).transform;root.SetParent(parent);root.position=p;variants=new Transform[3];
            for(int level=0;level<3;level++){
                var variant=new GameObject(level==0?"Aldea":level==1?"Empalizada":"Muralla de piedra").transform;variant.SetParent(root,false);variants[level]=variant;
                for(int i=0;i<7+level*2;i++){
                    float a=i*2.39996f;float r=18+(i%3)*10;float h=10+(i%3)*4+level*2;
                    var local=new Vector3(Mathf.Cos(a)*r,h*.5f,Mathf.Sin(a)*r);
                    Cube("Casa de adobe",variant,local,new Vector3(14,h,12),clay);
                    Cube("Azotea",variant,local+Vector3.up*(h*.5f),new Vector3(16,2,14),roof);
                    Cube("Puerta",variant,local+new Vector3(0,-h*.5f+3,-6.1f),new Vector3(3,6,.5f),ink);
                }
                Cube("Edificio central",variant,new Vector3(0,13+level*3,0),new Vector3(19,26+level*6,18),clay);
                if(level>0)for(int i=0;i<16;i++){
                    float a=i*Mathf.PI*2/16;if(i==12)continue;
                    var wall=Cube("Defensa",variant,new Vector3(Mathf.Cos(a)*52,level==1?6:9,Mathf.Sin(a)*52),new Vector3(21,level==1?12:18,4),level==1?wood:stone);
                    wall.transform.localRotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg-90,0);
                }
                Cube("Poste de acceso",variant,new Vector3(-12,15,-53),new Vector3(4,30,4),wood);
                Cube("Bandera de ciudad",variant,new Vector3(-7,27,-53),new Vector3(10,6,1),cloth);
                variant.gameObject.SetActive(level==(fixedStone?2:0));
            }
            return root;
        }
        static LineRenderer Line(string name,Transform parent,Material material,float width,Vector3[] points){var l=new GameObject(name).AddComponent<LineRenderer>();l.transform.SetParent(parent);l.sharedMaterial=material;l.widthMultiplier=width;l.useWorldSpace=true;l.positionCount=points.Length;l.SetPositions(points);return l;}
    }
}

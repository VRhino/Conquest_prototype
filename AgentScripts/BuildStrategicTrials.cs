using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Conquest.WorldMap;
public static class BuildStrategicTrials {
 const string Folder="Assets/Scenes/WorldMapTools/Experiments/StrategicTrials";
 public static string Run(){
  if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().isDirty)throw new Exception("Exit Play and save current scene");
  var path=Folder+"/StrategicTrials.unity";if(File.Exists(path))throw new Exception("Existing strategic scene preserved");
  var scene=EditorSceneManager.OpenScene("Assets/Scenes/WorldMapTools/Experiments/CampaignRegion/CampaignRegion.unity",OpenSceneMode.Single);EditorSceneManager.SaveScene(scene,path,true);scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
  var lab=UnityEngine.Object.FindAnyObjectByType<WorldMapRegionLab>();lab.gameObject.name="Strategic regional trials";var scale=lab.scale;
  var template=UnityEngine.Object.Instantiate(lab.cities[0].gameObject);template.SetActive(false);
  var originalChildren=lab.transform.Cast<Transform>().ToArray();
  int[] areas={12,11,5,3,4};string[] labels={"Península","Estepa","Cuenca fértil","Tierras secas","Levantino"};
  var flags=new[]{Make("Kingdom red","Universal Render Pipeline/Simple Lit",new Color(.85f,.28f,.14f)),Make("Kingdom blue","Universal Render Pipeline/Simple Lit",new Color(.12f,.7f,.9f))};
  var borders=new[]{Make("Border red","BronzeAge/Campaign Scale Overlay",new Color(.9f,.32f,.15f)),Make("Border blue","BronzeAge/Campaign Scale Overlay",new Color(.1f,.75f,.95f))};
  var wood=Make("Wood witness","BronzeAge/Campaign Scale Overlay",Color.green);var corridor=Make("Diagnostic corridors","BronzeAge/Campaign Scale Overlay",new Color(1,.77f,.25f,.8f));
  var typeColors=new Dictionary<string,Color>{{"piedra",Color.gray},{"cobre",new Color(.95f,.4f,.08f)},{"estano",new Color(.2f,.5f,1)},{"ganaderia",Color.white},{"oro",Color.yellow},{"territorio",new Color(.2f,.95f,.4f)},{"paso",new Color(.9f,.3f,.9f)}};
  var nodeMats=typeColors.ToDictionary(k=>k.Key,k=>Make("Node "+k.Key,"Universal Render Pipeline/Simple Lit",k.Value));
  var poiMats=typeColors.ToDictionary(k=>k.Key,k=>Make("Focus "+k.Key,"BronzeAge/Campaign Scale Overlay",k.Value));
  var views=new List<RegionTrialView>();
  for(int bank=0;bank<areas.Length;bank++){
   var asset=AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"/region-"+areas[bank]+".json");var data=JsonUtility.FromJson<RegionTrialData>(asset.text);
   var root=new GameObject(data.region);root.transform.SetParent(lab.transform);var view=new RegionTrialView{label=labels[bank],data=asset,root=root,cities=new Transform[4],woodWitnesses=new Transform[4],borders=new LineRenderer[4],markers=new Transform[data.markers.Length]};
   for(int i=0;i<4;i++){
    var s=data.sites[i];var city=UnityEngine.Object.Instantiate(template,root.transform).transform;city.name=s.name+" · R"+(s.kingdom+1);city.position=Ground(scale,s.x,s.z);city.gameObject.SetActive(true);view.cities[i]=city;
    foreach(var r in city.GetComponentsInChildren<Renderer>(true))if(r.name.Contains("Bandera"))r.sharedMaterial=flags[s.kingdom];
    view.borders[i]=Line("Influence "+s.id,root.transform,borders[s.kingdom],3,new Vector3[0]);
    var witness=Circle("Wood "+s.id,root.transform,wood,scale,s.woodX,s.woodZ,15,2);view.woodWitnesses[i]=witness.transform;
   }
   view.resourceRoot=new GameObject("Existing resources");view.resourceRoot.transform.SetParent(root.transform);
   foreach(var n in data.resources){var g=GameObject.CreatePrimitive(PrimitiveType.Sphere);g.name=n.type+" "+n.id;g.transform.SetParent(view.resourceRoot.transform);g.transform.position=Ground(scale,n.x,n.z)+Vector3.up*10;g.transform.localScale=Vector3.one*18;g.GetComponent<Renderer>().sharedMaterial=nodeMats[n.type];UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());}
   view.focalRoot=new GameObject("Diagnostic focuses");view.focalRoot.transform.SetParent(root.transform);
   for(int i=0;i<data.markers.Length;i++){var m=data.markers[i];var marker=new GameObject(m.name).transform;marker.SetParent(view.focalRoot.transform);marker.position=Ground(scale,m.x,m.z);view.markers[i]=marker;
    Circle(m.name+" ring",marker,poiMats[m.type],scale,m.x,m.z,m.type=="territorio"?70:35,4);
    Line("Focus pin",marker,poiMats[m.type],4,new[]{marker.position+Vector3.up*5,marker.position+Vector3.up*70});
   }
   view.corridorRoot=new GameObject("Diagnostic corridors");view.corridorRoot.transform.SetParent(root.transform);
   foreach(var route in data.corridors)Line(route.name,view.corridorRoot.transform,corridor,7,route.points.Select(p=>Ground(scale,p.x,p.z)+Vector3.up*4).ToArray());
   views.Add(view);
  }
  foreach(var old in originalChildren)UnityEngine.Object.DestroyImmediate(old.gameObject);UnityEngine.Object.DestroyImmediate(template);
  lab.trialViews=views.ToArray();
  // Uniform campaign crown representation derived from original terrain forests.
  var allCities=views.SelectMany(v=>v.cities).ToArray();var forests=new List<WorldMapScaleLab.ForestComparison>();
  foreach(var t in scale.terrains){var prior=scale.forests.FirstOrDefault(f=>f.terrain==t);var native=prior==null?t.terrainData:prior.native;
   var proxy=UnityEngine.Object.Instantiate(native);var trees=new List<TreeInstance>();var seen=new HashSet<Vector2Int>();var o=t.transform.position;var size=native.size;
   foreach(var tree in native.treeInstances){var p=o+Vector3.Scale(tree.position,size);if(allCities.Any(c=>new Vector2(p.x-c.position.x,p.z-c.position.z).sqrMagnitude<80*80))continue;
    if(!seen.Add(new Vector2Int(Mathf.FloorToInt(p.x/50),Mathf.FloorToInt(p.z/50))))continue;
    var crown=tree;crown.widthScale*=2.2f;crown.heightScale*=.65f;trees.Add(crown);
   }
   proxy.SetTreeInstances(trees.ToArray(),false);AssetDatabase.CreateAsset(proxy,Folder+"/"+t.name+"-canopies.asset");proxy.terrainLayers=native.terrainLayers;proxy.alphamapResolution=native.alphamapResolution;proxy.SetAlphamaps(0,0,native.GetAlphamaps(0,0,native.alphamapWidth,native.alphamapHeight));foreach(var splat in proxy.alphamapTextures)EditorUtility.SetDirty(splat);EditorUtility.SetDirty(proxy);forests.Add(new WorldMapScaleLab.ForestComparison{terrain=t,native=native,campaign=proxy});
  }
  scale.forests=forests.ToArray();scale.ApplyForest();lab.SwitchRegion(0);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Selection.activeGameObject=lab.gameObject;
  return $"Scene={path}; Regions=5; Cities=20; Forest tiles={forests.Count}; Crowns={forests.Sum(f=>f.campaign.treeInstanceCount)}";
 }
 static Vector3 Ground(WorldMapScaleLab s,float x,float z){var p=new Vector3(x,0,z);p.y=s.Height(p);return p;}
 static Material Make(string n,string shader,Color c){var m=new Material(Shader.Find(shader));m.SetColor("_BaseColor",c);AssetDatabase.CreateAsset(m,Folder+"/"+n+".mat");return m;}
 static LineRenderer Line(string n,Transform parent,Material m,float width,Vector3[] points){var l=new GameObject(n).AddComponent<LineRenderer>();l.transform.SetParent(parent);l.sharedMaterial=m;l.useWorldSpace=true;l.widthMultiplier=width;l.positionCount=points.Length;l.SetPositions(points);return l;}
 static LineRenderer Circle(string n,Transform p,Material m,WorldMapScaleLab s,float x,float z,float radius,float width){return Line(n,p,m,width,Enumerable.Range(0,49).Select(i=>Ground(s,x+radius*Mathf.Cos(i*Mathf.PI*2/48),z+radius*Mathf.Sin(i*Mathf.PI*2/48))+Vector3.up*3).ToArray());}
}

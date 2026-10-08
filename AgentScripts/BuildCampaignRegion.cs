using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Conquest.WorldMap;
using Conquest.WorldMap.Editor;
public static class BuildCampaignRegion {
 const string Folder="Assets/Scenes/WorldMapTools/Experiments/CampaignRegion";
 public static string Run(){
  if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().isDirty)throw new Exception("Save scene and exit Play first");
  var path=Folder+"/CampaignRegion.unity";if(File.Exists(path))throw new Exception("Existing trial is preserved");
  var source=EditorSceneManager.OpenScene(WorldMapScaleLabBuilder.ScenePath,OpenSceneMode.Single);
  var original=UnityEngine.Object.FindAnyObjectByType<WorldMapScaleLab>();original.movementSpeed=12;original.actorVisualScale=1.8f;original.ResetJourney();EditorSceneManager.SaveScene(source);
  EditorSceneManager.SaveScene(source,path,true);var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
  var scale=UnityEngine.Object.FindAnyObjectByType<WorldMapScaleLab>();scale.enabled=false;
  var root=new GameObject("Regional occupancy calibration");var lab=root.AddComponent<WorldMapRegionLab>();lab.scale=scale;
  lab.dataAsset=AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"/campaign-region-trial.json");var data=lab.Data;
  lab.cities=new Transform[data.sites.Length];lab.borders=new LineRenderer[data.sites.Length];lab.woodWitnesses=new Transform[data.sites.Length];
  Color[] colors={new Color(.8f,.32f,.18f),new Color(.2f,.65f,.8f),new Color(.75f,.64f,.18f),new Color(.7f,.3f,.7f),new Color(.2f,.8f,.4f)};
  Material[] flags=new Material[data.kingdomCount],lines=new Material[data.kingdomCount];
  for(int i=0;i<data.kingdomCount;i++){
   flags[i]=Make("Kingdom "+(i+1),"Universal Render Pipeline/Simple Lit",colors[i]);
   lines[i]=Make("Influence "+(i+1),"BronzeAge/Campaign Scale Overlay",colors[i]);
  }
  var wood=Make("Wood witness","BronzeAge/Campaign Scale Overlay",new Color(.2f,.95f,.2f));
  for(int i=0;i<data.sites.Length;i++){
   var s=data.sites[i];var city=UnityEngine.Object.Instantiate(scale.cityA.gameObject,root.transform).transform;city.name=s.name+" · R"+(s.kingdom+1);
   var pos=new Vector3(s.x,0,s.z);pos.y=scale.Height(pos);city.position=pos;city.gameObject.SetActive(true);lab.cities[i]=city;
   foreach(var r in city.GetComponentsInChildren<Renderer>(true))if(r.name.Contains("Bandera"))r.sharedMaterial=flags[s.kingdom];
   var border=new GameObject("Influence "+s.id).AddComponent<LineRenderer>();border.transform.SetParent(root.transform);border.sharedMaterial=lines[s.kingdom];border.widthMultiplier=3;lab.borders[i]=border;
   var witness=new GameObject("Madera "+s.id).AddComponent<LineRenderer>();witness.transform.SetParent(root.transform);witness.sharedMaterial=wood;witness.widthMultiplier=2;witness.positionCount=33;
   for(int j=0;j<33;j++){float a=j*Mathf.PI*2/32;var p=new Vector3(s.woodX+15*Mathf.Cos(a),0,s.woodZ+15*Mathf.Sin(a));p.y=scale.Height(p)+3;witness.SetPosition(j,p);}lab.woodWitnesses[i]=witness.transform;
  }
  scale.cityA.gameObject.SetActive(false);scale.cityB.gameObject.SetActive(false);scale.actor.gameObject.SetActive(false);scale.caravan.gameObject.SetActive(false);scale.influence.gameObject.SetActive(false);scale.routeLine.gameObject.SetActive(false);
  lab.resourceRoot=new GameObject("Existing resource nodes");lab.resourceRoot.transform.SetParent(root.transform);
  var mats=new Dictionary<string,Material>();string[] types={"piedra","cobre","estano","ganaderia","oro"};Color[] nodeColors={Color.gray,new Color(.9f,.42f,.12f),new Color(.2f,.5f,1),Color.white,Color.yellow};
  for(int i=0;i<types.Length;i++)mats[types[i]]=Make("Node "+types[i],"Universal Render Pipeline/Simple Lit",nodeColors[i]);
  foreach(var n in data.resources){var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name=n.type+" "+n.id;go.transform.SetParent(lab.resourceRoot.transform);var p=new Vector3(n.x,0,n.z);p.y=scale.Height(p)+10;go.transform.position=p;go.transform.localScale=Vector3.one*18;go.GetComponent<Renderer>().sharedMaterial=mats[n.type];UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());}
  var min=lab.cities[0].position;var max=min;foreach(var c in lab.cities){min=Vector3.Min(min,c.position);max=Vector3.Max(max,c.position);}min-=new Vector3(650,0,650);max+=new Vector3(650,0,650);
  var comparisons=new List<WorldMapScaleLab.ForestComparison>();int originalTrees=0,proxyTrees=0;
  foreach(var t in scale.terrains){var o=t.transform.position;var size=t.terrainData.size;var prior=scale.forests.FirstOrDefault(f=>f.terrain==t);var native=prior==null?t.terrainData:prior.native;t.terrainData=native;
   if(o.x>max.x||o.x+size.x<min.x||o.z>max.z||o.z+size.z<min.z)continue;
   var proxy=UnityEngine.Object.Instantiate(native);var seen=new HashSet<Vector2Int>();var trees=new List<TreeInstance>();originalTrees+=native.treeInstanceCount;
   foreach(var tree in native.treeInstances){var p=o+Vector3.Scale(tree.position,size);
    if(p.x>=min.x&&p.x<=max.x&&p.z>=min.z&&p.z<=max.z){
     if(lab.cities.Any(c=>new Vector2(p.x-c.position.x,p.z-c.position.z).sqrMagnitude<80*80))continue;
     if(!seen.Add(new Vector2Int(Mathf.FloorToInt(p.x/50),Mathf.FloorToInt(p.z/50))))continue;
     var crown=tree;crown.widthScale*=2.2f;crown.heightScale*=.65f;trees.Add(crown);
    }else trees.Add(tree);
   }
   proxy.SetTreeInstances(trees.ToArray(),false);proxyTrees+=trees.Count;AssetDatabase.CreateAsset(proxy,Folder+"/"+t.name+"-canopies.asset");proxy.terrainLayers=native.terrainLayers;proxy.alphamapResolution=native.alphamapResolution;proxy.SetAlphamaps(0,0,native.GetAlphamaps(0,0,native.alphamapWidth,native.alphamapHeight));foreach(var splat in proxy.alphamapTextures)EditorUtility.SetDirty(splat);EditorUtility.SetDirty(proxy);comparisons.Add(new WorldMapScaleLab.ForestComparison{terrain=t,native=native,campaign=proxy});
  }
  scale.forests=comparisons.ToArray();scale.campaignForest=true;scale.ApplyForest();lab.selected=1;lab.Apply();
  EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Selection.activeGameObject=root;
  return $"Scene={path}; Cities={lab.cities.Length}; Forest tiles={comparisons.Count}; Native={originalTrees}; Proxy={proxyTrees}";
 }
 static Material Make(string name,string shader,Color color){var s=Shader.Find(shader);if(!s)throw new Exception("Shader missing "+shader);var m=new Material(s);m.SetColor("_BaseColor",color);AssetDatabase.CreateAsset(m,Folder+"/"+name+".mat");return m;}
}



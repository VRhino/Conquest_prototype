using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class InspectWorldMapHeroSource
{
 public static string Run()
 {
  if(Application.isPlaying)throw new Exception("Inspect in Edit mode");
  var active=SceneManager.GetActiveScene();
  var scene=EditorSceneManager.OpenScene("Assets/Scenes/GameScenes/FeudoScene.unity",OpenSceneMode.Additive);
  try{
   var registry=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<VisualPrefabRegistry>(true)).FirstOrDefault();
   if(!registry)throw new Exception("No visual registry in FeudoScene");
   var prefab=registry.GetDefaultHeroPrefab();
   var config=new SerializedObject(registry).FindProperty("config").objectReferenceValue;
   if(!prefab)throw new Exception("No default hero configured");
   var anim=prefab.GetComponentInChildren<Animator>(true);
   var log="Config: "+AssetDatabase.GetAssetPath(config)+"\nHero: "+AssetDatabase.GetAssetPath(prefab)+"\nController: "+AssetDatabase.GetAssetPath(anim.runtimeAnimatorController)+"\nAvatar: "+AssetDatabase.GetAssetPath(anim.avatar)+"\n";
   log+=string.Join("\n",prefab.GetComponentsInChildren<Component>(true).Where(c=>c is MonoBehaviour||c is Animator||c is UnityEngine.AI.NavMeshAgent||c is CharacterController||c is SkinnedMeshRenderer).Select(c=>c.gameObject.name+": "+c.GetType().FullName+(c is Behaviour b?" enabled="+b.enabled:"")+(c is SkinnedMeshRenderer r?" active="+r.gameObject.activeInHierarchy+" mesh="+(r.sharedMesh?r.sharedMesh.name:"null"):"")));
   log+="\nParameters: "+string.Join(",",anim.parameters.Select(p=>p.name+" "+p.type));
   return log;
  }finally{EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(active);}
 }
}

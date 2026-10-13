using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Conquest.WorldMap;
using Conquest.WorldMap.Editor;
using ConquestTactics.Visual;
using Data.Avatar;

public static class BuildWorldMapHero
{
 const string HeroPath="Assets/WorldMap/Campaign/Symbols/Hero.prefab";
 public static string Run()
 {
  if(Application.isPlaying||SceneManager.GetActiveScene().isDirty)throw new Exception("Save scene and exit Play first.");
  var originalScene=SceneManager.GetActiveScene().path;
  var config=AssetDatabase.LoadAssetAtPath<VisualPrefabConfiguration>("Assets/ScriptableObjects/VisualPrefabConfig.asset");
  var source=config.GetDefaultHeroPrefab();if(!source)throw new Exception("Feudo's default hero is missing.");
  var sourcePath=AssetDatabase.GetAssetPath(source);
  var visual=PrefabUtility.LoadPrefabContents(sourcePath);
  GameObject root=null;
  try
  {
   // Use the same body database and helpers as Feudo; bake a self-contained test appearance.
   var db=Resources.Load<AvatarPartDatabase>("Data/Avatar/AvatarPartDatabase");
   if(!db)throw new Exception("Avatar database missing");
   var extra=new List<string>{db.faceParts.First().id,db.hairParts.First().id,db.eyebrowsParts.First().id};
   var selector=visual.GetComponent<AvatarPartSelector>();selector.allGenderParts.SetActive(true);
   AvatarVisualUtils.ResetModularDummyToBase(visual.transform,db,extra,Gender.Male);
   var animator=visual.GetComponentInChildren<Animator>(true);
   if(!animator||!animator.avatar||!animator.avatar.isHuman||!animator.runtimeAnimatorController)throw new Exception("Hero is not a configured humanoid.");
   // Keep skinning/rig/Animator; the campaign controller is the only movement owner.
   foreach(var script in visual.GetComponentsInChildren<MonoBehaviour>(true))UnityEngine.Object.DestroyImmediate(script);
   foreach(var collider in visual.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
   foreach(var body in visual.GetComponentsInChildren<Rigidbody>(true))UnityEngine.Object.DestroyImmediate(body);
   foreach(var agent in visual.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true))UnityEngine.Object.DestroyImmediate(agent);
   // Remove unused modular renderers without deleting skeleton transforms.
   foreach(var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
   {
    if(!renderer.gameObject.activeInHierarchy)UnityEngine.Object.DestroyImmediate(renderer);
    else {renderer.updateWhenOffscreen=true;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;}
   }
   animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
   root=new GameObject("Campaign hero");SceneManager.MoveGameObjectToScene(root,visual.scene);visual.transform.SetParent(root.transform,false);visual.name="Feudo hero visual";
   // Feudo's instantiation replaces the prefab root scale with the gameplay scale too.
   visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=Quaternion.identity;visual.transform.localScale=Vector3.one;
   var presentation=root.AddComponent<WorldMapHeroPresentation>();presentation.visualAnimator=animator;
   // Use the user-approved scale. Pre-animation skin bounds do not represent this rig's posed size.
   PrefabUtility.SaveAsPrefabAsset(root,HeroPath);
   foreach(var scenePath in new[]{WorldMapCalibrationBuilder.MainScene,WorldMapCalibrationBuilder.TrialScene})
   {
    var scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Single);
    var c=UnityEngine.Object.FindAnyObjectByType<WorldMapCampaignController>();var old=c.actor;
    var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HeroPath),scene);
    go.transform.SetParent(old.parent,false);go.transform.position=old.position;go.transform.rotation=old.rotation;
    go.transform.localScale=Vector3.one*c.scaleContract.heroSymbolScale;
    c.actor=go.transform;c.actorLift=.05f;go.GetComponent<WorldMapHeroPresentation>().campaign=c;
    UnityEngine.Object.DestroyImmediate(old.gameObject);
    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
   }
   EditorSceneManager.OpenScene(originalScene,OpenSceneMode.Single);AssetDatabase.SaveAssets();
   return "Campaign and calibration now use "+HeroPath+"; source "+sourcePath+"; scale comes from heroSymbolScale; active skins="+root.GetComponentsInChildren<SkinnedMeshRenderer>().Length;
  }
  finally
  {
   if(visual)PrefabUtility.UnloadPrefabContents(visual);
   if(root)UnityEngine.Object.DestroyImmediate(root);
  }
 }
}

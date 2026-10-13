using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Conquest.WorldMap;
using Conquest.WorldMap.Editor;
using ConquestTactics.Visual;
public static class ApplyWorldMapHeroScale
{
 public static string Run()
 {
  if(Application.isPlaying)throw new Exception("Exit Play first");
  var current=SceneManager.GetActiveScene();var returnPath=current.path;
  if(current.isDirty)EditorSceneManager.SaveScene(current);
  var profile=AssetDatabase.LoadAssetAtPath<WorldMapScaleContract>(WorldMapCalibrationBuilder.ContractPath);
  profile.heroSymbolScale=6;EditorUtility.SetDirty(profile);
  foreach(var path in new[]{WorldMapCalibrationBuilder.MainScene,WorldMapCalibrationBuilder.TrialScene})
  {
   var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
   var c=UnityEngine.Object.FindAnyObjectByType<WorldMapCampaignController>();
   if(!c.actor.GetComponent<WorldMapHeroPresentation>())throw new Exception("Animated hero missing in "+path);
   c.actor.localScale=Vector3.one*profile.heroSymbolScale;c.ApplyScaleContract();
   EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  }
  AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(returnPath,OpenSceneMode.Single);
  return "Scale6 saved in both campaign scenes and scale contract. Movement follows the existing contract.";
 }
}

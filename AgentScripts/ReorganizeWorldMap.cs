using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class ReorganizeWorldMap {
 static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;var parent=path.Substring(0,path.LastIndexOf('/'));Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
 public static string Run(){
  if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
  if(EditorSceneManager.GetActiveScene().isDirty)throw new Exception("Save scene before moving");
  var moves=new[]{
   new[]{"Assets/Scenes/WorldMapCampaign.unity","Assets/Scenes/GameScenes/WorldMapCampaign.unity"},
   new[]{"Assets/Scenes/SettlementPreview.unity","Assets/Scenes/GameScenes/SettlementPreview.unity"},
   new[]{"Assets/Scenes/WorldMapPreview.unity","Assets/Scenes/WorldMapTools/WorldMapPreview.unity"},
   new[]{"Assets/WorldMap/Experiments","Assets/Scenes/WorldMapTools/Experiments"},
   new[]{"Assets/WorldMap/Runtime","Assets/Scripts/Map/WorldMap"},
   new[]{"Assets/WorldMap/Editor","Assets/Scripts/Editor/WorldMap"}
  };
  var guids=moves.ToDictionary(m=>m[0],m=>AssetDatabase.AssetPathToGUID(m[0]));
  var build=EditorBuildSettings.scenes.Select(s=>new EditorBuildSettingsScene(s.path,s.enabled)).ToArray();
  foreach(var m in moves){if(string.IsNullOrEmpty(guids[m[0]]))throw new Exception("Missing "+m[0]);if(!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(m[1])))throw new Exception("Already exists "+m[1]);}
  foreach(var m in moves){Folder(m[1].Substring(0,m[1].LastIndexOf('/')));var error=AssetDatabase.MoveAsset(m[0],m[1]);if(!string.IsNullOrEmpty(error))throw new Exception(error);if(AssetDatabase.AssetPathToGUID(m[1])!=guids[m[0]])throw new Exception("GUID changed");}
  foreach(var s in build)foreach(var m in moves)if(s.path==m[0]||s.path.StartsWith(m[0]+"/"))s.path=m[1]+s.path.Substring(m[0].Length);
  EditorBuildSettings.scenes=build;AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/GameScenes/WorldMapCampaign.unity");
  return "6 moves completed; GUIDs preserved; build paths updated";
 }
}

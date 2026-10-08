using System;using System.IO;using UnityEditor;using UnityEditor.AddressableAssets.Settings;using UnityEditor.AddressableAssets.Build;
public static class BuildCampaignContent {
 public static string Run(){if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play");EditorApplication.delayCall+=Build;return "Addressables build scheduled; report=Assets/WorldMap/Campaign/ContentBuild.txt";}
 public static string RunNow(){if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play");Build();return File.ReadAllText("Assets/WorldMap/Campaign/ContentBuild.txt");}
 static void Build(){try{AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);File.WriteAllText("Assets/WorldMap/Campaign/ContentBuild.txt",string.IsNullOrEmpty(result.Error)?"Succeeded\nDuration="+result.Duration+"\nOutput="+result.OutputPath:"Failed\n"+result.Error);}catch(Exception e){File.WriteAllText("Assets/WorldMap/Campaign/ContentBuild.txt","Failed\n"+e);}}
}

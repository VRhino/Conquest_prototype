using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Conquest.WorldMap;
using Newtonsoft.Json.Linq;
public static class SetupCampaignInteraction {
 public static string Run(){if(EditorApplication.isPlaying)throw new System.Exception("Stop Play first");var scene=EditorSceneManager.OpenScene("Assets/Scenes/GameScenes/WorldMapCampaign.unity");var c=Object.FindFirstObjectByType<WorldMapCampaignController>();var sample=EditorSceneManager.OpenScene("Assets/Scenes/WorldMapTools/SettlementPreview.unity",OpenSceneMode.Additive);var preview=Object.FindFirstObjectByType<Conquest.SettlementPreview.SettlementPreview>();var source=preview.source;var id=string.IsNullOrEmpty(preview.settlementId)?(string)JObject.Parse(source.text)["asentamientos"][0]["id"]:preview.settlementId;EditorSceneManager.CloseScene(sample,true);
  var cities=new[]{c.cityA,c.cityB};for(int i=0;i<cities.Length;i++){var city=cities[i];var target=city.GetComponent<WorldMapSettlementTarget>();if(!target)target=city.gameObject.AddComponent<WorldMapSettlementTarget>();target.settlementId="local-city-"+i;target.displayName=i==0?"Ciudad de la cuenca":"Ciudad del paso";target.previewSource=source;target.previewSettlementId=id;target.entrance=c.demoRoute[i==0?0:c.demoRoute.Length-1];var bounds=new Bounds(city.position,Vector3.zero);foreach(var r in city.GetComponentsInChildren<Renderer>())bounds.Encapsulate(r.bounds);var collider=city.GetComponent<BoxCollider>();if(!collider)collider=city.gameObject.AddComponent<BoxCollider>();collider.center=city.InverseTransformPoint(bounds.center);var scale=city.lossyScale;collider.size=new Vector3(bounds.size.x/scale.x,bounds.size.y/scale.y,bounds.size.z/scale.z);collider.isTrigger=true;EditorUtility.SetDirty(target);}
  if(!c.GetComponent<WorldMapCampaignHud>())c.gameObject.AddComponent<WorldMapCampaignHud>();c.showDiagnostics=false;EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();return "HUD, two clickable towns, preview source and entrance anchors saved";
 }
}

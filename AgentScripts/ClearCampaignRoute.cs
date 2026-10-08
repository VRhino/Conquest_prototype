using System;using System.Linq;using UnityEngine;using UnityEditor;using Conquest.WorldMap;
public static class ClearCampaignRoute {
 public static string Run(){if(Application.isPlaying)throw new Exception("Edit mode");var c=UnityEngine.Object.FindAnyObjectByType<WorldMapCampaignController>();int removed=0;
  foreach(var s in c.streamer.manifest.sectors){string path="Assets/WorldMap/Campaign/Sectors/"+s.id+".prefab";var root=PrefabUtility.LoadPrefabContents(path);try{var sector=root.GetComponent<WorldMapCampaignSector>();var native=sector.terrain.terrainData;var o=s.origin;var size=native.size;
   if(c.demoRoute.Any(p=>p.x>=o.x-200&&p.x<=o.x+size.x+200&&p.z>=o.z-200&&p.z<=o.z+size.z+200)){
    string dataPath="Assets/WorldMap/Campaign/Sectors/"+s.id+"-terrain.asset";if(AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath))throw new Exception("Clearing already applied");var data=UnityEngine.Object.Instantiate(native);
    var trees=native.treeInstances.Where(tree=>{var p=o+Vector3.Scale(tree.position,size);if(new Vector2(p.x-c.cityA.position.x,p.z-c.cityA.position.z).sqrMagnitude<125*125||new Vector2(p.x-c.cityB.position.x,p.z-c.cityB.position.z).sqrMagnitude<125*125)return false;
     var a=c.demoRoute[0];var b=c.demoRoute[c.demoRoute.Length-1];var d=b-a;d.y=0;var v=p-a;v.y=0;float t=Mathf.Clamp01(Vector3.Dot(v,d)/d.sqrMagnitude);return (v-d*t).sqrMagnitude>=55*55;}).ToArray();removed+=native.treeInstanceCount-trees.Length;data.SetTreeInstances(trees,false);AssetDatabase.CreateAsset(data,dataPath);sector.terrain.terrainData=data;root.GetComponentInChildren<TerrainCollider>().terrainData=data;PrefabUtility.SaveAsPrefabAsset(root,path);
   }
  }finally{PrefabUtility.UnloadPrefabContents(root);}}
  AssetDatabase.SaveAssets();return "Visual trees removed around fixture cities/route="+removed+"; canonical forest untouched";
 }
}

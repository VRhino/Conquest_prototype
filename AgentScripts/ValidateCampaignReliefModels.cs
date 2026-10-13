using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;
using Conquest.WorldMap;
using Conquest.WorldMap.Editor;
public static class ValidateCampaignReliefModels {
 public static string Run(){
  if(Application.isPlaying)throw new Exception("Exit Play for validation");
  var controller=UnityEngine.Object.FindAnyObjectByType<WorldMapCampaignController>();var c=WorldMapReliefModelBuilder.Read(controller);
  var layout=AssetDatabase.LoadAssetAtPath<WorldMapReliefLayout>(WorldMapReliefModelBuilder.Folder+"/Layout.asset");
  if(layout.sourceSHA256!=c.manifest.sourceSHA256||layout.seed!=c.manifest.seed)throw new Exception("Stale layout");
  if(layout.fingerprint!=WorldMapReliefModelBuilder.Fingerprint(WorldMapReliefModelBuilder.Generate(c)))throw new Exception("Seeded layout changed");
  if(layout.placements.Select(p=>p.id).Distinct().Count()!=layout.placements.Length)throw new Exception("Duplicate placements");
  foreach(var p in layout.placements)if(!WorldMapReliefModelBuilder.SafeFootprint(c,p.footprint,p.kind!="coast"))throw new Exception("Walkable cell or resource obscured: "+p.id);
  var clone=UnityEngine.Object.Instantiate(c.manifest);c.manifest=clone;var hashes=new System.Collections.Generic.HashSet<string>();
  try{foreach(int seed in new[]{1,7,42}){clone.seed=seed;string a=WorldMapReliefModelBuilder.Fingerprint(WorldMapReliefModelBuilder.Generate(c));string b=WorldMapReliefModelBuilder.Fingerprint(WorldMapReliefModelBuilder.Generate(c));if(a!=b)throw new Exception("Non deterministic art seed "+seed);hashes.Add(a);}}finally{UnityEngine.Object.DestroyImmediate(clone);c.manifest=controller.streamer.manifest;}
  if(hashes.Count!=3)throw new Exception("Seed does not vary models");
  int drawn=0;long triangles=0;
  foreach(var s in c.manifest.sectors){
   var root=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WorldMap/Campaign/Sectors/"+s.id+".prefab");var near=root.transform.Find(WorldMapReliefModelBuilder.ChildName);var far=controller.streamer.overview.Single(o=>o.position==s.origin).Find(WorldMapReliefModelBuilder.ChildName);
   if((bool)near!=(bool)far)throw new Exception("Near/far mismatch");if(!near)continue;
   var renderer=near.GetComponent<WorldMapSyntyRenderer>();var data=renderer.instances;if(data!=far.GetComponent<WorldMapSyntyRenderer>().instances)throw new Exception("Near/far model drift");
   if(near.GetComponentsInChildren<Collider>(true).Length>0||near.childCount!=0)throw new Exception("Individual model colliders or GameObjects");
   foreach(var batch in data.batches){if(batch.matrices.Length>1023||!batch.material.enableInstancing||!batch.always)throw new Exception("Invalid instancing batch");drawn+=batch.matrices.Length;triangles+=(long)batch.mesh.GetIndexCount(batch.submesh)/3*batch.matrices.Length;
    foreach(var m in batch.matrices){if(m.determinant<=0||float.IsNaN(m.determinant))throw new Exception("Invalid model transform");var b=WorldMapSyntyBaker.TransformBounds(batch.mesh.bounds,m);if(!batch.bounds.Contains(b.min)||!batch.bounds.Contains(b.max))throw new Exception("Culling loses model");}
   }
  }
  if(drawn!=layout.placements.Length)throw new Exception("Missing or duplicate rendered model");
  string report=$"{drawn} Synty models; {triangles} triangles; same seed rebuild matches saved layout; art seeds 1/7/42 deterministic and distinct on the same geography; all expanded footprints avoid walkable cells and resources; near/far instances match; instancing and culling bounds valid; no per-model GameObjects or colliders.";
  File.WriteAllText("Docs/ReliefModelValidation.txt",report);return report;
 }
}

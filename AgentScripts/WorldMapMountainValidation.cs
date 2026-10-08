using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using Conquest.WorldMap;
using Conquest.WorldMap.Editor;
using BronzeAge.RegionalMapTool;

public static class WorldMapMountainValidation
{
#pragma warning disable CS0649 // Fields populated by Json.NET.
    [Serializable] class World { public Resource[] recursos; }
    [Serializable] class Resource { public string id,tipo; public int area; public float x,z,alturaY; }
#pragma warning restore CS0649
    public static object Run()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!=WorldMapMountainTrial.ScenePath)throw new Exception("Open mountain experiment");
        var p=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<WorldMapPreviewController>(true)).Single();
        var ts=p.worldRoot.GetComponentsInChildren<Terrain>();
        var package=Path.GetDirectoryName(File.ReadAllLines(WorldMapMountainTrial.Folder+"/Ready.txt").Single(l=>l.StartsWith("Source=")).Substring(7));
        var canonical=Newtonsoft.Json.JsonConvert.DeserializeObject<World>(File.ReadAllText(Path.Combine(package,"world.json")));
        var resources=p.worldRoot.GetComponentsInChildren<BronzeAgeRegionalMarker>(true).ToDictionary(m=>m.sourceId);
        if(ts.Length!=48 || resources.Count!=canonical.recursos.Length)throw new Exception("Missing canonical geometry/resources");
        foreach(var r in canonical.recursos)
        {
            if(!resources.TryGetValue(r.id,out var m)||m.resourceType!=r.tipo||m.region!=r.area+1)throw new Exception("Canonical resource changed: "+r.id);
            if((m.canonicalPosition-new Vector3(r.x,r.alturaY,r.z)).magnitude>.01f)throw new Exception("Canonical resource position changed");
        }
        for(int region=1;region<=16;region++)foreach(var type in new[]{"cobre","estano"})
            if(!resources.Values.Any(r=>r.region==region&&r.resourceType==type))throw new Exception("Region missing metal");
        var maps=ts.ToDictionary(t=>t,t=>t.terrainData.GetAlphamaps(0,0,256,256));
        var heights=ts.ToDictionary(t=>t,t=>t.terrainData.GetHeights(0,0,257,257));
        int trees=0,borders=0;double alphaSeam=0,heightSeam=0,sumError=0,rootError=0,rawError=0;
        foreach(var t in ts)
        {
            var d=t.terrainData;
            if(!AssetDatabase.GetAssetPath(d).StartsWith(WorldMapMountainTrial.Folder+"/")||t.GetComponent<TerrainCollider>().terrainData!=d||d.heightmapResolution!=257)throw new Exception("Shared or mismatched terrain");
            if(d.terrainLayers.Length!=8||d.terrainLayers.Any(l=>!AssetDatabase.GetAssetPath(l.diffuseTexture).StartsWith("Assets/Yoge/")))throw new Exception("Approved soil lost");
            if(ShaderUtil.ShaderHasError(t.materialTemplate.shader)||Math.Abs(t.materialTemplate.GetVector("_RegionalMountainLook").x-.88f)>.001)throw new Exception("Mountain shader not applied");
            if(Math.Abs(t.materialTemplate.GetVector("_RegionalMountainDetail").x-.8f)>.001)throw new Exception("Regional rock detail missing");
            if(d.treePrototypes.Any(v=>!v.prefab||!v.prefab.GetComponent<LODGroup>()))throw new Exception("Missing native tree LOD");
            trees+=d.treeInstanceCount;
            foreach(var tree in d.treeInstances)
            {
                var v=tree.position;
                if(float.IsNaN(v.y)||v.x<0||v.x>1||v.z<0||v.z>1||v.y*1000<379||d.GetSteepness(v.x,v.z)>32.001f)throw new Exception("Invalid forest root");
                rootError=Math.Max(rootError,Math.Abs(v.y*d.size.y-d.GetInterpolatedHeight(v.x,v.z)));
            }
            var map=maps[t];for(int z=0;z<256;z++)for(int x=0;x<256;x++)
            {
                double sum=0;for(int i=0;i<8;i++){var w=map[z,x,i];if(float.IsNaN(w)||w<0||w>1)throw new Exception("Invalid alpha");sum+=w;}
                sumError=Math.Max(sumError,Math.Abs(sum-1));
            }
            var raw=File.ReadAllBytes(Path.Combine(package,t.name+".r16"));
            for(int z=0;z<257;z++)for(int x=0;x<257;x++){int at=(z*257+x)*2;rawError=Math.Max(rawError,Math.Abs(heights[t][z,x]-(raw[at]|raw[at+1]<<8)/65535f)*1000);}
            foreach(var direction in new[]{Vector3.right,Vector3.forward})
            {
                var neighbor=ts.FirstOrDefault(o=>(o.transform.position-t.transform.position-direction*4000).sqrMagnitude<.01f);
                if(!neighbor)continue;borders++;
                for(int k=0;k<257;k++)heightSeam=Math.Max(heightSeam,direction.x>0?Math.Abs(heights[t][k,256]-heights[neighbor][k,0])*1000:Math.Abs(heights[t][256,k]-heights[neighbor][0,k])*1000);
                for(int k=0;k<256;k++)for(int i=0;i<8;i++)alphaSeam=Math.Max(alphaSeam,direction.x>0?Math.Abs(map[k,255,i]-maps[neighbor][k,0,i]):Math.Abs(map[255,k,i]-maps[neighbor][0,k,i]));
            }
        }
        if(trees<100000||borders!=82||heightSeam>.001||alphaSeam>1e-5||sumError>.025||rootError>.01||rawError>.02)throw new Exception("Terrain/forest seam or canonical mismatch");
        var overview=p.worldRoot.GetComponent<WorldMapForestOverview>();
        if(!overview || !overview.canopy || overview.canopy.sourceTrees!=trees || overview.viewCamera!=p.mapCamera)
            throw new Exception("Overview lost native forest identity");
        var cache=overview.canopy;
        if(cache.canopyGroups<1000 || cache.canopyGroups>=trees || cache.batches.Sum(b=>b.matrices.Length)!=cache.canopyGroups
            ||cache.batches.Any(b=>!b.mesh||!b.material||!b.material.enableInstancing||b.matrices.Length>1023)
            ||ts.Any(t=>!t.drawTreesAndFoliage||t.heightmapPixelError!=2))throw new Exception("Invalid overview cache or near-view state leaked");
        var report="Scene="+scene.path+"\nSectors="+ts.Length+"\nHeight resolution=257\nTrees="+trees+"\nResources="+resources.Count+
            "\nTin="+resources.Values.Count(r=>r.resourceType=="estano")+"\nCopper="+resources.Values.Count(r=>r.resourceType=="cobre")+
            "\nRegions with both metals=16\nShared borders="+borders+"\nHeight border difference metres="+heightSeam+
            "\nLayer border difference="+alphaSeam+"\nLayer sum error="+sumError+"\nForest root error metres="+rootError+"\nR16 import error metres="+rawError+
            "\nOverview canopy groups="+cache.canopyGroups+"\nOverview instanced batches="+cache.batches.Length;
        File.WriteAllText(WorldMapMountainTrial.Folder+"/LiveValidation.txt",report);return report;
    }
}

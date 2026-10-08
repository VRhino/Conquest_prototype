using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Conquest.WorldMap;
using Conquest.WorldMap.Editor;
using BronzeAge.RegionalMapTool;
using sc.terrain.vegetationspawner;

public static class WorldMapForestValidation
{
    class Point { public Vector2 position; public float spacing; public int sector; }
    public static object Run()
    {
        var lab = SceneManager.GetSceneByPath(WorldMapLandscapeLab.ScenePath);
        if (!lab.IsValid() || !lab.isLoaded) throw new Exception("Open the lab first");
        var preview = lab.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WorldMapPreviewController>(true)).First();
        var spawner = preview.worldRoot.GetComponent<VegetationSpawner>();
        string first = Signature(spawner);
        WorldMapVegetationTrial.Generate();
        if (first != Signature(spawner)) throw new Exception("Generation is not deterministic");
        var original = SceneManager.GetSceneByPath(WorldMapPreviewBuilder.ScenePath);
        bool close = !original.IsValid() || !original.isLoaded;
        if (close) original = EditorSceneManager.OpenScene(WorldMapPreviewBuilder.ScenePath, OpenSceneMode.Additive);
        try
        {
            var originals = original.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Terrain>(true)).ToDictionary(t => t.transform.position);
            int samples = 0;
            foreach (var terrain in preview.worldRoot.GetComponentsInChildren<Terrain>(true))
            {
                var data = terrain.terrainData; var expected = originals[terrain.transform.position].terrainData;
                if (data == expected || terrain.GetComponent<TerrainCollider>().terrainData != data || data.size != expected.size)
                    throw new Exception("Terrain data isolation failed");
                int n = data.heightmapResolution; var a = data.GetHeights(0,0,n,n); var b = expected.GetHeights(0,0,n,n);
                for (int z=0;z<n;z++) for(int x=0;x<n;x++) { if(a[z,x]!=b[z,x]) throw new Exception("Height changed"); samples++; }
            }
            int resources = lab.GetRootGameObjects().Sum(r => r.GetComponentsInChildren<BronzeAgeRegionalMarker>(true).Length);
            if (resources != original.GetRootGameObjects().Sum(r => r.GetComponentsInChildren<BronzeAgeRegionalMarker>(true).Length)) throw new Exception("Resources changed");
            var profile = AssetDatabase.LoadAssetAtPath<WorldMapForestTrialProfile>(WorldMapVegetationTrial.ProfilePath);
            var grid = new Dictionary<Vector2Int,List<Point>>(); int trees=0, borderPairs=0; float minimum=float.MaxValue;
            for(int sector=0;sector<spawner.terrains.Count;sector++)
            {
                var terrain=spawner.terrains[sector]; var data=terrain.terrainData;
                var source=originals[terrain.transform.position];
                var path=Path.Combine(Path.GetDirectoryName(AssetDatabase.GetAssetPath(source.terrainData)),terrain.name+".layers.bytes");
                var raw=File.ReadAllBytes(path); int n=Mathf.RoundToInt(Mathf.Sqrt(raw.Length/7f));
                foreach(var tree in data.treeInstances)
                {
                    float u=tree.position.x,v=tree.position.z,x=u*(n-1),z=v*(n-1); int ix=(int)x,iz=(int)z,xx=Mathf.Min(ix+1,n-1),zz=Mathf.Min(iz+1,n-1);
                    float density=Mathf.Lerp(Mathf.Lerp(raw[(iz*n+ix)*7],raw[(iz*n+xx)*7],x-ix),Mathf.Lerp(raw[(zz*n+ix)*7],raw[(zz*n+xx)*7],x-ix),z-iz)/255f;
                    int offset=(Mathf.RoundToInt(v*(n-1))*n+Mathf.RoundToInt(u*(n-1)))*7;
                    if(density<profile.minimumDensity-1e-5f || raw[offset+3]==255 || data.GetSteepness(u,v)>32.001f) throw new Exception("Invalid mask/slope");
                    var world=terrain.transform.position+Vector3.Scale(tree.position,data.size);
                    density=profile.VisualDensity(density,new Vector2(world.x,world.z));
                    if(density<profile.minimumDensity-1e-5f)throw new Exception("Invalid organic forest edge");
                    if(world.y<spawner.waterHeight-.01f) throw new Exception("Underwater tree");
                    if(Mathf.Abs(world.y-terrain.transform.position.y-data.GetInterpolatedHeight(u,v))>.01f) throw new Exception("Floating tree");
                    var p=new Point{position=new Vector2(world.x,world.z),spacing=profile.Spacing(density),sector=sector};
                    var key=new Vector2Int(Mathf.FloorToInt(world.x/75),Mathf.FloorToInt(world.z/75));
                    for(int bx=key.x-1;bx<=key.x+1;bx++)for(int bz=key.y-1;bz<=key.y+1;bz++)
                    {
                        if(!grid.TryGetValue(new Vector2Int(bx,bz),out var list))continue;
                        foreach(var other in list)
                        {
                            float distance=Vector2.Distance(p.position,other.position);minimum=Mathf.Min(minimum,distance);
                            if(distance+.01f<Mathf.Max(p.spacing,other.spacing))throw new Exception("Spacing violation across species/sectors");
                            if(other.sector!=sector && distance<profile.maximumSpacing)borderPairs++;
                        }
                    }
                    if(!grid.TryGetValue(key,out var entries))grid[key]=entries=new List<Point>();entries.Add(p);trees++;
                }
            }
            var report=new {trees,minimumSeparation=minimum,crossSectorNeighborPairs=borderPairs,deterministic=true,heightSamplesUnchanged=samples,resources,
                independentTerrainData=true,spacingVerified=true,masksVerified=true};
            File.WriteAllText(WorldMapLandscapeLab.Folder+"/ForestValidation.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
            return report;
        }
        finally { if(close)EditorSceneManager.CloseScene(original,true);SceneManager.SetActiveScene(lab); }
    }
    static string Signature(VegetationSpawner spawner)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {
            foreach(var terrain in spawner.terrains)foreach(var tree in terrain.terrainData.treeInstances)
            {writer.Write(tree.position.x);writer.Write(tree.position.y);writer.Write(tree.position.z);writer.Write(tree.prototypeIndex);writer.Write(tree.widthScale);writer.Write(tree.heightScale);writer.Write(tree.rotation);}
            writer.Flush();using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(stream.ToArray()));
        }
    }
}

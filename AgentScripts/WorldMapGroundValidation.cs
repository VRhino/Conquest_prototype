using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using Conquest.WorldMap;
using Conquest.WorldMap.Editor;
using BronzeAge.RegionalMapTool;

public static class WorldMapGroundValidation
{
    public static object Run()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!=WorldMapLandscapeLab.ScenePath)throw new Exception("Open LandscapeLab");
        var preview=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<WorldMapPreviewController>(true)).First();
        var ts=preview.worldRoot.GetComponentsInChildren<Terrain>(true);
        int trees=0,pixels=0,borders=0;double maxSumError=0,maxBorderDifference=0,borderTotal=0;int borderSamples=0;
        var maps=ts.ToDictionary(t=>t,t=>t.terrainData.GetAlphamaps(0,0,t.terrainData.alphamapResolution,t.terrainData.alphamapResolution));
        foreach(var t in ts)
        {
            if(!AssetDatabase.GetAssetPath(t.terrainData).StartsWith(WorldMapLandscapeLab.Folder+"/"))throw new Exception("Shared terrain");
            if(t.terrainData.terrainLayers.Length!=8||t.terrainData.terrainLayers.Any(l=>!AssetDatabase.GetAssetPath(l.diffuseTexture).StartsWith("Assets/Yoge/")))throw new Exception("Old or missing textures");
            if(t.materialTemplate.shader.name!="BronzeAge/Regional Ground Lab" || ShaderUtil.ShaderHasError(t.materialTemplate.shader))throw new Exception("Shader error");
            var map=maps[t];int n=map.GetLength(0);trees+=t.terrainData.treeInstanceCount;
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {
                double sum=0;for(int i=0;i<8;i++){float w=map[z,x,i];if(float.IsNaN(w)||w<0||w>1)throw new Exception("Invalid weight");sum+=w;}
                maxSumError=Math.Max(maxSumError,Math.Abs(sum-1));pixels++;
            }
            foreach(var direction in new[]{Vector3.right,Vector3.forward})
            {
                var neighbor=ts.FirstOrDefault(o=>(o.transform.position-t.transform.position-direction*4000).sqrMagnitude<.01f);
                if(!neighbor)continue;borders++;var other=maps[neighbor];
                for(int k=0;k<n;k++)for(int i=0;i<8;i++)
                {
                    double d=direction.x>0?Math.Abs(map[k,n-1,i]-other[k,0,i]):Math.Abs(map[n-1,k,i]-other[0,k,i]);
                    maxBorderDifference=Math.Max(maxBorderDifference,d);borderTotal+=d;borderSamples++;
                }
            }
        }
        int resources=preview.worldRoot.GetComponentsInChildren<BronzeAgeRegionalMarker>(true).Length;
        if(ts.Length!=48||trees!=192974||resources!=1354||maxSumError>.025||maxBorderDifference>1e-5)throw new Exception("Preservation/normalization/seam mismatch");
        string report="Sectors="+ts.Length+"\nLayers=8\nPixels="+pixels+"\nTrees="+trees+"\nResources="+resources+
            "\nSerialized sum error="+maxSumError+"\nShared borders="+borders+"\nMaximum border layer difference="+maxBorderDifference+
            "\nMean border layer difference="+(borderTotal/Math.Max(1,borderSamples));
        File.WriteAllText(WorldMapLandscapeLab.Folder+"/RegionalGround/LiveValidation.txt",report);
        return report;
    }
}

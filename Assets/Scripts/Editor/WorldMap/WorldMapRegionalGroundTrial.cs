using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Conquest.WorldMap.Editor
{
    /// <summary>Visual authoring only. Global mask sampling avoids tile-shaped biome boundaries.</summary>
    public static class WorldMapRegionalGroundTrial
    {
        public const string ProfilePath = WorldMapLandscapeLab.Folder + "/RegionalGroundProfile.asset";
        const string LayerFolder = WorldMapLandscapeLab.Folder + "/RegionalGround";
        class Field { public byte[] bytes; public int n; }
        static readonly Vector2[] RegionOffsets = { Vector2.zero, Vector2.right, Vector2.left, Vector2.up, Vector2.down };

        public static WorldMapGroundTrialProfile Profile()
        {
            var p = AssetDatabase.LoadAssetAtPath<WorldMapGroundTrialProfile>(ProfilePath);
            if (!p) { p = ScriptableObject.CreateInstance<WorldMapGroundTrialProfile>(); p.SetRegionalDefaults(); AssetDatabase.CreateAsset(p, ProfilePath); }
            return p;
        }
        [MenuItem("Tools/BronzeAge/Pruebas/Editar suelos regionales")]
        public static void EditProfile() { Selection.activeObject = Profile(); }

        [MenuItem("Tools/BronzeAge/Pruebas/Actualizar texturas y tonos regionales")]
        public static void RefreshSurfaces() => RefreshSurfaces(SceneManager.GetActiveScene(), Profile(), WorldMapLandscapeLab.Folder, LayerFolder);

        public static void RefreshSurfaces(Scene scene, WorldMapGroundTrialProfile p, string terrainFolder, string layerFolder)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Sal de Play.");
            if(!scene.path.StartsWith(WorldMapLandscapeLab.Folder+"/")) throw new InvalidOperationException("Solo escenas experimentales.");
            var terrains=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Terrain>(true)).ToArray();
            if(terrains.Any(t=>!AssetDatabase.GetAssetPath(t.materialTemplate).StartsWith(terrainFolder+"/") || !AssetDatabase.GetAssetPath(t.terrainData).StartsWith(terrainFolder+"/") || t.terrainData.terrainLayers.Length!=8 || t.terrainData.terrainLayers.Any(l=>!AssetDatabase.GetAssetPath(l).StartsWith(layerFolder+"/"))))
                throw new InvalidOperationException("Capas o TerrainData no aislados.");
            if(p.surfaces.Any(s=>!s.color))throw new InvalidOperationException("Texturas incompletas.");
            var shader=Shader.Find("BronzeAge/Regional Ground Lab");
            if(!shader || ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Shader regional no disponible o con errores.");
            foreach(var t in terrains)
            {
                var layers=t.terrainData.terrainLayers;
                for(int i=0;i<8;i++)
                {
                    var s=p.surfaces[i];var l=layers[i];l.diffuseTexture=s.color;l.normalMapTexture=s.normal;
                    l.normalScale=s.normalStrength;l.tileSize=Vector2.one*s.tileMetres;
                    l.tileOffset=new Vector2(t.transform.position.x,t.transform.position.z);
                    l.diffuseRemapMin=Vector4.zero;l.diffuseRemapMax=new Vector4(s.tint.r,s.tint.g,s.tint.b,1);
                    EditorUtility.SetDirty(l);
                }
                t.materialTemplate.shader=shader;t.materialTemplate.SetVector("_RegionalTextureWarp",p.textureWarp);EditorUtility.SetDirty(t.materialTemplate);
                t.materialTemplate.SetVector("_RegionalMountainLook",p.mountainMassTreatment);
                t.materialTemplate.SetColor("_RegionalMountainColor",p.mountainMassColor);
                t.materialTemplate.SetVector("_RegionalMountainDetail",p.mountainDetail);
                t.materialTemplate.SetVector("_RegionalMountainDetailArea",p.mountainDetailArea);
                t.terrainData.SetBaseMapDirty();t.Flush();
            }
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("Tools/BronzeAge/Pruebas/Aplicar suelos regionales")]
        public static void Apply() => Apply(SceneManager.GetActiveScene(), Profile(), WorldMapLandscapeLab.Folder,
            LayerFolder, "Assets/BronzeAgeRegionalImports/Seed_42");

        public static void Apply(Scene scene, WorldMapGroundTrialProfile p, string terrainFolder, string layerFolder, string maskFolder)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Sal de Play antes de pintar.");
            if (!scene.path.StartsWith(WorldMapLandscapeLab.Folder+"/")) throw new InvalidOperationException("Solo escenas experimentales.");
            if (p.surfaces.Length != 8 || p.surfaces.Any(s => !s.color)) throw new InvalidOperationException("Asigna las ocho texturas antes de aplicar.");
            if (!AssetDatabase.IsValidFolder(layerFolder)) AssetDatabase.CreateFolder(Path.GetDirectoryName(layerFolder).Replace("\\", "/"), Path.GetFileName(layerFolder));
            var terrains = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Terrain>(true)).ToArray();
            if (terrains.Length != 48 || terrains.Any(t => !AssetDatabase.GetAssetPath(t.terrainData).StartsWith(terrainFolder + "/")))
                throw new InvalidOperationException("Se requieren los 48 TerrainData aislados del laboratorio.");
            var fields = new Dictionary<Vector2Int, Field>();
            var terrainByCell=terrains.ToDictionary(t=>new Vector2Int(Mathf.RoundToInt(t.transform.position.x/4000),Mathf.RoundToInt(t.transform.position.z/4000)));
            foreach (var t in terrains)
            {
                var bytes = File.ReadAllBytes(maskFolder + "/" + t.name + ".layers.bytes");
                int n = Mathf.RoundToInt(Mathf.Sqrt(bytes.Length / 7f));
                if (n * n * 7 != bytes.Length || n != 64) throw new InvalidDataException(t.name);
                fields.Add(new Vector2Int(Mathf.RoundToInt(t.transform.position.x / 4000), Mathf.RoundToInt(t.transform.position.z / 4000)), new Field { bytes = bytes, n = n });
            }
            var parameters = new Vector3[17]; parameters[0] = new Vector3(.5f,.2f,.1f);
            foreach(var r in p.regions) parameters[r.id] = new Vector3(r.aridity,r.sand,r.alluvial);
            var layers = new TerrainLayer[8];
            for (int i = 0; i < 8; i++)
            {
                string path = layerFolder + "/Surface" + i + ".terrainlayer";
                var s = p.surfaces[i]; var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                if (!l) { l = new TerrainLayer(); AssetDatabase.CreateAsset(l, path); }
                l.name = s.name; l.diffuseTexture = s.color; l.normalMapTexture = s.normal;
                l.normalScale = s.normalStrength; l.tileSize = Vector2.one * s.tileMetres;
                l.diffuseRemapMin = Vector4.zero; l.diffuseRemapMax = new Vector4(s.tint.r,s.tint.g,s.tint.b,1);
                l.metallic = 0; l.smoothness = .03f; layers[i] = l; EditorUtility.SetDirty(l);
            }
            int painted = 0, preservedTrees = 0, preservedHeights = 0; double maxError = 0; var regionWeights = new double[16,8]; var counts = new int[16];
            bool editing=true; AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var t in terrains)
                {
                    EditorUtility.DisplayProgressBar("Suelos regionales", t.name, painted / 48f);
                    var data = t.terrainData;
                    var heightsBefore = data.GetHeights(0,0,data.heightmapResolution,data.heightmapResolution);
                    var treesBefore = data.treeInstances;
                    // Per-tile layer offsets align texture phase in world metres, including non-divisor tiling sizes.
                    var tileLayers = new TerrainLayer[8];
                    for (int i = 0; i < 8; i++)
                    {
                        string path = layerFolder + "/" + t.name + "-" + i + ".terrainlayer";
                        var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                        if (!l) { l = UnityEngine.Object.Instantiate(layers[i]); AssetDatabase.CreateAsset(l,path); }
                        EditorUtility.CopySerialized(layers[i], l);
                        l.tileOffset = new Vector2(t.transform.position.x,t.transform.position.z);
                        tileLayers[i] = l; EditorUtility.SetDirty(l);
                    }
                    data.terrainLayers = tileLayers; data.alphamapResolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(p.alphamapResolution,64,512));
                    int n = data.alphamapResolution; var alpha = new float[n,n,8];
                    for (int z = 0; z < n; z++) for (int x = 0; x < n; x++)
                    {
                        float u = (float)x/(n-1), v = (float)z/(n-1);
                        var world = new Vector2(t.transform.position.x+u*data.size.x,t.transform.position.z+v*data.size.z);
                        float forest = Sample(fields,world,0), fertility = Sample(fields,world,1), pasture = Sample(fields,world,2);
                        var regional = Vector3.zero;
                        foreach(var offset in RegionOffsets) regional += Parameters(fields,world+offset*p.regionTransitionMetres,parameters);
                        regional /= RegionOffsets.Length;
                        float broad = Mathf.PerlinNoise(world.x/p.broadPatchMetres+p.seed,world.y/p.broadPatchMetres+31);
                        float medium = Mathf.PerlinNoise(world.x/p.mediumPatchMetres+17,world.y/p.mediumPatchMetres+p.seed);
                        float dry = Mathf.Clamp01(regional.x + (broad-.5f)*p.patchVariation*2 - (fertility-.5f)*.35f);
                        float slope = SharedSlope(terrainByCell,world);
                        float rock = Mathf.SmoothStep(0,1,Mathf.InverseLerp(p.rockStartSlope,p.rockFullSlope,slope));
                        float woodland = Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.8f,forest))*(1-rock*.65f);
                        float open = (1-rock)*(1-woodland);
                        float sand = regional.y*dry*(.65f+.35f*broad);
                        float fertile = regional.z*fertility*(1-dry)*(.45f+.55f*medium);
                        float gravel = dry*(1-fertility)*(.1f+.35f*medium);
                        float grass = Mathf.Clamp01((1-dry)*(.55f+.45f*fertility)+(broad-.5f)*p.patchVariation*2);
                        float[] w = { open*grass*(1-fertile*.65f), open*(1-grass)*(1-sand)*(1-fertile), rock,
                            woodland, open*fertile*1.2f, open*sand, open*gravel, open*pasture*dry*(.12f+p.dryPastureStrength*medium) };
                        float total = w.Sum();
                        int region = RegionId(fields,world)-1;
                        float sum = 0;
                        for(int i=0;i<8;i++) { alpha[z,x,i]=w[i]/Mathf.Max(total,.00001f); sum+=alpha[z,x,i]; if(region>=0&&region<16)regionWeights[region,i]+=alpha[z,x,i]; }
                        if(region>=0&&region<16)counts[region]++;
                        maxError = Math.Max(maxError, Math.Abs(sum-1));
                    }
                    data.SetAlphamaps(0,0,alpha); data.SetBaseMapDirty();
                    if(!heightsBefore.Cast<float>().SequenceEqual(data.GetHeights(0,0,data.heightmapResolution,data.heightmapResolution).Cast<float>()) || !treesBefore.SequenceEqual(data.treeInstances))
                        throw new InvalidOperationException("La prueba ha alterado alturas o árboles: "+t.name);
                    preservedHeights += heightsBefore.Length; preservedTrees += treesBefore.Length;
                    EditorUtility.SetDirty(data); t.Flush(); painted++;
                }
                AssetDatabase.StopAssetEditing(); editing=false;
                RefreshSurfaces(scene,p,terrainFolder,layerFolder);
                var lines = new List<string>{ "Región,muestras,"+string.Join(",",p.surfaces.Select(s=>s.name)) };
                for(int r=0;r<16;r++)lines.Add((r+1)+","+counts[r]+","+string.Join(",",Enumerable.Range(0,8).Select(i=>(regionWeights[r,i]/Math.Max(1,counts[r])).ToString("F4",System.Globalization.CultureInfo.InvariantCulture))));
                File.WriteAllLines(layerFolder+"/RegionalCoverage.csv",lines);
                File.WriteAllText(layerFolder+"/Validation.txt", "Sectores="+painted+"\nCapas=8\nAlturas preservadas="+preservedHeights+"\nÁrboles preservados="+preservedTrees+"\nError de normalización="+maxError);
                Debug.Log("REGIONAL_GROUND_READY: "+painted+" sectores, 8 superficies, error de suma "+maxError);
            }
            finally { if(editing) AssetDatabase.StopAssetEditing(); EditorUtility.ClearProgressBar(); }
        }
        static byte Cell(Dictionary<Vector2Int,Field> fields,int gx,int gz,int c)
        {
            gx=Mathf.Clamp(gx,0,511); gz=Mathf.Clamp(gz,0,383);
            var f=fields[new Vector2Int(gx/64,gz/64)]; return f.bytes[((gz%64)*64+gx%64)*7+c];
        }
        static float Sample(Dictionary<Vector2Int,Field> fields,Vector2 world,int c)
        {
            var q=world/62.5f-Vector2.one*.5f; int x=Mathf.FloorToInt(q.x),z=Mathf.FloorToInt(q.y);
            return Mathf.Lerp(Mathf.Lerp(Cell(fields,x,z,c),Cell(fields,x+1,z,c),q.x-x),Mathf.Lerp(Cell(fields,x,z+1,c),Cell(fields,x+1,z+1,c),q.x-x),q.y-z)/255f;
        }
        static int RegionId(Dictionary<Vector2Int,Field> fields,Vector2 world) => Cell(fields,Mathf.FloorToInt(world.x/62.5f),Mathf.FloorToInt(world.y/62.5f),4);
        static float SharedSlope(Dictionary<Vector2Int,Terrain> terrains,Vector2 world)
        {
            // Native Terrain gradients differ at their last samples. Both sides of an edge must
            // evaluate the same neighbors, including all four terrains at a shared corner.
            int left=Mathf.FloorToInt((world.x-.25f)/4000),right=Mathf.FloorToInt((world.x+.25f)/4000);
            int bottom=Mathf.FloorToInt((world.y-.25f)/4000),top=Mathf.FloorToInt((world.y+.25f)/4000);
            float sum=0;int count=0;
            for(int z=bottom;z<=top;z++)for(int x=left;x<=right;x++)
                if(terrains.TryGetValue(new Vector2Int(x,z),out var t))
                {
                    sum+=t.terrainData.GetSteepness(Mathf.Clamp01((world.x-t.transform.position.x)/4000),Mathf.Clamp01((world.y-t.transform.position.z)/4000));count++;
                }
            return count==0?0:sum/count;
        }
        static Vector3 Parameters(Dictionary<Vector2Int,Field> fields,Vector2 world,Vector3[] parameters)
        {
            // Interpolate parameters, never interpolate categorical region IDs.
            var q=world/62.5f-Vector2.one*.5f; int x=Mathf.FloorToInt(q.x),z=Mathf.FloorToInt(q.y);
            Vector3 Value(int gx,int gz) => parameters[Cell(fields,gx,gz,4)];
            return Vector3.Lerp(Vector3.Lerp(Value(x,z),Value(x+1,z),q.x-x),Vector3.Lerp(Value(x,z+1),Value(x+1,z+1),q.x-x),q.y-z);
        }
    }
}

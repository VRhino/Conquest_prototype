using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Conquest.WorldMap.Editor
{
    // A presentation bake only: original heights, resource IDs and simulation geometry are untouched.
    public static class WorldCampaignArt
    {
        const string Folder = "Assets/WorldMap/CampaignArt";

        const int TextureSize = 256;

        const string Ground = "Assets/Synty/PolygonFantasyKingdom/Textures/Ground/";

        public static WorldMapArtSettings Settings()
        {
            string path=Folder+"/EasternMediterranean.asset";
            var profile=AssetDatabase.LoadAssetAtPath<WorldMapArtSettings>(path);
            if(!profile){profile=ScriptableObject.CreateInstance<WorldMapArtSettings>();AssetDatabase.CreateAsset(profile,path);}
            return profile;
        }

        [MenuItem("Tools/BronzeAge/Aplicar aspecto de campaña a la preview")]
        public static void ApplyCurrent()
        {
            if (Application.isPlaying) return;
            var preview = UnityEngine.Object.FindFirstObjectByType<WorldMapPreviewController>();
            if (!preview) { Debug.LogError("Abre primero la preview del mundo."); return; }
            Apply(preview);
            EditorSceneManager.MarkSceneDirty(preview.gameObject.scene);
        }

        public static void Apply(WorldMapPreviewController preview,string waterFolder=null)
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/WorldMap", "CampaignArt");
            var profile=preview.artProfile?preview.artProfile:Settings();
            preview.artProfile=profile;
            var terrains = preview.worldRoot.GetComponentsInChildren<Terrain>();
            var mountainSettings=WorldMapSyntyBaker.Settings();
            // Four layers stay in a single terrain splat pass; riverside soils use these same surfaces.
            var layers = new TerrainLayer[4];
            Color[] colors = {profile.fertileGround,profile.dryGround,profile.limestone,profile.forestFloor};
            for (int i=0;i<layers.Length;i++) {
                string[] diffuse={"PFK_Texture_Ground_Grass_03.png","PFK_Texture_Ground_Sand_03.png","","PFK_Texture_Ground_Grass_01_Dark.png"};
                string[] normals={"PFK_Texture_Ground_Grass_03_Normals.png","PFK_Texture_Ground_Sand_03_Normals.png","PFK_Texture_Ground_Base_Normals.png","PFK_Texture_Ground_Grass_02_Normal.png"};
                var texture = string.IsNullOrEmpty(diffuse[i])?null:AssetDatabase.LoadAssetAtPath<Texture2D>(Ground+diffuse[i]);
                texture=texture?PaletteTexture(texture,i,colors[i],profile.textureContrast):SurfaceTexture(i,colors[i]);
                string path=Folder+"/Surface"+i+".terrainlayer";
                layers[i]=AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                if (!layers[i]) { layers[i]=new TerrainLayer(); AssetDatabase.CreateAsset(layers[i],path); }
                layers[i].diffuseTexture=texture;
                layers[i].normalMapTexture=i==2?RockNormals():AssetDatabase.LoadAssetAtPath<Texture2D>(Ground+normals[i]);
                layers[i].normalScale=i==2?.15f:.3f;
                layers[i].diffuseRemapMax=Vector4.one;
                float textureSize=i==2?profile.rockTextureSize:profile.groundTextureSize;
                layers[i].tileSize=new Vector2(textureSize,textureSize);
                layers[i].smoothness=0; layers[i].metallic=0;
                EditorUtility.SetDirty(layers[i]);
            }
            foreach(var terrain in terrains) {
                var blend=SurfaceBlend(terrain,mountainSettings);
                terrain.terrainData.terrainLayers=layers;
                terrain.terrainData.SetAlphamaps(0,0,blend);
                terrain.heightmapPixelError=1;
                terrain.drawInstanced=true;
                terrain.basemapDistance=profile.terrainSurfaceDistance;
                EditorUtility.SetDirty(terrain.terrainData); EditorUtility.SetDirty(terrain);
            }
            Shader.SetGlobalVector("_BronzeAgeTerrainLook",profile.TerrainLook);
            BakeWater(preview,terrains,profile,waterFolder??Folder);
            BakeRivers(preview,terrains,waterFolder??Folder);
            WorldMapSyntyBaker.Apply(preview,terrains);
            var oldFaces=preview.worldRoot.Find("Campaign Mountain Faces");
            if(oldFaces)UnityEngine.Object.DestroyImmediate(oldFaces.gameObject);
            var mountainShader=Shader.Find("BronzeAge/Campaign Terrain");
            if(mountainShader)foreach(var terrain in terrains) {
                var material=terrain.materialTemplate;
                if(material){material.shader=mountainShader;EditorUtility.SetDirty(material);}
            }
            foreach(var marker in preview.worldRoot.GetComponentsInChildren<BronzeAge.RegionalMapTool.BronzeAgeRegionalMarker>(true)) {
                var renderer=marker.GetComponent<Renderer>();if(renderer)renderer.enabled=false;
            }
            foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) {
                if(light.gameObject.scene!=preview.gameObject.scene || light.type!=LightType.Directional) continue;
                light.transform.rotation=Quaternion.Euler(profile.sunRotation);
                light.color=profile.sunlight; light.intensity=profile.sunIntensity; light.shadows=LightShadows.Soft;
            }
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=profile.skyLight;
            RenderSettings.ambientEquatorColor=profile.horizonLight;
            RenderSettings.ambientGroundColor=profile.groundLight;
            RenderSettings.fog=false;
            preview.mapCamera.backgroundColor=new Color(.10f,.17f,.20f);
            preview.FocusCampaign();
            EditorUtility.SetDirty(preview);
            AssetDatabase.SaveAssets();
        }

        static Texture2D PaletteTexture(Texture2D source,int kind,Color color,float contrast)
        {
            // Derive campaign copies without enabling Read/Write or changing the licensed Synty source.
            string path=Folder+"/PaletteSurface"+kind+".asset";
            var result=AssetDatabase.LoadAssetAtPath<Texture2D>(path);bool created=!result;
            if(created)result=new Texture2D(TextureSize,TextureSize,TextureFormat.RGB24,true);
            var previous=RenderTexture.active;
            var temporary=RenderTexture.GetTemporary(TextureSize,TextureSize,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var copy=new Texture2D(TextureSize,TextureSize,TextureFormat.RGB24,false);
            try {
                Graphics.Blit(source,temporary);RenderTexture.active=temporary;
                copy.ReadPixels(new Rect(0,0,TextureSize,TextureSize),0,0);copy.Apply();
                var pixels=copy.GetPixels();float average=0;
                foreach(var pixel in pixels)average+=pixel.grayscale;
                average=Mathf.Max(.01f,average/pixels.Length);
                for(int i=0;i<pixels.Length;i++)pixels[i]=color*Mathf.Clamp(1+(pixels[i].grayscale/average-1)*contrast,.7f,1.3f);
                result.SetPixels(pixels);result.Apply();result.wrapMode=TextureWrapMode.Repeat;result.anisoLevel=8;
                if(created)AssetDatabase.CreateAsset(result,path);else EditorUtility.SetDirty(result);
                return result;
            } finally {RenderTexture.active=previous;RenderTexture.ReleaseTemporary(temporary);UnityEngine.Object.DestroyImmediate(copy);}
        }

        static Texture2D SurfaceTexture(int kind,Color baseColor)
        {
            string path=Folder+"/Surface"+kind+".asset";
            var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            bool created=!tex;
            if(created) tex=new Texture2D(TextureSize,TextureSize,TextureFormat.RGB24,true);
            var pixels=new Color[TextureSize*TextureSize];
            // Seamless noise: broad mineral stains and fine grain, without a regular wave pattern.
            for(int y=0;y<TextureSize;y++) for(int x=0;x<TextureSize;x++) {
                float u=x/(float)TextureSize,v=y/(float)TextureSize;
                float broad=TileNoise(u,v,4,kind*11),grain=TileNoise(u,v,57,kind*23);
                float cracks=kind==2?Mathf.Pow(1-Mathf.Abs(TileNoise(u,v,12,37)*2-1),12):0;
                float value=kind==2?.82f+broad*.32f+(grain-.5f)*.14f-cracks*.08f:.85f+broad*.28f+(grain-.5f)*.13f;
                pixels[y*TextureSize+x]=baseColor*value;
            }
            tex.SetPixels(pixels); tex.Apply(); tex.wrapMode=TextureWrapMode.Repeat; tex.anisoLevel=8;
            if(created) AssetDatabase.CreateAsset(tex,path);else EditorUtility.SetDirty(tex);return tex;
        }

        static float TileNoise(float x,float y,float scale,float offset)
        {
            return Mathf.Lerp(Mathf.Lerp(Mathf.PerlinNoise(x*scale+offset,y*scale+offset),Mathf.PerlinNoise((x-1)*scale+offset,y*scale+offset),x),
                Mathf.Lerp(Mathf.PerlinNoise(x*scale+offset,(y-1)*scale+offset),Mathf.PerlinNoise((x-1)*scale+offset,(y-1)*scale+offset),x),y);
        }

        static Texture2D RockNormals()
        {
            string path=Folder+"/RockNormals.asset";
            var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);bool created=!tex;
            if(created)tex=new Texture2D(TextureSize,TextureSize,TextureFormat.RGBA32,true,true);
            var pixels=new Color[TextureSize*TextureSize];
            for(int y=0;y<TextureSize;y++)for(int x=0;x<TextureSize;x++) {
                float u=x/(float)TextureSize,v=y/(float)TextureSize,step=1f/TextureSize;
                float dx=RockRelief((u+step)%1,v)-RockRelief((u-step+1)%1,v);
                float dy=RockRelief(u,(v+step)%1)-RockRelief(u,(v-step+1)%1);
                var normal=new Vector3(-dx*2,-dy*2,1).normalized;
                // Packed AG normal for the desktop URP normal-map path.
                pixels[y*TextureSize+x]=new Color(1,normal.y*.5f+.5f,normal.z*.5f+.5f,normal.x*.5f+.5f);
            }
            tex.SetPixels(pixels);tex.Apply();tex.wrapMode=TextureWrapMode.Repeat;tex.anisoLevel=8;
            if(created)AssetDatabase.CreateAsset(tex,path);else EditorUtility.SetDirty(tex);return tex;
        }

        static float RockRelief(float u,float v)
        {
            float grain=TileNoise(u,v,18,31),mass=TileNoise(u,v,5,9);
            return mass*.65f+grain*.35f;
        }

        static float[,,] SurfaceBlend(Terrain terrain,WorldMapSyntySettings settings)
        {
            var data=terrain.terrainData;
            string path=Path.Combine(Path.GetDirectoryName(AssetDatabase.GetAssetPath(data)),terrain.name+".layers.bytes");
            if(!File.Exists(path)) throw new FileNotFoundException("Missing canonical geographic fields",path);
            var raw=File.ReadAllBytes(path);int n=data.alphamapWidth;
            if(raw.Length!=n*n*7) throw new InvalidDataException("Surface field size mismatch: "+path);
            var blend=new float[n,n,4];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++) {
                int p=(y*n+x)*7;float u=(x+.5f)/n,v=(y+.5f)/n;
                float slope=data.GetSteepness(u,v);
                float rock=Mathf.SmoothStep(0,1,Mathf.InverseLerp(settings.rockSurfaceStartSlope,settings.rockSurfaceFullSlope,slope));
                float forest=raw[p]/255f*(1-rock*.85f),fertility=raw[p+1]/255f,soil=(1-forest)*(1-rock);
                blend[y,x,0]=soil*fertility;blend[y,x,1]=soil*(1-fertility);blend[y,x,2]=(1-forest)*rock;blend[y,x,3]=forest;
            }
            return blend;
        }

        static void BakeWater(WorldMapPreviewController preview,Terrain[] terrains,WorldMapArtSettings profile,string folder)
        {
            const int width=512,height=384;
            var tex=new Texture2D(width,height,TextureFormat.RFloat,false,true);
            var pixels=new Color[width*height];
            for(int y=0;y<height;y++) for(int x=0;x<width;x++) {
                var p=new Vector3((x+.5f)/width*preview.worldSize.x,0,(y+.5f)/height*preview.worldSize.y);
                float h=0;
                foreach(var t in terrains) { var o=t.transform.position;var s=t.terrainData.size;
                    if(p.x>=o.x && p.x<=o.x+s.x && p.z>=o.z && p.z<=o.z+s.z) { h=t.SampleHeight(p)+o.y;break; }
                }
                pixels[y*width+x]=new Color(h/1000,0,0,1);
            }
            tex.SetPixels(pixels);tex.Apply();tex.wrapMode=TextureWrapMode.Clamp;
            string path=folder+"/GeographicHeight.asset";
            var old=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(old) { EditorUtility.CopySerialized(tex,old); UnityEngine.Object.DestroyImmediate(tex); tex=old; } else AssetDatabase.CreateAsset(tex,path);
            var shader=Shader.Find("BronzeAge/Campaign Water");
            if(!shader) throw new InvalidOperationException("Campaign Water shader missing");
            foreach(var renderer in preview.worldRoot.GetComponentsInChildren<MeshRenderer>()) {
                if(renderer.GetComponent<BronzeAge.RegionalMapTool.BronzeAgeRegionalMarker>()) continue;
                // Only the importer's sea and lake meshes use the Water material.
                if(!renderer.sharedMaterial || (renderer.sharedMaterial.name!="Water" && !renderer.sharedMaterial.name.StartsWith("Water_"))) continue;
                string matPath=folder+"/Water_"+renderer.name+".mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if(!mat) {mat=new Material(shader);AssetDatabase.CreateAsset(mat,matPath);}
                mat.shader=shader;
                mat.SetTexture("_DepthMap",tex);mat.SetVector("_WorldSize",new Vector4(preview.worldSize.x,preview.worldSize.y,0,0));
                mat.SetFloat("_WaterLevel",renderer.bounds.center.y);
                mat.SetFloat("_Coastal",renderer.name=="Mar"?1:0);
                ConfigureWaterMaterial(mat,profile,renderer.name=="Mar"?profile.seaDeep:profile.lakeDeep,
                    renderer.name=="Mar"?profile.seaShallow:profile.lakeShallow);
                renderer.sharedMaterial=mat;EditorUtility.SetDirty(mat);EditorUtility.SetDirty(renderer);
            }
            foreach(var river in preview.worldRoot.GetComponentsInChildren<LineRenderer>()) {
                river.startColor=new Color(.30f,.53f,.51f);river.endColor=river.startColor;
                river.numCornerVertices=3;river.numCapVertices=3;
            }
        }

        public static void ConfigureWaterMaterial(Material material,WorldMapArtSettings profile,Color deep,Color shallow)
        {
            material.SetColor("_DeepColor",deep);material.SetColor("_ShallowColor",shallow);
            material.SetFloat("_Highlights",profile.waterHighlights);
            material.SetFloat("_WaveSize",profile.waterWaveSize);
            material.SetFloat("_ShoreDepth",profile.coastalDepth);
            EditorUtility.SetDirty(material);
        }

        static void BakeRivers(WorldMapPreviewController preview,Terrain[] terrains,string folder)
        {
            WorldCampaignWater.Apply(preview,terrains,folder);
        }

        public static void BatchRender()
        {
            try {
                WorldMapPreviewBuilder.Build(Environment.GetEnvironmentVariable("BRONZEAGE_PREVIEW_MANIFEST"),false);
                var preview=UnityEngine.Object.FindFirstObjectByType<WorldMapPreviewController>();
                Apply(preview);preview.FocusCampaign();
                var forest=preview.worldRoot.GetComponent<WorldMapSyntyRenderer>();
                if(!forest || !forest.instances || forest.instances.trees==0)throw new Exception("Synty forest representation is empty");
                if(preview.worldRoot.GetComponentsInChildren<Terrain>().Length!=48)throw new Exception("Terrain count changed");
                if(preview.worldRoot.GetComponentsInChildren<BronzeAge.RegionalMapTool.BronzeAgeRegionalMarker>(true).Length!=1354)throw new Exception("Resource identities changed");
                Capture(preview.mapCamera,"campaign-detail.png");
                preview.FitWorld();Capture(preview.mapCamera,"campaign-world.png");
                Debug.Log("CAMPAIGN_ART_VALIDATED");EditorApplication.Exit(0);
            }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        static void Capture(Camera camera,string name)
        {
            var rt=new RenderTexture(1600,1000,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();
            File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName,name),image.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}



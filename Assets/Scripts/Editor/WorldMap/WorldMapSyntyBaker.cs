using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Conquest.WorldMap.Editor
{
    public static class WorldMapSyntyBaker
    {
        public const int Revision=8;
        const string Folder="Assets/WorldMap/CampaignArt";
        const string Env="Assets/Synty/PolygonFantasyKingdom/Prefabs/Environments/";
        const string Generic="Assets/Synty/PolygonFantasyKingdom/Prefabs/Generic/";

        [MenuItem("Tools/BronzeAge/Reconstruir modelos Synty del mundo")]
        public static void Rebuild()
        {
            if(Application.isPlaying)return;
            var p=UnityEngine.Object.FindFirstObjectByType<WorldMapPreviewController>();
            if(!p)throw new InvalidOperationException("Abre la preview del mundo");
            Apply(p,p.worldRoot.GetComponentsInChildren<Terrain>(),true);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(p.gameObject.scene);
        }

        public static void Apply(WorldMapPreviewController preview,Terrain[] terrains,bool force=false,WorldMapSyntySettings overrideSettings=null)
        {
            var settings=overrideSettings?overrideSettings:Settings();
            string path=Folder+"/Synty_"+preview.worldRoot.name+".asset";
            var data=AssetDatabase.LoadAssetAtPath<WorldMapSyntyInstances>(path);
            if(!data || data.revision!=Revision || force) {
                var next=Bake(settings,terrains,preview.worldRoot.GetComponentsInChildren<BronzeAge.RegionalMapTool.BronzeAgeRegionalMarker>(true),preview.worldRoot.GetComponentsInChildren<LineRenderer>());
                if(data){EditorUtility.CopySerialized(next,data);UnityEngine.Object.DestroyImmediate(next);}else{data=next;AssetDatabase.CreateAsset(data,path);}
                EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();
            }
            var renderer=preview.worldRoot.GetComponent<WorldMapSyntyRenderer>();
            if(!renderer)renderer=preview.worldRoot.gameObject.AddComponent<WorldMapSyntyRenderer>();
            renderer.instances=data;renderer.viewCamera=preview.mapCamera;renderer.artProfile=preview.artProfile;EditorUtility.SetDirty(renderer);
            var old=preview.worldRoot.Find("Campaign Forests");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
            Debug.Log($"SYNTY_CAMPAIGN: trees={data.trees}, rocks={data.rocks}, cliffs={data.cliffs}, batches={data.batches.Length}, detailTriangles={data.detailTriangles}, distantTriangles={data.distantTriangles}");
        }

        public static WorldMapSyntySettings Settings()
        {
            string path=Folder+"/SyntySettings.asset";
            var settings=AssetDatabase.LoadAssetAtPath<WorldMapSyntySettings>(path);
            if(settings)return settings;
            settings=ScriptableObject.CreateInstance<WorldMapSyntySettings>();
            settings.broadleaf=Load(Env,"SM_Env_Tree_Round_01","SM_Env_Tree_Round_02","SM_Env_Tree_Round_03");
            settings.riverside=Load(Env,"SM_Env_Tree_Thin_01","SM_Env_Tree_Thin_02");
            settings.mountain=Load(Env,"SM_Env_Tree_Pine_Cluster_01","SM_Env_Tree_Pine_Cluster_02");
            settings.distant=Load(Generic,"SM_Generic_Tree_01","SM_Generic_Tree_02");
            settings.rocks=Load(Env,"SM_Env_Rock_01","SM_Env_Rock_02","SM_Env_Rock_Chunk_01");
            settings.cliffs=Load(Env,"SM_Env_Rock_Cliff_01","SM_Env_Rock_Cliff_02");
            AssetDatabase.CreateAsset(settings,path);return settings;
        }
        static GameObject[] Load(string folder,params string[] names)
        {
            var prefabs=new GameObject[names.Length];
            for(int i=0;i<names.Length;i++){prefabs[i]=AssetDatabase.LoadAssetAtPath<GameObject>(folder+names[i]+".prefab");if(!prefabs[i])throw new FileNotFoundException(folder+names[i]);}
            return prefabs;
        }

        public sealed class Model
        {
            public readonly List<Part> parts=new List<Part>();
            public Bounds bounds;
        }
        public sealed class Part { public Mesh mesh;public Material material;public int submesh;public Matrix4x4 transform; }
        sealed class Bucket {public Part part;public bool distant,always;public readonly List<Matrix4x4> matrices=new List<Matrix4x4>();}

        static WorldMapSyntyInstances Bake(WorldMapSyntySettings settings,Terrain[] terrains,BronzeAge.RegionalMapTool.BronzeAgeRegionalMarker[] resources,LineRenderer[] rivers)
        {
            var data=ScriptableObject.CreateInstance<WorldMapSyntyInstances>();data.revision=Revision;data.settings=settings;
            var models=new Dictionary<GameObject,Model>();var materials=new Dictionary<Material,Material>();
            var batches=new List<WorldMapInstanceBatch>();
            var overviewBatches=new List<WorldMapInstanceBatch>();
            var waterSettings=WorldCampaignWater.Settings();var riverSegments=new List<(Vector2 a,Vector2 b,float radius)>();
            foreach(var river in rivers)for(int i=1;i<river.positionCount;i++) {
                var a=river.GetPosition(i-1);var b=river.GetPosition(i);
                if(!river.useWorldSpace){a=river.transform.TransformPoint(a);b=river.transform.TransformPoint(b);}
                float radius=Mathf.Max(waterSettings.minimumRiverWidth,river.widthMultiplier*waterSettings.riverWidthScale)*.5f+waterSettings.treeRiverClearance;
                riverSegments.Add((new Vector2(a.x,a.z),new Vector2(b.x,b.z),radius));
            }
            foreach(var terrain in terrains) {
                var buckets=new Dictionary<(Mesh,Material,int,bool,bool,int,int),Bucket>();var td=terrain.terrainData;var size=td.size;var origin=terrain.transform.position;
                var localRivers=riverSegments.FindAll(s=>Mathf.Max(s.a.x,s.b.x)+s.radius>=origin.x&&Mathf.Min(s.a.x,s.b.x)-s.radius<=origin.x+size.x&&Mathf.Max(s.a.y,s.b.y)+s.radius>=origin.z&&Mathf.Min(s.a.y,s.b.y)-s.radius<=origin.z+size.z);
                string fields=Path.Combine(Path.GetDirectoryName(AssetDatabase.GetAssetPath(td)),terrain.name+".layers.bytes");
                if(!File.Exists(fields))throw new FileNotFoundException("Missing canonical geographic fields",fields);
                var raw=File.ReadAllBytes(fields);int n=Mathf.RoundToInt(Mathf.Sqrt(raw.Length/7f));
                if(n*n*7!=raw.Length)throw new InvalidDataException(fields);
                var random=new System.Random(42+(int)origin.x*17+(int)origin.z);int cells=Mathf.FloorToInt(size.x/settings.spacing);
                for(int z=0;z<cells;z++)for(int x=0;x<cells;x++) {
                    float nx=(x+.1f+(float)random.NextDouble()*.8f)/cells,nz=(z+.1f+(float)random.NextDouble()*.8f)/cells;
                    int offset=(Mathf.Min(n-1,(int)(nz*n))*n+Mathf.Min(n-1,(int)(nx*n)))*7;
                    float density=raw[offset]/255f;int forestType=raw[offset+6];
                    float height=td.GetInterpolatedHeight(nx,nz)+origin.y,slope=td.GetSteepness(nx,nz);
                    if(height<settings.seaElevation+4 || raw[offset+3]==255)continue;
                    var position=new Vector3(origin.x+nx*size.x,height,origin.z+nz*size.z);
                    if(density>=.18f && slope<settings.maximumForestSlope && random.NextDouble()<density*.9f) {
                        int cluster=Mathf.Max(1,density>.5f?settings.denseClusterTrees:settings.openClusterTrees);
                        for(int k=0;k<cluster;k++) {
                            var p=position+new Vector3(((float)random.NextDouble()-.5f)*settings.clusterSpread,0,((float)random.NextDouble()-.5f)*settings.clusterSpread);
                            float u=(p.x-origin.x)/size.x,v=(p.z-origin.z)/size.z;
                            if(u<0||v<0||u>1||v>1||td.GetSteepness(u,v)>settings.maximumForestSlope)continue;
                            int f=(Mathf.Min(n-1,(int)(v*n))*n+Mathf.Min(n-1,(int)(u*n)))*7;
                            p.y=td.GetInterpolatedHeight(u,v)+origin.y;if(p.y<settings.seaElevation+4||raw[f+3]==255||raw[f]<46)continue;
                            if(InRiver(p,localRivers))continue;
                            // Read the family at the actual tree position, including at biome boundaries.
                            var localForestType=raw[f+6];
                            var family=localForestType==4?settings.mountain:localForestType==3?settings.riverside:settings.broadleaf;
                            float targetHeight=settings.treeHeight*(.8f+(float)random.NextDouble()*.4f),yaw=(float)random.NextDouble()*360;
                            Place(family[random.Next(family.Length)],p,targetHeight,yaw,false,false);
                            if(data.trees%settings.distantStride==0)Place(settings.distant[random.Next(settings.distant.Length)],p,targetHeight,yaw,true,false);
                            data.trees++;
                        }
                    }
                    bool cliff=slope>=settings.cliffSlope;
                    if(slope>8 && random.NextDouble()<(cliff?settings.cliffProbability:settings.rockProbability)) {
                        var family=cliff?settings.cliffs:settings.rocks;
                        var normal=td.GetInterpolatedNormal(nx,nz);
                        float yaw=cliff?Mathf.Atan2(normal.x,normal.z)*Mathf.Rad2Deg+((float)random.NextDouble()-.5f)*25:(float)random.NextDouble()*360;
                        float target=(cliff?settings.cliffHeight:settings.rockSize)*(.8f+(float)random.NextDouble()*.5f);
                        if(cliff)position.y-=target*settings.cliffEmbed;
                        Place(family[random.Next(family.Length)],position,target,yaw,false,true,!cliff);
                        if(cliff)data.cliffs++;else data.rocks++;
                    }
                }
                foreach(var resource in resources) {
                    var p=resource.canonicalPosition;
                    if(resource.resourceType!="piedra" || p.x<origin.x || p.z<origin.z || p.x>=origin.x+size.x || p.z>=origin.z+size.z)continue;
                    p.y=td.GetInterpolatedHeight((p.x-origin.x)/size.x,(p.z-origin.z)/size.z)+origin.y;
                    Place(settings.rocks[random.Next(settings.rocks.Length)],p,settings.rockSize*1.3f,(float)random.NextDouble()*360,false,true,true);
                    data.rocks++;
                }
                var overviewBuckets=new Dictionary<(Mesh,Material,int,bool),Bucket>();
                foreach(var bucket in buckets.Values) {
                    if(!bucket.distant && !bucket.always)continue;
                    var key=(bucket.part.mesh,bucket.part.material,bucket.part.submesh,bucket.always);
                    if(!overviewBuckets.TryGetValue(key,out var overviewBucket)) {
                        overviewBucket=new Bucket{part=bucket.part,distant=bucket.distant,always=bucket.always};
                        overviewBuckets.Add(key,overviewBucket);
                    }
                    overviewBucket.matrices.AddRange(bucket.matrices);
                }
                foreach(var bucket in buckets.Values)AddBatches(bucket,batches,true);
                foreach(var bucket in overviewBuckets.Values)AddBatches(bucket,overviewBatches,false);

                void AddBatches(Bucket bucket,List<WorldMapInstanceBatch> destination,bool countTriangles)
                {
                for(int start=0;start<bucket.matrices.Count;start+=1023) {
                    int count=Mathf.Min(1023,bucket.matrices.Count-start);var matrices=bucket.matrices.GetRange(start,count).ToArray();
                    var bounds=new Bounds();bool first=true;
                    foreach(var matrix in matrices){var b=TransformBounds(bucket.part.mesh.bounds,matrix);if(first){bounds=b;first=false;}else bounds.Encapsulate(b);}
                    destination.Add(new WorldMapInstanceBatch{mesh=bucket.part.mesh,material=bucket.part.material,submesh=bucket.part.submesh,matrices=matrices,bounds=bounds,distant=bucket.distant,always=bucket.always});
                    long triangles=(long)bucket.part.mesh.GetIndexCount(bucket.part.submesh)/3*count;
                    if(countTriangles){if(bucket.distant)data.distantTriangles+=triangles;else data.detailTriangles+=triangles;}
                }
                }

                void Place(GameObject prefab,Vector3 position,float height,float yaw,bool distant,bool always,bool diameter=false)
                {
                    if(!models.TryGetValue(prefab,out var model)) {model=ReadModel(prefab,materials);models.Add(prefab,model);}
                    float dimension=diameter?Mathf.Max(model.bounds.size.x,Mathf.Max(model.bounds.size.y,model.bounds.size.z)):model.bounds.size.y;
                    float scale=height/Mathf.Max(.01f,dimension);
                    var dimensions=always&&!diameter?new Vector3(settings.cliffWidth,1,settings.cliffDepth):Vector3.one;
                    if(always && !diameter && settings.anchorCliffsToFootSlope)
                    {
                        // Flat-bottomed cliff meshes must meet the downhill edge, not hang above it.
                        // Sample neighboring tiles as well; a cliff's footprint can straddle a border.
                        float Ground(Vector3 q) {
                            foreach(var candidate in terrains) {
                                var o=candidate.transform.position;var s=candidate.terrainData.size;
                                if(q.x>=o.x&&q.x<=o.x+s.x&&q.z>=o.z&&q.z<=o.z+s.z)return candidate.SampleHeight(q)+o.y;
                            }
                            return td.GetInterpolatedHeight(Mathf.Clamp01((q.x-origin.x)/size.x),Mathf.Clamp01((q.z-origin.z)/size.z))+origin.y;
                        }
                        float centerGround=Ground(position),minimumGround=centerGround;
                        var rotation=Quaternion.Euler(0,yaw,0);
                        var halfFootprint=Vector3.Scale(model.bounds.size,dimensions*scale)*.5f;
                        foreach(float sx in new[]{-1f,1f})foreach(float sz in new[]{-1f,1f})
                            minimumGround=Mathf.Min(minimumGround,Ground(position+rotation*new Vector3(halfFootprint.x*sx,0,halfFootprint.z*sz)));
                        position.y-=centerGround-minimumGround;
                    }
                    var baseMatrix=Matrix4x4.TRS(position,Quaternion.Euler(0,yaw,0),dimensions*scale)*Matrix4x4.Translate(new Vector3(-model.bounds.center.x,-model.bounds.min.y,-model.bounds.center.z));
                    foreach(var part in model.parts) {
                        // Local cells keep off-screen forests out of a visible terrain-wide batch.
                        int cellX=Mathf.FloorToInt((position.x-origin.x)/settings.cullingCellSize);
                        int cellZ=Mathf.FloorToInt((position.z-origin.z)/settings.cullingCellSize);
                        var key=(part.mesh,part.material,part.submesh,distant,always,cellX,cellZ);
                        if(!buckets.TryGetValue(key,out var bucket)){bucket=new Bucket{part=part,distant=distant,always=always};buckets.Add(key,bucket);}
                        bucket.matrices.Add(baseMatrix*part.transform);
                    }
                }
            }
            data.batches=batches.ToArray();data.overviewBatches=overviewBatches.ToArray();
            if(batches.Count>0){data.bounds=batches[0].bounds;foreach(var batch in batches)data.bounds.Encapsulate(batch.bounds);}
            return data;
        }

        static bool InRiver(Vector3 position,List<(Vector2 a,Vector2 b,float radius)> segments)
        {
            var p=new Vector2(position.x,position.z);
            foreach(var segment in segments) {
                var delta=segment.b-segment.a;
                float t=delta.sqrMagnitude>0?Mathf.Clamp01(Vector2.Dot(p-segment.a,delta)/delta.sqrMagnitude):0;
                if((p-(segment.a+delta*t)).sqrMagnitude<segment.radius*segment.radius)return true;
            }
            return false;
        }

        public static Model ReadModel(GameObject prefab,Dictionary<Material,Material> materials)
        {
            var model=new Model();bool first=true;
            var filters=prefab.GetComponentsInChildren<MeshFilter>(true);
            foreach(var filter in filters) {
                var renderer=filter.GetComponent<MeshRenderer>();var mesh=filter.sharedMesh;if(!renderer||!mesh)continue;
                var local=prefab.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                var bounds=TransformBounds(mesh.bounds,local);if(first){model.bounds=bounds;first=false;}else model.bounds.Encapsulate(bounds);
                for(int sub=0;sub<mesh.subMeshCount;sub++) {
                    var source=renderer.sharedMaterials[Mathf.Min(sub,renderer.sharedMaterials.Length-1)];
                    if(!source)throw new InvalidDataException("Missing original Synty material: "+prefab.name);
                    if(!materials.TryGetValue(source,out var material)) {
                        string materialPath=Folder+"/SyntyMaterial_"+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source))+".mat";
                        material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                        if(!material){material=new Material(source);AssetDatabase.CreateAsset(material,materialPath);}
                        material.enableInstancing=true;EditorUtility.SetDirty(material);materials.Add(source,material);
                    }
                    model.parts.Add(new Part{mesh=mesh,material=material,submesh=sub,transform=local});
                }
            }
            if(first)throw new InvalidDataException("No renderable mesh: "+prefab.name);
            Debug.Log($"SYNTY_MODEL {prefab.name}: height={model.bounds.size.y:F2}m, meshParts={model.parts.Count}");return model;
        }

        public static Bounds TransformBounds(Bounds b,Matrix4x4 m)
        {
            var bounds=new Bounds(m.MultiplyPoint3x4(b.center),Vector3.zero);
            for(int i=0;i<8;i++)bounds.Encapsulate(m.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
            return bounds;
        }
    }
}

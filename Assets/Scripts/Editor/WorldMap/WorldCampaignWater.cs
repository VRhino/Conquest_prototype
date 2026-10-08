using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Conquest.WorldMap.Editor
{
    public static class WorldCampaignWater
    {
        const string Folder="Assets/WorldMap/CampaignArt";
        public static WorldMapWaterSettings Settings()
        {
            string settingsPath=Folder+"/WaterSettings.asset";
            var settings=AssetDatabase.LoadAssetAtPath<WorldMapWaterSettings>(settingsPath);
            if(!settings){settings=ScriptableObject.CreateInstance<WorldMapWaterSettings>();AssetDatabase.CreateAsset(settings,settingsPath);}
            return settings;
        }
        public static void Apply(WorldMapPreviewController preview,Terrain[] terrains,string assetFolder=null)
        {
            var folder=assetFolder??Folder;
            var settings=Settings();
            var profile=preview.artProfile?preview.artProfile:WorldCampaignArt.Settings();
            var lakeMaterial=Material("LakeWater",profile,profile.lakeDeep,profile.lakeShallow,false,folder);
            var riverMaterial=Material("RiverWater",profile,profile.riverDeep,profile.riverShallow,true,folder);
            foreach(var filter in preview.worldRoot.GetComponentsInChildren<MeshFilter>()) {
                var renderer=filter.GetComponent<MeshRenderer>();
                if(!renderer || filter.name=="Mar" || !renderer.sharedMaterial || !renderer.sharedMaterial.name.StartsWith("Water_"))continue;
                var source=filter.GetComponent<WorldMapLakeSource>();
                if(!source){source=filter.gameObject.AddComponent<WorldMapLakeSource>();source.importedMesh=filter.sharedMesh;EditorUtility.SetDirty(source);}
            }
            // Lakes already using the inland material still regenerate from their retained source.
            foreach(var source in preview.worldRoot.GetComponentsInChildren<WorldMapLakeSource>())
                Lake(source.GetComponent<MeshFilter>(),source.importedMesh,settings,lakeMaterial,folder);
            var lakeAreas=new List<LakeArea>();
            foreach(var source in preview.worldRoot.GetComponentsInChildren<WorldMapLakeSource>()) {
                var mesh=source.GetComponent<MeshFilter>().sharedMesh;var all=mesh.vertices;int count=(all.Length-1)/4;
                var outline=new Vector3[count];for(int i=0;i<count;i++)outline[i]=source.transform.TransformPoint(all[i]);
                lakeAreas.Add(new LakeArea{outline=outline,bounds=source.GetComponent<MeshRenderer>().bounds});
            }
            float seaElevation=WorldMapSyntyBaker.Settings().seaElevation;
            var root=preview.worldRoot.Find("Campaign Rivers");
            if(root)Object.DestroyImmediate(root.gameObject);
            root=new GameObject("Campaign Rivers").transform;root.SetParent(preview.worldRoot,false);
            foreach(var line in preview.worldRoot.GetComponentsInChildren<LineRenderer>()) {
                if(line.positionCount<2)continue;
                var input=new Vector3[line.positionCount];
                for(int i=0;i<input.Length;i++)input[i]=line.useWorldSpace?line.GetPosition(i):line.transform.TransformPoint(line.GetPosition(i));
                float radius=Mathf.Max(settings.minimumRiverWidth,line.widthMultiplier*settings.riverWidthScale)*.5f;
                var points=new List<Vector3>();
                for(int i=0;i<input.Length-1;i++) {
                    var a=input[i];var b=input[i+1];int count=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z))/settings.riverStep));
                    for(int j=0;j<count;j++) {
                        float t=j/(float)count;
                        var straight=Vector3.Lerp(a,b,t);
                        var curved=Catmull(input[Mathf.Max(0,i-1)],a,b,input[Mathf.Min(input.Length-1,i+2)],t);
                        var shift=curved-straight;shift.y=0;
                        points.Add(straight+Vector3.ClampMagnitude(shift,radius*.3f));
                    }
                }
                points.Add(input[input.Length-1]);
                var vertices=new List<Vector3>();var colors=new List<Color>();var uv=new List<Vector2>();var indices=new List<int>();
                float distance=0;float[] across={-1,-.7f,0,.7f,1};
                for(int i=0;i<points.Count;i++) {
                    if(i>0)distance+=Vector3.Distance(points[i-1],points[i]);
                    var tangent=points[Mathf.Min(i+1,points.Count-1)]-points[Mathf.Max(0,i-1)];tangent.y=0;
                    var side=Vector3.Cross(Vector3.up,tangent.normalized)*radius;
                    foreach(float width in across) {
                        var p=points[i]+side*width;p.y=Ground(p,terrains)+settings.riverLift;
                        vertices.Add(p);colors.Add(new Color(1-Mathf.Abs(width),0,0,1));uv.Add(new Vector2(distance/80,width));
                    }
                    if(i==points.Count-1)continue;
                    var middle=(points[i]+points[i+1])*.5f;
                    if(Ground(middle,terrains)<seaElevation || InLake(middle,lakeAreas))continue;
                    for(int k=0;k<4;k++){int a=i*5+k,b=a+5;indices.Add(a);indices.Add(b);indices.Add(a+1);indices.Add(a+1);indices.Add(b);indices.Add(b+1);}
                }
                var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
                mesh=Save(mesh,folder+"/River_"+line.name+".asset");
                var go=new GameObject(line.name);go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=riverMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;
                line.enabled=false;
            }
        }

        static void Lake(MeshFilter filter,Mesh source,WorldMapWaterSettings settings,Material material,string folder)
        {
            if(!source || source.vertexCount<4)return;
            var original=source.vertices;var center=original[0];var outline=new List<Vector3>();
            for(int i=1;i<original.Length;i++)outline.Add(original[i]);
            for(int pass=0;pass<settings.lakeRoundingPasses;pass++) {
                var next=new List<Vector3>();
                for(int i=0;i<outline.Count;i++){var a=outline[i];var b=outline[(i+1)%outline.Count];next.Add(Vector3.Lerp(a,b,settings.lakeCornerCut));next.Add(Vector3.Lerp(a,b,1-settings.lakeCornerCut));}
                outline=next;
            }
            var vertices=new List<Vector3>();var colors=new List<Color>();var indices=new List<int>();int n=outline.Count;
            float[] rings={0,.025f,.08f,.2f};
            foreach(float inset in rings)foreach(var p in outline){var v=Vector3.Lerp(p,center,inset);vertices.Add(v);colors.Add(new Color(Mathf.Clamp01(Vector3.Distance(p,v)/settings.lakeShoreWidth),0,0,1));}
            for(int r=0;r<rings.Length-1;r++)for(int i=0;i<n;i++) {
                int a=r*n+i,b=r*n+(i+1)%n,c=a+n,d=b+n;
                // Source outlines use clockwise XZ winding.
                indices.Add(a);indices.Add(c);indices.Add(b);indices.Add(b);indices.Add(c);indices.Add(d);
            }
            int last=vertices.Count;vertices.Add(center);colors.Add(Color.red);
            for(int i=0;i<n;i++){indices.Add(last);indices.Add((rings.Length-1)*n+(i+1)%n);indices.Add((rings.Length-1)*n+i);}
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            // Normalize winding from the source instead of assuming export orientation.
            if(mesh.normals.Length>0){float sum=0;foreach(var normal in mesh.normals)sum+=normal.y;if(sum<0){for(int i=0;i<indices.Count;i+=3){int t=indices[i+1];indices[i+1]=indices[i+2];indices[i+2]=t;}mesh.SetTriangles(indices,0);mesh.RecalculateNormals();}}
            filter.sharedMesh=Save(mesh,folder+"/Lake_"+filter.name+".asset");filter.GetComponent<MeshRenderer>().sharedMaterial=material;
            EditorUtility.SetDirty(filter);EditorUtility.SetDirty(filter.GetComponent<MeshRenderer>());
        }

        static float Ground(Vector3 p,Terrain[] terrains)
        {
            foreach(var t in terrains){var o=t.transform.position;var s=t.terrainData.size;if(p.x>=o.x&&p.x<=o.x+s.x&&p.z>=o.z&&p.z<=o.z+s.z)return t.SampleHeight(p)+o.y;}
            return p.y;
        }
        sealed class LakeArea {public Vector3[] outline;public Bounds bounds;}
        static bool InLake(Vector3 p,List<LakeArea> lakes)
        {
            foreach(var lake in lakes) {
                var min=lake.bounds.min;var max=lake.bounds.max;
                if(p.x<min.x||p.x>max.x||p.z<min.z||p.z>max.z)continue;
                bool inside=false;var points=lake.outline;
                for(int i=0,j=points.Length-1;i<points.Length;j=i++) {
                    var a=points[i];var b=points[j];
                    if((a.z>p.z)!=(b.z>p.z) && p.x<(b.x-a.x)*(p.z-a.z)/(b.z-a.z)+a.x)inside=!inside;
                }
                if(inside)return true;
            }
            return false;
        }
        static Vector3 Catmull(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float t)=>.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t);
        static Mesh Save(Mesh mesh,string path)
        {
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!old){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;
        }
        static Material Material(string name,WorldMapArtSettings profile,Color deep,Color shallow,bool flow,string folder)
        {
            string path=folder+"/"+name+".mat";var shader=Shader.Find("BronzeAge/Campaign Water");
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
            material.shader=shader;material.SetFloat("_Coastal",0);material.SetFloat("_LocalShore",1);material.SetFloat("_Flow",flow?1:0);
            WorldCampaignArt.ConfigureWaterMaterial(material,profile,deep,shallow);return material;
        }
    }
}

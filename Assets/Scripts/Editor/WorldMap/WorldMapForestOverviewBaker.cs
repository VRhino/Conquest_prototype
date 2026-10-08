using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Conquest.WorldMap.Editor
{
    public static class WorldMapForestOverviewBaker
    {
        const float CellMetres = 90;
        sealed class Group
        {
            public Vector3 sum;
            public int count;
            public float minX=float.MaxValue,maxX=float.MinValue,minZ=float.MaxValue,maxZ=float.MinValue;
            public void Add(Vector3 p) { sum+=p;count++;minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minZ=Mathf.Min(minZ,p.z);maxZ=Mathf.Max(maxZ,p.z); }
        }
        public static string Install(WorldMapPreviewController preview,string folder)
        {
            if (AssetDatabase.LoadAssetAtPath<WorldMapForestOverviewData>(folder+"/ForestOverview.asset")) throw new InvalidOperationException("Overview already baked");
            var mesh = CanopyMesh(); AssetDatabase.CreateAsset(mesh,folder+"/ForestCanopy.asset");
            var palette = new[] {new Color(.22f,.33f,.17f),new Color(.27f,.37f,.19f),new Color(.17f,.29f,.19f)};
            var materials = palette.Select((color,i)=> {
                var material=new Material(Shader.Find("Universal Render Pipeline/Simple Lit")) {name="Mediterranean canopy "+i,enableInstancing=true};
                material.SetColor("_BaseColor",color); material.SetFloat("_Smoothness",0);
                AssetDatabase.CreateAsset(material,folder+"/ForestCanopy"+i+".mat");return material;
            }).ToArray();
            var batches=new List<WorldMapInstanceBatch>(); int source=0,totalGroups=0;
            var terrains=preview.worldRoot.GetComponentsInChildren<Terrain>();
            var groups=new Dictionary<(int,int),Group>();
            foreach(var terrain in terrains) {
                var data=terrain.terrainData;
                foreach(var tree in data.treeInstances) {
                    var p=terrain.transform.position+Vector3.Scale(tree.position,data.size);
                    var key=(Mathf.FloorToInt(p.x/CellMetres),Mathf.FloorToInt(p.z/CellMetres));
                    if(!groups.TryGetValue(key,out var g))groups.Add(key,g=new Group()); g.Add(p);source++;
                }
            }
                var matrices=new List<Matrix4x4>[3]; for(int i=0;i<3;i++)matrices[i]=new List<Matrix4x4>();
                foreach(var entry in groups) {
                    var g=entry.Value;var p=g.sum/g.count;
                    var terrain=terrains.First(t=>p.x>=t.transform.position.x && p.z>=t.transform.position.z
                        && p.x<=t.transform.position.x+t.terrainData.size.x && p.z<=t.transform.position.z+t.terrainData.size.z);
                    var data=terrain.terrainData;
                    var local=p-terrain.transform.position;
                    var normal=data.GetInterpolatedNormal(local.x/data.size.x,local.z/data.size.z);
                    p.y=terrain.SampleHeight(p)+terrain.transform.position.y+12;
                    // Extents come from the real trees: don't paint a filled square per grid cell.
                    float sx=Mathf.Clamp((g.maxX-g.minX)*.6f+14,14,65),sz=Mathf.Clamp((g.maxZ-g.minZ)*.6f+14,14,65);
                    int paletteIndex=(int)((uint)(entry.Key.Item1*73856093^entry.Key.Item2*19349663)%3);
                    matrices[paletteIndex].Add(Matrix4x4.TRS(p,Quaternion.FromToRotation(Vector3.up,normal),new Vector3(sx,12+Mathf.Sqrt(g.count)*2,sz)));
                }
                totalGroups+=groups.Count;
                for(int i=0;i<3;i++) for(int start=0;start<matrices[i].Count;start+=1023) {
                    var part=matrices[i].Skip(start).Take(1023).ToArray();
                    var bounds=new Bounds(part[0].MultiplyPoint3x4(Vector3.zero),Vector3.zero);
                    foreach(var m in part) bounds.Encapsulate(m.MultiplyPoint3x4(Vector3.zero));bounds.Expand(250);
                    batches.Add(new WorldMapInstanceBatch {mesh=mesh,material=materials[i],matrices=part,bounds=bounds});
                }
            var cache=ScriptableObject.CreateInstance<WorldMapForestOverviewData>();cache.sourceTrees=source;cache.canopyGroups=totalGroups;cache.batches=batches.ToArray();
            AssetDatabase.CreateAsset(cache,folder+"/ForestOverview.asset");
            var renderer=preview.worldRoot.GetComponent<WorldMapForestOverview>();
            if(!renderer)renderer=preview.worldRoot.gameObject.AddComponent<WorldMapForestOverview>();
            renderer.canopy=cache;renderer.viewCamera=preview.mapCamera;
            EditorUtility.SetDirty(renderer);AssetDatabase.SaveAssets();
            return "Native trees="+source+", canopy groups="+totalGroups+", instanced batches="+batches.Count;
        }
        static Mesh CanopyMesh()
        {
            var vertices=new List<Vector3>();var indices=new List<int>();
            for(int i=0;i<8;i++) {
                float a=i*Mathf.PI*.25f,b=(i+1)*Mathf.PI*.25f;
                float ra=1+.12f*Mathf.Sin(i*2.7f),rb=1+.12f*Mathf.Sin((i+1)%8*2.7f);
                int n=vertices.Count;
                vertices.Add(new Vector3(.06f,1,-.04f));
                vertices.Add(new Vector3(Mathf.Cos(b)*rb,0,Mathf.Sin(b)*rb));
                vertices.Add(new Vector3(Mathf.Cos(a)*ra,0,Mathf.Sin(a)*ra));
                indices.Add(n);indices.Add(n+1);indices.Add(n+2);
            }
            var mesh=new Mesh {name="Faceted forest canopy"};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}

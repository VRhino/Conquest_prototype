using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Conquest.WorldMap.Editor
{
    /// <summary>Baked vertex-cluster LODs with flat normals and retained Synty atlas/material boundaries.</summary>
    internal static class WorldMapForestLod
    {
        internal static Renderer[] Create(GameObject source, Transform parent, string model, string level, float fraction)
        {
            var root = new GameObject(level); root.transform.SetParent(parent, false);
            root.transform.localPosition = source.transform.localPosition;
            root.transform.localRotation = source.transform.localRotation;
            root.transform.localScale = source.transform.localScale;
            var renderers = new List<Renderer>(); int index = 0;
            foreach (var filter in source.GetComponentsInChildren<MeshFilter>())
            {
                var originalRenderer = filter.GetComponent<MeshRenderer>();
                if (!filter.sharedMesh || !originalRenderer) continue;
                var matrix = source.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var mesh = Simplify(filter.sharedMesh, matrix, fraction);
                string path = WorldMapLandscapeLab.Folder + "/" + model + "_" + level + "_" + index++ + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing) { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; EditorUtility.SetDirty(mesh); }
                else AssetDatabase.CreateAsset(mesh, path);
                var child = new GameObject(filter.name); child.transform.SetParent(root.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = child.AddComponent<MeshRenderer>(); renderer.sharedMaterials = originalRenderer.sharedMaterials;
                renderer.shadowCastingMode = originalRenderer.shadowCastingMode; renderer.receiveShadows = originalRenderer.receiveShadows;
                renderers.Add(renderer);
            }
            return renderers.ToArray();
        }

        static Mesh Simplify(Mesh source, Matrix4x4 transform, float fraction)
        {
            var points = source.vertices.Select(v => transform.MultiplyPoint3x4(v)).ToArray();
            var uv = source.uv; var colors = source.colors32;
            var bounds = new Bounds(points[0], Vector3.zero); foreach (var p in points) bounds.Encapsulate(p);
            float cell = Mathf.Max(.0001f, Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)) * fraction);
            var lookup = new Dictionary<(int,int,int,int,int), int>(); var remap = new int[points.Length];
            var sums = new List<Vector3>(); var counts = new List<int>(); var tex = new List<Vector2>(); var tint = new List<Color32>();
            for (int i = 0; i < points.Length; i++)
            {
                var p = points[i] - bounds.min; var t = uv.Length == points.Length ? uv[i] : Vector2.zero;
                // Keep distant vertices from unrelated atlas swatches in separate clusters.
                var key = (Mathf.FloorToInt(p.x/cell),Mathf.FloorToInt(p.y/cell),Mathf.FloorToInt(p.z/cell),Mathf.FloorToInt(t.x*32),Mathf.FloorToInt(t.y*32));
                if (!lookup.TryGetValue(key, out int id))
                {
                    id=sums.Count;lookup[key]=id;sums.Add(Vector3.zero);counts.Add(0);tex.Add(t);
                    tint.Add(colors.Length==points.Length?colors[i]:new Color32(255,255,255,255));
                }
                remap[i]=id;sums[id]+=points[i];counts[id]++;
            }
            for(int i=0;i<sums.Count;i++)sums[i]/=counts[i];
            var vertices=new List<Vector3>();var texture=new List<Vector2>();var color=new List<Color32>();
            var submeshes=new List<int[]>();
            for(int sub=0;sub<source.subMeshCount;sub++)
            {
                var triangles=source.GetTriangles(sub);var output=new List<int>();
                for(int i=0;i<triangles.Length;i+=3)
                {
                    int a=remap[triangles[i]],b=remap[triangles[i+1]],c=remap[triangles[i+2]];
                    if(a==b||b==c||a==c||Vector3.Cross(sums[b]-sums[a],sums[c]-sums[a]).sqrMagnitude<1e-12f)continue;
                    foreach(int id in new[]{a,b,c}){output.Add(vertices.Count);vertices.Add(sums[id]);texture.Add(tex[id]);color.Add(tint[id]);}
                }
                submeshes.Add(output.ToArray());
            }
            if(vertices.Count==0)throw new InvalidOperationException("LOD vacío: "+source.name);
            var mesh=new Mesh{name=source.name+"_ClusterLOD",indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.SetVertices(vertices);mesh.SetUVs(0,texture);mesh.SetColors(color);mesh.subMeshCount=submeshes.Count;
            for(int i=0;i<submeshes.Count;i++)mesh.SetTriangles(submeshes[i],i);
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            return mesh;
        }
    }
}

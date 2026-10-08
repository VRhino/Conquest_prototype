using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BronzeAge.RegionalMapTool {
 [Serializable] public class CoastalWorld { public CoastalData costas; }
 [Serializable] public class CoastalData { public string revision; public int seed; public CoastalSegment[] segmentos; }
 [Serializable] public class CoastalSegment { public string id,tipo; public float intensidad; public CoastalPoint[] puntos; }
 [Serializable] public class CoastalPoint { public float x,z,alturaY; public Vector3 Position=>new Vector3(x,alturaY,z); }
 /// <summary>Builds presentation from worldgen-exported geometry. No RNG or geographic decisions in Unity.</summary>
 public static class BronzeAgeCoastalImporter {
  public const float FaceSpacing=6,FaceLift=2.5f,UpperApron=24;
  public sealed class HeightGrid {public Vector3 origin,size;public float[,] heights;}
  public static Func<Vector3,float> HeightSampler(IEnumerable<HeightGrid> grids) {
   var tiles=grids.ToArray();
   float minX=tiles.Min(t=>t.origin.x),minZ=tiles.Min(t=>t.origin.z),maxX=tiles.Max(t=>t.origin.x+t.size.x),maxZ=tiles.Max(t=>t.origin.z+t.size.z);
   return p=>{
    // Seaward skirts can extend past the finite world's edge.
    p.x=Mathf.Clamp(p.x,minX,maxX);p.z=Mathf.Clamp(p.z,minZ,maxZ);
    var tile=tiles.FirstOrDefault(t=>p.x>=t.origin.x&&p.z>=t.origin.z&&p.x<=t.origin.x+t.size.x&&p.z<=t.origin.z+t.size.z);
    if(tile==null)throw new InvalidOperationException("Coastal point outside height grids: "+p);
    int nx=tile.heights.GetLength(1)-1,nz=tile.heights.GetLength(0)-1;
    float x=Mathf.Clamp01((p.x-tile.origin.x)/tile.size.x)*nx,z=Mathf.Clamp01((p.z-tile.origin.z)/tile.size.z)*nz;
    int ix=Mathf.Min((int)x,nx-1),iz=Mathf.Min((int)z,nz-1);
    float u=x-ix,v=z-iz,a=tile.heights[iz,ix],b=tile.heights[iz,ix+1],c=tile.heights[iz+1,ix],d=tile.heights[iz+1,ix+1];
    // Terrain renders planar triangles, not the bilinear surface used by GetInterpolatedHeight.
    // Cover both diagonal orientations so a change in Terrain LOD cannot expose teeth.
    float diagonalA=u>=v?a+(b-a)*u+(d-b)*v:a+(d-c)*u+(c-a)*v;
    float diagonalB=u+v<=1?a+(b-a)*u+(c-a)*v:d+(c-d)*(1-u)+(b-d)*(1-v);
    return tile.origin.y+tile.size.y*Mathf.Max(diagonalA,diagonalB);
   };
  }
  public static Mesh BuildMesh(CoastalData data,Vector3 origin,Vector2 size,Func<Vector3,float> surfaceHeight) {
   if(data==null || data.revision!="costas-1")throw new InvalidOperationException("Unsupported coastal field");
   var vertices=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
   foreach(var segment in data.segmentos??Array.Empty<CoastalSegment>()) {
    if(segment.puntos==null || segment.puntos.Length!=6)throw new InvalidOperationException("Invalid coast segment "+segment.id);
    var p=segment.puntos.Select(v=>v.Position).ToArray();var center=(p[0]+p[1])*.5f;
    if(center.x<origin.x||center.z<origin.z||center.x>=origin.x+size.x||center.z>=origin.z+size.y)continue;
    var tangent=p[1]-p[0];tangent.y=0;tangent.Normalize();
    var inward=p[4]-p[0];inward.y=0;
    // Terrain's finite grid broadens the shoulder beyond the analytic top ring.
    // Continue the rock surface over that shoulder instead of exposing its triangle tips.
    var topA=p[4]-p[0];topA.y=0;var topB=p[5]-p[1];topB.y=0;
    p=p.Concat(new[]{p[4]+topA.normalized*UpperApron,p[5]+topB.normalized*UpperApron}).ToArray();
    bool flip=Vector3.Dot(Vector3.Cross(p[1]-p[0],p[2]-p[0]),inward)>0;
    void Triangle(Vector3 a,Vector3 b,Vector3 c) {
     if(flip){var swap=b;b=c;c=swap;}
     foreach(var v in new[]{a,b,c}) {indices.Add(vertices.Count);vertices.Add(v-origin);uv.Add(new Vector2(Vector3.Dot(v,tangent)/35,v.y/35));}
    }
    // Follow the imported height grid instead of intersecting it with broad planar faces.
    // The footprint remains canonical; tessellation only resolves presentation.
    for(int band=0;band<3;band++){
     int lo=band*2,hi=lo+2;
     var acrossA=p[hi]-p[lo];acrossA.y=0;var acrossB=p[hi+1]-p[lo+1];acrossB.y=0;
     int columns=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(new Vector2(p[0].x,p[0].z),new Vector2(p[1].x,p[1].z))/FaceSpacing));
     int rows=Mathf.Max(1,Mathf.CeilToInt(Mathf.Max(acrossA.magnitude,acrossB.magnitude)/FaceSpacing));
     Vector3 Sample(float u,float v){var q=Vector3.Lerp(Vector3.Lerp(p[lo],p[lo+1],u),Vector3.Lerp(p[hi],p[hi+1],u),v);q.y=surfaceHeight(q)+FaceLift;return q;}
     for(int row=0;row<rows;row++)for(int col=0;col<columns;col++){
      var a=Sample(col/(float)columns,row/(float)rows);var b=Sample((col+1f)/columns,row/(float)rows);
      var c=Sample(col/(float)columns,(row+1f)/rows);var d=Sample((col+1f)/columns,(row+1f)/rows);
      Triangle(a,b,c);Triangle(b,d,c);
     }
    }
   }
   if(vertices.Count==0)return null;
   var mesh=new Mesh{name="Canonical coastal faces",indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
   mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
  }
  public static GameObject Install(Transform parent,Mesh mesh,Material material) {
   var old=parent.Find("Canonical coastal faces");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
   if(!mesh)return null;
   var go=new GameObject("Canonical coastal faces");go.transform.SetParent(parent,false);
   go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
   return go;
  }
  public static Material Material(string folder,Texture rock=null) {
   var path=folder+"/CoastRock.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
   var shader=Shader.Find("BronzeAge/Campaign Coastal Rock");
   if(!shader)shader=Shader.Find("Universal Render Pipeline/Simple Lit");
   if(!mat){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}else mat.shader=shader;
   mat.SetColor("_BaseColor",new Color(.53f,.48f,.38f));
   // Broad mineral variation replaces the fine pebble texture at campaign scale.
   if(mat.HasProperty("_BaseMap"))mat.SetTexture("_BaseMap",null);
   EditorUtility.SetDirty(mat);return mat;
  }
 }
}

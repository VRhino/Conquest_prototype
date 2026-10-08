#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
namespace BronzeAge.RegionalMapTool
{
 [Serializable] internal class RegionalManifest { public string formato; public int formatoVersion,seed; public float ancho,alto,alturaMaxima,nivelMar; public string world; public RegionalTile[] bloques; public RegionalFile[] archivos; public string[] canales; }
 [Serializable] internal class RegionalFile { public string archivo,sha256; public long bytes; }
 [Serializable] internal class RegionalTile { public string id,alturas,capas; public int columna,fila,resolucion,resolucionCapas; public float x,z,ancho,alto; }
 [Serializable] internal class RegionalPoint { public float x,z,alturaY; }
 [Serializable] internal class RegionalResource { public string id,tipo,categoriaEstano,clusterEstano; public int area; public float x,z,alturaY; public RegionalEnclave enclaveInicial; }
 [Serializable] internal class RegionalEnclave { public string piedraId; public float radio; }
 [Serializable] internal class RegionalRiver { public string id; public float ancho; public RegionalPoint[] puntos; }
 [Serializable] internal class RegionalLake { public string id; public float alturaY; public RegionalPoint centro; public RegionalPoint[] contorno; }
 [Serializable] internal class RegionalWorld { public CoastalData costas; public RegionalResource[] recursos; public RegionalRiver[] rios; public RegionalLake[] lagos; }
 public static class BronzeAgeRegionalImporter
 {
  [MenuItem("BronzeAge/Importar mundo regional")]
  public static void ChoosePackage() {
   var file=EditorUtility.OpenFilePanel("Manifest del mundo regional","","json");if(string.IsNullOrEmpty(file))return;
   try { Import(file); } catch(Exception e) { Debug.LogException(e); EditorUtility.DisplayDialog("Importación regional",e.Message,"Cerrar"); }
  }
  public static void Import(string manifestPath) {
   var folder=Path.GetDirectoryName(Path.GetFullPath(manifestPath));var m=JsonUtility.FromJson<RegionalManifest>(File.ReadAllText(manifestPath));
   if(m==null||m.formato!="bronzeage-regional-unity"||m.formatoVersion!=1||m.bloques==null||m.archivos==null||m.canales==null||m.canales.Length!=7)throw new InvalidDataException("Formato regional no soportado");
   foreach(var f in m.archivos) {var path=SafePath(folder,f.archivo);var info=new FileInfo(path);if(!info.Exists||info.Length!=f.bytes)throw new InvalidDataException("Archivo incompleto: "+f.archivo);
    using(var sha=SHA256.Create())using(var stream=File.OpenRead(path)) {var hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();if(hash!=f.sha256)throw new InvalidDataException("Checksum incorrecto: "+f.archivo);}
   }
   var world=JsonUtility.FromJson<RegionalWorld>(File.ReadAllText(SafePath(folder,m.world)));
   const string parent="Assets/BronzeAgeRegionalImports";if(!AssetDatabase.IsValidFolder(parent))AssetDatabase.CreateFolder("Assets","BronzeAgeRegionalImports");
   var destination=AssetDatabase.GenerateUniqueAssetPath(parent+"/Seed_"+m.seed);AssetDatabase.CreateFolder(parent,Path.GetFileName(destination));
   var root=new GameObject("BronzeAgeRegional_seed"+m.seed);Undo.RegisterCreatedObjectUndo(root,"Importar mundo regional");var terrains=new Dictionary<string,Terrain>();
   try {
    var layers=CreateLayers(destination);
    for(int i=0;i<m.bloques.Length;i++) {
     var b=m.bloques[i];EditorUtility.DisplayProgressBar("BronzeAge",b.id,(float)i/m.bloques.Length);
     var bytes=File.ReadAllBytes(SafePath(folder,b.alturas));if(bytes.Length!=b.resolucion*b.resolucion*2)throw new InvalidDataException("Alturas incompletas: "+b.id);
     var h=new float[b.resolucion,b.resolucion];for(int y=0;y<b.resolucion;y++)for(int x=0;x<b.resolucion;x++){int p=(y*b.resolucion+x)*2;h[y,x]=(bytes[p]|bytes[p+1]<<8)/65535f;}
     var data=new TerrainData {heightmapResolution=b.resolucion,size=new Vector3(b.ancho,m.alturaMaxima,b.alto)};data.SetHeights(0,0,h);data.terrainLayers=layers;
     var masks=File.ReadAllBytes(SafePath(folder,b.capas));if(masks.Length!=b.resolucionCapas*b.resolucionCapas*7)throw new InvalidDataException("Capas incompletas: "+b.id);
     File.WriteAllBytes(destination+"/"+b.id+".layers.bytes",masks);
     data.alphamapResolution=b.resolucionCapas;var a=new float[b.resolucionCapas,b.resolucionCapas,4];
     for(int y=0;y<b.resolucionCapas;y++)for(int x=0;x<b.resolucionCapas;x++){int p=(y*b.resolucionCapas+x)*7;float forest=masks[p]/255f,fertility=masks[p+1]/255f,rock=Mathf.Clamp01(masks[p+5]/255f*2),soil=(1-forest)*(1-rock);a[y,x,0]=soil*fertility;a[y,x,1]=soil*(1-fertility);a[y,x,2]=(1-forest)*rock;a[y,x,3]=forest;}
     data.SetAlphamaps(0,0,a);AssetDatabase.CreateAsset(data,destination+"/"+b.id+".asset");data.SetAlphamaps(0,0,a);foreach(var splat in data.alphamapTextures)EditorUtility.SetDirty(splat);EditorUtility.SetDirty(data);
     var go=Terrain.CreateTerrainGameObject(data);go.name=b.id;go.transform.SetParent(root.transform,false);go.transform.position=new Vector3(b.x,0,b.z);terrains.Add(b.columna+","+b.fila,go.GetComponent<Terrain>());
    }
    foreach(var b in m.bloques)terrains[b.columna+","+b.fila].SetNeighbors(Find(terrains,b.columna-1,b.fila),Find(terrains,b.columna,b.fila+1),Find(terrains,b.columna+1,b.fila),Find(terrains,b.columna,b.fila-1));
    if(world.costas!=null){var rock=BronzeAgeCoastalImporter.Material(destination);
     var heightGrids=new List<BronzeAgeCoastalImporter.HeightGrid>();foreach(var terrain in terrains.Values){var data=terrain.terrainData;heightGrids.Add(new BronzeAgeCoastalImporter.HeightGrid{origin=terrain.transform.position,size=data.size,heights=data.GetHeights(0,0,data.heightmapResolution,data.heightmapResolution)});}
     var sampleHeight=BronzeAgeCoastalImporter.HeightSampler(heightGrids);
     foreach(var b in m.bloques){var terrain=terrains[b.columna+","+b.fila];var mesh=BronzeAgeCoastalImporter.BuildMesh(world.costas,terrain.transform.position,new Vector2(b.ancho,b.alto),sampleHeight);
      if(mesh){AssetDatabase.CreateAsset(mesh,destination+"/"+b.id+"-coast.asset");BronzeAgeCoastalImporter.Install(terrain.transform,mesh,rock);}}
    }
    var water=CreateMaterial(destination,"Water",new Color(.12f,.36f,.48f));
    CreatePolygon(root.transform,"Mar",new[]{new Vector3(0,m.nivelMar,0),new Vector3(0,m.nivelMar,m.alto),new Vector3(m.ancho,m.nivelMar,m.alto),new Vector3(m.ancho,m.nivelMar,0)},new[]{0,1,2,0,2,3},water);
    foreach(var l in world.lagos??Array.Empty<RegionalLake>()) {
     if(l.contorno==null||l.contorno.Length<3||l.centro==null)continue;var vertices=new Vector3[l.contorno.Length+1];vertices[0]=new Vector3(l.centro.x,l.alturaY+.2f,l.centro.z);
     for(int i=0;i<l.contorno.Length;i++)vertices[i+1]=new Vector3(l.contorno[i].x,l.alturaY+.2f,l.contorno[i].z);
     var triangles=new int[l.contorno.Length*3];for(int i=0;i<l.contorno.Length;i++){triangles[i*3]=0;triangles[i*3+1]=(i+1)%l.contorno.Length+1;triangles[i*3+2]=i+1;}CreatePolygon(root.transform,l.id,vertices,triangles,water);
    }
    foreach(var r in world.rios??Array.Empty<RegionalRiver>()) {var go=new GameObject(r.id);go.transform.SetParent(root.transform,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=water;line.widthMultiplier=Mathf.Max(2,r.ancho);line.positionCount=r.puntos.Length;for(int i=0;i<r.puntos.Length;i++)line.SetPosition(i,new Vector3(r.puntos[i].x,r.puntos[i].alturaY+2,r.puntos[i].z));}
    var materials=new Dictionary<string,Material>();
    foreach(var r in world.recursos??Array.Empty<RegionalResource>()) {
     if(!materials.TryGetValue(r.tipo,out var mat)){var color=r.tipo=="estano"?Color.cyan:r.tipo=="cobre"?new Color(.9f,.45f,.18f):r.tipo=="oro"?Color.yellow:r.tipo=="piedra"?Color.gray:new Color(.85f,.75f,.55f);mat=CreateMaterial(destination,r.tipo,color);materials.Add(r.tipo,mat);}
     var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name=r.id;go.transform.SetParent(root.transform,false);go.transform.position=new Vector3(r.x,r.alturaY+10,r.z);go.transform.localScale=Vector3.one*20;
     UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=mat;
     var marker=go.AddComponent<BronzeAgeRegionalMarker>();marker.sourceId=r.id;marker.resourceType=r.tipo;marker.region=r.area+1;marker.tinCategory=r.categoriaEstano;marker.tinCluster=r.clusterEstano;marker.canonicalPosition=new Vector3(r.x,r.alturaY,r.z);marker.geographicStartPrepared=r.enclaveInicial!=null;
    }
    AssetDatabase.SaveAssets();Selection.activeGameObject=root;EditorGUIUtility.PingObject(root);Debug.Log("Mundo regional importado: geometría visual, sin estado económico ni reglas de partida.",root);
   } finally {EditorUtility.ClearProgressBar();}
  }
  static string SafePath(string folder,string relative){var root=Path.GetFullPath(folder)+Path.DirectorySeparatorChar;var p=Path.GetFullPath(Path.Combine(folder,relative));if(!p.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Ruta fuera del paquete");return p;}
  static Terrain Find(Dictionary<string,Terrain> all,int x,int y)=>all.TryGetValue(x+","+y,out var t)?t:null;
  static TerrainLayer[] CreateLayers(string folder) {
   var colors=new[]{new Color(.42f,.57f,.30f),new Color(.67f,.58f,.39f),new Color(.43f,.45f,.43f),new Color(.17f,.32f,.20f)};var layers=new TerrainLayer[4];
   for(int i=0;i<4;i++){var tex=new Texture2D(2,2);tex.SetPixels(new[]{colors[i],colors[i],colors[i],colors[i]});tex.Apply();AssetDatabase.CreateAsset(tex,folder+"/Color_"+i+".asset");layers[i]=new TerrainLayer{diffuseTexture=tex,tileSize=new Vector2(50,50)};AssetDatabase.CreateAsset(layers[i],folder+"/Layer_"+i+".terrainlayer");}return layers;
  }
  static Material CreateMaterial(string folder,string name,Color color){var shader=Shader.Find("Universal Render Pipeline/Unlit")??Shader.Find("Unlit/Color");if(shader==null)throw new InvalidOperationException("No hay shader Unlit compatible");var mat=new Material(shader){color=color};AssetDatabase.CreateAsset(mat,folder+"/"+name+".mat");return mat;}
  static void CreatePolygon(Transform root,string name,Vector3[] vertices,int[] triangles,Material material){var go=new GameObject(name);go.transform.SetParent(root,false);var mesh=new Mesh{vertices=vertices,triangles=triangles};mesh.RecalculateNormals();mesh.RecalculateBounds();go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;AssetDatabase.AddObjectToAsset(mesh,material);}
 }
}
#endif

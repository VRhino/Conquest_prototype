using System;
using Conquest.BronzeAge;
using UnityEngine;
using Newtonsoft.Json.Linq;
public static class ValidateBronzeAgeProjection {
 public static string Run(){
  var root=JObject.Parse(@"{""gameId"":""g"",""mapaId"":""m"",""version"":5,""instante"":1000,""heroeId"":""h"",""heroe"":{""id"":""h"",""ubicacion"":{""tipo"":""columna"",""ejercitoId"":""own""}},""ejercitos"":[{""id"":""own"",""posicionActual"":{""x"":17452,""y"":6550}}],""ejercitosAvistados"":[{""id"":""enemy"",""posicionActual"":{""x"":99,""y"":99}}],""asentamientosAvistados"":[{""id"":""city"",""nombre"":""Ciudad"",""nivel"":2,""posicion"":{""x"":18000,""y"":6500}}],""asentamientosConocidos"":[{""asentamientoId"":""city"",""posicion"":{""x"":1,""y"":1}}]}");
  var bounds=new Vector2(32000,24000);var p=CampaignProjection.Parse(root.ToString(),"g","m",bounds);if(p.actorPosition.Value!=new Vector3(17452,0,6550)||p.cities.Count!=1||p.cities[0].remembered)throw new Exception("Player/visible-city selection failed");
  int rejected=0;Action<Action> reject=a=>{try{a();}catch(InvalidOperationException){rejected++;return;}throw new Exception("Accepted invalid projection");};
  reject(()=>CampaignProjection.Parse(root.ToString(),"another","m",bounds));reject(()=>CampaignProjection.Parse(root.ToString(),"g","another",bounds));
  var absent=(JObject)root.DeepClone();absent["ejercitos"]=new JArray();reject(()=>CampaignProjection.Parse(absent.ToString(),"g","m",bounds));
  var outside=(JObject)root.DeepClone();outside["ejercitos"][0]["posicionActual"]["x"]=40000;reject(()=>CampaignProjection.Parse(outside.ToString(),"g","m",bounds));
  var noHero=(JObject)root.DeepClone();noHero["sinHeroe"]=true;reject(()=>CampaignProjection.Parse(noHero.ToString(),"g","m",bounds));
  var inside=(JObject)root.DeepClone();inside["heroe"]["ubicacion"]=JObject.Parse(@"{""tipo"":""asentamiento"",""asentamientoId"":""city""}");var city=CampaignProjection.Parse(inside.ToString(),"g","m",bounds);if(city.actorPosition.HasValue||city.settlementId!="city")throw new Exception("Invented exterior position");
  var host=new GameObject("Projection validation temporary");var manifest=ScriptableObject.CreateInstance<Conquest.WorldMap.WorldMapCampaignManifest>();
  try{manifest.worldSize=bounds;manifest.sourceSHA256="canonical";var streamer=host.AddComponent<Conquest.WorldMap.WorldMapSectorStreamer>();streamer.enabled=false;streamer.manifest=manifest;var controller=host.AddComponent<Conquest.WorldMap.WorldMapCampaignController>();controller.enabled=false;controller.streamer=streamer;controller.actor=host.transform;var connection=host.AddComponent<BronzeAgeCampaignConnection>();connection.controller=controller;
   var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;typeof(BronzeAgeCampaignConnection).GetField("gameId",flags).SetValue(connection,"g");typeof(BronzeAgeCampaignConnection).GetField("mapId",flags).SetValue(connection,"m");
   if(!connection.AcceptProjection(root.ToString()))throw new Exception("Initial snapshot rejected");var stale=(JObject)root.DeepClone();stale["version"]=4;stale["ejercitos"][0]["posicionActual"]["x"]=20000;if(connection.AcceptProjection(stale.ToString())||host.transform.position.x!=17452)throw new Exception("Stale snapshot changed actor");
   controller.authoritativePosition=true;controller.moving=true;controller.Advance(10);if(host.transform.position.x!=17452)throw new Exception("Local movement applied in authoritative mode");
   reject(()=>connection.Configure("http://localhost:3000","g","m","test-session","wrong-hash"));
  }finally{UnityEngine.Object.DestroyImmediate(host);UnityEngine.Object.DestroyImmediate(manifest);}
  string report="Synthetic player projection: own-column position; visible city overrides memory; 6 invalid contracts rejected; stale snapshot cannot rewind actor; local movement disabled in authoritative mode; interior never invents an exterior position. No live server request performed.";System.IO.File.WriteAllText("Docs/BronzeAgeProjectionValidation.txt",report);return report;
 }
}

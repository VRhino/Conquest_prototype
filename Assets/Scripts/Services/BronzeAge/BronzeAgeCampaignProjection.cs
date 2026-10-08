using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Conquest.BronzeAge {
 public sealed class CampaignCityView { public string id,name;public int level;public Vector3 position;public bool remembered; }
 public sealed class CampaignProjection {
  public string gameId,mapId,heroId,locationType,settlementId;public long version,instant;
  public Vector3? actorPosition;public readonly List<CampaignCityView> cities=new List<CampaignCityView>();
  // Read ONLY the player projection. Never infer a position from an enemy army or stale city memory.
  public static CampaignProjection Parse(string json,string expectedGame,string expectedMap,Vector2 bounds){
   var root=JObject.Parse(json);var p=new CampaignProjection{gameId=(string)root["gameId"],mapId=(string)root["mapaId"],heroId=(string)root["heroeId"]};
   if(p.gameId!=expectedGame||p.mapId!=expectedMap)throw new InvalidOperationException("La proyección pertenece a otra partida o mapa.");
   if((bool?)root["sinHeroe"]==true)throw new InvalidOperationException("La membresía todavía no tiene héroe.");
   if(string.IsNullOrEmpty(p.heroId)||(string)root["heroe"]?["id"]!=p.heroId)throw new InvalidOperationException("Identidad de héroe inconsistente.");
   p.version=Number(root,"version");p.instant=Number(root,"instante");
   var location=root["heroe"]["ubicacion"];p.locationType=(string)location?["tipo"];
   if(p.locationType=="columna"){
    string armyId=(string)location["ejercitoId"];JToken own=null;
    foreach(var army in root["ejercitos"] as JArray??new JArray())if((string)army["id"]==armyId){own=army;break;}
    if(own==null)throw new InvalidOperationException("Falta la columna propia en la proyección.");
    p.actorPosition=Point(own["posicionActual"],bounds);
   }else if(p.locationType=="desconectado")p.actorPosition=Point(location["punto"],bounds);
   else if(p.locationType=="asentamiento")p.settlementId=(string)location["asentamientoId"];
   else if(p.locationType!="mercenarios")throw new InvalidOperationException("Ubicación de héroe desconocida.");
   var seen=new HashSet<string>();
   foreach(var key in new[]{"asentamientos","asentamientosAvistados","asentamientosConocidos"})foreach(var city in root[key] as JArray??new JArray()){
    bool remembered=key=="asentamientosConocidos";string id=(string)city[remembered?"asentamientoId":"id"];
    if(string.IsNullOrEmpty(id))throw new InvalidOperationException("Asentamiento sin identidad.");
    if(!seen.Add(id))continue;
    p.cities.Add(new CampaignCityView{id=id,name=(string)city["nombre"]??id,level=(int?)city["nivel"]??1,position=Point(city["posicion"],bounds),remembered=remembered});
   }
   return p;
  }
  static long Number(JObject root,string key){if(root[key]?.Type!=JTokenType.Integer)throw new InvalidOperationException("Falta "+key);long value=(long)root[key];if(value<0)throw new InvalidOperationException("Valor inválido: "+key);return value;}
  static Vector3 Point(JToken point,Vector2 bounds){if(point==null||point["x"]==null||point["y"]==null)throw new InvalidOperationException("Falta posición.");float x=(float)point["x"],z=(float)point["y"];if(float.IsNaN(x)||float.IsInfinity(x)||float.IsNaN(z)||float.IsInfinity(z)||x<0||z<0||x>bounds.x||z>bounds.y)throw new InvalidOperationException("Posición fuera del mundo cargado.");return new Vector3(x,0,z);}
 }
}

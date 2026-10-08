using System;
using System.Collections;
using Conquest.WorldMap;
using UnityEngine;
using UnityEngine.Networking;
namespace Conquest.BronzeAge {
 // Optional read-only connection. Authentication is injected in memory, never saved in scene assets.
 public sealed class BronzeAgeCampaignConnection : MonoBehaviour {
  public WorldMapCampaignController controller;
  [Min(.5f)] public float pollSeconds=2;
  public CampaignProjection Current {get;private set;}
  public string Status {get;private set;}="Sin conexión BronzeAge";
  public event Action<CampaignProjection> ProjectionReceived;
  string baseUrl,gameId,mapId,sessionId;Coroutine polling;
  public void Configure(string url,string game,string map,string session,string exportedSHA256){
   if(!controller||!controller.streamer||!controller.streamer.manifest)throw new InvalidOperationException("Falta el manifiesto local.");
   if(string.IsNullOrWhiteSpace(game)||string.IsNullOrWhiteSpace(map)||string.IsNullOrWhiteSpace(session))throw new ArgumentException("Falta partida, mapa o sesión.");
   if(!string.Equals(exportedSHA256,controller.streamer.manifest.sourceSHA256,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("El contenido exportado no coincide con el mundo cargado.");
   if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||(uri.Scheme!="https"&&!(uri.Scheme=="http"&&uri.IsLoopback)))throw new ArgumentException("Usa HTTPS o HTTP local.");
   Disconnect();baseUrl=url.TrimEnd('/');gameId=game;mapId=map;sessionId=session;Current=null;Status="Conectando";controller.authoritativePosition=true;controller.moving=false;polling=StartCoroutine(Poll());
  }
  public bool AcceptProjection(string json){
   var candidate=CampaignProjection.Parse(json,gameId,mapId,controller.streamer.manifest.worldSize);
   if(Current!=null&&(candidate.version<Current.version||candidate.instant<Current.instant))return false;
   Current=candidate;if(candidate.actorPosition.HasValue)controller.ApplyAuthoritativePosition(candidate.actorPosition.Value);
   Status="Proyección recibida · versión "+candidate.version;ProjectionReceived?.Invoke(candidate);return true;
  }
  IEnumerator Poll(){while(true){using(var request=UnityWebRequest.Get(baseUrl+"/v1/jugador/partidas/"+Uri.EscapeDataString(gameId))){request.timeout=10;request.SetRequestHeader("Authorization","sesion "+sessionId);yield return request.SendWebRequest();if(request.result!=UnityWebRequest.Result.Success){Status="No se pudo consultar la proyección (HTTP "+request.responseCode+")";if(request.responseCode==401||request.responseCode==403){sessionId=null;polling=null;yield break;}}else {try{AcceptProjection(request.downloadHandler.text);}catch(Exception e){Status=e.Message;polling=null;yield break;}}}yield return new WaitForSecondsRealtime(Mathf.Max(.5f,pollSeconds));}}
  public void Disconnect(){if(polling!=null)StopCoroutine(polling);polling=null;sessionId=null;Status="Desconectado";}
  void OnDisable(){Disconnect();}
 }
}

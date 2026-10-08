using System;
using UnityEngine;
namespace Conquest.WorldMap {
 [Serializable] public class CampaignSectorAddress {public string id,address;public int column,row;public Vector3 origin;public Vector2 size;}
 // Static geography only. No owner, stock, hidden entity or server credentials.
 public sealed class WorldMapCampaignManifest : ScriptableObject {
  public string worldId,worldgenVersion,sourceSHA256,coastRevision,sourcePackage;public int seed;
  public Vector2 worldSize=new Vector2(32000,24000);public float sectorSize=4000,seaHeight=375;
  public CampaignSectorAddress[] sectors;
 }
}

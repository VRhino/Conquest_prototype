using UnityEngine;
namespace Conquest.WorldMap {
 public sealed class WorldMapCampaignSector : MonoBehaviour {
  public Terrain terrain;public WorldMapSyntyRenderer surroundings;
  public void Bind(Camera camera){if(surroundings)surroundings.viewCamera=camera;}
 }
}

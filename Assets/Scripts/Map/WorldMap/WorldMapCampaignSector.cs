using UnityEngine;
namespace Conquest.WorldMap {
 public sealed class WorldMapCampaignSector : MonoBehaviour {
  public Terrain terrain;public WorldMapSyntyRenderer surroundings;
  public void Bind(Camera camera){foreach(var renderer in GetComponentsInChildren<WorldMapSyntyRenderer>(true))renderer.viewCamera=camera;}
 }
}

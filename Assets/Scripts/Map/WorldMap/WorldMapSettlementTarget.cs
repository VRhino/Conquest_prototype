using UnityEngine;
namespace Conquest.WorldMap {
 // Presentation identity and entrance anchor. Server projection will supply these fields.
 public sealed class WorldMapSettlementTarget : MonoBehaviour {
  public string settlementId,displayName="Asentamiento",regionName="Cuenca fértil";
  [Range(1,5)] public int level=1;
  public Vector3 entrance;
  public TextAsset previewSource;
  public string previewSettlementId;
  public float DistanceFrom(Vector3 position)=>Vector2.Distance(new Vector2(position.x,position.z),new Vector2(entrance.x,entrance.z));
 }
}

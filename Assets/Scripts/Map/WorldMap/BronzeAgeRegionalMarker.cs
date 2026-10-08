using UnityEngine;
namespace BronzeAge.RegionalMapTool
{
 public sealed class BronzeAgeRegionalMarker : MonoBehaviour
 {
  public string sourceId, resourceType, tinCategory, tinCluster;
  public int region;
  public Vector3 canonicalPosition;
  public bool geographicStartPrepared;
 }
}

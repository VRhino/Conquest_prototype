using UnityEngine;
namespace Conquest.WorldMap {
 // Representation only: source trees and economic forests are unchanged.
 [CreateAssetMenu(menuName="BronzeAge/Forest mass profile")]
 public sealed class WorldMapForestMassProfile:ScriptableObject {
  public float edgeDiameter=34,denseDiameter=98,edgeHeight=21,denseHeight=32;
  [Range(0,1)]public float diameterVariation=.18f;
  public float shadeScale=380,canopyNoiseScale=140;
  public int seed=42;
 }
}

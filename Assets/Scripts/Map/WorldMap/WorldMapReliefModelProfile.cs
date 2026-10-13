using System;
using UnityEngine;

namespace Conquest.WorldMap {
 [CreateAssetMenu(menuName="BronzeAge/World map relief models")]
 public sealed class WorldMapReliefModelProfile : ScriptableObject {
  public GameObject[] greenMountains,snowMountains,coastalCliffs;
  public bool wholeWorld;
  public Rect mountainTrial=new Rect(.625f,.175f,.095f,.085f);
  public Rect coastTrial=new Rect(.28f,.27f,.055f,.06f);
  [Min(50)] public float candidateSpacing=150,mountainSeparation=190;
  [Min(1)] public int maximumMountains=36,maximumCliffs=50;
  public float minimumElevation=660,snowElevation=835;
  public Vector2 mountainWidth=new Vector2(100,210),mountainHeight=new Vector2(75,140);
  [Range(0,1)] public float mountainEmbed=.32f;
  public Vector2 cliffWidth=new Vector2(45,80),cliffDepth=new Vector2(20,32);
  [Min(5)] public float cliffSpacing=55;
  [Min(0)] public float cliffBelowSea=14,walkableClearance=12,resourceClearance=35;
 }
}

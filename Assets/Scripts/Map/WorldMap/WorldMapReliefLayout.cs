using System;
using UnityEngine;
namespace Conquest.WorldMap {
 [Serializable] public sealed class WorldMapReliefPlacement {
  public string id,kind,sectorId;
  public GameObject prefab;
  public Vector3 position,scale;
  public float yaw,exposedHeight;
  public Bounds footprint;
  public Matrix4x4 Matrix=>Matrix4x4.TRS(position,Quaternion.Euler(0,yaw,0),scale);
 }
 public sealed class WorldMapReliefLayout : ScriptableObject {
  public int revision,seed;
  public string sourceSHA256,fingerprint;
  public WorldMapReliefModelProfile profile;
  public WorldMapReliefPlacement[] placements;
 }
}

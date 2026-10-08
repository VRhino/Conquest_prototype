using UnityEngine;
namespace Conquest.WorldMap
{
    public sealed class WorldMapWaterSettings : ScriptableObject
    {
        [Min(2)] public float riverStep=6;
        [Min(.2f)] public float riverLift=2;
        [Min(1)] public float riverWidthScale=1.25f;
        [Min(2)] public float minimumRiverWidth=8;
        [Range(0,3)] public int lakeRoundingPasses=2;
        [Range(0,.25f)] public float lakeCornerCut=.2f;
        [Min(1)] public float lakeShoreWidth=35;
        [Min(0)] public float treeRiverClearance=6;
    }
}

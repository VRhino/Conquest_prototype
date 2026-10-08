using UnityEngine;

namespace Conquest.WorldMap
{
    [CreateAssetMenu(menuName="BronzeAge/World map Synty settings")]
    public sealed class WorldMapSyntySettings : ScriptableObject
    {
        public GameObject[] broadleaf, riverside, mountain, distant, rocks, cliffs;
        [Min(30)] public float spacing = 135;
        [Min(1)] public int denseClusterTrees = 9;
        [Min(1)] public int openClusterTrees = 3;
        [Min(1)] public float clusterSpread = 100;
        [Min(1)] public float treeHeight = 55;
        [Min(1)] public float rockSize = 24;
        [Min(1)] public float cliffHeight = 95;
        [Range(0,1)] public float cliffProbability = .3f;
        [Range(0,1)] public float cliffEmbed = .65f;
        [Min(1)] public float cliffWidth = 1.8f;
        [Min(1)] public float cliffDepth = 1.2f;
        public bool anchorCliffsToFootSlope;
        [Range(0,90)] public float rockSurfaceStartSlope = 8;
        [Range(0,90)] public float rockSurfaceFullSlope = 26;
        [Range(0,90)] public float maximumForestSlope = 32;
        [Range(0,90)] public float cliffSlope = 22;
        [Range(0,1)] public float rockProbability = .10f;
        [Min(500)] public float detailDistance = 7500;
        [Min(1)] public int distantStride = 8;
        [Min(500)] public float cullingCellSize = 1000;
        public float seaElevation = 375;
    }
}

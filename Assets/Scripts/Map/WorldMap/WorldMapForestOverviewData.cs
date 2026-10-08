using UnityEngine;

namespace Conquest.WorldMap
{
    // Presentation cache derived from native tree instances, never an economic forest mask.
    public sealed class WorldMapForestOverviewData : ScriptableObject
    {
        public int sourceTrees;
        public int canopyGroups;
        public WorldMapInstanceBatch[] batches;
    }
}

using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Conquest.WorldMap
{
    [Serializable] public sealed class WorldMapInstanceBatch
    {
        public Mesh mesh;
        public Material material;
        public int submesh;
        public Bounds bounds;
        public Matrix4x4[] matrices;
        public bool distant;
        public bool always;
    }
    public sealed class WorldMapSyntyInstances : ScriptableObject
    {
        public int revision;
        public WorldMapSyntySettings settings;
        public WorldMapInstanceBatch[] batches;
        public WorldMapInstanceBatch[] overviewBatches;
        public Bounds bounds;
        public int trees, rocks, cliffs;
        public long detailTriangles, distantTriangles;
    }

}

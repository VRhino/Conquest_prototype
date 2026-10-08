using UnityEngine;
namespace Conquest.WorldMap
{
    // Retains the imported outline so repeated visual bakes never shrink the lake again.
    [DisallowMultipleComponent]
    public sealed class WorldMapLakeSource : MonoBehaviour
    {
        public Mesh importedMesh;
    }
}

using UnityEngine;
using UnityEngine.Rendering;

namespace Conquest.WorldMap
{
    [ExecuteAlways]
    public sealed class WorldMapForestOverview : MonoBehaviour
    {
        public Camera viewCamera;
        public WorldMapForestOverviewData canopy;
        [Min(1000)] public float overviewHeight = 9000;
        [Min(1)] public float overviewPixelError = 12;
        public bool enableOverview = true;
        Terrain[] terrains;
        bool[] foliage;
        float[] pixelErrors;
        bool overridden;
        readonly Plane[] planes = new Plane[6];

        void OnEnable()
        {
            terrains = GetComponentsInChildren<Terrain>();
            foliage = new bool[terrains.Length]; pixelErrors = new float[terrains.Length];
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
            RenderPipelineManager.endCameraRendering += AfterCamera;
        }
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCamera;
            RenderPipelineManager.endCameraRendering -= AfterCamera;
            Restore();
        }
        void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            Restore();
            if (!enableOverview || !canopy || camera != viewCamera || camera.transform.position.y < overviewHeight) return;
            for (int i=0;i<terrains.Length;i++) {
                if (!terrains[i]) continue;
                foliage[i] = terrains[i].drawTreesAndFoliage;
                pixelErrors[i] = terrains[i].heightmapPixelError;
                terrains[i].drawTreesAndFoliage = false;
                terrains[i].heightmapPixelError = overviewPixelError;
            }
            overridden = true;
            GeometryUtility.CalculateFrustumPlanes(camera,planes);
            foreach (var batch in canopy.batches) {
                if (!GeometryUtility.TestPlanesAABB(planes,batch.bounds)) continue;
                Graphics.DrawMeshInstanced(batch.mesh,0,batch.material,batch.matrices,batch.matrices.Length,null,
                    ShadowCastingMode.Off,false,gameObject.layer,camera);
            }
        }
        void AfterCamera(ScriptableRenderContext context, Camera camera) { Restore(); }
        void Restore()
        {
            if (!overridden) return;
            for (int i=0;i<terrains.Length;i++) if (terrains[i]) {
                terrains[i].drawTreesAndFoliage = foliage[i];
                terrains[i].heightmapPixelError = pixelErrors[i];
            }
            overridden = false;
        }
    }
}

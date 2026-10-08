using UnityEngine;
using UnityEngine.Rendering;
namespace Conquest.WorldMap
{
    // Only rendering: canonical IDs, influence and resource state remain in their owning systems.
    [ExecuteAlways]
    public sealed class WorldMapSyntyRenderer : MonoBehaviour
    {
        public WorldMapSyntyInstances instances;
        public Camera viewCamera;
        public WorldMapArtSettings artProfile;
        private readonly Plane[] frustumPlanes = new Plane[6];
        private static readonly int TerrainLook = Shader.PropertyToID("_BronzeAgeTerrainLook");
        private void OnEnable() { RenderPipelineManager.beginCameraRendering += RenderSRP; Camera.onPreCull += RenderBuiltin; }
        private void OnDisable() { RenderPipelineManager.beginCameraRendering -= RenderSRP; Camera.onPreCull -= RenderBuiltin; }
        private void RenderSRP(ScriptableRenderContext context,Camera camera) { Render(camera); }
        private void RenderBuiltin(Camera camera) { if(GraphicsSettings.currentRenderPipeline==null) Render(camera); }
        private void Render(Camera camera)
        {
            if(!instances || instances.batches==null || !instances.settings || !SystemInfo.supportsInstancing) return;
            if(camera!=viewCamera && camera.cameraType!=CameraType.SceneView) return;
            if(artProfile)Shader.SetGlobalVector(TerrainLook,artProfile.TerrainLook);
            GeometryUtility.CalculateFrustumPlanes(camera,frustumPlanes);
            float detailSquared=instances.settings.detailDistance*instances.settings.detailDistance;
            // The overview cache is valid only when every instance is outside the detail distance.
            bool overview=instances.overviewBatches!=null && instances.overviewBatches.Length>0 &&
                instances.bounds.SqrDistance(camera.transform.position)>detailSquared;
            var batches=overview?instances.overviewBatches:instances.batches;
            foreach(var batch in batches) {
                if(!batch.mesh || !batch.material || batch.matrices==null || batch.matrices.Length==0)continue;
                if(!GeometryUtility.TestPlanesAABB(frustumPlanes,batch.bounds))continue;
                bool far=(camera.transform.position-batch.bounds.center).sqrMagnitude>detailSquared;
                if(!batch.always && batch.distant!=far)continue;
                Graphics.DrawMeshInstanced(batch.mesh,batch.submesh,batch.material,batch.matrices,batch.matrices.Length,null,
                    ShadowCastingMode.Off,true,gameObject.layer,camera);
            }
        }
    }
}


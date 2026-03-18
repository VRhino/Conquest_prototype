using Unity.Entities;
using UnityEngine;

/// <summary>
/// Sistema que crea la entidad de cámara y asigna el target automáticamente al héroe local (IsLocalPlayer).
/// </summary>
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class CameraBootstrapSystem : SystemBase
{
    // Constantes de configuración de cámara
    private const float CAMERA_INITIAL_ZOOM = 2f;
    private const float CAMERA_OFFSET_Y = 1.8f;
    private const float CAMERA_OFFSET_X = 0f;
    private const float CAMERA_OFFSET_Z = 1f;
    private const float CAMERA_MIN_ZOOM = 1f;
    private const float CAMERA_MAX_ZOOM = 10f;
    private const float CAMERA_ZOOM_SPEED = 10f;
    private const float CAMERA_ROTATION_SENSITIVITY = 35f;

    protected override void OnUpdate()
    {
        EntityQuery heroQuery = SystemAPI.QueryBuilder().WithAll<IsLocalPlayer>().Build();
        if (heroQuery.IsEmpty)
            return;

        Entity hero = heroQuery.GetSingletonEntity();

        EntityQuery cameraQuery = SystemAPI.QueryBuilder()
            .WithAll<CameraTargetComponent, CameraSettingsComponent, CameraStateComponent>()
            .Build();

        Entity cameraEntity;
        if (cameraQuery.IsEmpty)
        {
            cameraEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(cameraEntity, new CameraTargetComponent {
                followTarget = hero,
                zoomLevel = CAMERA_INITIAL_ZOOM,
                offset = new Unity.Mathematics.float3(CAMERA_OFFSET_X, CAMERA_OFFSET_Y, CAMERA_OFFSET_Z)
            });
            EntityManager.AddComponentData(cameraEntity, new CameraSettingsComponent
            {
                minZoom = CAMERA_MIN_ZOOM,
                maxZoom = CAMERA_MAX_ZOOM,
                zoomSpeed = CAMERA_ZOOM_SPEED,
                rotationSensitivity = CAMERA_ROTATION_SENSITIVITY
            });
            EntityManager.AddComponentData(cameraEntity, new CameraStateComponent
            {
                // Estado inicial si es necesario
            });
        }
        else
        {
            cameraEntity = cameraQuery.GetSingletonEntity();
            var camTarget = EntityManager.GetComponentData<CameraTargetComponent>(cameraEntity);
            if (camTarget.followTarget != hero)
            {
                EntityManager.SetComponentData(cameraEntity, new CameraTargetComponent { followTarget = hero });
            }
        }
    }
}

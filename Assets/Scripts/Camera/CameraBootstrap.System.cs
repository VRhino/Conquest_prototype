using Unity.Entities;
using UnityEngine;

/// <summary>
/// Sistema que crea la entidad de cámara y asigna el target automáticamente al héroe local (IsLocalPlayer).
/// </summary>
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class CameraBootstrapSystem : SystemBase
{
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
                zoomLevel = 8f, // Distancia inicial de la cámara
                offset = new Unity.Mathematics.float3(0f, 2.5f, 0f) // Altura sobre el héroe
            });
            EntityManager.AddComponentData(cameraEntity, new CameraSettingsComponent
            {
                minZoom = 1f,
                maxZoom = 20f,
                zoomSpeed = 10f,
                rotationSensitivity = 100f
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

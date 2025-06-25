using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// MonoBehaviour that controls the third person camera and follows the
/// local player's hero entity.
/// </summary>
public class HeroCameraController : MonoBehaviour
{
    EntityManager _entityManager;
    Entity _cameraEntity;
    Entity _heroEntity;
    float _yaw;

    void Awake()
    {
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
    }

    void Update()
    {
        if (_cameraEntity == Entity.Null || !_entityManager.Exists(_cameraEntity))
            FindCameraEntity();

        if (_cameraEntity == Entity.Null || !_entityManager.Exists(_cameraEntity))
            return;

        var camTarget = _entityManager.GetComponentData<CameraTargetComponent>(_cameraEntity);
        var settings = _entityManager.GetComponentData<CameraSettingsComponent>(_cameraEntity);

        // Validate hero entity
        if (_heroEntity == Entity.Null || !_entityManager.Exists(_heroEntity))
            _heroEntity = camTarget.followTarget;
        if (_heroEntity == Entity.Null || !_entityManager.Exists(_heroEntity))
            return;

        var life = _entityManager.GetComponentData<HeroLifeComponent>(_heroEntity);
        var state = _entityManager.GetComponentData<CameraStateComponent>(_cameraEntity);
        if (!life.isAlive)
        {
            state.state = CameraState.Spectator;
            _entityManager.SetComponentData(_cameraEntity, state);
            return;
        }

        // Input handling
        _yaw += Input.GetAxis("Mouse X") * settings.rotationSensitivity;
        camTarget.zoomLevel = math.clamp(
            camTarget.zoomLevel - Input.GetAxis("Mouse ScrollWheel") * settings.zoomSpeed,
            settings.minZoom,
            settings.maxZoom);
        camTarget.tacticalMode = Input.GetKey(KeyCode.LeftAlt);
        state.state = camTarget.tacticalMode ? CameraState.Tactical : CameraState.Normal;

        _entityManager.SetComponentData(_cameraEntity, camTarget);
        _entityManager.SetComponentData(_cameraEntity, state);

        var heroTransform = _entityManager.GetComponentData<LocalTransform>(_heroEntity);

        float3 offset = camTarget.offset;
        if (camTarget.tacticalMode)
            offset += new float3(0f, 3f, -3f);

        quaternion rot = quaternion.Euler(0f, math.radians(_yaw), 0f);
        float3 desired = heroTransform.Position + math.mul(rot, new float3(0f, offset.y, -camTarget.zoomLevel) + new float3(offset.x, 0f, offset.z));

        // Raycast to avoid clipping
        Vector3 from = heroTransform.Position + new float3(0f, offset.y, 0f);
        Vector3 to = desired;
        Vector3 dir = to - from;
        if (Physics.Raycast(from, dir.normalized, out RaycastHit hit, dir.magnitude))
        {
            desired = hit.point;
        }

        transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * 5f);
        transform.rotation = Quaternion.LookRotation(heroTransform.Position - transform.position);
    }

    void FindCameraEntity()
    {
        var query = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CameraTargetComponent>(),
            ComponentType.ReadOnly<IsLocalPlayer>());
        using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        if (entities.Length > 0)
            _cameraEntity = entities[0];
    }
}

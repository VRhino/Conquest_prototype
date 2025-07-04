using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

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
    float _mouseX;
    float _scroll;
    bool _tacticalMode;

    private void OnEnable()
    {
        // Suscribe input events
        var playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            playerInput.actions["Look"].performed += ctx => _mouseX = ctx.ReadValue<Vector2>().x;
            playerInput.actions["Zoom"].performed += ctx => _scroll = ctx.ReadValue<float>();
            playerInput.actions["Tactical"].performed += ctx => _tacticalMode = ctx.ReadValue<float>() > 0.5f;
        }
    }

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

        // Input handling (nuevo Input System)
        _yaw += _mouseX * settings.rotationSensitivity * Time.deltaTime;
        camTarget.zoomLevel = math.clamp(
            camTarget.zoomLevel - _scroll * settings.zoomSpeed * Time.deltaTime,
            settings.minZoom,
            settings.maxZoom);
        camTarget.tacticalMode = _tacticalMode;
        state.state = camTarget.tacticalMode ? CameraState.Tactical : CameraState.Normal;

        _entityManager.SetComponentData(_cameraEntity, camTarget);
        _entityManager.SetComponentData(_cameraEntity, state);

        var heroTransform = _entityManager.GetComponentData<LocalTransform>(_heroEntity);

        float3 offset = camTarget.offset;
        if (camTarget.tacticalMode)
            offset += new float3(0f, 3f, -3f);

        quaternion rot = quaternion.Euler(0f, math.radians(_yaw), 0f);
        float3 desiredFloat = heroTransform.Position + math.mul(rot, new float3(0f, offset.y, -camTarget.zoomLevel) + new float3(offset.x, 0f, offset.z));
        Vector3 desired = (Vector3)desiredFloat;

        // Raycast to avoid clipping
        Vector3 from = (Vector3)(heroTransform.Position + new float3(0f, offset.y, 0f));
        Vector3 to = desired;
        Vector3 dir = to - from;
        if (Physics.Raycast(from, dir.normalized, out RaycastHit hit, dir.magnitude))
        {
            desired = hit.point;
        }

        transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * 5f);
        transform.rotation = Quaternion.LookRotation((Vector3)heroTransform.Position - transform.position);

        // Reset input deltas
        _mouseX = 0f;
        _scroll = 0f;
    }

    void FindCameraEntity()
    {
        var query = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CameraTargetComponent>(),
            ComponentType.ReadOnly<CameraSettingsComponent>(),
            ComponentType.ReadOnly<CameraStateComponent>());
        using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        if (entities.Length > 0)
            _cameraEntity = entities[0];
    }

    #region Public API for Animation Controller

    /// <summary>
    /// Gets the normalised forward vector of the camera with the Y value zeroed.
    /// Required for SamplePlayerAnimationController_ECS compatibility.
    /// </summary>
    /// <returns>The normalised forward vector of the camera with the Y value zeroed.</returns>
    public Vector3 GetCameraForwardZeroedYNormalised()
    {
        var forward = transform.forward;
        return new Vector3(forward.x, 0, forward.z).normalized;
    }

    /// <summary>
    /// Gets the normalised right vector of the camera with the Y value zeroed.
    /// Required for SamplePlayerAnimationController_ECS compatibility.
    /// </summary>
    /// <returns>The normalised right vector of the camera with the Y value zeroed.</returns>
    public Vector3 GetCameraRightZeroedYNormalised()
    {
        var right = transform.right;
        return new Vector3(right.x, 0, right.z).normalized;
    }

    /// <summary>
    /// Gets the X value of the camera tilt.
    /// Required for SamplePlayerAnimationController_ECS compatibility.
    /// </summary>
    /// <returns>The X value of the camera tilt.</returns>
    public float GetCameraTiltX()
    {
        return transform.eulerAngles.x;
    }

    #endregion
}

using UnityEngine;
using UnityEngine.InputSystem;
using static DialogueUIState;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// MonoBehaviour that controls the third person camera and follows the
/// local player's hero entity.
/// </summary>
public class HeroCameraController : MonoBehaviour
{
    [Header("Camera Control")]
    [Tooltip("Si está en true, la cámara queda estática y el héroe se mueve libremente. Si está en false, la cámara sigue al héroe.")]
    [SerializeField] public bool staticCamera = false;

    [Header("Camera Settings (Override)")]
    [Tooltip("Sensibilidad de rotación de la cámara (si se usa override)")]
    [SerializeField] private float rotationSensitivityOverride = 0f;
    [Tooltip("Distancia mínima de la cámara (si se usa override)")]
    [SerializeField] private float minZoomOverride = 0f;
    [SerializeField] public bool disableCameraFollow = false;

    // Permite acceso global para pausar la cámara desde UI/dialogue
    private static HeroCameraController _instance;
    public static HeroCameraController Instance => _instance;

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    EntityManager _entityManager;
    Entity _cameraEntity;
    Entity _heroEntity;
    // Ángulos de referencia (de la cámara de ejemplo)
    private const float REFERENCE_YAW = 2f;
    private const float REFERENCE_PITCH = 8f;
    // Offset global de referencia (posición cámara - posición héroe)
    private static readonly Vector3 REFERENCE_OFFSET_GLOBAL = new Vector3(0f, 1.8f, -3.478333f);

    float _yaw = REFERENCE_YAW;
    float _pitch = REFERENCE_PITCH;
    Vector3 _offsetLocal;
    float _mouseX;
    float _mouseY; // Nuevo: input vertical
    float _scroll;
    bool _tacticalMode;

    private void OnEnable()
    {
        _instance = this;
        // Suscribe input events
        var playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            playerInput.actions["Look"].performed += ctx => {
                // Si el diálogo está abierto, ignorar input de cámara
                if (DialogueUIState.IsDialogueOpen) return;
                var look = ctx.ReadValue<Vector2>();
                _mouseX = look.x;
                _mouseY = look.y;
            };
            playerInput.actions["Zoom"].performed += ctx => {
                if (DialogueUIState.IsDialogueOpen) return;
                _scroll = ctx.ReadValue<float>();
            };
            // playerInput.actions["Tactical"].performed += ctx => _tacticalMode = ctx.ReadValue<float>() > 0.5f;
        }
    }

    void Awake()
    {
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        // Calcular el offset local respecto a la rotación de referencia
        Quaternion referenceRot = Quaternion.Euler(REFERENCE_PITCH, REFERENCE_YAW, 0f);
        _offsetLocal = Quaternion.Inverse(referenceRot) * REFERENCE_OFFSET_GLOBAL;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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
        float rotSens = rotationSensitivityOverride > 0f ? rotationSensitivityOverride : settings.rotationSensitivity;
        float minZoom = minZoomOverride > 0f ? minZoomOverride : settings.minZoom;
        _yaw += _mouseX * rotSens * Time.deltaTime;
        _pitch -= _mouseY * rotSens * Time.deltaTime;
        _pitch = Mathf.Clamp(_pitch, -60f, 80f);
        camTarget.zoomLevel = math.clamp(
            camTarget.zoomLevel - _scroll * settings.zoomSpeed * Time.deltaTime,
            minZoom,
            settings.maxZoom);
        // camTarget.tacticalMode = _tacticalMode;
        // state.state = camTarget.tacticalMode ? CameraState.Tactical : CameraState.Normal;

        _entityManager.SetComponentData(_cameraEntity, camTarget);
        _entityManager.SetComponentData(_cameraEntity, state);


        // Intentar seguir un punto específico del visual (CameraFollowPoint) si existe
        Vector3? followPos = null;
        if (_entityManager.HasComponent<HeroVisualInstance>(_heroEntity))
        {
            var visualInstance = _entityManager.GetComponentData<HeroVisualInstance>(_heroEntity);
            var visualObj = FindVisualByInstanceId(visualInstance.visualInstanceId);
            if (visualObj != null)
            {
                var followPoint = visualObj.transform.Find("CameraFollowPoint");
                if (followPoint != null)
                    followPos = followPoint.position;
                else
                    followPos = visualObj.transform.position;
            }
        }
        if (!followPos.HasValue)
            followPos = (Vector3)_entityManager.GetComponentData<LocalTransform>(_heroEntity).Position;

        if (!staticCamera)
        {
            // Calcula la rotación a partir de _pitch y _yaw (ambos controlados por el mouse)
            quaternion rot = quaternion.Euler(math.radians(_pitch), math.radians(_yaw), 0f);
            Vector3 offset = (Vector3)math.mul(rot, _offsetLocal * camTarget.zoomLevel);
            Vector3 desired = followPos.Value + offset;

            // Raycast para evitar clipping
            Vector3 from = followPos.Value + new Vector3(0f, offset.y, 0f);
            Vector3 to = desired;
            Vector3 dir = to - from;
            if (Physics.Raycast(from, dir.normalized, out RaycastHit hit, dir.magnitude))
            {
                desired = hit.point;
            }
            if (disableCameraFollow || DialogueUIState.IsDialogueOpen)
            {
                desired = transform.position;
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * 5f);
                transform.rotation = Quaternion.LookRotation(followPos.Value - transform.position);
            }
        }
        // Si staticCamera == true, la cámara se queda en su posición y rotación actuales

        // Reset input deltas
        _mouseX = 0f;
        _mouseY = 0f; // Nuevo: resetear input vertical
        _scroll = 0f;
    }

    /// <summary>
    /// Permite pausar/reanudar el seguimiento de cámara desde UI/dialogue
    /// </summary>
    public void SetCameraFollowEnabled(bool enabled)
    {
        disableCameraFollow = !enabled;
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
    /// Busca el GameObject visual por su InstanceID.
    /// </summary>
    private GameObject FindVisualByInstanceId(int instanceId)
    {
        if (instanceId == 0) return null;
        var allVisuals = GameObject.FindObjectsOfType<ConquestTactics.Visual.EntityVisualSync>();
        foreach (var visual in allVisuals)
        {
            if (visual.gameObject.GetInstanceID() == instanceId)
                return visual.gameObject;
        }
        return null;
    }

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

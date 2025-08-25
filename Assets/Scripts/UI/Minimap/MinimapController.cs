using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controlador minimalista del minimapa que usa el patrón de HeroDetail3DPreview.
/// Maneja una cámara ortográfica que sigue al player y renderiza a una RawImage siempre visible.
/// </summary>
public class MinimapController : MonoBehaviour
{
    [Header("Camera Setup")]
    [SerializeField] private Camera minimapCamera;
    [SerializeField] private RenderTexture renderTexture;
    [SerializeField] private RawImage minimapDisplay;

    [Header("Camera Settings")]
    [SerializeField] private float cameraHeight = 20f;
    [SerializeField] private float orthographicSize = 15f;
    [SerializeField] private LayerMask minimapLayers = -1;

    [Header("Zoom Controls")]
    [SerializeField] private Button zoomInButton;
    [SerializeField] private Button zoomOutButton;
    [SerializeField] private float minZoom = 5f;
    [SerializeField] private float maxZoom = 30f;
    [SerializeField] private float zoomStep = 2f;

    [Header("Player Tracking")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float followSmoothness = 5f;

    // Referencias del sistema
    private Transform playerTransform;
    private Vector3 cameraOffset;

    #region Unity Lifecycle

    private void Awake()
    {
        InitializeCamera();
        InitializeRenderTexture();
    }

    private void Start()
    {
        FindPlayerTransform();
        EnableMinimap();
        InitializeZoomButtons();
    }

    private void LateUpdate()
    {
        UpdateCameraPosition();
    }

    #endregion

    #region Initialization

    private void InitializeCamera()
    {
        if (minimapCamera == null)
        {
            minimapCamera = GetComponent<Camera>();
            if (minimapCamera == null)
            {
                Debug.LogError("[MinimapController] No camera found on this GameObject");
                return;
            }
        }

        // Configurar cámara para minimapa
        minimapCamera.orthographic = true;
        minimapCamera.orthographicSize = orthographicSize;
        minimapCamera.cullingMask = minimapLayers;
        minimapCamera.clearFlags = CameraClearFlags.SolidColor;
        minimapCamera.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f); // Fondo oscuro

        // Orientación desde arriba
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        cameraOffset = new Vector3(0f, cameraHeight, 0f);
    }

    private void InitializeRenderTexture()
    {
        if (renderTexture == null)
        {
            // Crear RenderTexture pequeño para minimapa
            renderTexture = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32);
            renderTexture.name = "MinimapRenderTexture";
        }

        if (minimapCamera != null)
        {
            minimapCamera.targetTexture = renderTexture;
        }

        if (minimapDisplay != null)
        {
            minimapDisplay.texture = renderTexture;
        }
    }

    #endregion

    #region Player Tracking

    private void FindPlayerTransform()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObject != null)
        {
            playerTransform = playerObject.transform;
            Debug.Log($"[MinimapController] Player found: {playerObject.name}");
        }
        else
        {
            Debug.LogWarning($"[MinimapController] No GameObject found with tag: {playerTag}");
        }
    }

    private void UpdateCameraPosition()
    {
        if (playerTransform == null)
        {
            // Intentar encontrar el player si no lo tenemos
            FindPlayerTransform();
            return;
        }

        // Posición objetivo: player + offset hacia arriba
        Vector3 targetPosition = playerTransform.position + cameraOffset;

        // Smooth following (opcional, se puede quitar para seguimiento instantáneo)
        if (followSmoothness > 0f)
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition,
                followSmoothness * Time.deltaTime);
        }
        else
        {
            transform.position = targetPosition;
        }
    }

    #endregion

    #region Public API
    /// <summary>
    /// Inicializa los botones de zoom y asigna los listeners.
    /// </summary>
    private void InitializeZoomButtons()
    {
        if (zoomInButton != null)
            zoomInButton.onClick.AddListener(ZoomIn);
        if (zoomOutButton != null)
            zoomOutButton.onClick.AddListener(ZoomOut);
    }

    /// <summary>
    /// Aumenta el zoom del minimapa.
    /// </summary>
    private void ZoomIn()
    {
        float newSize = Mathf.Max(minZoom, orthographicSize - zoomStep);
        SetOrthographicSize(newSize);
    }

    /// <summary>
    /// Disminuye el zoom del minimapa.
    /// </summary>
    private void ZoomOut()
    {
        float newSize = Mathf.Min(maxZoom, orthographicSize + zoomStep);
        SetOrthographicSize(newSize);
    }

    /// <summary>
    /// Activa el minimapa.
    /// </summary>
    public void EnableMinimap()
    {
        if (minimapCamera != null)
        {
            minimapCamera.enabled = true;
        }

        if (minimapDisplay != null)
        {
            minimapDisplay.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Desactiva el minimapa.
    /// </summary>
    public void DisableMinimap()
    {
        if (minimapCamera != null)
        {
            minimapCamera.enabled = false;
        }

        if (minimapDisplay != null)
        {
            minimapDisplay.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Configura el tamaño ortográfico de la cámara (zoom).
    /// </summary>
    /// <param name="size">Nuevo tamaño ortográfico</param>
    public void SetOrthographicSize(float size)
    {
        orthographicSize = size;
        if (minimapCamera != null)
        {
            minimapCamera.orthographicSize = orthographicSize;
        }
    }

    /// <summary>
    /// Configura la altura de la cámara sobre el player.
    /// </summary>
    /// <param name="height">Nueva altura</param>
    public void SetCameraHeight(float height)
    {
        cameraHeight = height;
        cameraOffset = new Vector3(0f, cameraHeight, 0f);
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        if (renderTexture != null)
        {
            renderTexture.Release();
        }
    }

    #endregion
}

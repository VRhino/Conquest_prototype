using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Maneja el preview 3D del héroe usando RenderTexture con sistema de rotación orbital.
/// Sistema simplificado que observa el modelo existente del héroe en escena.
/// </summary>
public class HeroDetail3DPreview : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [Header("Camera Setup")]
    [SerializeField] private Camera previewCamera;
    [SerializeField] private RenderTexture renderTexture;
    [SerializeField] private RawImage previewDisplay;
    
    [Header("Rotation Settings")]
    [SerializeField] private float mouseSensitivity = 1.0f;
    [SerializeField] private float verticalAngleLimit = 80f;
    [SerializeField] private float cameraDistance = 3f;
    
    [Header("Initial Setup")]
    [SerializeField] private Vector3 initialCameraPosition = new Vector3(0, 1.5f, 2f);
    [SerializeField] private Vector3 cameraTarget = new Vector3(0, 1f, 0);
    
    // Estado de rotación
    private Vector2 currentRotation;
    private Vector2 initialRotation;
    private bool isDragging = false;
    
    // Referencias del sistema
    private Transform heroTransform;
    
    #region Unity Lifecycle
    
    private void Awake()
    {
        InitializeCamera();
    }
    
    private void Start()
    {
        SetupInitialRotation();
    }
    
    private void Update()
    {
        // Mantener la cámara actualizada si el héroe se mueve
        if (heroTransform != null && previewCamera != null && previewCamera.enabled)
        {
            UpdateCameraTarget();
        }
    }
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// Habilita el preview 3D del héroe.
    /// </summary>
    public void EnablePreview()
    {
        FindHeroInScene();
        
        if (previewCamera != null)
        {
            previewCamera.enabled = true;
            ResetCameraPosition();
            Debug.Log("[HeroDetail3DPreview] Preview enabled");
        }
    }
    
    /// <summary>
    /// Deshabilita el preview 3D del héroe.
    /// </summary>
    public void DisablePreview()
    {
        if (previewCamera != null)
        {
            previewCamera.enabled = false;
        }
        
        isDragging = false;
        Debug.Log("[HeroDetail3DPreview] Preview disabled");
    }
    
    /// <summary>
    /// Resetea la cámara a la posición inicial.
    /// </summary>
    public void ResetCameraPosition()
    {
        currentRotation = initialRotation;
        ApplyOrbitalRotation();
    }
    
    /// <summary>
    /// Configura la máscara de capas para la cámara.
    /// </summary>
    /// <param name="layerMask">Máscara de capas para renderizar</param>
    public void SetCullingMask(LayerMask layerMask)
    {
        if (previewCamera != null)
        {
            previewCamera.cullingMask = layerMask;
        }
    }
    
    #endregion
    
    #region Camera Setup
    
    private void InitializeCamera()
    {
        // Crear RenderTexture si no existe
        if (renderTexture == null)
        {
            renderTexture = new RenderTexture(512, 512, 16);
            renderTexture.Create();
        }
        
        // Configurar cámara
        if (previewCamera != null)
        {
            previewCamera.targetTexture = renderTexture;
            
            // Intentar usar layer hero_preview, si no existe usar Default
            int heroLayer = LayerMask.NameToLayer("hero_preview");
            if (heroLayer == -1)
            {
                Debug.LogWarning("[HeroDetail3DPreview] Layer 'hero_preview' not found, using Default layer");
                heroLayer = 0; // Default layer
            }
            
            previewCamera.cullingMask = 1 << heroLayer;
            previewCamera.enabled = false; // Inicialmente deshabilitada
        }
        
        // Conectar RawImage
        if (previewDisplay != null)
        {
            previewDisplay.texture = renderTexture;
        }
        
        Debug.Log("[HeroDetail3DPreview] Camera initialized");
    }
    
    private void SetupInitialRotation()
    {
        // Calcular rotación inicial basada en la posición inicial de la cámara
        Vector3 directionToCamera = (initialCameraPosition - cameraTarget).normalized;
        initialRotation.x = Mathf.Atan2(directionToCamera.x, directionToCamera.z) * Mathf.Rad2Deg;
        initialRotation.y = Mathf.Asin(directionToCamera.y) * Mathf.Rad2Deg;
        
        currentRotation = initialRotation;
    }
    
    #endregion
    
    #region Hero Detection
    
    private void FindHeroInScene()
    {
        // Buscar el héroe activo en la escena
        GameObject heroObject = GameObject.FindWithTag("Player");
        
        if (heroObject != null)
        {
            heroTransform = heroObject.transform;
            UpdateCameraTarget();
            
            Debug.Log($"[HeroDetail3DPreview] Hero found at: {heroTransform.position}");
        }
        else
        {
            Debug.LogWarning("[HeroDetail3DPreview] No hero found with 'Player' tag");
        }
    }
    
    private void UpdateCameraTarget()
    {
        if (heroTransform != null)
        {
            // Ajustar el target de la cámara basado en la posición del héroe
            cameraTarget = heroTransform.position + Vector3.up * 1f;
        }
    }
    
    #endregion
    
    #region Rotation System
    
    private void ApplyOrbitalRotation()
    {
        if (previewCamera == null) return;
        
        // Calcular nueva posición orbital
        Quaternion rotation = Quaternion.Euler(currentRotation.y, currentRotation.x, 0);
        Vector3 direction = rotation * Vector3.forward;
        Vector3 newPosition = cameraTarget - direction * cameraDistance;
        
        // Aplicar posición y rotación a la cámara
        previewCamera.transform.position = newPosition;
        previewCamera.transform.LookAt(cameraTarget);
    }
    
    #endregion
    
    #region Input Handlers
    
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            isDragging = true;
            Debug.Log("[HeroDetail3DPreview] Started dragging");
        }
    }
    
    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            isDragging = false;
            Debug.Log("[HeroDetail3DPreview] Stopped dragging");
        }
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        if (isDragging)
        {
            // Convertir el delta del mouse a sensibilidad configurada
            Vector2 mouseDelta = eventData.delta * mouseSensitivity * 0.1f; // Factor de ajuste
            
            // Rotación horizontal sin límites
            currentRotation.x += mouseDelta.x;
            
            // Rotación vertical con límites
            currentRotation.y = Mathf.Clamp(currentRotation.y - mouseDelta.y, -verticalAngleLimit, verticalAngleLimit);
            
            ApplyOrbitalRotation();
        }
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

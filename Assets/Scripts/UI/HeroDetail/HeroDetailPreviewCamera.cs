using UnityEngine;

namespace UI.HeroDetail
{
    /// <summary>
    /// Maneja una cámara dedicada para el preview 3D del héroe en la interfaz.
    /// Esta cámara se posiciona y orienta para mostrar el modelo existente del héroe
    /// sin duplicar la geometría.
    /// </summary>
    public class HeroDetailPreviewCamera : MonoBehaviour
    {
        [Header("Preview Camera")]
        [SerializeField] private Camera _previewCamera;
        [SerializeField] private RenderTexture _renderTexture;
        
        [Header("Camera Settings")]
        [SerializeField] private Vector3 _offsetFromHero = new Vector3(2f, 1.5f, -3f);
        [SerializeField] private bool _followHeroPosition = true;
        [SerializeField] private bool _enableMouseRotation = true;
        [SerializeField] private float _rotationSpeed = 2f;
        [SerializeField] private LayerMask _heroPreviewMask = -1;
        
        [Header("Zoom Settings")]
        [SerializeField] private float _zoomSpeed = 2f;
        [SerializeField] private float _minDistance = 1f;
        [SerializeField] private float _maxDistance = 5f;
        
        private Transform _heroTransform;
        private bool _isPreviewActive = false;
        private Vector3 _currentOffset;
        private Vector3 _lookAtOffset = new Vector3(0f, 1.5f, 0f);
        
        // Para controles de mouse
        private bool _isDragging = false;
        private Vector3 _lastMousePosition;
        
        public RenderTexture RenderTexture => _renderTexture;
        public bool IsPreviewActive => _isPreviewActive;
        
        private void Awake()
        {
            // Validar componentes requeridos
            if (_previewCamera == null)
                _previewCamera = GetComponent<Camera>();
                
            if (_previewCamera == null)
            {
                Debug.LogError("[HeroDetailPreviewCamera] No se encontró componente Camera");
                return;
            }
            
            // Inicializar offset
            _currentOffset = _offsetFromHero;
            
            // Configurar la cámara por defecto
            _previewCamera.cullingMask = _heroPreviewMask;
            _previewCamera.enabled = false; // Inicialmente desactivada
            
            // Crear RenderTexture si no existe
            if (_renderTexture == null)
            {
                _renderTexture = new RenderTexture(512, 512, 16);
                _renderTexture.Create();
            }
            
            _previewCamera.targetTexture = _renderTexture;
        }
        
        private void Update()
        {
            if (!_isPreviewActive || _heroTransform == null)
                return;
            
            HandleMouseInput();
            UpdateCameraPosition();
        }
        
        /// <summary>
        /// Configura el objetivo del héroe para el preview.
        /// </summary>
        /// <param name="heroTransform">Transform del GameObject del héroe</param>
        public void SetHeroTarget(Transform heroTransform)
        {
            _heroTransform = heroTransform;
            
            if (_heroTransform != null && _previewCamera != null)
            {
                UpdateCameraPosition();
                Debug.Log($"[HeroDetailPreviewCamera] Hero target set: {_heroTransform.name}");
            }
        }
        
        /// <summary>
        /// Activa el preview de la cámara.
        /// </summary>
        public void EnablePreview()
        {
            if (_previewCamera != null && _heroTransform != null)
            {
                _isPreviewActive = true;
                _previewCamera.enabled = true;
                Debug.Log("[HeroDetailPreviewCamera] Preview enabled");
            }
            else
            {
                Debug.LogWarning("[HeroDetailPreviewCamera] Cannot enable preview - missing camera or hero target");
            }
        }
        
        /// <summary>
        /// Desactiva el preview de la cámara.
        /// </summary>
        public void DisablePreview()
        {
            _isPreviewActive = false;
            if (_previewCamera != null)
            {
                _previewCamera.enabled = false;
                Debug.Log("[HeroDetailPreviewCamera] Preview disabled");
            }
        }
        
        /// <summary>
        /// Actualiza la posición de la cámara basada en el héroe y offset actual.
        /// </summary>
        private void UpdateCameraPosition()
        {
            if (_heroTransform == null || _previewCamera == null)
                return;
            
            // Calcular posición deseada
            Vector3 heroPosition = _heroTransform.position;
            Vector3 desiredPosition = heroPosition + _currentOffset;
            
            // Aplicar suavizado si está siguiendo al héroe
            if (_followHeroPosition)
            {
                transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * 2f);
            }
            else
            {
                transform.position = desiredPosition;
            }
            
            // Hacer que la cámara mire al héroe
            Vector3 lookAtPosition = heroPosition + _lookAtOffset;
            transform.LookAt(lookAtPosition);
        }
        
        /// <summary>
        /// Maneja la entrada del mouse para rotación y zoom.
        /// </summary>
        private void HandleMouseInput()
        {
            if (_heroTransform == null)
                return;
            
            // Detección de clic/drag sobre la cámara (esto se manejará desde el UI)
            if (_enableMouseRotation && Input.GetMouseButtonDown(0))
            {
                _isDragging = true;
                _lastMousePosition = Input.mousePosition;
            }
            
            if (_isDragging && Input.GetMouseButton(0))
            {
                // Calcular delta del mouse
                Vector3 mouseDelta = Input.mousePosition - _lastMousePosition;
                float mouseX = mouseDelta.x * _rotationSpeed * Time.deltaTime;
                
                // Rotar el offset alrededor del héroe
                _currentOffset = Quaternion.AngleAxis(mouseX, Vector3.up) * _currentOffset;
                
                _lastMousePosition = Input.mousePosition;
            }
            
            if (Input.GetMouseButtonUp(0))
            {
                _isDragging = false;
            }
            
            // Zoom con scroll del mouse
            float scrollWheel = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scrollWheel) > 0.01f)
            {
                float currentDistance = _currentOffset.magnitude;
                float newDistance = Mathf.Clamp(currentDistance - scrollWheel * _zoomSpeed, 
                                              _minDistance, _maxDistance);
                
                _currentOffset = _currentOffset.normalized * newDistance;
            }
        }
        
        /// <summary>
        /// Resetea la posición de la cámara al offset por defecto.
        /// </summary>
        public void ResetCameraPosition()
        {
            _currentOffset = _offsetFromHero;
            UpdateCameraPosition();
            Debug.Log("[HeroDetailPreviewCamera] Camera position reset");
        }
        
        /// <summary>
        /// Configura la máscara de capas que verá la cámara.
        /// </summary>
        /// <param name="layerMask">Máscara de capas</param>
        public void SetCullingMask(LayerMask layerMask)
        {
            _heroPreviewMask = layerMask;
            if (_previewCamera != null)
            {
                _previewCamera.cullingMask = _heroPreviewMask;
            }
        }
        
        private void OnDestroy()
        {
            if (_renderTexture != null)
            {
                _renderTexture.Release();
            }
        }
        
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_heroTransform != null)
            {
                // Visualizar la posición objetivo de la cámara
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(_heroTransform.position + _currentOffset, 0.2f);
                
                // Línea desde la cámara al héroe
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, _heroTransform.position + _lookAtOffset);
            }
        }
#endif
    }
}

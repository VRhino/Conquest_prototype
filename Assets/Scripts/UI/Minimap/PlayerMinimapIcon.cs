using UnityEngine;

/// <summary>
/// Icono del player en el minimapa que muestra posición y dirección.
/// Se coloca como hijo del player o como GameObject independiente que lo sigue.
/// </summary>
public class PlayerMinimapIcon : MonoBehaviour
{
    [Header("Icon Settings")]
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private Sprite playerIconSprite;
    [SerializeField] private Color iconColor = Color.white;
    [SerializeField] private float iconSize = 1f;
    
    [Header("Player Tracking")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool followPlayerPosition = true;
    [SerializeField] private bool followPlayerRotation = true;
    [SerializeField] private Vector3 positionOffset = Vector3.zero;
    [SerializeField] private float iconHeight = 15f; // Altura fija para vista aérea
    
    // Referencias
    private Transform playerTransform;
    
    #region Unity Lifecycle
    
    private void Awake()
    {
        InitializeIcon();
    }
    
    private void Start()
    {
        FindPlayerTransform();
        SetupIconAppearance();
    }
    
    private void LateUpdate()
    {
        UpdateIconTransform();
    }
    
    #endregion
    
    #region Initialization
    
    private void InitializeIcon()
    {
        // Obtener o crear SpriteRenderer
        if (iconRenderer == null)
        {
            iconRenderer = GetComponent<SpriteRenderer>();
            if (iconRenderer == null)
            {
                iconRenderer = gameObject.AddComponent<SpriteRenderer>();
            }
        }
        
        // Configurar layer para minimapa
        gameObject.layer = LayerMask.NameToLayer("minimap"); // Ajustar según tu configuración
    }
    
    private void SetupIconAppearance()
    {
        if (iconRenderer == null) return;
        
        iconRenderer.sprite = playerIconSprite;
        iconRenderer.color = iconColor;
        iconRenderer.sortingOrder = 10; // Asegurar que aparezca encima de otros elementos
        
        // Configurar tamaño
        transform.localScale = Vector3.one * iconSize;
    }
    
    #endregion
    
    #region Player Tracking
    
    private void FindPlayerTransform()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObject != null)
        {
            playerTransform = playerObject.transform;
            Debug.Log($"[PlayerMinimapIcon] Player found: {playerObject.name}");
        }
        else
        {
            Debug.LogWarning($"[PlayerMinimapIcon] No GameObject found with tag: {playerTag}");
        }
    }
    
    private void UpdateIconTransform()
    {
        if (playerTransform == null)
        {
            FindPlayerTransform();
            return;
        }
        
        // Actualizar posición
        if (followPlayerPosition)
        {
            // Usar altura fija para vista aérea del minimapa
            Vector3 playerPos = playerTransform.position;
            Vector3 iconPosition = new Vector3(playerPos.x, iconHeight, playerPos.z) + positionOffset;
            transform.position = iconPosition;
        }
        
        // Actualizar rotación para mostrar dirección del player
        if (followPlayerRotation)
        {
            // Para sprite 2D visto desde cámara ortográfica desde arriba:
            // El sprite debe rotar en el plano XZ, no en Z
            float playerYRotation = playerTransform.eulerAngles.y;
            // Rotar el sprite para que "mire" en la dirección correcta en el plano del minimapa
            transform.rotation = Quaternion.Euler(90f, playerYRotation, 0f);
        }
    }
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// Configura el sprite del icono del player.
    /// </summary>
    /// <param name="sprite">Nuevo sprite para el icono</param>
    public void SetIconSprite(Sprite sprite)
    {
        playerIconSprite = sprite;
        if (iconRenderer != null)
        {
            iconRenderer.sprite = playerIconSprite;
        }
    }
    
    /// <summary>
    /// Configura el color del icono del player.
    /// </summary>
    /// <param name="color">Nuevo color para el icono</param>
    public void SetIconColor(Color color)
    {
        iconColor = color;
        if (iconRenderer != null)
        {
            iconRenderer.color = iconColor;
        }
    }
    
    /// <summary>
    /// Configura el tamaño del icono.
    /// </summary>
    /// <param name="size">Nuevo tamaño del icono</param>
    public void SetIconSize(float size)
    {
        iconSize = size;
        transform.localScale = Vector3.one * iconSize;
    }
    
    /// <summary>
    /// Muestra u oculta el icono.
    /// </summary>
    /// <param name="visible">True para mostrar, false para ocultar</param>
    public void SetIconVisible(bool visible)
    {
        if (iconRenderer != null)
        {
            iconRenderer.enabled = visible;
        }
    }
    
    /// <summary>
    /// Configura manualmente el transform del player a seguir.
    /// Útil si el player se crea dinámicamente.
    /// </summary>
    /// <param name="playerTransform">Transform del player</param>
    public void SetPlayerTransform(Transform playerTransform)
    {
        this.playerTransform = playerTransform;
    }
    
    #endregion
}

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controlador visual para puntos de suministro que muestra información de control territorial.
/// Solo visual/informativo - sin interactividad.
/// </summary>
public class SupplyPointIconControllerUI : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private Side side;
    
    [Header("Visual Elements")]
    [SerializeField] private Image pointImage;
    
    // Colores predefinidos
    private static readonly Color AlliedColor = new Color(0.349f, 0.486f, 0.667f, 0.451f); // #597CAA con alpha 115
    private static readonly Color EnemyColor = new Color(0.698f, 0.259f, 0.259f, 0.686f);   // #B24242 con alpha 175
    
    #region Public Properties
    
    /// <summary>
    /// Obtiene el side configurado para este supply point.
    /// </summary>
    public Side Side => side;
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// Inicializa el supply point estableciendo su color según la relación ally/enemy.
    /// </summary>
    /// <param name="playerSide">Side del jugador para determinar si es aliado o enemigo</param>
    public void Initialize(Side playerSide)
    {
        if (pointImage == null)
        {
            Debug.LogWarning($"[SupplyPointControllerUI] Point image not assigned on {gameObject.name}");
            return;
        }
        
        // Determinar color basado en la comparación de sides
        Color targetColor = (playerSide == side) ? AlliedColor : EnemyColor;
        
        // Aplicar color
        pointImage.color = targetColor;
        
        // Log para debug
        string status = (playerSide == side) ? "Allied" : "Enemy";
        Debug.Log($"[SupplyPointControllerUI] {gameObject.name} initialized as {status} (Player: {playerSide}, Point: {side})");
    }
    
    /// <summary>
    /// Cambia manualmente el color del supply point.
    /// </summary>
    /// <param name="isAllied">True para color aliado, false para enemigo</param>
    public void SetColor(bool isAllied)
    {
        if (pointImage == null)
        {
            Debug.LogWarning($"[SupplyPointControllerUI] Point image not assigned on {gameObject.name}");
            return;
        }
        
        Color targetColor = isAllied ? AlliedColor : EnemyColor;
        pointImage.color = targetColor;
    }
    
    #endregion
    
    #region Unity Events
    
    void Awake()
    {
        // Validar componente de imagen
        if (pointImage == null)
            pointImage = GetComponent<Image>();
        
        if (pointImage == null)
            Debug.LogError($"[SupplyPointControllerUI] No Image component found on {gameObject.name}. Please assign one manually.");
    }
    
    #endregion
    
    #region Validation
    
    /// <summary>
    /// Valida la configuración del componente en el editor.
    /// </summary>
    private void OnValidate()
    {
        // Buscar imagen automáticamente si no está asignada
        if (pointImage == null)
            pointImage = GetComponent<Image>();
        
        // Advertir sobre configuración incompleta
        if (pointImage == null)
            Debug.LogWarning($"[SupplyPointControllerUI] No Image component assigned on {gameObject.name}. Supply point will not display correctly.");
        
        if (side == Side.None)
            Debug.LogWarning($"[SupplyPointControllerUI] Side is set to 'None' on {gameObject.name}. This may cause unexpected behavior.");
    }
    
    #endregion
}
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controlador visual para puntos de captura que muestra información de control territorial.
/// Solo visual/informativo - sin interactividad.
/// </summary>
public class CapturePointIconControllerUI : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private Side side;
    
    [Header("Visual Elements")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image border1Image;
    [SerializeField] private Image border2Image;
    
    // Colores predefinidos para Background
    private static readonly Color AlliedBackgroundColor = new Color(0.565f, 0.631f, 0.745f, 0.718f); // #90A1BE con alpha 183
    private static readonly Color EnemyBackgroundColor = new Color(0.737f, 0.392f, 0.400f, 0.639f);   // #BC6466 con alpha 163
    
    // Colores predefinidos para Borders
    private static readonly Color AlliedBorderColor = new Color(0.314f, 0.427f, 0.620f, 1f); // #506D9E con alpha 255
    private static readonly Color EnemyBorderColor = new Color(0.667f, 0.290f, 0.298f, 1f);   // #AA4A4C con alpha 255
    
    #region Public Properties
    
    /// <summary>
    /// Obtiene el side configurado para este capture point.
    /// </summary>
    public Side Side => side;
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// Inicializa el capture point estableciendo sus colores según la relación ally/enemy.
    /// </summary>
    /// <param name="playerSide">Side del jugador para determinar si es aliado o enemigo</param>
    public void Initialize(Side playerSide)
    {
        if (!ValidateImages())
            return;
        
        // Determinar colores basado en la comparación de sides
        bool isAllied = (playerSide == side);
        
        Color backgroundTargetColor = isAllied ? AlliedBackgroundColor : EnemyBackgroundColor;
        Color borderTargetColor = isAllied ? AlliedBorderColor : EnemyBorderColor;
        
        // Aplicar colores
        backgroundImage.color = backgroundTargetColor;
        border1Image.color = borderTargetColor;
        border2Image.color = borderTargetColor;
        
        // Log para debug
        string status = isAllied ? "Allied" : "Enemy";
        Debug.Log($"[CapturePointIconControllerUI] {gameObject.name} initialized as {status} (Player: {playerSide}, Point: {side})");
    }
    
    /// <summary>
    /// Cambia manualmente los colores del capture point.
    /// </summary>
    /// <param name="isAllied">True para colores aliados, false para enemigos</param>
    public void SetColors(bool isAllied)
    {
        if (!ValidateImages())
            return;
        
        Color backgroundTargetColor = isAllied ? AlliedBackgroundColor : EnemyBackgroundColor;
        Color borderTargetColor = isAllied ? AlliedBorderColor : EnemyBorderColor;
        
        backgroundImage.color = backgroundTargetColor;
        border1Image.color = borderTargetColor;
        border2Image.color = borderTargetColor;
    }
    
    #endregion
    
    #region Unity Events
    
    void Awake()
    {
        // Validar componentes de imagen
        AutoAssignImages();
    }
    
    #endregion
    
    #region Private Methods
    
    /// <summary>
    /// Intenta auto-asignar las imágenes si no están configuradas.
    /// </summary>
    private void AutoAssignImages()
    {
        Image[] images = GetComponentsInChildren<Image>();
        
        // Si no hay imágenes asignadas y hay exactamente 3 componentes Image
        if (backgroundImage == null && border1Image == null && border2Image == null && images.Length >= 3)
        {
            backgroundImage = images[0];
            border1Image = images[1];
            border2Image = images[2];
            
            Debug.Log($"[CapturePointIconControllerUI] Auto-assigned images on {gameObject.name}");
        }
    }
    
    /// <summary>
    /// Valida que todas las imágenes requeridas estén asignadas.
    /// </summary>
    /// <returns>True si todas las imágenes están presentes</returns>
    private bool ValidateImages()
    {
        if (backgroundImage == null)
        {
            Debug.LogError($"[CapturePointIconControllerUI] Background image not assigned on {gameObject.name}");
            return false;
        }
        
        if (border1Image == null)
        {
            Debug.LogError($"[CapturePointIconControllerUI] Border1 image not assigned on {gameObject.name}");
            return false;
        }
        
        if (border2Image == null)
        {
            Debug.LogError($"[CapturePointIconControllerUI] Border2 image not assigned on {gameObject.name}");
            return false;
        }
        
        return true;
    }
    
    #endregion
    
    #region Validation
    
    /// <summary>
    /// Valida la configuración del componente en el editor.
    /// </summary>
    private void OnValidate()
    {
        // Intentar auto-asignar si faltan imágenes
        if (backgroundImage == null || border1Image == null || border2Image == null)
            AutoAssignImages();
        
        // Advertir sobre configuración incompleta
        if (backgroundImage == null)
            Debug.LogWarning($"[CapturePointIconControllerUI] Background Image not assigned on {gameObject.name}");
            
        if (border1Image == null)
            Debug.LogWarning($"[CapturePointIconControllerUI] Border1 Image not assigned on {gameObject.name}");
            
        if (border2Image == null)
            Debug.LogWarning($"[CapturePointIconControllerUI] Border2 Image not assigned on {gameObject.name}");
        
        if (side == Side.None)
            Debug.LogWarning($"[CapturePointIconControllerUI] Side is set to 'None' on {gameObject.name}. This may cause unexpected behavior.");
    }
    
    #endregion
}
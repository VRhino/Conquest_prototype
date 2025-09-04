using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controlador para elementos Squad_Icon en el battle preparation UI.
/// Maneja la visualización de información básica del squad (background, underline, icon).
/// Solo para display, no hay interacción directa.
/// </summary>
public class SquadIconController : MonoBehaviour
{
    #region UI References

    [Header("Visual Elements")]
    [SerializeField] public Image background;
    [SerializeField] public Image underline;
    [SerializeField] public Image icon;

    #endregion

    #region Private Fields

    private SquadIconData _squadData;

    #endregion

    #region Unity Lifecycle

    void OnDestroy()
    {
        // Cleanup si es necesario
        _squadData = null;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Inicializa el controlador con datos del squad.
    /// Recibe toda la información como parámetro.
    /// </summary>
    /// <param name="squadData">Datos completos del squad desde ScriptableObject</param>
    public void Initialize(SquadIconData squadIconData)
    {
        if (squadIconData == null)
        {
            Debug.LogWarning("[SquadIconController] SquadIconData is null");
            Clear();
            return;
        }
        
        _squadData = squadIconData;
        background.sprite = squadIconData.backgroundSprite;
        icon.sprite = squadIconData.iconSprite;
        underline.color = squadIconData.underlineColor;
    }

    /// <summary>
    /// Limpia todos los elementos visuales
    /// </summary>
    public void Clear()
    {
        if (background != null) background.sprite = null;
        if (underline != null) underline.color = Color.clear;
        if (icon != null) icon.sprite = null;
    }

    #endregion

}

public class SquadIconData
{
    public string squadId;
    public Sprite backgroundSprite;
    public Sprite iconSprite;
    public Color underlineColor;

    public SquadIconData(string squadId, Sprite backgroundSprite, Sprite iconSprite, Color underlineColor)
    {
        this.squadId = squadId;
        this.backgroundSprite = backgroundSprite;
        this.iconSprite = iconSprite;
        this.underlineColor = underlineColor;
    }
}

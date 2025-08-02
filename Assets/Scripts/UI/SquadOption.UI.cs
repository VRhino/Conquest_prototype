using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controlador UI para una opción de escuadrón en el panel de selección.
/// </summary>
public class SquadOptionUI : MonoBehaviour
{
    [Header("Referencias UI")]
    public Image backgroundImage;
    public Image unitImage;
    public Button selectButton;

    public TextMeshProUGUI levelText;

    // Callback para click
    public System.Action onClick;

    private SquadData squadData;

    /// <summary>
    /// Asigna los datos visuales de la opción.
    /// </summary>
    public void SetSquadData(SquadData data)
    {
        squadData = data;
        if (unitImage != null)
            unitImage.sprite = data.unitImage;
        if (backgroundImage != null)
            backgroundImage.sprite = data.background;
    }

    public void setProgress(string progress)
    {
        if (levelText != null)
            levelText.text = $"LV.{progress}";
    }

    void Awake()
    {
        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnClickInternal);
        }
    }

    private void OnClickInternal()
    {
        onClick?.Invoke();
    }
}

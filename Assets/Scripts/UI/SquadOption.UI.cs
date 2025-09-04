using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controlador UI para una opción de escuadrón en el panel de selección.
/// </summary>
public class SquadOptionUI : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image unitImage;
    [SerializeField] private Button selectButton;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private Image dividerImage;
    [SerializeField] private Image selectedImage;
    [SerializeField] private TextMeshProUGUI leadershipText;
    [SerializeField] private TextMeshProUGUI unitCountText;

    // Callback para click
    public System.Action onClick;

    private SquadData squadData;

    /// <summary>
    /// Asigna los datos visuales de la opción.
    /// </summary>
    public void SetSquadData(SquadData data)
    {
        squadData = data;
        if (unitImage != null) unitImage.sprite = data.unitImage;
        if (backgroundImage != null) backgroundImage.sprite = data.background;
        if (dividerImage != null) dividerImage.color = SquadUtils.GetRarityColor(data.rarity);
    }

    public void ToggleBattlePreMode()
    {
        if (leadershipText != null)
        {
            leadershipText.gameObject.SetActive(true);
            leadershipText.text = squadData.leadershipCost.ToString();
        }
        if (unitCountText != null)
        {
            unitCountText.gameObject.SetActive(true);
            unitCountText.text = squadData.unitCount.ToString();
        }
    }

    public void SetSelected(bool isSelected)
    {
        if (selectedImage != null)
            selectedImage.gameObject.SetActive(isSelected);
    }

    public void SetInstanceData(string progress, int unitAliveCount)
    {
        if (levelText != null) levelText.text = $"LV.{progress}";
        if (unitCountText != null) unitCountText.text = $"{unitAliveCount.ToString()}/{squadData.unitCount}";
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

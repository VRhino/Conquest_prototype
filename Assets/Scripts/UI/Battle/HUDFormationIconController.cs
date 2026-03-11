using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Per-formation icon controller for the battle HUD.
/// Shows the formation sprite, keybind label, and active highlight.
/// </summary>
public class HUDFormationIconController : MonoBehaviour
{
    [SerializeField] private Image _outline;
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _formationKey;

    private FormationType _formationType;

    public FormationType FormationType => _formationType;

    public void Initialize(GridFormationScriptableObject formation, int index)
    {
        _formationType = formation.formationType;
        if (_icon != null && formation.formationIcon != null)
            _icon.sprite = formation.formationIcon;
        if (_formationKey != null)
            _formationKey.text = $"F{index + 1}";
        SetActive(false);
    }

    public void SetActive(bool isActive)
    {
        if (_outline != null)
            _outline.gameObject.SetActive(isActive);
    }
}

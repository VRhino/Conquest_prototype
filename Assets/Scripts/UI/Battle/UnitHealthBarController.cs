using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Per-unit health bar prefab controller. Updates fill amount and color
/// based on the unit's current health percentage.
/// </summary>
public class UnitHealthBarController : MonoBehaviour
{
    [SerializeField] private Image _foreground;

    [SerializeField] private Color _normalColor = new Color(0.2f, 0.8f, 0.2f, 1f);
    [SerializeField] private Color _lowHealthColor = new Color(0.8f, 0.2f, 0.2f, 1f);
    [SerializeField][Range(0f, 1f)] private float _lowHealthThreshold = 0.3f;

    public float CurrentPercent { get; private set; }

    public void SetHealthPercent(float percent)
    {
        if (_foreground == null) return;
        CurrentPercent = Mathf.Clamp01(percent);
        _foreground.fillAmount = CurrentPercent;
        _foreground.color = CurrentPercent <= _lowHealthThreshold ? _lowHealthColor : _normalColor;
    }
}

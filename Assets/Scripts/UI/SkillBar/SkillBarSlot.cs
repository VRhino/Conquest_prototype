using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One slot of the skill bar: icon, radial cooldown and seconds left. Empty slots are dimmed or hidden.</summary>
public class SkillBarSlot : MonoBehaviour
{
    [SerializeField] Image icon;
    [SerializeField] Image cooldownFill;
    [SerializeField] TMP_Text cooldownText;
    [SerializeField] CanvasGroup group;
    [Tooltip("Oculta el hueco si no hay habilidad (escuadra sin desbloquear); si no, lo atenúa.")]
    [SerializeField] bool hideWhenEmpty;

    const float EmptyAlpha = 0.35f;

    public void Show(UISkillSlotData data, Sprite sprite)
    {
        bool empty = data.skillId == 0;
        if (group != null) group.alpha = empty ? (hideWhenEmpty ? 0f : EmptyAlpha) : 1f;
        if (icon != null)
        {
            icon.sprite = sprite;
            icon.enabled = !empty;
        }
        bool cooling = !empty && data.cooldown > 0f;
        if (cooldownFill != null)
        {
            cooldownFill.enabled = cooling;
            cooldownFill.fillAmount = data.cooldown;
        }
        if (cooldownText != null)
        {
            cooldownText.enabled = cooling;
            if (cooling) cooldownText.text = Mathf.CeilToInt(data.remaining).ToString();
        }
    }
}

using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Una entrada de Floating Combat Text. Vive en un prefab con un Canvas world-space.
/// Anima flotando hacia arriba + fade, luego se devuelve al pool.
/// </summary>
public class FCTEntry : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Image icon;

    [Header("Escala")]
    [Tooltip("Tamaño base del texto en unidades canvas.")]
    [SerializeField] private float baseFontSize = 40f;

    [Tooltip("Escala world-space del FCT completo.")]
    [SerializeField] private float worldScale = 0.005f;

    [Header("Font Scale por tipo")]
    [SerializeField] private float fontScaleCritical = 1.4f;
    [SerializeField] private float fontScaleDeath    = 1.2f;

    [Header("Colores por tipo")]
    [SerializeField] private Color colorNormal   = Color.white;
    [SerializeField] private Color colorCritical = new Color(1f, 0.85f, 0f);       // #FFD700
    [SerializeField] private Color colorBlocked  = new Color(0.67f, 0.67f, 0.67f); // #AAAAAA
    [SerializeField] private Color colorDeath    = new Color(1f, 0.2f, 0.2f);      // #FF3333

    private const string LabelCritical = "CRITICO";
    private const string LabelBlocked  = "BLOQ";

    private const float FloatHeight   = 1.5f;
    private const float Duration      = 1.2f;
    private const float FadeStartFrac = 0.6f;

    public Action OnRelease;

    private Coroutine _activeCoroutine;

    public void Activate(Vector3 worldPos, DamageCategory type, float value, Sprite iconSprite)
    {
        Debug.Log($"[BattleTestDebug] FCTEntry.Activate: type={type}, value={value:F1}, pos={worldPos}, label={(label == null ? "NULL" : "OK")}");

        transform.position   = worldPos;
        transform.localScale = Vector3.one * worldScale;

        ConfigureVisuals(type, value, iconSprite);

        gameObject.SetActive(true);

        if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
        _activeCoroutine = StartCoroutine(AnimateRoutine());
    }

    private void ConfigureVisuals(DamageCategory type, float value, Sprite iconSprite)
    {
        label.fontSize = baseFontSize;
        icon.enabled   = false;

        switch (type)
        {
            case DamageCategory.Normal:
                label.text  = Mathf.RoundToInt(value).ToString();
                label.color = colorNormal;
                break;

            case DamageCategory.Critical:
                label.text     = $"{LabelCritical} {Mathf.RoundToInt(value)}";
                label.color    = colorCritical;
                label.fontSize = baseFontSize * fontScaleCritical;
                break;

            case DamageCategory.Blocked:
                label.text  = LabelBlocked;
                label.color = colorBlocked;
                SetIcon(iconSprite);
                break;

            case DamageCategory.Death:
                label.text     = Mathf.RoundToInt(value).ToString();
                label.color    = colorDeath;
                label.fontSize = baseFontSize * fontScaleDeath;
                SetIcon(iconSprite);
                break;

            default:
                label.text  = Mathf.RoundToInt(value).ToString();
                label.color = colorNormal;
                break;
        }
    }

    private void SetIcon(Sprite sprite)
    {
        if (sprite == null) return;
        icon.sprite  = sprite;
        icon.enabled = true;
    }

    private void LateUpdate()
    {
        if (Camera.main != null)
            transform.rotation = Camera.main.transform.rotation;
    }

    private IEnumerator AnimateRoutine()
    {
        Vector3 startPos = transform.position;
        Vector3 endPos   = startPos + Vector3.up * FloatHeight;

        Color labelColor = label.color;
        Color iconColor  = icon.enabled ? icon.color : Color.white;

        float elapsed = 0f;
        while (elapsed < Duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / Duration;

            transform.position = Vector3.Lerp(startPos, endPos, t);

            float alpha = t < FadeStartFrac
                ? 1f
                : 1f - (t - FadeStartFrac) / (1f - FadeStartFrac);

            label.color = new Color(labelColor.r, labelColor.g, labelColor.b, alpha);
            if (icon.enabled)
                icon.color = new Color(iconColor.r, iconColor.g, iconColor.b, alpha);

            yield return null;
        }

        _activeCoroutine = null;
        OnRelease?.Invoke();
    }
}

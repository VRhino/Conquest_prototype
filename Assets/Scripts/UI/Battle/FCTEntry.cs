using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Una entrada de Floating Combat Text. Vive en un prefab con un Canvas world-space.
/// Anima en parábola hacia la derecha + fade, luego se devuelve al pool.
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

    private const float ArcWidth     = 1.2f;  // desplazamiento horizontal total (derecha)
    private const float ArcHeight    = 1.0f;  // altura del punto de control de la cima
    private const float Duration      = 1.2f;
    private const float FadeStartFrac = 0.6f;

    public Action OnRelease;

    private Coroutine _activeCoroutine;

    public void Activate(Vector3 worldPos, FCTCategoryEntry entry, float value)
    {
        transform.position   = worldPos;
        transform.localScale = Vector3.one * worldScale;

        ConfigureVisuals(entry, value);

        gameObject.SetActive(true);

        if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
        _activeCoroutine = StartCoroutine(AnimateRoutine());
    }

    private void ConfigureVisuals(FCTCategoryEntry entry, float value)
    {
        label.fontSize = baseFontSize;
        icon.enabled   = false;

        if (entry == null)
        {
            label.text  = Mathf.RoundToInt(value).ToString();
            label.color = Color.white;
            return;
        }

        label.color    = entry.color;
        label.fontSize = baseFontSize * entry.fontScale;

        string numberPart = entry.showValue ? Mathf.RoundToInt(value).ToString() : string.Empty;
        label.text = string.IsNullOrEmpty(entry.label)
            ? numberPart
            : string.IsNullOrEmpty(numberPart) ? entry.label : $"{entry.label} {numberPart}";

        if (entry.icon != null)
        {
            icon.sprite  = entry.icon;
            icon.enabled = true;
        }
    }

    private void LateUpdate()
    {
        if (Camera.main != null)
            transform.rotation = Camera.main.transform.rotation;
    }

    private IEnumerator AnimateRoutine()
    {
        Vector3 p0 = transform.position;
        Vector3 p1 = p0 + Vector3.right * (ArcWidth * 0.5f) + Vector3.up * ArcHeight;
        Vector3 p2 = p0 + Vector3.right * ArcWidth;

        Color labelColor = label.color;
        Color iconColor  = icon.enabled ? icon.color : Color.white;

        float elapsed = 0f;
        while (elapsed < Duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / Duration;

            // Bezier cuadrática: (1-t)²·p0 + 2(1-t)t·p1 + t²·p2
            float u = 1f - t;
            transform.position = u * u * p0 + 2f * u * t * p1 + t * t * p2;

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

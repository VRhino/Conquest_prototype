using UnityEngine;
using TMPro;

/// <summary>
/// Controlador simple para las entradas de estadísticas en el tooltip.
/// Se usa como prefab para mostrar cada stat del equipamiento.
/// </summary>
public class TooltipStatEntry : MonoBehaviour
{
    [Header("Text Components")]
    public TMP_Text statNameText;
    public TMP_Text statValueText;

    /// <summary>
    /// Configura los textos de la entrada de estadística.
    /// </summary>
    /// <param name="statName">Nombre de la estadística</param>
    /// <param name="statValue">Valor de la estadística</param>
    public void SetStatData(string statName, string statValue)
    {
        if (statNameText != null)
            statNameText.text = statName;
            
        if (statValueText != null)
            statValueText.text = statValue;
    }

    /// <summary>
    /// Configura los textos con datos de estadística completos.
    /// </summary>
    /// <param name="statName">Nombre de la estadística</param>
    /// <param name="statValue">Valor numérico de la estadística</param>
    public void SetStatData(string statName, float statValue)
    {
        string formattedValue = statValue % 1 == 0 ? statValue.ToString("F0") : statValue.ToString("F1");
        SetStatData(statName, formattedValue);
    }

    /// <summary>
    /// Configura solo el nombre de la stat (para layout de una sola columna).
    /// </summary>
    /// <param name="fullText">Texto completo "Nombre: Valor"</param>
    public void SetSingleText(string fullText)
    {
        if (statNameText != null)
        {
            statNameText.text = fullText;
        }

        // Ocultar el campo de valor si solo usamos uno
        if (statValueText != null)
        {
            statValueText.gameObject.SetActive(false);
        }
    }
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Sistema para mostrar el rarity visual de una unidad usando estrellas y color.
/// </summary>
public class SquadRarityUI : MonoBehaviour
{
    [Header("Estrellas")]
    public Image[] starImages; // Asigna en el inspector (5 máximo)
    [Header("Color de fondo")]
    public Image rarityBackground;
    [Header("Texto opcional")]
    public TMP_Text rarityText;

    // Colores de rarity
    public Color gray = new Color(0.5f, 0.5f, 0.5f);
    public Color green = new Color(0.2f, 0.8f, 0.2f);
    public Color blue = new Color(0.35f, 0.5f, 0.9f);
    public Color purple = new Color(0.6f, 0.3f, 0.8f);
    public Color gold = new Color(1f, 0.85f, 0.2f);

    /// <summary>
    /// Muestra el rarity visual según el valor de estrellas (puede ser decimal).
    /// </summary>
    public void SetRarity(float stars)
    {
        // Determinar color
        Color rarityColor = gray;
        if (stars >= 2f && stars < 3f) rarityColor = green;
        else if (stars >= 3f && stars < 4f) rarityColor = blue;
        else if (stars >= 4f && stars < 5f) rarityColor = purple;
        else if (stars >= 5f) rarityColor = gold;

        // Asignar color
        if (rarityBackground != null)
            rarityBackground.color = rarityColor;

        // Mostrar estrellas (soporta decimales)
        for (int i = 0; i < starImages.Length; i++)
        {
            if (starImages[i] == null) continue;
            if (stars >= i + 1)
            {
                // Estrella llena
                starImages[i].fillAmount = 1f;
                starImages[i].enabled = true;
            }
            else if (stars > i)
            {
                // Estrella parcial
                starImages[i].fillAmount = stars - i;
                starImages[i].enabled = true;
            }
            else
            {
                // Estrella vacía
                starImages[i].fillAmount = 0f;
                starImages[i].enabled = true;
            }
        }

        // Texto opcional
        if (rarityText != null)
            rarityText.text = $"{stars:0.0}";
    }
}

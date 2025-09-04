using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Utilidades compartidas para manejo de squads en UI.
/// Centraliza lógica común como colores por rareza.
/// </summary>
public static class SquadUtils
{
    /// <summary>
    /// Información de colores y estrellas por rareza de squad
    /// </summary>
    public static readonly Dictionary<SquadRarity, (Color color, float stars)> RarityInfo = new()
    {
        { SquadRarity.peasant_tier,   (new Color(0.5f, 0.5f, 0.5f), 0.5f) }, // gray
        { SquadRarity.levy_tier,      (new Color(0.5f, 0.5f, 0.5f), 1f) },
        { SquadRarity.conscript_tier, (new Color(0.5f, 0.5f, 0.5f), 1.5f) },
        { SquadRarity.trained_tier,   (new Color32(72, 180, 122, 255), 2f) }, // green #48B47A
        { SquadRarity.seasoned_tier,  (new Color32(72, 180, 122, 255), 2.5f) }, // green #48B47A
        { SquadRarity.veteran_tier,   (new Color32(89, 131, 203, 255), 3f) }, // blue #5983CB
        { SquadRarity.hardened_tier,  (new Color32(89, 131, 203, 255), 3.5f) }, // blue #5983CB
        { SquadRarity.elite_tier,     (new Color32(167, 108, 203, 255), 4f) }, // purple #A76CCB
        { SquadRarity.master_tier,    (new Color32(167, 108, 203, 255), 4.5f) }, // purple #A76CCB
        { SquadRarity.legendary_tier, (new Color32(217, 159, 87, 255), 5f) } // golden #D99F57
    };

    /// <summary>
    /// Obtiene la información de color y estrellas para una rareza específica
    /// </summary>
    /// <param name="rarity">Rareza del squad</param>
    /// <returns>Tupla con color y cantidad de estrellas</returns>
    public static (Color color, float stars) GetRarityInfo(SquadRarity rarity)
    {
        if (RarityInfo.TryGetValue(rarity, out var info))
            return info;
        
        return (Color.white, 0f);
    }

    /// <summary>
    /// Obtiene solo el color para una rareza específica
    /// </summary>
    /// <param name="rarity">Rareza del squad</param>
    /// <returns>Color correspondiente a la rareza</returns>
    public static Color GetRarityColor(SquadRarity rarity)
    {
        return GetRarityInfo(rarity).color;
    }

    /// <summary>
    /// Obtiene solo la cantidad de estrellas para una rareza específica
    /// </summary>
    /// <param name="rarity">Rareza del squad</param>
    /// <returns>Cantidad de estrellas</returns>
    public static float GetRarityStars(SquadRarity rarity)
    {
        return GetRarityInfo(rarity).stars;
    }
}

using System;

/// <summary>
/// Contiene las bonificaciones que aporta el equipamiento a los atributos del héroe.
/// Estas bonificaciones se calculan dinámicamente basándose en los items equipados.
/// </summary>
[Serializable]
public struct EquipmentBonuses
{
    /// <summary>Strength bonus from equipped items.</summary>
    public int strengthBonus;
    
    /// <summary>Dexterity bonus from equipped items.</summary>
    public int dexterityBonus;
    
    /// <summary>Armor bonus from equipped items.</summary>
    public int armorBonus;
    
    /// <summary>Vitality bonus from equipped items.</summary>
    public int vitalityBonus;

    /// <summary>
    /// Creates an empty equipment bonus structure.
    /// </summary>
    public static EquipmentBonuses Empty => new EquipmentBonuses
    {
        strengthBonus = 0,
        dexterityBonus = 0,
        armorBonus = 0,
        vitalityBonus = 0
    };

    /// <summary>
    /// Checks if this bonus structure has any non-zero values.
    /// </summary>
    public bool HasBonuses => strengthBonus != 0 || dexterityBonus != 0 || armorBonus != 0 || vitalityBonus != 0;
}

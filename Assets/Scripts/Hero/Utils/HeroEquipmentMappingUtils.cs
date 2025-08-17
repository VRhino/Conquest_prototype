using Data.Avatar;

/// <summary>
/// Utilidades para mapear entre tipos de ítems y slots de avatar.
/// Centraliza la lógica de conversión entre los diferentes sistemas.
/// </summary>
public static class HeroEquipmentMappingUtils
{
    /// <summary>
    /// Convierte un ItemType a su AvatarSlot correspondiente.
    /// </summary>
    /// <param name="itemType">Tipo de ítem</param>
    /// <returns>Slot de avatar correspondiente</returns>
    /// <exception cref="System.ArgumentException">Si el ItemType no mapea a un AvatarSlot</exception>
    public static AvatarSlot ItemTypeToAvatarSlot(ItemCategory itemCategory)
    {
        return itemCategory switch
        {
            ItemCategory.Helmet => AvatarSlot.Head,
            ItemCategory.Torso => AvatarSlot.Torso,
            ItemCategory.Gloves => AvatarSlot.Gloves,
            ItemCategory.Pants => AvatarSlot.Pants,
            ItemCategory.Boots => AvatarSlot.Boots,
            ItemCategory.Bow => AvatarSlot.Weapon,
            ItemCategory.Spear => AvatarSlot.Weapon,
            ItemCategory.TwoHandedSword => AvatarSlot.Weapon,
            ItemCategory.SwordAndShield => AvatarSlot.Weapon,
            _ => throw new System.ArgumentException($"ItemCategory {itemCategory} no mapeable a AvatarSlot")
        };
    }
}

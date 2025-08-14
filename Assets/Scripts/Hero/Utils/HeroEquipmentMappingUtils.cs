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
    public static AvatarSlot ItemTypeToAvatarSlot(ItemType itemType)
    {
        return itemType switch
        {
            ItemType.Helmet => AvatarSlot.Head,
            ItemType.Torso => AvatarSlot.Torso,
            ItemType.Gloves => AvatarSlot.Gloves,
            ItemType.Pants => AvatarSlot.Pants,
            ItemType.Boots => AvatarSlot.Boots,
            ItemType.Weapon => AvatarSlot.Weapon,
            _ => throw new System.ArgumentException($"ItemType {itemType} no mapeable a AvatarSlot")
        };
    }

    /// <summary>
    /// Convierte un AvatarSlot a su ItemType correspondiente.
    /// </summary>
    /// <param name="avatarSlot">Slot de avatar</param>
    /// <returns>Tipo de ítem correspondiente</returns>
    /// <exception cref="System.ArgumentException">Si el AvatarSlot no mapea a un ItemType</exception>
    public static ItemType AvatarSlotToItemType(AvatarSlot avatarSlot)
    {
        return avatarSlot switch
        {
            AvatarSlot.Head => ItemType.Helmet,
            AvatarSlot.Torso => ItemType.Torso,
            AvatarSlot.Gloves => ItemType.Gloves,
            AvatarSlot.Pants => ItemType.Pants,
            AvatarSlot.Boots => ItemType.Boots,
            AvatarSlot.Weapon => ItemType.Weapon,
            _ => throw new System.ArgumentException($"AvatarSlot {avatarSlot} no mapeable a ItemType")
        };
    }

    /// <summary>
    /// Verifica si un ItemType es equipable (tiene correspondencia visual).
    /// </summary>
    /// <param name="itemType">Tipo de ítem</param>
    /// <returns>True si es equipable</returns>
    public static bool IsEquippableItemType(ItemType itemType)
    {
        return itemType switch
        {
            ItemType.Helmet or ItemType.Torso or ItemType.Gloves or 
            ItemType.Pants or ItemType.Boots or ItemType.Weapon => true,
            _ => false
        };
    }

    /// <summary>
    /// Obtiene una descripción legible del slot de equipamiento.
    /// </summary>
    /// <param name="itemType">Tipo de ítem</param>
    /// <returns>Nombre legible del slot</returns>
    public static string GetSlotDisplayName(ItemType itemType)
    {
        return itemType switch
        {
            ItemType.Helmet => "Casco",
            ItemType.Torso => "Torso",
            ItemType.Gloves => "Guantes",
            ItemType.Pants => "Pantalones",
            ItemType.Boots => "Botas",
            ItemType.Weapon => "Arma",
            ItemType.Consumable => "Consumible",
            ItemType.Visual => "Visual",
            _ => "Desconocido"
        };
    }
}

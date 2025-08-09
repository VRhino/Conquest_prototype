using System;

namespace Data.Items
{
    [Serializable]
    public class ItemData
    {
        public string id;
        public string name;
        public string iconPath;
        public string description;
        public ItemRarity rarity = ItemRarity.Common; // 0 = Common, 1 = Uncommon, 2 = Rare, 3 = Epic, 4 = Legendary
        public string visualPartId; // Referencia a AvatarPartDefinition por ID
        public bool stackable = true; // Si el ítem puede apilarse en el inventario
        public ItemType itemType = ItemType.None; // Tipo para categorización rápida
        // ...otros campos...
    }
}

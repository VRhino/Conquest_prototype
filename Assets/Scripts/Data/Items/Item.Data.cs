using System;
using UnityEngine;

namespace Data.Items
{
    [Serializable]
    public class ItemData
    {
        [Header("Basic Info")]
        public string id;
        public string name;
        public string iconPath;
        [TextArea(2, 4)]
        public string description;
        public ItemRarity rarity = ItemRarity.Common;
        public ItemType itemType = ItemType.None;
        public ItemCategory itemCategory = ItemCategory.None;
        public ArmorType armorType = ArmorType.None;
        public bool stackable = true;
        

        [Header("Visual")]
        public string visualPartId; // Referencia a AvatarPartDefinition por ID

        [Header("Equipment Generation")]
        public ItemStatGenerator statGenerator; // null para consumables

        [Header("Consumable Effects")]
        public ItemEffect[] effects; // null para equipment

        [Header("Usage Settings")]
        public bool consumeOnUse = true;
        public bool requiresConfirmation = false;
        public string useButtonText = "Use";

        [Header("Pricing")]
        public UnityEngine.ScriptableObject pricingConfig; // PricingConfigurationSO - Opcional, si es null usa precio por defecto

        /// <summary>
        /// Verifica si este ítem es equipment (tiene generador de stats).
        /// </summary>
        public bool IsEquipment => statGenerator != null;

        /// <summary>
        /// Verifica si este ítem es consumible (tiene efectos).
        /// </summary>
        public bool IsConsumable => effects != null && effects.Length > 0;

        /// <summary>
        /// Verifica si este ítem debe crear instancias únicas.
        /// </summary>
        public bool RequiresInstances => IsEquipment;
    }
}

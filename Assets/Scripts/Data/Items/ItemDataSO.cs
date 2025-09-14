using System;
using UnityEngine;

namespace Data.Items
{
    /// <summary>
    /// ScriptableObject version of ItemData for modular item system.
    /// Each item will be its own asset file for better organization and scalability.
    /// </summary>
    [CreateAssetMenu(fileName = "New Item", menuName = "Items/Item Data", order = 1)]
    public class ItemDataSO : ScriptableObject
    {
        [Header("Basic Info")]
        [SerializeField] private string _id;
        [SerializeField] private string _name;
        [SerializeField] private Sprite _Icon;
        [SerializeField, TextArea(2, 4)] private string _description;
        [SerializeField] private ItemRarity _rarity = ItemRarity.Common;
        [SerializeField] private ItemType _itemType = ItemType.None;
        [SerializeField] private ItemCategory _itemCategory = ItemCategory.None;
        [SerializeField] private ArmorType _armorType = ArmorType.None;
        [SerializeField] private bool _stackable = true;

        [Header("Visual")]
        [SerializeField] private string _visualPartId; // Referencia a AvatarPartDefinition por ID

        [Header("Equipment Generation")]
        [SerializeField] private ItemStatGenerator _statGenerator; // null para consumables

        [Header("Consumable Effects")]
        [SerializeField] private ItemEffect[] _effects; // null para equipment

        [Header("Usage Settings")]
        [SerializeField] private bool _consumeOnUse = true;
        [SerializeField] private bool _requiresConfirmation = false;
        [SerializeField] private string _useButtonText = "Use";

        [Header("Pricing")]
        [SerializeField] private UnityEngine.ScriptableObject _pricingConfig; // PricingConfigurationSO - Opcional

        #region Public Properties

        public string id => _id;
        public new string name => _name;
        public Sprite icon => _Icon;
        public string description => _description;
        public ItemRarity rarity => _rarity;
        public ItemType itemType => _itemType;
        public ItemCategory itemCategory => _itemCategory;
        public ArmorType armorType => _armorType;
        public bool stackable => _stackable;
        public string visualPartId => _visualPartId;
        public ItemStatGenerator statGenerator => _statGenerator;
        public ItemEffect[] effects => _effects;
        public bool consumeOnUse => _consumeOnUse;
        public bool requiresConfirmation => _requiresConfirmation;
        public string useButtonText => _useButtonText;
        public UnityEngine.ScriptableObject pricingConfig => _pricingConfig;

        #endregion

        #region Computed Properties

        /// <summary>
        /// Verifica si este ítem es equipment (tiene generador de stats).
        /// </summary>
        public bool IsEquipment => _statGenerator != null;

        /// <summary>
        /// Verifica si este ítem es consumible (tiene efectos).
        /// </summary>
        public bool IsConsumable => _effects != null && _effects.Length > 0;

        /// <summary>
        /// Verifica si este ítem debe crear instancias únicas.
        /// </summary>
        public bool RequiresInstances => IsEquipment;

        #endregion

        #region Validation

        /// <summary>
        /// Valida que este ItemDataSO tenga configuración correcta.
        /// </summary>
        /// <returns>True si es válido, false si tiene errores</returns>
        public bool Validate()
        {
            bool isValid = true;

            if (string.IsNullOrEmpty(_id))
            {
                Debug.LogError($"[ItemDataSO] {name}: ID cannot be empty", this);
                isValid = false;
            }

            if (string.IsNullOrEmpty(_name))
            {
                Debug.LogWarning($"[ItemDataSO] {_id}: Name is empty", this);
            }

            if (_itemType == ItemType.None)
            {
                Debug.LogWarning($"[ItemDataSO] {_id}: ItemType is None", this);
            }

            if (_itemCategory == ItemCategory.None)
            {
                Debug.LogWarning($"[ItemDataSO] {_id}: ItemCategory is None", this);
            }

            // Validar que equipment tenga statGenerator
            if (_itemType != ItemType.Consumable && _statGenerator == null)
            {
                Debug.LogWarning($"[ItemDataSO] {_id}: Equipment item missing statGenerator", this);
            }

            // Validar que consumible tenga efectos
            if (_itemType == ItemType.Consumable && (_effects == null || _effects.Length == 0))
            {
                Debug.LogWarning($"[ItemDataSO] {_id}: Consumable item missing effects", this);
            }

            return isValid;
        }

        #endregion

        #region Unity Events

        private void OnValidate()
        {
            // Auto-set name from file name if empty
            if (string.IsNullOrEmpty(_name))
            {
                _name = this.name;
            }

            // Auto-set ID from file name if empty
            if (string.IsNullOrEmpty(_id))
            {
                _id = this.name;
            }
        }

        #endregion
    }
}
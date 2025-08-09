using System.Collections.Generic;
using UnityEngine;

namespace Data.Items
{
    /// <summary>
    /// Database for item definitions.
    /// </summary>
    [CreateAssetMenu(menuName = "Item/ItemDatabase")]
    public class ItemDatabase : ScriptableObject
    {
        [Header("Items Configuration")]
        public List<ItemData> items;

        // Singleton para acceso global
        private static ItemDatabase _instance;
        public static ItemDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<ItemDatabase>("Data/Items/ItemDatabase");
                    if (_instance == null)
                    {
                        Debug.LogError("[ItemDatabase] No ItemDatabase found in Resources/Data/Items/ folder!");
                    }
                }
                return _instance;
            }
        }

        public ItemData GetItemDataById(string id)
        {
            return items?.Find(item => item.id == id);
        }

        /// <summary>
        /// Verifica si un ítem puede apilarse en el inventario.
        /// </summary>
        public bool IsStackable(string itemId)
        {
            var item = GetItemDataById(itemId);
            return item != null && item.stackable;
        }

        /// <summary>
        /// Obtiene el tipo de un ítem por su ID.
        /// </summary>
        public ItemType GetItemType(string itemId)
        {
            var item = GetItemDataById(itemId);
            return item?.itemType ?? ItemType.None;
        }

        /// <summary>
        /// Verifica si un ítem existe en la base de datos.
        /// </summary>
        public bool ItemExists(string itemId)
        {
            return GetItemDataById(itemId) != null;
        }

        /// <summary>
        /// Obtiene todos los ítems de un tipo específico.
        /// </summary>
        public List<ItemData> GetItemsByType(ItemType itemType)
        {
            return items?.FindAll(item => item.itemType == itemType) ?? new List<ItemData>();
        }

        /// <summary>
        /// Obtiene el nombre display de un ítem.
        /// </summary>
        public string GetItemName(string itemId)
        {
            var item = GetItemDataById(itemId);
            return item?.name ?? "Unknown Item";
        }

        /// <summary>
        /// Obtiene la descripción de un ítem.
        /// </summary>
        public string GetItemDescription(string itemId)
        {
            var item = GetItemDataById(itemId);
            return item?.description ?? "";
        }

        /// <summary>
        /// Valida que todos los ítems en la base de datos tengan configuración correcta.
        /// </summary>
        [ContextMenu("Validate Database")]
        public void ValidateDatabase()
        {
            if (items == null || items.Count == 0)
            {
                Debug.LogWarning("[ItemDatabase] No items configured in database");
                return;
            }

            int errors = 0;
            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.id))
                {
                    Debug.LogError($"[ItemDatabase] Item at index {items.IndexOf(item)} has empty ID");
                    errors++;
                }
                if (string.IsNullOrEmpty(item.name))
                {
                    Debug.LogWarning($"[ItemDatabase] Item '{item.id}' has empty name");
                    errors++;
                }
                if (item.itemType == ItemType.None)
                {
                    Debug.LogWarning($"[ItemDatabase] Item '{item.id}' has ItemType.None");
                    errors++;
                }
            }

            if (errors == 0)
            {
                Debug.Log($"[ItemDatabase] Validation complete: {items.Count} items, no errors found");
            }
            else
            {
                Debug.LogError($"[ItemDatabase] Validation found {errors} issues");
            }
        }
    }
}
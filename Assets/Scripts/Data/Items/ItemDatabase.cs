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
                if (item.itemCategory == ItemCategory.None)
                {
                    Debug.LogWarning($"[ItemDatabase] Item '{item.id}' has ItemCategory.None");
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
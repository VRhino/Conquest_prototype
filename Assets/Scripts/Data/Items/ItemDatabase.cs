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
        public List<ItemData> items;

        public ItemData GetItemDataById(string id)
        {
            return items.Find(item => item.id == id);
        }
    }
}
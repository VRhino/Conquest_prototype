using UnityEngine;
using UnityEditor;
using Data.Items;

/// <summary>
/// Editor personalizado para ItemDatabase que facilita la re-serialización.
/// </summary>
[CustomEditor(typeof(ItemDatabase))]
public class ItemDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        GUILayout.Space(10);
        
        ItemDatabase database = (ItemDatabase)target;
        
        if (GUILayout.Button("Force Re-serialize All Items"))
        {
            ForceReserializeItems(database);
        }
        
        if (GUILayout.Button("Add Missing ItemCategory Fields"))
        {
            AddMissingFields(database);
        }
    }
    
    private void ForceReserializeItems(ItemDatabase database)
    {
        if (database.items == null) return;
        
        Debug.Log($"[ItemDatabaseEditor] Forcing re-serialization for {database.items.Count} items...");
        
        // Marcar el asset como dirty
        EditorUtility.SetDirty(database);
        
        // Forzar serialización tocando cada item
        foreach (var item in database.items)
        {
            if (item != null)
            {
                // Forzar que Unity detecte cambios
                var temp = item.itemCategory;
                item.itemCategory = ItemCategory.None;
                item.itemCategory = temp;
            }
        }
        
        // Guardar cambios
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log("[ItemDatabaseEditor] Re-serialization completed!");
        
        // Forzar repaint del inspector
        Repaint();
    }
    
    private void AddMissingFields(ItemDatabase database)
    {
        if (database.items == null) return;
        
        Debug.Log("[ItemDatabaseEditor] Adding missing ItemCategory fields...");
        
        EditorUtility.SetDirty(database);
        
        foreach (var item in database.items)
        {
            if (item != null && item.itemCategory == ItemCategory.None)
            {
                // Asignar categoria por defecto basada en itemType
                item.itemCategory = ItemCategory.None;
                Debug.Log($"Set {item.id} category to {item.itemCategory}");
            }
        }
        
        AssetDatabase.SaveAssets();
        Debug.Log("[ItemDatabaseEditor] Missing fields added!");
    }
}

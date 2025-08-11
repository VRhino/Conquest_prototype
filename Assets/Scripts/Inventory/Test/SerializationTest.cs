using UnityEngine;
using System.Collections.Generic;
using Data.Items;

/// <summary>
/// Test temporal para verificar que la serialización de stats funciona correctamente.
/// </summary>
public class SerializationTest : MonoBehaviour
{
    [ContextMenu("Test Serialization")]
    private void TestSerialization()
    {
        Debug.Log("=== TESTING SERIALIZATION ===");
        
        // Crear un item de equipment con stats
        var testItem = new InventoryItem
        {
            itemId = "TestWeapon",
            itemType = ItemType.Weapon,
            quantity = 1,
            instanceId = System.Guid.NewGuid().ToString()
        };

        // Agregar algunos stats generados
        testItem.SetStat("Damage", 25.5f);
        testItem.SetStat("CritChance", 12.0f);
        testItem.SetStat("Durability", 100.0f);

        Debug.Log($"Original item stats: {testItem.GeneratedStats.Count}");
        foreach (var stat in testItem.GeneratedStats)
        {
            Debug.Log($"  {stat.Key}: {stat.Value}");
        }

        // Serializar a JSON
        string json = JsonUtility.ToJson(testItem, true);
        Debug.Log($"Serialized JSON:\n{json}");

        // Deserializar desde JSON
        var deserializedItem = JsonUtility.FromJson<InventoryItem>(json);
        
        // Verificar que los stats se restauraron correctamente
        Debug.Log($"Deserialized item stats: {deserializedItem.GeneratedStats.Count}");
        foreach (var stat in deserializedItem.GeneratedStats)
        {
            Debug.Log($"  {stat.Key}: {stat.Value}");
        }

        // Verificar valores específicos
        bool allStatsCorrect = true;
        allStatsCorrect &= deserializedItem.GetStat("Damage") == 25.5f;
        allStatsCorrect &= deserializedItem.GetStat("CritChance") == 12.0f;
        allStatsCorrect &= deserializedItem.GetStat("Durability") == 100.0f;

        Debug.Log($"All stats correctly serialized/deserialized: {allStatsCorrect}");
        Debug.Log("=== SERIALIZATION TEST COMPLETE ===");
    }
}

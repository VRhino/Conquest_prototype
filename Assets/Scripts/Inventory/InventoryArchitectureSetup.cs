using UnityEngine;
using Data.Items;

/// <summary>
/// Archivo temporal para verificar que todas las dependencias de la nueva arquitectura funcionen correctamente.
/// Este archivo puede eliminarse una vez que se resuelvan todos los errores de compilación.
/// </summary>
[System.Serializable]
public class InventoryArchitectureSetup : MonoBehaviour
{
    [Header("Architecture Test")]
    [SerializeField] private ItemData testItemData;
    [SerializeField] private ItemStatGenerator testStatGenerator;
    [SerializeField] private ItemEffect testItemEffect;

    [Space]
    [Header("Testing")]
    [SerializeField] private bool logArchitectureInfo = true;

    private void Start()
    {
        if (logArchitectureInfo)
        {
            LogArchitectureStatus();
        }
    }

    private void LogArchitectureStatus()
    {
        Debug.Log("=== INVENTORY ARCHITECTURE STATUS ===");
        
        // Test ItemData
        if (testItemData != null)
        {
            Debug.Log($"ItemData Test: {testItemData.name}");
            Debug.Log($"Is Equipment: {testItemData.IsEquipment}");
            Debug.Log($"Is Consumable: {testItemData.IsConsumable}");
            Debug.Log($"Requires Instances: {testItemData.RequiresInstances}");
        }
        
        // Test StatRange
        var testRange = new FloatRange(10f, 20f);
        Debug.Log($"StatRange Test: {testRange.GetRandomValue()}");
        
        Debug.Log("=== Architecture Ready ===");
    }

    [ContextMenu("Test Item Instance Creation")]
    private void TestItemInstanceCreation()
    {
        if (testItemData == null)
        {
            Debug.LogWarning("No test ItemData assigned");
            return;
        }

        // Simular creación de instancia
        var testItem = new InventoryItem
        {
            itemId = testItemData.id,
            itemType = testItemData.itemType,
            quantity = 1
        };

        if (testItemData.IsEquipment)
        {
            testItem.instanceId = System.Guid.NewGuid().ToString();
            Debug.Log($"Created equipment instance: {testItem.instanceId}");
            
            if (testItemData.statGenerator != null)
            {
                testItem.GeneratedStats = testItemData.statGenerator.GenerateStats();
                Debug.Log($"Generated {testItem.GeneratedStats.Count} stats");
            }
        }

        Debug.Log($"Test item created: {testItem.itemId} (Equipment: {testItem.IsEquipment})");
    }

    [ContextMenu("Test Item Effects")]
    private void TestItemEffects()
    {
        if (testItemData?.effects == null)
        {
            Debug.LogWarning("No test ItemData or effects assigned");
            return;
        }

        Debug.Log($"Testing {testItemData.effects.Length} effects:");
        foreach (var effect in testItemData.effects)
        {
            if (effect != null)
            {
                Debug.Log($"Effect: {effect.name} - Preview: {effect.GetPreviewText()}");
            }
        }
    }
}

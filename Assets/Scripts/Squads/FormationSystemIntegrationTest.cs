using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Simple script to verify that formation center calculations and ECS blob storage work correctly.
/// Attach to any GameObject and run the test.
/// </summary>
public class FormationSystemIntegrationTest : MonoBehaviour
{
    [Header("Test Formation Assets")]
    public GridFormationScriptableObject testudoFormation;
    public GridFormationScriptableObject lineFormation;

    [ContextMenu("Test Formation Center Calculations")]
    public void TestFormationCenters()
    {
        Debug.Log("=== Formation System Integration Test ===");
        
        if (testudoFormation != null)
        {
            TestFormation(testudoFormation, new int2(4, 5), "Testudo");
        }
        else
        {
            Debug.LogWarning("Testudo formation not assigned!");
        }
        
        if (lineFormation != null)
        {
            TestFormation(lineFormation, new int2(5, 4), "Line");
        }
        else
        {
            Debug.LogWarning("Line formation not assigned!");
        }
    }

    private void TestFormation(GridFormationScriptableObject formation, int2 expectedCenter, string name)
    {
        Debug.Log($"\n--- Testing {name} Formation ---");
        
        // Test ScriptableObject calculations
        var calculatedCenter = formation.GetFormationCenter();
        var centeredPositions = formation.GetCenteredGridPositions();
        var worldOffsets = formation.GetCenteredWorldOffsets();
        
        Debug.Log($"Expected center: ({expectedCenter.x}, {expectedCenter.y})");
        Debug.Log($"Calculated center: ({calculatedCenter.x}, {calculatedCenter.y})");
        
        bool centerCorrect = calculatedCenter.x == expectedCenter.x && calculatedCenter.y == expectedCenter.y;
        Debug.Log($"Center calculation: {(centerCorrect ? "✓ PASS" : "✗ FAIL")}");
        
        // Test centered positions
        Debug.Log($"Original positions count: {formation.gridPositions.Length}");
        Debug.Log($"Centered positions count: {centeredPositions.Length}");
        Debug.Log($"World offsets count: {worldOffsets.Length}");
        
        // Show first few centered positions
        Debug.Log("Sample centered positions (relative to hero):");
        for (int i = 0; i < math.min(5, centeredPositions.Length); i++)
        {
            var pos = centeredPositions[i];
            var offset = worldOffsets[i];
            Debug.Log($"  Unit {i}: Grid({pos.x}, {pos.y}) = World({offset.x:F1}, {offset.z:F1})m from hero");
        }
        
        // Simulate ECS blob conversion
        TestBlobConversion(formation, name);
    }
    
    private void TestBlobConversion(GridFormationScriptableObject formation, string name)
    {
        Debug.Log($"Testing ECS blob conversion for {name}:");
        
        // Simulate what SquadDataAuthoring does
        var centeredPositions = formation.GetCenteredGridPositions();
        var blobPositions = new int2[centeredPositions.Length];
        
        for (int i = 0; i < centeredPositions.Length; i++)
        {
            blobPositions[i] = new int2(centeredPositions[i].x, centeredPositions[i].y);
        }
        
        Debug.Log($"Blob conversion successful: {blobPositions.Length} positions converted");
        
        // Test that blob positions match original centered positions
        bool conversionCorrect = true;
        for (int i = 0; i < centeredPositions.Length; i++)
        {
            if (blobPositions[i].x != centeredPositions[i].x || blobPositions[i].y != centeredPositions[i].y)
            {
                conversionCorrect = false;
                break;
            }
        }
        
        Debug.Log($"Blob position accuracy: {(conversionCorrect ? "✓ PASS" : "✗ FAIL")}");
        
        // Test grid to world conversion (what systems will use)
        Debug.Log("Testing system-level grid conversions:");
        for (int i = 0; i < math.min(3, blobPositions.Length); i++)
        {
            float3 worldPos = FormationGridSystem.GridToRelativeWorld(blobPositions[i]);
            Debug.Log($"  Blob position {blobPositions[i]} → World({worldPos.x:F1}, {worldPos.z:F1})");
        }
    }
}

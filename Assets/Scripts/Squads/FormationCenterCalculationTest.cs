using UnityEngine;

/// <summary>
/// Simple manual test to verify formation center calculations.
/// Run this in the console to verify the calculations match expected values.
/// </summary>
public static class FormationCenterCalculationTest
{
    [RuntimeInitializeOnLoadMethod]
    public static void RunTests()
    {
        Debug.Log("=== Formation Center Calculation Test ===");
        
        // Test Testudo formation: expected center (4, 5)
        TestFormationCenter("Testudo", new Vector2Int[]
        {
            new Vector2Int(3, 6), new Vector2Int(4, 6), new Vector2Int(5, 6),
            new Vector2Int(2, 5), new Vector2Int(3, 5), new Vector2Int(4, 5), new Vector2Int(5, 5), new Vector2Int(6, 5),
            new Vector2Int(3, 4), new Vector2Int(4, 4), new Vector2Int(5, 4), new Vector2Int(6, 4)
        }, new Vector2Int(4, 5));
        
        // Test Line formation: expected center (5, 4)
        TestFormationCenter("Line", new Vector2Int[]
        {
            new Vector2Int(2, 4), new Vector2Int(3, 4), new Vector2Int(4, 4), new Vector2Int(6, 4), new Vector2Int(7, 4), new Vector2Int(8, 4),
            new Vector2Int(2, 3), new Vector2Int(3, 3), new Vector2Int(4, 3), new Vector2Int(6, 3), new Vector2Int(7, 3), new Vector2Int(8, 3)
        }, new Vector2Int(5, 4));
    }
    
    private static void TestFormationCenter(string name, Vector2Int[] positions, Vector2Int expectedCenter)
    {
        if (positions.Length == 0)
        {
            Debug.LogError($"{name}: No positions provided!");
            return;
        }
        
        // Calculate bounds
        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;
        
        foreach (var pos in positions)
        {
            minX = Mathf.Min(minX, pos.x);
            maxX = Mathf.Max(maxX, pos.x);
            minY = Mathf.Min(minY, pos.y);
            maxY = Mathf.Max(maxY, pos.y);
        }
        
        // Calculate center using the same logic as GridFormationScriptableObject
        float centerX = (minX + maxX) / 2.0f;
        float centerY = (minY + maxY) / 2.0f;
        int finalCenterX = Mathf.RoundToInt(centerX);
        int finalCenterY = Mathf.RoundToInt(centerY);
        
        Vector2Int calculatedCenter = new Vector2Int(finalCenterX, finalCenterY);
        
        // Results
        int width = maxX - minX + 1;
        int height = maxY - minY + 1;
        bool isCorrect = calculatedCenter.x == expectedCenter.x && calculatedCenter.y == expectedCenter.y;
        string status = isCorrect ? "✓ PASS" : "✗ FAIL";
        
        Debug.Log($"{name} Formation:");
        Debug.Log($"  Bounds: X({minX}-{maxX}), Y({minY}-{maxY})");
        Debug.Log($"  Dimensions: {width}x{height}");
        Debug.Log($"  Mathematical center: ({centerX:F1}, {centerY:F1})");
        Debug.Log($"  Calculated center: ({calculatedCenter.x}, {calculatedCenter.y})");
        Debug.Log($"  Expected center: ({expectedCenter.x}, {expectedCenter.y})");
        Debug.Log($"  Status: {status}");
        
        if (!isCorrect)
        {
            Debug.LogError($"{name} formation center calculation failed!");
        }
        
        Debug.Log("");
    }
}

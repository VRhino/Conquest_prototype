using UnityEngine;
using Unity.Mathematics;

/// <summary>
/// Test script to verify formation center calculations and display results.
/// Attach this to a GameObject and assign formation assets to test.
/// </summary>
public class FormationCenterTest : MonoBehaviour
{
    [Header("Formation Assets to Test")]
    public GridFormationScriptableObject[] formationsToTest;

    [Header("Test Results (Read Only)")]
    [SerializeField] private FormationTestResult[] testResults;

    [System.Serializable]
    public struct FormationTestResult
    {
        public string formationName;
        public FormationType formationType;
        public Vector2Int originalCenter;
        public Vector2Int calculatedCenter;
        public Vector2Int[] originalPositions;
        public Vector2Int[] centeredPositions;
        public string summary;
    }

    [ContextMenu("Run Formation Center Tests")]
    public void RunTests()
    {
        if (formationsToTest == null || formationsToTest.Length == 0)
        {
            Debug.LogWarning("No formations assigned to test!");
            return;
        }

        testResults = new FormationTestResult[formationsToTest.Length];

        for (int i = 0; i < formationsToTest.Length; i++)
        {
            var formation = formationsToTest[i];
            if (formation == null)
            {
                Debug.LogWarning($"Formation at index {i} is null!");
                continue;
            }

            testResults[i] = AnalyzeFormation(formation);
            LogFormationAnalysis(testResults[i], formation);
        }

        Debug.Log($"Completed analysis of {formationsToTest.Length} formations. Check inspector for detailed results.");
    }

    private FormationTestResult AnalyzeFormation(GridFormationScriptableObject formation)
    {
        var result = new FormationTestResult();
        result.formationName = formation.name;
        result.formationType = formation.formationType;
        result.originalPositions = formation.gridPositions;
        result.calculatedCenter = formation.GetFormationCenter();
        result.centeredPositions = formation.GetCenteredGridPositions();

        // Calculate original bounds for comparison
        if (formation.gridPositions.Length > 0)
        {
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            foreach (var pos in formation.gridPositions)
            {
                minX = math.min(minX, pos.x);
                maxX = math.max(maxX, pos.x);
                minY = math.min(minY, pos.y);
                maxY = math.max(maxY, pos.y);
            }

            result.originalCenter = new Vector2Int((minX + maxX) / 2, (minY + maxY) / 2);
            
            int width = maxX - minX + 1;
            int height = maxY - minY + 1;
            result.summary = $"Grid: {width}x{height}, Center: {result.calculatedCenter}, Units: {formation.gridPositions.Length}";
        }

        return result;
    }

    private void LogFormationAnalysis(FormationTestResult result, GridFormationScriptableObject formation)
    {
        Debug.Log($"=== FORMATION ANALYSIS: {result.formationName} ===");
        Debug.Log($"Type: {result.formationType}");
        Debug.Log($"Summary: {result.summary}");
        Debug.Log($"Calculated Center: {result.calculatedCenter}");
        
        Debug.Log("Original Positions:");
        for (int i = 0; i < result.originalPositions.Length; i++)
        {
            Debug.Log($"  Unit {i}: {result.originalPositions[i]}");
        }

        Debug.Log("Centered Positions (Hero-Relative):");
        for (int i = 0; i < result.centeredPositions.Length; i++)
        {
            Debug.Log($"  Unit {i}: {result.centeredPositions[i]} (offset from hero)");
        }

        Debug.Log("World Offsets (in meters):");
        var worldOffsets = formation.GetCenteredWorldOffsets();
        for (int i = 0; i < worldOffsets.Length; i++)
        {
            Debug.Log($"  Unit {i}: {worldOffsets[i]}m from hero");
        }
    }

    /// <summary>
    /// Validates that formation centers match expected values
    /// </summary>
    [ContextMenu("Validate Expected Centers")]
    public void ValidateExpectedCenters()
    {
        if (formationsToTest == null || formationsToTest.Length == 0)
        {
            Debug.LogWarning("No formations assigned to test!");
            return;
        }

        Debug.Log("=== VALIDATING EXPECTED FORMATION CENTERS ===");

        foreach (var formation in formationsToTest)
        {
            if (formation == null) continue;

            var center = formation.GetFormationCenter();
            var bounds = GetFormationBounds(formation);
            
            Debug.Log($"\n--- {formation.name} ---");
            Debug.Log($"Grid Bounds: X({bounds.minX}-{bounds.maxX}), Y({bounds.minY}-{bounds.maxY})");
            Debug.Log($"Grid Dimensions: {bounds.maxX - bounds.minX + 1}x{bounds.maxY - bounds.minY + 1}");
            Debug.Log($"Calculated Center: ({center.x}, {center.y})");
            
            // Expected centers based on user requirements
            Vector2Int expectedCenter = Vector2Int.zero;
            bool hasExpected = false;
            
            switch (formation.formationType)
            {
                case FormationType.Line:
                    expectedCenter = new Vector2Int(5, 4);
                    hasExpected = true;
                    break;
                case FormationType.Testudo:
                    expectedCenter = new Vector2Int(4, 5);
                    hasExpected = true;
                    break;
            }
            
            if (hasExpected)
            {
                bool isCorrect = center.x == expectedCenter.x && center.y == expectedCenter.y;
                string status = isCorrect ? "✓ CORRECT" : "✗ INCORRECT";
                Debug.Log($"Expected Center: ({expectedCenter.x}, {expectedCenter.y}) - {status}");
                
                if (!isCorrect)
                {
                    Debug.LogError($"Formation {formation.name} has incorrect center! Expected ({expectedCenter.x}, {expectedCenter.y}) but got ({center.x}, {center.y})");
                }
            }
        }
    }

    private FormationBounds GetFormationBounds(GridFormationScriptableObject formation)
    {
        if (formation.gridPositions.Length == 0) 
            return new FormationBounds();

        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;

        foreach (var pos in formation.gridPositions)
        {
            minX = math.min(minX, pos.x);
            maxX = math.max(maxX, pos.x);
            minY = math.min(minY, pos.y);
            maxY = math.max(maxY, pos.y);
        }

        return new FormationBounds
        {
            minX = minX,
            maxX = maxX,
            minY = minY,
            maxY = maxY
        };
    }

    [System.Serializable]
    public struct FormationBounds
    {
        public int minX, maxX, minY, maxY;
    }

    private void OnValidate()
    {
        // Clear test results when formations change
        if (formationsToTest != null)
        {
            testResults = new FormationTestResult[formationsToTest.Length];
        }
    }
}

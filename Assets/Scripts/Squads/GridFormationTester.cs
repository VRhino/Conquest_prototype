using UnityEngine;
using Unity.Mathematics;

/// <summary>
/// Script de prueba para verificar el sistema de formaciones basadas en grid.
/// Adjunta este script a un GameObject en la escena para probar las funcionalidades.
/// </summary>
public class GridFormationTester : MonoBehaviour
{
    [Header("Test Settings")]
    [SerializeField] private bool runTests = false;
    [SerializeField] private bool logResults = true;
    
    [Header("Formation Assets to Test")]
    [SerializeField] private GridFormationScriptableObject[] testFormations;
    
    void Start()
    {
        if (runTests)
        {
            RunGridSystemTests();
        }
    }
    
    [ContextMenu("Run Grid System Tests")]
    public void RunGridSystemTests()
    {
        Log("=== Starting Grid Formation System Tests ===");
        
        TestGridConversions();
        TestFormationAssets();
        
        Log("=== Grid Formation System Tests Complete ===");
    }
    
    private void TestGridConversions()
    {
        Log("Testing Grid Conversions...");
        
        // Test basic grid conversions
        int2[] testGridPositions = {
            new int2(0, 0),    // Center
            new int2(1, 0),    // Right
            new int2(-1, 0),   // Left
            new int2(0, 1),    // Back
            new int2(0, -1),   // Front
            new int2(2, 3),    // Far position
        };
        
        foreach (var gridPos in testGridPositions)
        {
            // Test grid to world conversion
            float3 worldPos = FormationGridSystem.GridToRelativeWorld(gridPos);
            
            // Test world to grid conversion (round trip)
            int2 convertedBack = FormationGridSystem.RelativeWorldToGrid(worldPos);
            
            bool roundTripSuccess = gridPos.Equals(convertedBack);
            
            Log($"Grid({gridPos.x}, {gridPos.y}) → World({worldPos.x:F2}, {worldPos.z:F2}) → Grid({convertedBack.x}, {convertedBack.y}) | Round Trip: {roundTripSuccess}");
            
            if (!roundTripSuccess)
            {
                LogError($"Round trip failed for grid position {gridPos}!");
            }
        }
        
        // Test snapping
        float3[] testWorldPositions = {
            new float3(0.1f, 0, 0.1f),      // Should snap to (0,0)
            new float3(1.6f, 0, 2.1f),      // Should snap to nearest grid point
            new float3(-0.8f, 0, -1.1f),    // Should snap to nearest grid point
        };
        
        foreach (var worldPos in testWorldPositions)
        {
            float3 snapped = FormationGridSystem.SnapToGrid(worldPos);
            int2 gridPos = FormationGridSystem.RelativeWorldToGrid(snapped);
            
            Log($"World({worldPos.x:F2}, {worldPos.z:F2}) → Snapped({snapped.x:F2}, {snapped.z:F2}) → Grid({gridPos.x}, {gridPos.y})");
        }
    }
    
    private void TestFormationAssets()
    {
        Log("Testing Formation Assets...");
        
        if (testFormations == null || testFormations.Length == 0)
        {
            LogWarning("No test formations assigned. Please assign some GridFormationScriptableObject assets.");
            return;
        }
        
        foreach (var formation in testFormations)
        {
            if (formation == null) continue;
            
            Log($"Testing formation: {formation.name} (Type: {formation.formationType})");
            
            // Test formation area calculation
            Vector2 area = formation.GetFormationArea();
            Log($"  Formation area: {area.x:F1}m x {area.y:F1}m");
            
            // Test world offset conversion (using centered positions)
            Vector3[] worldOffsets = formation.GetCenteredWorldOffsets();
            Log($"  Unit count: {worldOffsets.Length}");
            
            // Show formation center
            Vector2Int center = formation.GetFormationCenter();
            Log($"  Formation center: ({center.x}, {center.y})");
            
            // Show some sample unit positions relative to hero (center)
            if (worldOffsets.Length > 0)
            {
                Log($"  Sample unit positions relative to hero:");
                for (int i = 0; i < math.min(3, worldOffsets.Length); i++)
                {
                    Vector3 offset = worldOffsets[i];
                    Log($"    Unit {i}: ({offset.x:F1}, {offset.z:F1})m from hero");
                }
            }
            
            // Test grid positions
            for (int i = 0; i < formation.gridPositions.Length; i++)
            {
                var gridPos = formation.gridPositions[i];
                var worldPos = worldOffsets[i];
                
                Log($"    Unit {i}: Grid({gridPos.x}, {gridPos.y}) → World({worldPos.x:F1}, {worldPos.z:F1})");
            }
        }
    }
    
    private void Log(string message)
    {
        if (logResults)
        {
            Debug.Log($"[GridFormationTester] {message}");
        }
    }
    
    private void LogWarning(string message)
    {
        Debug.LogWarning($"[GridFormationTester] {message}");
    }
    
    private void LogError(string message)
    {
        Debug.LogError($"[GridFormationTester] {message}");
    }
    
    // Visualize grid in scene view
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        
        // Draw grid around this object's position
        Vector3 center = transform.position;
        
        // Draw a 5x5 grid for visualization
        Gizmos.color = Color.cyan;
        for (int x = -2; x <= 2; x++)
        {
            for (int z = -2; z <= 2; z++)
            {
                float3 gridWorldPos = FormationGridSystem.GridToRelativeWorld(new int2(x, z));
                Vector3 worldPos = center + new Vector3(gridWorldPos.x, 0, gridWorldPos.z);
                
                // Draw grid cell
                Gizmos.DrawWireCube(worldPos, new Vector3(FormationGridSystem.CellWidth, 0.1f, FormationGridSystem.CellDepth));
                
                // Draw center point
                Gizmos.color = (x == 0 && z == 0) ? Color.red : Color.cyan;
                Gizmos.DrawSphere(worldPos, 0.1f);
                Gizmos.color = Color.cyan;
            }
        }
        
        // Draw formation positions if we have test formations
        if (testFormations != null)
        {
            Gizmos.color = Color.green;
            foreach (var formation in testFormations)
            {
                if (formation == null) continue;
                
                Vector3[] offsets = formation.GetCenteredWorldOffsets();
                for (int i = 0; i < offsets.Length; i++)
                {
                    Vector3 worldPos = center + offsets[i];
                    Gizmos.DrawSphere(worldPos, 0.15f);
                    
                    // Draw index
                    #if UNITY_EDITOR
                    UnityEditor.Handles.Label(worldPos + Vector3.up * 0.5f, i.ToString());
                    #endif
                }
                break; // Only show first formation for clarity
            }
        }
    }
}

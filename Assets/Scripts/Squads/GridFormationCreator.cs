using UnityEngine;
using UnityEditor;
using Unity.Mathematics;

#if UNITY_EDITOR
/// <summary>
/// Helper class to create common grid formation patterns in the editor.
/// </summary>
public static class GridFormationCreator
{
    [MenuItem("Tools/Formations/Create Line Formation (Grid)")]
    public static void CreateLineFormation()
    {
        var formation = ScriptableObject.CreateInstance<GridFormationScriptableObject>();
        formation.formationType = FormationType.Line;
        formation.name = "Line Formation (Grid)";
        
        // Line formation for 12 units (2 rows of 6) with 3-cell border
        // Grid size: 12x8 (6+6 width, 2+6 height for border)
        formation.gridPositions = new Vector2Int[]
        {
            // Row 1 (units at y=4, x=3-8)
            new Vector2Int(3, 4), new Vector2Int(4, 4), new Vector2Int(5, 4), 
            new Vector2Int(6, 4), new Vector2Int(7, 4), new Vector2Int(8, 4),
            // Row 2 (units at y=3, x=3-8)  
            new Vector2Int(3, 3), new Vector2Int(4, 3), new Vector2Int(5, 3),
            new Vector2Int(6, 3), new Vector2Int(7, 3), new Vector2Int(8, 3)
        };
        
        SaveFormationAsset(formation, "LineFormationGrid");
    }
    
    [MenuItem("Tools/Formations/Create Column Formation (Grid)")]
    public static void CreateColumnFormation()
    {
        var formation = ScriptableObject.CreateInstance<GridFormationScriptableObject>();
        formation.formationType = FormationType.Column;
        formation.name = "Column Formation (Grid)";
        
        // Column formation for 12 units (single file) with 3-cell border
        // Grid size: 8x18 (2+6 width, 12+6 height for border)
        formation.gridPositions = new Vector2Int[]
        {
            // Single column at x=4, y=3-14
            new Vector2Int(4, 14), new Vector2Int(4, 13), new Vector2Int(4, 12), new Vector2Int(4, 11),
            new Vector2Int(4, 10), new Vector2Int(4, 9),  new Vector2Int(4, 8),  new Vector2Int(4, 7),
            new Vector2Int(4, 6),  new Vector2Int(4, 5),  new Vector2Int(4, 4),  new Vector2Int(4, 3)
        };
        
        SaveFormationAsset(formation, "ColumnFormationGrid");
    }
    
    [MenuItem("Tools/Formations/Create Wedge Formation (Grid)")]
    public static void CreateWedgeFormation()
    {
        var formation = ScriptableObject.CreateInstance<GridFormationScriptableObject>();
        formation.formationType = FormationType.Wedge;
        formation.name = "Wedge Formation (Grid)";
        
        // Wedge formation for 12 units with 3-cell border
        // Grid size: 12x10 (6+6 width, 4+6 height for border)
        formation.gridPositions = new Vector2Int[]
        {
            // Row 1 (tip): 2 units at y=6, x=5-6
            new Vector2Int(5, 6), new Vector2Int(6, 6),
            // Row 2: 4 units at y=5, x=4-7
            new Vector2Int(4, 5), new Vector2Int(5, 5), new Vector2Int(6, 5), new Vector2Int(7, 5),
            // Row 3: 6 units at y=4, x=3-8
            new Vector2Int(3, 4), new Vector2Int(4, 4), new Vector2Int(5, 4), 
            new Vector2Int(6, 4), new Vector2Int(7, 4), new Vector2Int(8, 4)
        };
        
        SaveFormationAsset(formation, "WedgeFormationGrid");
    }
    
    [MenuItem("Tools/Formations/Create Square Formation (Grid)")]
    public static void CreateSquareFormation()
    {
        var formation = ScriptableObject.CreateInstance<GridFormationScriptableObject>();
        formation.formationType = FormationType.Square;
        formation.name = "Square Formation (Grid)";
        
        // Square formation for 12 units (3x4 rectangle) with 3-cell border
        // Grid size: 10x10 (4+6 width, 4+6 height for border)
        formation.gridPositions = new Vector2Int[]
        {
            // Row 1: 3 units at y=6, x=3-5
            new Vector2Int(3, 6), new Vector2Int(4, 6), new Vector2Int(5, 6),
            // Row 2: 3 units at y=5, x=3-5
            new Vector2Int(3, 5), new Vector2Int(4, 5), new Vector2Int(5, 5),
            // Row 3: 3 units at y=4, x=3-5
            new Vector2Int(3, 4), new Vector2Int(4, 4), new Vector2Int(5, 4),
            // Row 4: 3 units at y=3, x=3-5
            new Vector2Int(3, 3), new Vector2Int(4, 3), new Vector2Int(5, 3)
        };
        
        SaveFormationAsset(formation, "SquareFormationGrid");
    }
    
    [MenuItem("Tools/Formations/Create Testudo Formation (Grid)")]
    public static void CreateTestudoFormation()
    {
        var formation = ScriptableObject.CreateInstance<GridFormationScriptableObject>();
        formation.formationType = FormationType.Square; // Using Square enum for now
        formation.name = "Testudo Formation (Grid)";
        
        // Testudo formation for 12 units (4x3 rectangle) with 3-cell border
        // Grid size: 10x9 (4+6 width, 3+6 height for border)
        formation.gridPositions = new Vector2Int[]
        {
            // Row 1: 4 units at y=5, x=3-6
            new Vector2Int(3, 5), new Vector2Int(4, 5), new Vector2Int(5, 5), new Vector2Int(6, 5),
            // Row 2: 4 units at y=4, x=3-6
            new Vector2Int(3, 4), new Vector2Int(4, 4), new Vector2Int(5, 4), new Vector2Int(6, 4),
            // Row 3: 4 units at y=3, x=3-6
            new Vector2Int(3, 3), new Vector2Int(4, 3), new Vector2Int(5, 3), new Vector2Int(6, 3)
        };
        
        SaveFormationAsset(formation, "TestudoFormationGrid");
    }
    
    private static void SaveFormationAsset(GridFormationScriptableObject formation, string fileName)
    {
        string path = $"Assets/ScriptableObjects/Formations/{fileName}.asset";
        
        // Ensure the directory exists
        string directory = System.IO.Path.GetDirectoryName(path);
        if (!System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }
        
        AssetDatabase.CreateAsset(formation, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Selection.activeObject = formation;
        EditorUtility.FocusProjectWindow();
        
        Debug.Log($"Created grid formation asset: {path}");
    }
}

/// <summary>
/// Custom property drawer for GridFormationScriptableObject to provide better editor experience.
/// </summary>
[CustomEditor(typeof(GridFormationScriptableObject))]
public class GridFormationScriptableObjectEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        GridFormationScriptableObject formation = (GridFormationScriptableObject)target;
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Formation Info", EditorStyles.boldLabel);
        
        if (formation.gridPositions != null && formation.gridPositions.Length > 0)
        {
            Vector2 area = formation.GetFormationArea();
            EditorGUILayout.LabelField($"Formation Area: {area.x:F1}m x {area.y:F1}m");
            EditorGUILayout.LabelField($"Unit Count: {formation.gridPositions.Length}");
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Grid Positions:", EditorStyles.boldLabel);
            
            for (int i = 0; i < formation.gridPositions.Length; i++)
            {
                var pos = formation.gridPositions[i];
                float3 worldPos = FormationGridSystem.GridToRelativeWorld(new int2(pos.x, pos.y));
                EditorGUILayout.LabelField($"Unit {i}: Grid({pos.x}, {pos.y}) → World({worldPos.x:F1}, {worldPos.z:F1})");
            }
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Note: Hero is not included in grid positions. All positions are for squad units only.", MessageType.Info);
        
        if (GUILayout.Button("Refresh Inspector"))
        {
            EditorUtility.SetDirty(formation);
            Repaint();
        }
    }
}
#endif

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(GridFormationScriptableObject))]
public class GridFormationEditor : Editor
{
    private int gridColumns = 10;
    private int gridRows = 10;
    private HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();
    private bool initialized = false;

    private Texture2D occupiedTex;
    private Texture2D emptyTex;
    private Texture2D heroTex;
    private GUIStyle cellStyle;
    private GUIStyle labelStyle;
    private GUIStyle frontLabelStyle;

    private const int CELL_SIZE = 28;

    private void InitializeFromData()
    {
        var formation = (GridFormationScriptableObject)target;
        occupiedCells.Clear();

        if (formation.gridPositions != null)
        {
            int maxX = 0, maxY = 0;
            foreach (var pos in formation.gridPositions)
            {
                occupiedCells.Add(pos);
                if (pos.x + 1 > maxX) maxX = pos.x + 1;
                if (pos.y + 1 > maxY) maxY = pos.y + 1;
            }
            // Ensure grid is large enough for existing data + some padding
            gridColumns = Mathf.Max(gridColumns, maxX + 2);
            gridRows = Mathf.Max(gridRows, maxY + 2);
        }

        initialized = true;
    }

    private void EnsureTextures()
    {
        if (occupiedTex == null)
        {
            occupiedTex = MakeTex(CELL_SIZE, CELL_SIZE, new Color(0.2f, 0.7f, 0.3f, 1f));
            emptyTex = MakeTex(CELL_SIZE, CELL_SIZE, new Color(0.22f, 0.22f, 0.22f, 1f));
            heroTex = MakeTex(CELL_SIZE, CELL_SIZE, new Color(0.9f, 0.7f, 0.1f, 1f));
        }
    }

    private void EnsureStyles()
    {
        if (cellStyle == null)
        {
            cellStyle = new GUIStyle(GUI.skin.button)
            {
                fixedWidth = CELL_SIZE,
                fixedHeight = CELL_SIZE,
                margin = new RectOffset(1, 1, 1, 1),
                padding = new RectOffset(0, 0, 0, 0),
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                fontStyle = FontStyle.Bold
            };
        }

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fixedWidth = CELL_SIZE,
                fixedHeight = CELL_SIZE,
                alignment = TextAnchor.MiddleCenter
            };
        }

        if (frontLabelStyle == null)
        {
            frontLabelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.4f, 0.2f) }
            };
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EnsureTextures();
        EnsureStyles();

        if (!initialized)
            InitializeFromData();

        // Draw default fields except gridPositions
        DrawPropertiesExcluding(serializedObject, "gridPositions", "m_Script");

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Grid Formation Editor", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Click cells to toggle unit positions. Yellow cell = hero origin (0,0). Green = occupied. " +
            "Row 0 is the FRONT of the formation (closest to enemy).",
            MessageType.Info);

        // Grid size controls
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Grid Size:", GUILayout.Width(65));
        int newCols = EditorGUILayout.IntField("Columns", gridColumns);
        int newRows = EditorGUILayout.IntField("Rows", gridRows);
        EditorGUILayout.EndHorizontal();

        newCols = Mathf.Clamp(newCols, 3, 30);
        newRows = Mathf.Clamp(newRows, 3, 30);
        if (newCols != gridColumns || newRows != gridRows)
        {
            gridColumns = newCols;
            gridRows = newRows;
        }

        // Utility buttons
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear All", GUILayout.Width(80)))
        {
            Undo.RecordObject(target, "Clear Formation Grid");
            occupiedCells.Clear();
            ApplyToSerializedData();
        }
        EditorGUILayout.LabelField($"Units: {occupiedCells.Count}", EditorStyles.miniLabel, GUILayout.Width(70));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // FRONT label
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(CELL_SIZE + 4); // offset for row labels
        EditorGUILayout.LabelField("\u25BC  FRONT (Row 0)  \u25BC", frontLabelStyle,
            GUILayout.Width(gridColumns * (CELL_SIZE + 2)));
        EditorGUILayout.EndHorizontal();

        // Column headers
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(CELL_SIZE + 4);
        for (int x = 0; x < gridColumns; x++)
        {
            GUILayout.Label(x.ToString(), labelStyle);
        }
        EditorGUILayout.EndHorizontal();

        // Grid rows (row 0 at top = front)
        for (int y = 0; y < gridRows; y++)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(y.ToString(), labelStyle);

            for (int x = 0; x < gridColumns; x++)
            {
                var pos = new Vector2Int(x, y);
                bool isOccupied = occupiedCells.Contains(pos);
                bool isHeroOrigin = (x == 0 && y == 0);

                // Set background color
                if (isHeroOrigin)
                    cellStyle.normal.background = heroTex;
                else if (isOccupied)
                    cellStyle.normal.background = occupiedTex;
                else
                    cellStyle.normal.background = emptyTex;

                string label = isOccupied ? "\u2588" : "";
                if (isHeroOrigin && !isOccupied) label = "H";

                if (GUILayout.Button(label, cellStyle))
                {
                    Undo.RecordObject(target, "Toggle Formation Cell");
                    if (isOccupied)
                        occupiedCells.Remove(pos);
                    else
                        occupiedCells.Add(pos);
                    ApplyToSerializedData();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(5);

        // Show raw data as readonly reference
        EditorGUI.BeginDisabledGroup(true);
        var gridProp = serializedObject.FindProperty("gridPositions");
        if (gridProp != null)
            EditorGUILayout.PropertyField(gridProp, new GUIContent("Raw Grid Positions"), true);
        EditorGUI.EndDisabledGroup();

        serializedObject.ApplyModifiedProperties();
    }

    private void ApplyToSerializedData()
    {
        var formation = (GridFormationScriptableObject)target;

        var positions = new List<Vector2Int>(occupiedCells);
        // Sort for deterministic ordering: by row (y) then column (x)
        positions.Sort((a, b) =>
        {
            int cmp = a.y.CompareTo(b.y);
            return cmp != 0 ? cmp : a.x.CompareTo(b.x);
        });

        formation.gridPositions = positions.ToArray();
        EditorUtility.SetDirty(target);
    }

    private static Texture2D MakeTex(int width, int height, Color col)
    {
        var pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;
        var result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    private void OnDisable()
    {
        if (occupiedTex != null) DestroyImmediate(occupiedTex);
        if (emptyTex != null) DestroyImmediate(emptyTex);
        if (heroTex != null) DestroyImmediate(heroTex);
    }
}

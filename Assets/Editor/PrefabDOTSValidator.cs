using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;

public class PrefabDOTSValidator : EditorWindow
{
    private GameObject prefabToCheck;
    private Vector2 scroll;
    private List<string> invalidComponents = new List<string>();

    [MenuItem("Tools/Validate DOTS Prefab")]
    public static void ShowWindow()
    {
        GetWindow<PrefabDOTSValidator>("DOTS Prefab Validator");
    }

    void OnGUI()
    {
        GUILayout.Label("DOTS Prefab Validator", EditorStyles.boldLabel);
        prefabToCheck = (GameObject)EditorGUILayout.ObjectField("Prefab", prefabToCheck, typeof(GameObject), false);

        if (GUILayout.Button("Validate Prefab"))
        {
            ValidatePrefab();
        }

        if (invalidComponents.Count > 0)
        {
            GUILayout.Label("Non-DOTS or suspicious components found:", EditorStyles.boldLabel);
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(200));
            foreach (var comp in invalidComponents)
            {
                GUILayout.Label(comp);
            }
            GUILayout.EndScrollView();
        }
    }

    void ValidatePrefab()
    {
        invalidComponents.Clear();
        if (prefabToCheck == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign a prefab to validate.", "OK");
            return;
        }
        var allComponents = prefabToCheck.GetComponentsInChildren<Component>(true);
        foreach (var comp in allComponents)
        {
            if (comp == null) continue;
            var type = comp.GetType();
            // Allow Transforms and DOTS authoring scripts (MonoBehaviour with Baker)
            if (type == typeof(Transform) || type == typeof(RectTransform))
                continue;
            if (type.Namespace != null && type.Namespace.Contains("Unity.Entities"))
                continue;
            // Allow MeshRenderer, MeshFilter, SkinnedMeshRenderer, SpriteRenderer
            if (type == typeof(MeshRenderer) || type == typeof(MeshFilter) || type == typeof(SkinnedMeshRenderer) || type == typeof(SpriteRenderer))
                continue;
            // Allow scripts with "Authoring" in the name (convention)
            if (type.Name.Contains("Authoring"))
                continue;
            invalidComponents.Add($"{type.FullName} on GameObject '{comp.gameObject.name}'");
        }
        if (invalidComponents.Count == 0)
        {
            EditorUtility.DisplayDialog("Validation Result", "Prefab is DOTS compatible!", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Validation Result", $"Found {invalidComponents.Count} non-DOTS components.", "OK");
        }
    }
}

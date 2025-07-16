using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class TreeLeafPathExporter : MonoBehaviour
{
    [Header("Assign the root of the tree")]
    public Transform root;

    [ContextMenu("Export Leaf Paths")]
    public void ExportLeafPaths()
    {
        if (root == null)
        {
            Debug.LogError("Root is not assigned.");
            return;
        }

        List<string> leafPaths = new List<string>();
        CollectLeafPaths(root, root.name, leafPaths);

        string filePath = Path.Combine(Application.dataPath, "leaf_paths.txt");
        File.WriteAllLines(filePath, leafPaths, Encoding.UTF8);
        Debug.Log($"Leaf paths exported to: {filePath}");
    }

    private void CollectLeafPaths(Transform node, string currentPath, List<string> result)
    {
        if (node.childCount == 0)
        {
            result.Add(currentPath);
            return;
        }
        for (int i = 0; i < node.childCount; i++)
        {
            var child = node.GetChild(i);
            CollectLeafPaths(child, currentPath + "/" + child.name, result);
        }
    }
}

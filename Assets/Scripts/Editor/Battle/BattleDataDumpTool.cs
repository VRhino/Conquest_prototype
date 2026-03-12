using UnityEngine;
using UnityEditor;
using System.IO;
using System;

public static class BattleDataDumpTool
{
    [MenuItem("Tools/Battle/Dump Battle Data")]
    public static void DumpBattleData()
    {
        BattleData data = GetBattleData();
        if (data == null)
        {
            EditorUtility.DisplayDialog("Battle Data Dump", "No hay BattleData disponible.\n\nEntra en Play Mode con la BattleScene activa, o asegúrate de que BattleTransitionData tiene datos.", "OK");
            return;
        }

        string json = JsonUtility.ToJson(data, true);

        // Log a consola
        Debug.Log($"[BattleDataDump]\n{json}");

        // Guardar a archivo
        string dir = Path.Combine(Application.dataPath, "Debug", "BattleDataDumps");
        Directory.CreateDirectory(dir);
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string filePath = Path.Combine(dir, $"battle_dump_{timestamp}.json");
        File.WriteAllText(filePath, json);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Battle Data Dump", $"Dump guardado en:\nAssets/Debug/BattleDataDumps/battle_dump_{timestamp}.json", "OK");
    }

    private static BattleData GetBattleData()
    {
        // 1. Play Mode: buscar BattleSceneController activo
        if (Application.isPlaying)
        {
            var controller = UnityEngine.Object.FindFirstObjectByType<BattleSceneController>();
            if (controller != null && controller.CurrentBattleData != null)
                return controller.CurrentBattleData;
        }

        // 2. Fallback: BattleTransitionData singleton (en memoria)
        // _battleData es privado y GetAndClearBattleData() lo consume — usamos reflection para leer sin destruir
        if (BattleTransitionData.Instance.HasBattleData())
        {
            var field = typeof(BattleTransitionData)
                .GetField("_battleData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(BattleTransitionData.Instance) as BattleData;
        }

        return null;
    }
}

using UnityEditor;
using UnityEngine;

/// <summary>
/// One-off conversion of the old integer-grid formations into data-driven <see cref="FormationDefinition"/>s,
/// in place (asset GUIDs and every reference are kept). Defaults per formation type follow tasks/todo.md (decisions 1-6).
/// </summary>
public static class FormationMigration
{
    [MenuItem("Tools/Formations/Migrate Legacy Grid Formations")]
    public static void MigrateAll() => Debug.Log($"[FormationMigration] {Run()}");

    /// <summary>Converts every formation that still holds the legacy grid; returns a one-line report.</summary>
    public static string Run()
    {
        int migrated = 0;
        var report = new System.Text.StringBuilder();
        foreach (string guid in AssetDatabase.FindAssets("t:FormationDefinition"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var formation = AssetDatabase.LoadAssetAtPath<FormationDefinition>(path);
            if (formation == null || !formation.HasLegacyGrid) continue;

            switch (formation.formationType)
            {
                case FormationType.Testudo:
                    formation.MigrateFromLegacyGrid(0.8f, 0f, 0f, FormationLayoutKind.Template, flanksFaceOutward: true);
                    break;
                case FormationType.Dispersed:
                    formation.MigrateFromLegacyGrid(2f, 0.4f, 8f, FormationLayoutKind.Scatter, flanksFaceOutward: false);
                    break;
                default:
                    formation.MigrateFromLegacyGrid(1.1f, 0.08f, 3f, FormationLayoutKind.Template, flanksFaceOutward: false);
                    break;
            }
            EditorUtility.SetDirty(formation);
            migrated++;
            report.Append($" {formation.name}({formation.formationType}, {formation.layout}, {formation.EffectiveSpacing} m)");
        }

        // Rewrite squads so the renamed field (gridFormations → formations) is stored under its new name.
        foreach (string guid in AssetDatabase.FindAssets("t:SquadData"))
        {
            var squad = AssetDatabase.LoadAssetAtPath<SquadData>(AssetDatabase.GUIDToAssetPath(guid));
            if (squad != null) EditorUtility.SetDirty(squad);
        }
        AssetDatabase.SaveAssets();
        return $"migrated {migrated}:{report}";
    }
}

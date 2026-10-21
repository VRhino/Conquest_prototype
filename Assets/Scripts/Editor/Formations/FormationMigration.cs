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

    [MenuItem("Tools/Formations/Apply Default Postures")]
    public static void ApplyPosturesMenu() => Debug.Log($"[FormationMigration] {ApplyDefaultPostures()}");

    /// <summary>
    /// Default posture tables (tasks/todo.md, decision 7) for shield formations that have none yet: Testudo front
    /// shield in front, flanks to the side, interior and rear over the head; Shield Wall braced when formed.
    /// </summary>
    public static string ApplyDefaultPostures()
    {
        int applied = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:FormationDefinition"))
        {
            var formation = AssetDatabase.LoadAssetAtPath<FormationDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (formation == null || (formation.postures != null && formation.postures.Length > 0)) continue;
            if (formation.formationType == FormationType.Testudo)
                formation.postures = new[]
                {
                    Posture(FormationSlotRole.Front, UnitStance.ShieldFront, UnitStance.ShieldFront),
                    Posture(FormationSlotRole.Flank, UnitStance.ShieldSide, UnitStance.ShieldSide),
                    Posture(FormationSlotRole.Interior, UnitStance.ShieldUp, UnitStance.ShieldUp),
                    Posture(FormationSlotRole.Rear, UnitStance.ShieldUp, UnitStance.ShieldUp),
                };
            else if (formation.formationType == FormationType.ShieldWall)
                formation.postures = new[]
                {
                    Posture(FormationSlotRole.Front, UnitStance.BracedShields, UnitStance.ShieldFront),
                    Posture(FormationSlotRole.Flank, UnitStance.BracedShields, UnitStance.ShieldFront),
                    Posture(FormationSlotRole.Interior, UnitStance.BracedShields, UnitStance.ShieldFront),
                    Posture(FormationSlotRole.Rear, UnitStance.BracedShields, UnitStance.ShieldFront),
                };
            else continue;
            EditorUtility.SetDirty(formation);
            applied++;
        }
        AssetDatabase.SaveAssets();
        return $"postures applied to {applied} formation(s)";
    }

    static FormationRolePosture Posture(FormationSlotRole role, UnitStance formed, UnitStance moving) =>
        new FormationRolePosture { role = role, formed = formed, moving = moving };

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

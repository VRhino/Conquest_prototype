using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the rest formation («En reposo») of every squad in Resources/Squads and assigns it to
/// <see cref="SquadData.restFormation"/>. Levies stand loose and out of order (Scatter); drilled troops close together in
/// a block. The values are a starting point: each asset is then tuned by hand like any other formation.
/// Batch: <c>-executeMethod RestFormationSetup.CreateAll</c>. An existing rest formation is left as it is.
/// </summary>
public static class RestFormationSetup
{
    struct Profile
    {
        public FormationLayoutKind layout;
        public int columns;
        public float spacing, noiseRadius, noiseAngle;
    }

    static Profile Block(int columns, float spacing, float noise, float angle) => new Profile
        { layout = FormationLayoutKind.Template, columns = columns, spacing = spacing, noiseRadius = noise, noiseAngle = angle };

    static Profile Loose(float spacing, float noise, float angle) => new Profile
        { layout = FormationLayoutKind.Scatter, spacing = spacing, noiseRadius = noise, noiseAngle = angle };

    /// <summary>By <see cref="SquadData.id"/>: how much drill and cohesion the troop has.</summary>
    static readonly Dictionary<string, Profile> Profiles = new Dictionary<string, Profile>
    {
        { "arc01", Loose(2.2f, 0.7f, 30f) },        // levy archers: loose, out of order
        { "spm01", Block(5, 1.1f, 0.05f, 3f) },     // spearmen: close and in order
        { "pkm01", Block(5, 1.1f, 0.05f, 3f) },     // pikemen
        { "sqd01", Block(5, 1.3f, 0.15f, 8f) },     // squires
        { "hac01", Block(4, 2.8f, 0.3f, 10f) },     // horse archers
        { "lcv01", Block(4, 2.8f, 0.3f, 10f) },     // lance cavalry
        { "scv01", Block(4, 2.8f, 0.3f, 10f) },     // sword cavalry
    };

    static readonly Profile Unknown = Loose(1.8f, 0.5f, 20f);

    [MenuItem("Tools/Formations/Create Rest Formations")]
    public static void CreateAll()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:SquadData", new[] { "Assets/Resources/Squads" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<SquadData>(path);
            if (data == null || data.restFormation != null)
                continue;
            if (!Profiles.TryGetValue(data.id ?? "", out var profile))
            {
                Debug.LogWarning($"[RestFormationSetup] {data.name} ({data.id}) has no profile; it gets a loose rest formation.");
                profile = Unknown;
            }
            data.restFormation = Create(data, path, profile);
            EditorUtility.SetDirty(data);
            Debug.Log($"[RestFormationSetup] {data.name}: {profile.layout}, spacing {profile.spacing} m, {data.unitCount} soldiers");
        }
        AssetDatabase.SaveAssets();
    }

    static FormationDefinition Create(SquadData data, string squadPath, Profile profile)
    {
        var folder = Path.Combine(Path.GetDirectoryName(squadPath), "Formations").Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(squadPath).Replace('\\', '/'), "Formations");

        var rest = ScriptableObject.CreateInstance<FormationDefinition>();
        rest.formationType = FormationType.Rest;
        rest.layout = profile.layout;
        rest.spacing = profile.spacing;
        rest.noiseRadius = profile.noiseRadius;
        rest.noiseAngleDegrees = profile.noiseAngle;
        rest.useNormalPostureInFreeCombat = true;
        if (profile.layout == FormationLayoutKind.Template)
            rest.templates = new[] { new FormationTemplate { minUnits = 0, slots = BlockSlots(profile.columns, data.unitCount) } };

        AssetDatabase.CreateAsset(rest, $"{folder}/{data.name.Replace(" ", "")}_Rest.asset");
        return rest;
    }

    /// <summary>Rows of <paramref name="columns"/> from the front back, one spare row for reinforcements.</summary>
    static FormationSlot[] BlockSlots(int columns, int soldiers)
    {
        int rows = Mathf.CeilToInt(Mathf.Max(1, soldiers) / (float)columns) + 1;
        var cells = new Vector2Int[rows * columns];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < columns; c++)
                cells[r * columns + c] = new Vector2Int(c, -r);
        return FormationSlotRoles.FromCells(cells, false);
    }
}

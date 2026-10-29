using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Data of one formation: layout, spacing, organic noise and templates per number of soldiers alive.
/// Slot positions are in spacing units, so the same template can be tightened or opened in play.
/// See Docs/Design/Movimiento_Formaciones.md §11.
/// </summary>
[CreateAssetMenu(menuName = "Formations/Formation Definition")]
public class FormationDefinition : ScriptableObject
{
    /// <summary>Spacing floor; smaller or invalid values fall back to it.</summary>
    public const float MinSpacing = 0.3f;

    public FormationType formationType;
    public Sprite formationIcon;
    public FormationLayoutKind layout = FormationLayoutKind.Template;

    [Tooltip("Distance between neighbouring slots (m). For Scatter, the cell size.")]
    [Min(MinSpacing)] public float spacing = 1f;
    [Tooltip("Organic position noise per slot (m), deterministic per squad and slot. For Scatter, the jitter.")]
    [Min(0f)] public float noiseRadius;
    [Tooltip("Organic facing noise per slot (degrees).")]
    [Min(0f)] public float noiseAngleDegrees;

    [Tooltip("Templates by minimum soldiers alive; the first one whose minimum fits is used. Ignored by Scatter.")]
    public FormationTemplate[] templates = System.Array.Empty<FormationTemplate>();

    [Tooltip("Posture per slot role (shield front, side or up). Roles not listed use Normal.")]
    public FormationRolePosture[] postures = System.Array.Empty<FormationRolePosture>();

    [Tooltip("Use Normal posture for an Attack order or pursuit outside Hold Position, even for a former front-rank soldier. Hold Position keeps the rank posture.")]
    public bool useNormalPostureInFreeCombat;

    /// <summary>Integer cells of the replaced grid model, read once by the migration.</summary>
    [SerializeField, HideInInspector, FormerlySerializedAs("gridPositions")]
    private Vector2Int[] legacyGridPositions;

    /// <summary>Spacing actually used: at least <see cref="MinSpacing"/>.</summary>
    public float EffectiveSpacing => float.IsFinite(spacing) && spacing >= MinSpacing ? spacing : MinSpacing;

    /// <summary>Most soldiers this formation can place (unbounded for Scatter).</summary>
    public int Capacity
    {
        get
        {
            if (layout == FormationLayoutKind.Scatter) return int.MaxValue;
            int max = 0;
            if (templates != null)
                foreach (var t in templates)
                    if (t?.slots != null) max = Mathf.Max(max, t.slots.Length);
            return max;
        }
    }

    /// <summary>Null when coherent; otherwise the first problem found.</summary>
    public string Validate()
    {
        if (!float.IsFinite(spacing) || spacing < MinSpacing) return $"spacing must be at least {MinSpacing} m.";
        if (layout == FormationLayoutKind.Scatter) return null;
        if (templates == null || templates.Length == 0) return "a Template formation needs at least one template.";
        for (int i = 0; i < templates.Length; i++)
        {
            var t = templates[i];
            if (t == null || t.slots == null || t.slots.Length == 0) return $"template {i} has no slots.";
            if (i > 0 && templates[i - 1] != null && t.minUnits >= templates[i - 1].minUnits)
                return "templates must be sorted by minUnits, largest first.";
            var seen = new HashSet<Vector2>();
            foreach (var s in t.slots)
                if (!seen.Add(s.position)) return $"template {i} repeats the slot {s.position}.";
        }
        return null;
    }

    void OnValidate()
    {
        string issue = Validate();
        if (issue != null) Debug.LogWarning($"[FormationDefinition] {name}: {issue}", this);
    }

    /// <summary>True until the migration has converted the legacy grid.</summary>
    public bool HasLegacyGrid => legacyGridPositions != null && legacyGridPositions.Length > 0;

#if UNITY_EDITOR

    /// <summary>
    /// Converts the legacy integer grid into one centred template (front = largest grid y), with roles derived from the
    /// layout: front row, extreme columns (flanks), last row (rear) and the rest (interior).
    /// </summary>
    public void MigrateFromLegacyGrid(float newSpacing, float newNoiseRadius, float newNoiseAngle,
        FormationLayoutKind newLayout, bool flanksFaceOutward)
    {
        var cells = legacyGridPositions ?? System.Array.Empty<Vector2Int>();
        layout = newLayout;
        spacing = Mathf.Max(MinSpacing, newSpacing);
        noiseRadius = newNoiseRadius;
        noiseAngleDegrees = newNoiseAngle;
        templates = cells.Length == 0
            ? System.Array.Empty<FormationTemplate>()
            : new[] { new FormationTemplate { minUnits = 0, slots = FormationSlotRoles.FromCells(cells, flanksFaceOutward) } };
        legacyGridPositions = null;
    }
#endif
}

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector for <see cref="FormationDefinition"/>: paints the slots of each template on a half-unit grid
/// (top = front = local forward), shows rows, roles and facings, and recomputes them from the layout.
/// </summary>
[CustomEditor(typeof(FormationDefinition))]
public class FormationDefinitionEditor : Editor
{
    const float Step = 0.5f;      // Authoring grid step, in spacing units.
    const int HalfExtent = 12;    // Grid goes from -HalfExtent to +HalfExtent steps on each axis.
    const int CellSize = 22;

    int _template;
    bool _flanksFaceOutward;
    GUIStyle _cell;

    static readonly Color Empty = new(0.22f, 0.22f, 0.22f);
    static readonly Color Anchor = new(0.9f, 0.7f, 0.1f);
    static readonly Dictionary<FormationSlotRole, Color> RoleColors = new()
    {
        { FormationSlotRole.Front, new Color(0.85f, 0.35f, 0.25f) },
        { FormationSlotRole.Flank, new Color(0.25f, 0.55f, 0.85f) },
        { FormationSlotRole.Interior, new Color(0.25f, 0.65f, 0.35f) },
        { FormationSlotRole.Rear, new Color(0.55f, 0.45f, 0.75f) },
    };

    public override void OnInspectorGUI()
    {
        var formation = (FormationDefinition)target;
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "templates");
        serializedObject.ApplyModifiedProperties();

        if (formation.HasLegacyGrid)
            EditorGUILayout.HelpBox("This formation still holds the old integer grid. Run Tools/Formations/Migrate Legacy Grid Formations.", MessageType.Warning);
        string issue = formation.Validate();
        if (issue != null) EditorGUILayout.HelpBox(issue, MessageType.Warning);

        if (formation.layout == FormationLayoutKind.Scatter)
        {
            EditorGUILayout.HelpBox("Scatter: each soldier takes a distinct random cell (seeded per squad) of size 'spacing', with 'noiseRadius' as jitter. No templates.", MessageType.Info);
            return;
        }

        DrawTemplateToolbar(formation);
        if (formation.templates == null || formation.templates.Length == 0) return;
        _template = Mathf.Clamp(_template, 0, formation.templates.Length - 1);
        var template = formation.templates[_template];

        EditorGUI.BeginChangeCheck();
        int minUnits = EditorGUILayout.IntField("Min soldiers alive", template.minUnits);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(formation, "Edit template minimum");
            template.minUnits = Mathf.Max(0, minUnits);
            EditorUtility.SetDirty(formation);
        }

        EditorGUILayout.BeginHorizontal();
        _flanksFaceOutward = EditorGUILayout.ToggleLeft("Flanks face outward, rear faces back", _flanksFaceOutward);
        if (GUILayout.Button("Recompute rows, roles and facing"))
            Modify(formation, "Recompute slot roles", () =>
                template.slots = FormationSlotRoles.Assign(Positions(template), _flanksFaceOutward));
        if (GUILayout.Button("Centre"))
            Modify(formation, "Centre template", () => Centre(template));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField($"Slots: {template.slots.Length}   ·   footprint with spacing {formation.EffectiveSpacing:0.##} m: {Footprint(template, formation.EffectiveSpacing)}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("▲ FRONT (local forward)", EditorStyles.centeredGreyMiniLabel);
        DrawGrid(formation, template);
        EditorGUILayout.LabelField("Colours: red front · blue flank · green interior · purple rear. Number = row, arrow = facing.", EditorStyles.miniLabel);
    }

    void DrawTemplateToolbar(FormationDefinition formation)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Templates (largest minimum first)", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        int count = formation.templates?.Length ?? 0;
        if (count > 0)
        {
            var names = new string[count];
            for (int i = 0; i < count; i++) names[i] = $"≥ {formation.templates[i].minUnits} ({formation.templates[i].slots.Length})";
            _template = GUILayout.Toolbar(Mathf.Clamp(_template, 0, count - 1), names);
        }
        if (GUILayout.Button("Add", GUILayout.Width(50)))
            Modify(formation, "Add template", () =>
            {
                var list = new List<FormationTemplate>(formation.templates ?? System.Array.Empty<FormationTemplate>());
                var copy = count > 0 ? formation.templates[_template] : null;
                list.Add(new FormationTemplate
                {
                    minUnits = 0,
                    slots = copy != null ? (FormationSlot[])copy.slots.Clone() : System.Array.Empty<FormationSlot>()
                });
                formation.templates = list.ToArray();
                _template = list.Count - 1;
            });
        if (count > 0 && GUILayout.Button("Remove", GUILayout.Width(60)))
            Modify(formation, "Remove template", () =>
            {
                var list = new List<FormationTemplate>(formation.templates);
                list.RemoveAt(_template);
                formation.templates = list.ToArray();
                _template = Mathf.Max(0, _template - 1);
            });
        EditorGUILayout.EndHorizontal();
    }

    void DrawGrid(FormationDefinition formation, FormationTemplate template)
    {
        _cell ??= new GUIStyle(GUI.skin.button)
        {
            fixedWidth = CellSize, fixedHeight = CellSize, margin = new RectOffset(1, 1, 1, 1),
            padding = new RectOffset(0, 0, 0, 0), fontSize = 9, alignment = TextAnchor.MiddleCenter
        };
        var bySlot = new Dictionary<Vector2, int>();
        for (int i = 0; i < template.slots.Length; i++) bySlot[template.slots[i].position] = i;

        for (int gy = HalfExtent; gy >= -HalfExtent; gy--)
        {
            EditorGUILayout.BeginHorizontal();
            for (int gx = -HalfExtent; gx <= HalfExtent; gx++)
            {
                var position = new Vector2(gx * Step, gy * Step);
                bool occupied = bySlot.TryGetValue(position, out int index);
                Color previous = GUI.backgroundColor;
                GUI.backgroundColor = occupied ? RoleColors[template.slots[index].role]
                    : gx == 0 && gy == 0 ? Anchor : Empty;
                string label = occupied ? $"{template.slots[index].row}{Arrow(template.slots[index].headingDegrees)}" : "";
                if (GUILayout.Button(label, _cell))
                    Modify(formation, "Toggle slot", () => Toggle(template, position, occupied ? index : -1));
                GUI.backgroundColor = previous;
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    static void Toggle(FormationTemplate template, Vector2 position, int index)
    {
        var list = new List<FormationSlot>(template.slots);
        if (index >= 0) list.RemoveAt(index);
        else list.Add(new FormationSlot { position = position });
        template.slots = list.ToArray();
    }

    static void Centre(FormationTemplate template)
    {
        if (template.slots.Length == 0) return;
        Vector2 min = template.slots[0].position, max = min;
        foreach (var s in template.slots) { min = Vector2.Min(min, s.position); max = Vector2.Max(max, s.position); }
        Vector2 centre = (min + max) * 0.5f;
        centre = new Vector2(Mathf.Round(centre.x / Step) * Step, Mathf.Round(centre.y / Step) * Step);
        for (int i = 0; i < template.slots.Length; i++) template.slots[i].position -= centre;
    }

    static Vector2[] Positions(FormationTemplate template)
    {
        var positions = new Vector2[template.slots.Length];
        for (int i = 0; i < positions.Length; i++) positions[i] = template.slots[i].position;
        return positions;
    }

    static string Footprint(FormationTemplate template, float spacing)
    {
        if (template.slots.Length == 0) return "—";
        Vector2 min = template.slots[0].position, max = min;
        foreach (var s in template.slots) { min = Vector2.Min(min, s.position); max = Vector2.Max(max, s.position); }
        Vector2 size = (max - min) * spacing;
        return $"{size.x:0.#} × {size.y:0.#} m";
    }

    static string Arrow(float headingDegrees)
    {
        float h = Mathf.Repeat(headingDegrees + 45f, 360f);
        return h < 90f ? "" : h < 180f ? "→" : h < 270f ? "↓" : "←";
    }

    static void Modify(FormationDefinition formation, string undo, System.Action change)
    {
        Undo.RecordObject(formation, undo);
        change();
        EditorUtility.SetDirty(formation);
    }
}

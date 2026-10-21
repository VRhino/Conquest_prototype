using System.Collections.Generic;
using UnityEngine;

/// <summary>Derives rows, roles and facings from a set of slot positions (authoring and migration).</summary>
public static class FormationSlotRoles
{
    /// <summary>
    /// Builds slots from integer cells, keeping their order: centred on the bounding box, front = largest y.
    /// Front row → Front; extreme columns of the other rows → Flank; last row → Rear; the rest → Interior.
    /// With <paramref name="flanksFaceOutward"/>, flanks face sideways and the rear faces back.
    /// </summary>
    public static FormationSlot[] FromCells(Vector2Int[] cells, bool flanksFaceOutward)
    {
        var positions = new Vector2[cells.Length];
        if (cells.Length == 0) return System.Array.Empty<FormationSlot>();
        Vector2 min = cells[0], max = cells[0];
        foreach (var c in cells) { min = Vector2.Min(min, c); max = Vector2.Max(max, c); }
        Vector2 center = (min + max) * 0.5f;
        for (int i = 0; i < cells.Length; i++) positions[i] = cells[i] - center;
        return Assign(positions, flanksFaceOutward);
    }

    /// <summary>Rows, roles and facings for slot positions already relative to the anchor (+y = forward).</summary>
    public static FormationSlot[] Assign(Vector2[] positions, bool flanksFaceOutward)
    {
        var slots = new FormationSlot[positions.Length];
        if (positions.Length == 0) return slots;

        // Rows: distinct y values, front (largest y) first.
        var rowYs = new List<float>();
        foreach (var p in positions)
            if (!rowYs.Exists(y => Mathf.Approximately(y, p.y))) rowYs.Add(p.y);
        rowYs.Sort((a, b) => b.CompareTo(a));
        int lastRow = rowYs.Count - 1;

        for (int i = 0; i < positions.Length; i++)
        {
            Vector2 p = positions[i];
            int row = rowYs.FindIndex(y => Mathf.Approximately(y, p.y));
            float rowMinX = float.MaxValue, rowMaxX = float.MinValue;
            foreach (var q in positions)
                if (Mathf.Approximately(q.y, p.y)) { rowMinX = Mathf.Min(rowMinX, q.x); rowMaxX = Mathf.Max(rowMaxX, q.x); }

            FormationSlotRole role;
            if (row == 0) role = FormationSlotRole.Front;
            else if (Mathf.Approximately(p.x, rowMinX) || Mathf.Approximately(p.x, rowMaxX)) role = FormationSlotRole.Flank;
            else if (row == lastRow) role = FormationSlotRole.Rear;
            else role = FormationSlotRole.Interior;

            float heading = 0f;
            if (flanksFaceOutward)
            {
                if (role == FormationSlotRole.Flank && Mathf.Abs(p.x) > 1e-3f) heading = p.x < 0f ? -90f : 90f;
                else if (role == FormationSlotRole.Rear) heading = 180f;
            }
            slots[i] = new FormationSlot { position = p, row = row, role = role, headingDegrees = heading };
        }
        return slots;
    }
}

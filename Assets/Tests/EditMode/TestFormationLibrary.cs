using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>Builds formation library blobs for tests, the same way the squad baker does.</summary>
public static class TestFormationLibrary
{
    /// <summary>One Template formation from integer cells, migrated like the legacy grid (centred, front = largest y).</summary>
    public static BlobAssetReference<FormationLibraryBlob> FromCells(FormationType type, float spacing, params int2[] cells)
    {
        var legacy = new Vector2Int[cells.Length];
        for (int i = 0; i < cells.Length; i++) legacy[i] = new Vector2Int(cells[i].x, cells[i].y);
        return Build(type, FormationLayoutKind.Template, spacing, 0f, 0f,
            new[] { new FormationTemplate { minUnits = 0, slots = FormationSlotRoles.FromCells(legacy, false) } });
    }

    public static BlobAssetReference<FormationLibraryBlob> FromCells(params int2[] cells) =>
        FromCells(FormationType.Line, 1f, cells);

    /// <summary>Any formation, from authoring data.</summary>
    public static BlobAssetReference<FormationLibraryBlob> Build(FormationType type, FormationLayoutKind layout,
        float spacing, float noiseRadius, float noiseAngleDegrees, FormationTemplate[] templates)
    {
        var builder = new BlobBuilder(Allocator.Temp);
        ref var root = ref builder.ConstructRoot<FormationLibraryBlob>();
        var formations = builder.Allocate(ref root.formations, 1);
        formations[0].formationType = type;
        formations[0].layout = layout;
        formations[0].spacing = spacing;
        formations[0].noiseRadius = noiseRadius;
        formations[0].noiseAngle = math.radians(noiseAngleDegrees);
        templates ??= System.Array.Empty<FormationTemplate>();
        var templateArray = builder.Allocate(ref formations[0].templates, templates.Length);
        for (int t = 0; t < templates.Length; t++)
        {
            templateArray[t].minUnits = templates[t].minUnits;
            var slots = builder.Allocate(ref templateArray[t].slots, templates[t].slots.Length);
            for (int k = 0; k < slots.Length; k++)
            {
                var s = templates[t].slots[k];
                slots[k] = new FormationSlotBlob
                {
                    position = s.position, row = s.row, role = s.role, heading = math.radians(s.headingDegrees)
                };
            }
        }
        var blob = builder.CreateBlobAssetReference<FormationLibraryBlob>(Allocator.Persistent);
        builder.Dispose();
        return blob;
    }

    /// <summary>Slot component for slot <paramref name="slotIndex"/> of the first formation, template 0.</summary>
    public static UnitGridSlotComponent Slot(BlobAssetReference<FormationLibraryBlob> blob, int slotIndex, float spacing = 1f)
    {
        FormationPositionCalculator.TryBuildSlot(ref blob.Value.formations[0], 0, slotIndex, spacing, 0u, out var slot);
        return slot;
    }
}

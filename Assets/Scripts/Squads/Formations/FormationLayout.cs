using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Pure formation layout (Burst-compatible): template choice by soldiers alive, slot lookup for Template and Scatter
/// layouts, and the local offset of a slot with spacing and deterministic organic noise.
/// </summary>
public static class FormationLayout
{
    /// <summary>Largest Scatter side whose cells fit the fixed list used to draw them.</summary>
    const int MaxScatterSide = 11;

    /// <summary>Index of the formation of the given type in the library, or -1.</summary>
    public static int Find(ref FormationLibraryBlob library, FormationType type)
    {
        for (int i = 0; i < library.formations.Length; i++)
            if (library.formations[i].formationType == type) return i;
        return -1;
    }

    /// <summary>Most soldiers the formation can place.</summary>
    public static int Capacity(ref FormationDataBlob formation)
    {
        if (formation.layout == FormationLayoutKind.Scatter) return MaxScatterSide * MaxScatterSide - 1;
        int max = 0;
        for (int t = 0; t < formation.templates.Length; t++)
            max = math.max(max, formation.templates[t].slots.Length);
        return max;
    }

    /// <summary>
    /// Template for <paramref name="aliveCount"/> soldiers: the first whose minimum fits and that has a slot for each
    /// soldier; otherwise the largest. Scatter returns its side length so a change of size is detected as a new layout.
    /// </summary>
    public static int SelectTemplate(ref FormationDataBlob formation, int aliveCount)
    {
        if (formation.layout == FormationLayoutKind.Scatter) return ScatterSide(aliveCount);
        int largest = -1, largestSlots = -1;
        for (int t = 0; t < formation.templates.Length; t++)
        {
            ref var template = ref formation.templates[t];
            if (template.minUnits <= aliveCount && template.slots.Length >= aliveCount) return t;
            if (template.slots.Length > largestSlots) { largest = t; largestSlots = template.slots.Length; }
        }
        return largest;
    }

    /// <summary>Slot <paramref name="slotIndex"/> of the chosen layout. False when there is none.</summary>
    public static bool TryGetSlot(ref FormationDataBlob formation, int templateIndex, int slotIndex, uint seed,
        out FormationSlotBlob slot)
    {
        slot = default;
        if (slotIndex < 0) return false;
        if (formation.layout == FormationLayoutKind.Scatter)
        {
            int side = math.clamp(templateIndex, 1, MaxScatterSide);
            if (slotIndex >= side * side - 1) return false;
            int2 cell = ScatterCell(side, slotIndex, seed);
            slot = new FormationSlotBlob
            {
                position = cell,
                row = side / 2 - cell.y,
                role = FormationSlotRole.Interior,
                heading = 0f
            };
            return true;
        }
        if (templateIndex < 0 || templateIndex >= formation.templates.Length) return false;
        ref var slots = ref formation.templates[templateIndex].slots;
        if (slotIndex >= slots.Length) return false;
        slot = slots[slotIndex];
        return true;
    }

    /// <summary>
    /// Unrotated offset of a slot from the anchor in metres, with deterministic noise, and its local facing (radians).
    /// </summary>
    public static float3 LocalOffset(in FormationSlotBlob slot, float spacing, float noiseRadius, float noiseAngle,
        uint seed, int slotIndex, out float heading)
    {
        var random = Random.CreateFromIndex(math.hash(new uint2(seed, (uint)slotIndex)));
        float2 noise = noiseRadius > 0f ? random.NextFloat2Direction() * (noiseRadius * math.sqrt(random.NextFloat())) : 0f;
        heading = slot.heading + (noiseAngle > 0f ? random.NextFloat(-noiseAngle, noiseAngle) : 0f);
        float2 p = slot.position * spacing + noise;
        return new float3(p.x, 0f, p.y);
    }

    /// <summary>Posture of a slot role in this formation (Normal when the role has none).</summary>
    public static UnitStance Posture(ref FormationDataBlob formation, FormationSlotRole role, bool moving)
    {
        for (int i = 0; i < formation.postures.Length; i++)
            if (formation.postures[i].role == role)
                return moving ? formation.postures[i].moving : formation.postures[i].formed;
        return UnitStance.Normal;
    }

    /// <summary>Spacing in effect for a formation scaled by the squad, never below the floor.</summary>
    public static float EffectiveSpacing(float formationSpacing, float squadScale)
    {
        float s = formationSpacing * (squadScale > 0f ? squadScale : 1f);
        return math.isfinite(s) ? math.max(FormationDefinition.MinSpacing, s) : FormationDefinition.MinSpacing;
    }

    /// <summary>Layout seed of a squad, stable for its whole battle.</summary>
    public static uint Seed(int squadInstanceId) => math.hash(new int2(squadInstanceId, 0x5f3759df));

    /// <summary>Scatter side: max(6, ceil(√(n+1))) forced odd, so the anchor cell sits in the middle.</summary>
    public static int ScatterSide(int unitCount)
    {
        int side = math.max(6, (int)math.ceil(math.sqrt(unitCount + 1f)));
        if ((side & 1) == 0) side++;
        return math.min(side, MaxScatterSide);
    }

    /// <summary>
    /// The k-th of distinct random cells in [-side/2, side/2]² without the anchor cell, from a seeded partial shuffle.
    /// </summary>
    public static int2 ScatterCell(int side, int k, uint seed)
    {
        int half = side / 2;
        var cells = new FixedList512Bytes<int>();
        for (int i = 0; i < side * side; i++)
            if (i != half * side + half) cells.Add(i);
        var random = Random.CreateFromIndex(seed);
        for (int i = 0; i <= k && i < cells.Length; i++)
        {
            int j = random.NextInt(i, cells.Length);
            (cells[i], cells[j]) = (cells[j], cells[i]);
        }
        int index = cells[math.min(k, cells.Length - 1)];
        return new int2(index % side - half, index / side - half);
    }
}

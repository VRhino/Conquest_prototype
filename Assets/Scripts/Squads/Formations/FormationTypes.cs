using System;
using UnityEngine;

/// <summary>How a formation lays out its slots.</summary>
public enum FormationLayoutKind : byte
{
    /// <summary>Authored templates, chosen by the number of soldiers alive.</summary>
    Template = 0,
    /// <summary>Distinct random cells around the anchor (seeded per squad), spaced by the formation spacing.</summary>
    Scatter = 1
}

/// <summary>Role of a slot inside its formation; drives facing and posture.</summary>
public enum FormationSlotRole : byte
{
    Interior = 0,
    Front = 1,
    Flank = 2,
    Rear = 3
}

/// <summary>One slot of a formation template, in spacing units relative to the anchor (+y = forward).</summary>
[Serializable]
public struct FormationSlot
{
    public Vector2 position;
    /// <summary>Rank counted from the front (0 = front row).</summary>
    public int row;
    public FormationSlotRole role;
    /// <summary>Local facing, degrees clockwise from forward (90 = right, -90 = left, 180 = back).</summary>
    public float headingDegrees;
}

/// <summary>Posture of the soldiers of one slot role, formed and while the formation moves.</summary>
[Serializable]
public struct FormationRolePosture
{
    public FormationSlotRole role;
    public UnitStance formed;
    public UnitStance moving;
}

/// <summary>A layout used while at least <see cref="minUnits"/> soldiers are alive.</summary>
[Serializable]
public class FormationTemplate
{
    [Min(0)] public int minUnits;
    public FormationSlot[] slots = Array.Empty<FormationSlot>();
}

using Unity.Entities;

/// <summary>
/// Current formation of a squad and the layout it was built with. Single writer: <see cref="FormationSystem"/>
/// (initial values from <see cref="SquadSpawningSystem"/>).
/// </summary>
public struct FormationComponent : IComponentData
{
    /// <summary>Formation selected by the player.</summary>
    public FormationType currentFormation;
    /// <summary>Template in use (Scatter: side of the cell square); -1 before the first layout.</summary>
    public int templateIndex;
    /// <summary>Soldiers alive when the layout was built.</summary>
    public int layoutUnitCount;
    /// <summary>Spacing in effect, metres (formation spacing × squad scale, at least 0.3).</summary>
    public float spacing;
    /// <summary>Squad multiplier of the formation spacing (1 = as authored).</summary>
    public float scale;
    /// <summary>Seed of the organic noise and Scatter cells, stable for the battle.</summary>
    public uint layoutSeed;
}

/// <summary>
/// Request to tighten or open the formation of a squad: new spacing multiplier. Consumed and removed by
/// <see cref="FormationSystem"/>.
/// </summary>
public struct SquadFormationSpacingRequest : IComponentData
{
    public float scale;
}

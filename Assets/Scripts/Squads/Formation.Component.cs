using Unity.Entities;

/// <summary>
/// Current formation of a squad and the layout it was built with. Single writer: <see cref="FormationSystem"/>
/// (initial values from <see cref="SquadSpawningSystem"/>).
/// </summary>
public struct FormationComponent : IComponentData
{
    /// <summary>Formation selected by the player.</summary>
    public FormationType currentFormation;
    /// <summary>Brace policy of the committed layout; copied while its formation blob is available.</summary>
    public bool supportsBrace;
    public float braceHalfAngleRadians;
    /// <summary>Template in use (Scatter: side of the cell square); -1 before the first layout.</summary>
    public int templateIndex;
    /// <summary>Soldiers alive when the layout was built or its gaps last closed.</summary>
    public int layoutUnitCount;
    /// <summary>Simulation time at which the gaps left by casualties are closed (L3-02); 0 = none waiting.</summary>
    public double compactAt;
    /// <summary>Spacing in effect, metres (formation spacing × squad scale, at least 0.3).</summary>
    public float spacing;
    /// <summary>Squad multiplier of the formation spacing (1 = as authored).</summary>
    public float scale;
    /// <summary>Seed of the organic noise and Scatter cells, stable for the battle.</summary>
    public uint layoutSeed;
}

/// <summary>
/// Enabled while a squad's layout may need rebuilding (Aprendizajes_Referencia.md L3-06): a formation was asked for, a
/// soldier died, or a spacing request arrived. <see cref="FormationSystem"/> only visits squads with it enabled and
/// turns it off once nothing is pending, so squads that do not change cost nothing.
/// Turned on by <see cref="OrderResolutionSystem"/>, <see cref="UnitDeathSystem"/> and <see cref="FormationSystem"/>.
/// </summary>
public struct FormationChangePending : IComponentData, IEnableableComponent { }

/// <summary>
/// Request to tighten or open the formation of a squad: new spacing multiplier. Consumed and removed by
/// <see cref="FormationSystem"/>.
/// </summary>
public struct SquadFormationSpacingRequest : IComponentData
{
    public float scale;
}

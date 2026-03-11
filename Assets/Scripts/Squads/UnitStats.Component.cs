using Unity.Entities;

/// <summary>
/// Base attributes for an individual unit inside a squad.
/// Values scale with squad level in <see cref="SquadProgressionSystem"/>.
/// </summary>
public struct UnitStatsComponent : IComponentData
{
    public float health;
    /// <summary>
    /// Final movement speed including base speed, level scaling, and weight multiplier.
    /// Calculated using UnitSpeedCalculator.CalculateFinalSpeed().
    /// </summary>
    public float speed;
    public float mass;
    /// <summary>
    /// Weight category of the unit: 1=light, 2=medium, 3=heavy.
    /// </summary>
    public float weight;
    public float block;

    public float slashingDefense;
    public float piercingDefense;
    public float bluntDefense;

    public float slashingDamage;
    public float piercingDamage;
    public float bluntDamage;

    public float slashingPenetration;
    public float piercingPenetration;
    public float bluntPenetration;

    public int leadershipCost;
}

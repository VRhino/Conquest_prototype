using Unity.Entities;

/// <summary>
/// Buffer element containing base stats for a single squad unit.
/// Values are copied from <see cref="SquadData"/> during baking.
/// </summary>
public struct UnitStatsBufferElement : IBufferElementData
{
    public int health;
    public int speed;
    public int mass;
    public int weightClass;
    public float blockValue;

    public float slashingDamage;
    public float piercingDamage;
    public float bluntDamage;

    public float slashingDefense;
    public float piercingDefense;
    public float bluntDefense;

    public int leadershipCost;
}

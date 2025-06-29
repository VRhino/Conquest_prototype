using Unity.Entities;

public class TeamBaker : Baker<TeamAuthoring>
{
    public override void Bake(TeamAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, new TeamComponent
        {
            value = (Team)authoring.teamValue
        });
    }
}

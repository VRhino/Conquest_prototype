using Unity.Entities;

public class IsLocalPlayerBaker : Baker<IsLocalPlayerAuthoring>
{
    public override void Bake(IsLocalPlayerAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent<IsLocalPlayer>(entity);
    }
}

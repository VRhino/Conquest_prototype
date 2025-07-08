using Unity.Entities;
using Unity.Mathematics;

public struct HeroMoveIntent : IComponentData
{
    public float3 Direction;
    public float Speed;
}

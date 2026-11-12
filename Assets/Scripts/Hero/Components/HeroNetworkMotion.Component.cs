using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Motion of a hero replicated from the battle server. A client has no NavMeshAgent to read it from, so
/// RemoteHeroAnimationDriver takes the velocity from here.
/// </summary>
public struct HeroNetworkMotion : IComponentData
{
    public float3 velocity;
    /// <summary>The speed the hero was asked to move at (walk or sprint), as NavMeshAgent.speed on the server.</summary>
    public float requestedSpeed;
}

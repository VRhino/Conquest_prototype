using Unity.Entities;
using UnityEngine.AI;

/// <summary>
/// Mueve unidades con NavMeshAgent hacia su slot de formación calculado por
/// GridFormationUpdateSystem, usando SetDestination() en vez de teleport directo.
/// Solo procesa entidades que tienen NavAgentComponent (NavMeshAgent adjunto en runtime).
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitFormationStateSystem))]
public partial class UnitNavMeshSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach (var (targetPos, entity) in
            SystemAPI.Query<RefRO<UnitTargetPositionComponent>>()
                     .WithAll<NavAgentComponent>()
                     .WithEntityAccess())
        {
            var agent = SystemAPI.ManagedAPI.GetComponent<NavMeshAgent>(entity);
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.updateRotation = true;
                agent.SetDestination(targetPos.ValueRO.position);
            }
        }
    }
}

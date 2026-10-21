using Unity.Entities;
using UnityEngine.AI;

/// <summary>
/// Moves AI heroes along an active <see cref="HeroDashComponent"/> with their NavMeshAgent (the local hero's dash goes
/// through <see cref="HeroMovementSystem"/> and its character controller). The agent keeps its path; the dash adds the
/// displacement and stays on the NavMesh.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(SkillEffectSystem))]
[UpdateAfter(typeof(HeroAIExecutionSystem))]
public partial class HeroAIDashSystem : SystemBase
{
    protected override void OnUpdate()
    {
        double now = SystemAPI.Time.ElapsedTime;
        float dt = SystemAPI.Time.DeltaTime;
        foreach (var (dash, entity) in SystemAPI.Query<RefRO<HeroDashComponent>>().WithAll<HeroAITag>().WithEntityAccess())
        {
            if (!dash.ValueRO.Active(now) || !EntityManager.HasComponent<NavMeshAgent>(entity)) continue;
            var agent = EntityManager.GetComponentObject<NavMeshAgent>(entity);
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) continue;
            agent.Move((UnityEngine.Vector3)(dash.ValueRO.direction * dash.ValueRO.speed * dt));
        }
    }
}

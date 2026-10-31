using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.AI;

/// <summary>
/// Single authority for writing agent.transform.rotation each frame.
///
/// Reads UnitRotationIntentComponent from every NavMesh unit — set by
/// UnitNavMeshSystem (priority Combat=10) and UnitFollowFormationSystem
/// (priority Formation=5) — applies the highest-priority intent, then
/// resets priority to 0 for the next frame.
///
/// If priority == 0 no intent was written this frame; NavMesh auto-rotation
/// (agent.updateRotation = true) remains in effect for that unit.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitFollowFormationSystem))]
public partial class UnitRotationResolutionSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
    }

    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;
        float combatTurnRate = (SystemAPI.HasSingleton<MovementTuningComponent>()
            ? SystemAPI.GetSingleton<MovementTuningComponent>() : MovementTuningComponent.Default).combatTurnRate;

        foreach (var (intent, entity) in SystemAPI
                     .Query<RefRW<UnitRotationIntentComponent>>()
                     .WithAll<NavAgentComponent>()
                     .WithEntityAccess())
        {
            bool braced = SquadCombatModeComponent.TryGetBrace(EntityManager, entity, out var brace);
            if (intent.ValueRO.priority > 0 || braced)
            {
                var agent = SystemAPI.ManagedAPI.GetComponent<NavMeshAgent>(entity);
                if (agent != null)
                {
                    // Combat facing turns at a capped rate (C7); formation intents already arrive smoothed.
                    quaternion target = intent.ValueRO.priority > 0 ? intent.ValueRO.targetRotation
                        : quaternion.LookRotationSafe(brace.forward, math.up());
                    quaternion rotation = intent.ValueRO.source == RotationSource.Combat
                        ? (UnityEngine.Quaternion)UnitTurn.Step(agent.transform.rotation, target, combatTurnRate, dt)
                        : (UnityEngine.Quaternion)target;
                    if (braced)
                    {
                        agent.updateRotation = false;
                        float front = math.atan2(brace.forward.x, brace.forward.z);
                        float3 facing = math.forward(rotation);
                        float offset = math.atan2(facing.x, facing.z) - front;
                        offset = math.atan2(math.sin(offset), math.cos(offset));
                        rotation = quaternion.RotateY(front + math.clamp(offset, -brace.halfAngleRadians, brace.halfAngleRadians));
                    }
                    agent.transform.rotation = rotation;
                }
            }

            // Reset for next frame — priority=0 means "no override, let NavMesh handle"
            intent.ValueRW.priority = 0;
        }
    }
}

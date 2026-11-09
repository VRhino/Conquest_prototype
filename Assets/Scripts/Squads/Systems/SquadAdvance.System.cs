using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.AI;

/// <summary>
/// Single writer of <see cref="SquadAdvanceComponent"/> and of <see cref="SquadHoldPositionComponent.advanceOffset"/>:
/// the squires' Testudo Advance. A toggle request starts it when the squad holds position in Testudo with every soldier
/// in position (formed, or within one spacing of its slot) and open ground ahead. While on, it moves the hold centre straight ahead (the formation's facing) at a fraction of the soldiers' base
/// speed; the formation systems carry the slots along with it. It stops on another toggle, on another order or
/// formation, when a soldier comes within contact distance of an enemy, or when the NavMesh ends ahead of the front rank.
/// Where it stops, the squad keeps holding, formed: the offset belongs to the Hold Position order.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(SquadOrderSystem))]
[UpdateBefore(typeof(FormationSystem))]
public partial class SquadAdvanceSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
    }

    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;
        var tuning = SystemAPI.HasSingleton<MovementTuningComponent>()
            ? SystemAPI.GetSingleton<MovementTuningComponent>() : MovementTuningComponent.Default;

        foreach (var (state, formation, units, entity) in SystemAPI
                     .Query<RefRO<SquadStateComponent>, RefRO<FormationComponent>, DynamicBuffer<SquadUnitElement>>()
                     .WithAll<SquadAdvanceComponent, SquadAdvanceToggleRequest>()
                     .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                     .WithEntityAccess())
        {
            bool toggled = SystemAPI.IsComponentEnabled<SquadAdvanceToggleRequest>(entity);
            if (toggled) SystemAPI.SetComponentEnabled<SquadAdvanceToggleRequest>(entity, false);
            bool holding = state.ValueRO.currentOrder == SquadOrderType.HoldPosition
                && state.ValueRO.currentState != SquadFSMState.Retreating
                && SystemAPI.HasComponent<SquadHoldPositionComponent>(entity);
            bool testudo = formation.ValueRO.currentFormation == FormationType.Testudo;

            if (!SystemAPI.IsComponentEnabled<SquadAdvanceComponent>(entity))
            {
                if (toggled && holding && testudo && AllInPosition(units, formation.ValueRO.spacing) && !Touching(entity, units, tuning))
                    TryStart(entity, units, tuning);
                continue;
            }

            var advance = SystemAPI.GetComponent<SquadAdvanceComponent>(entity);
            bool sameOrder = holding
                && math.all(SystemAPI.GetComponent<SquadHoldPositionComponent>(entity).holdCenter == advance.holdCenter);
            if (toggled || !sameOrder || !testudo || units.Length == 0
                || Touching(entity, units, tuning) || Blocked(units, advance.direction, tuning))
            {
                SystemAPI.SetComponentEnabled<SquadAdvanceComponent>(entity, false);
                continue;
            }
            var hold = SystemAPI.GetComponentRW<SquadHoldPositionComponent>(entity);
            hold.ValueRW.advanceOffset += advance.direction * advance.speed * dt;
        }
    }

    /// <summary>Starts walking along the formation's facing, at the advance fraction of its slowest soldier.</summary>
    void TryStart(Entity squad, DynamicBuffer<SquadUnitElement> units, in MovementTuningComponent tuning)
    {
        var hold = SystemAPI.GetComponent<SquadHoldPositionComponent>(squad);
        if (math.lengthsq(hold.holdRotation.value) < math.EPSILON) return; // No facing to walk along.
        float3 forward = math.mul(hold.holdRotation, math.forward());
        forward.y = 0f;
        if (math.lengthsq(forward) < math.EPSILON) return;
        float speed = tuning.advanceSpeedRatio * SlowestBaseSpeed(units);
        if (speed <= 0f || Blocked(units, math.normalize(forward), tuning)) return; // Nowhere to go.
        SystemAPI.SetComponent(squad, new SquadAdvanceComponent
        {
            holdCenter = hold.holdCenter,
            direction = math.normalize(forward),
            speed = speed,
        });
        SystemAPI.SetComponentEnabled<SquadAdvanceComponent>(squad, true);
    }

    /// <summary>Every soldier formed, or within one spacing of its resolved slot.</summary>
    bool AllInPosition(DynamicBuffer<SquadUnitElement> units, float spacing)
    {
        int placed = 0;
        foreach (var member in units)
        {
            if (!SystemAPI.Exists(member.Value) || !SystemAPI.HasComponent<UnitFormationStateComponent>(member.Value)) continue;
            bool formed = SystemAPI.GetComponent<UnitFormationStateComponent>(member.Value).State == UnitFormationState.Formed;
            bool near = SystemAPI.HasComponent<UnitResolvedSlotComponent>(member.Value) && SystemAPI.HasComponent<LocalTransform>(member.Value)
                && math.distance(SystemAPI.GetComponent<LocalTransform>(member.Value).Position.xz,
                    SystemAPI.GetComponent<UnitResolvedSlotComponent>(member.Value).position.xz) <= spacing;
            if (!formed && !near) return false;
            placed++;
        }
        return placed > 0;
    }

    float SlowestBaseSpeed(DynamicBuffer<SquadUnitElement> units)
    {
        float slowest = float.PositiveInfinity;
        foreach (var member in units)
            if (SystemAPI.Exists(member.Value) && SystemAPI.HasComponent<UnitStatsComponent>(member.Value))
                slowest = math.min(slowest, SystemAPI.GetComponent<UnitStatsComponent>(member.Value).speed);
        return float.IsInfinity(slowest) ? 0f : slowest;
    }

    /// <summary>Whether a soldier is within contact distance of one of the enemies the squad has detected.</summary>
    bool Touching(Entity squad, DynamicBuffer<SquadUnitElement> units, in MovementTuningComponent tuning)
    {
        if (!SystemAPI.HasBuffer<SquadTargetEntity>(squad)) return false;
        float reach = tuning.advanceContactDistance * tuning.advanceContactDistance;
        foreach (var enemy in SystemAPI.GetBuffer<SquadTargetEntity>(squad))
        {
            if (!SystemAPI.Exists(enemy.Value) || !SystemAPI.HasComponent<LocalTransform>(enemy.Value)) continue;
            float3 at = SystemAPI.GetComponent<LocalTransform>(enemy.Value).Position;
            foreach (var member in units)
                if (SystemAPI.Exists(member.Value) && SystemAPI.HasComponent<LocalTransform>(member.Value)
                    && math.distancesq(SystemAPI.GetComponent<LocalTransform>(member.Value).Position.xz, at.xz) <= reach)
                    return true;
        }
        return false;
    }

    /// <summary>Whether the NavMesh ends within the lookahead in front of a front-rank soldier (a wall, a cliff, water).
    /// A soldier off the NavMesh tells nothing.</summary>
    bool Blocked(DynamicBuffer<SquadUnitElement> units, float3 direction, in MovementTuningComponent tuning)
    {
        foreach (var member in units)
        {
            if (!SystemAPI.Exists(member.Value) || !SystemAPI.HasComponent<UnitGridSlotComponent>(member.Value)
                || !SystemAPI.HasComponent<LocalTransform>(member.Value)
                || SystemAPI.GetComponent<UnitGridSlotComponent>(member.Value).role != FormationSlotRole.Front)
                continue;
            float3 from = SystemAPI.GetComponent<LocalTransform>(member.Value).Position;
            if (!NavMesh.SamplePosition(from, out var onMesh, tuning.bodyRadius, NavMesh.AllAreas)) continue;
            var start = onMesh.position;
            if (NavMesh.Raycast(start, start + (UnityEngine.Vector3)(direction * tuning.advanceObstacleLookahead), out _, NavMesh.AllAreas))
                return true;
        }
        return false;
    }
}

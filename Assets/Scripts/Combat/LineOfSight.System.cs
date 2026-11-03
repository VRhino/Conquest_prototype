using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Line of sight of each squad to each enemy in range (Aprendizajes_Referencia.md L4-03, pattern of the reference): one
/// pair per squad and enemy squad or hero, batched physics rays against the scene (<see cref="LineOfSightTuning"/>),
/// a round-robin cursor per squad and a global budget per frame shared by demand. A ray goes from the squad's centre to
/// one soldier of the enemy squad at a time, at eye height. <see cref="EnemyDetectionSystem"/> publishes only the
/// enemies a squad sees, on the next frame. Only writes <see cref="SquadVisibilityElement"/> and <see cref="SquadVisibilityCursor"/>.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(EnemyDetectionSystem))]
public partial class LineOfSightSystem : SystemBase
{
    readonly List<Entity> _squads = new();
    readonly List<(Entity squad, int pair)> _rayOwners = new();
    /// <summary>Rays per job of the physics batch.</summary>
    const int RaycastBatchSize = 16;

    public int DebugRaysThisFrame { get; private set; }

    protected override void OnUpdate()
    {
        DebugRaysThisFrame = 0;
        var tuning = SystemAPI.TryGetSingleton<SquadSpawnConfigComponent>(out var config)
            ? config.lineOfSight : LineOfSightTuning.Default;
        if (!tuning.enabled) return;

        _squads.Clear();
        foreach (var (_, entity) in SystemAPI.Query<DynamicBuffer<SquadVisibilityElement>>()
                     .WithAll<SquadInRangeEnemy, SquadVisibilityCursor, SquadUnitElement>().WithEntityAccess())
            _squads.Add(entity);
        if (_squads.Count == 0) return;

        var demands = new NativeArray<int>(_squads.Count, Allocator.Temp);
        var quotas = new NativeArray<int>(_squads.Count, Allocator.Temp);
        var origins = new NativeArray<float3>(_squads.Count, Allocator.Temp);
        try
        {
            CastRays(tuning, demands, quotas, origins);
        }
        finally
        {
            demands.Dispose();
            quotas.Dispose();
            origins.Dispose();
        }
    }

    /// <summary>Syncs each squad's pairs, shares the budget and casts this frame's rays.</summary>
    void CastRays(in LineOfSightTuning tuning, NativeArray<int> demands, NativeArray<int> quotas, NativeArray<float3> origins)
    {
        for (int s = 0; s < _squads.Count; s++)
        {
            var visibility = EntityManager.GetBuffer<SquadVisibilityElement>(_squads[s]);
            LineOfSight.Sync(visibility, EntityManager.GetBuffer<SquadInRangeEnemy>(_squads[s], true));
            bool hasCentre = TryCentre(_squads[s], out float3 centre);
            origins[s] = centre + new float3(0f, tuning.eyeHeight, 0f);
            demands[s] = hasCentre ? visibility.Length : 0;
        }
        LineOfSight.Quotas(demands, tuning.raysPerFrame, quotas);

        int total = 0;
        for (int s = 0; s < quotas.Length; s++) total += quotas[s];
        if (total == 0) return;
        var commands = new NativeArray<RaycastCommand>(total, Allocator.TempJob);
        var hits = new NativeArray<RaycastHit>(total, Allocator.TempJob);
        try
        {
            BuildRays(tuning, quotas, origins, commands);
            RaycastCommand.ScheduleBatch(commands, hits, RaycastBatchSize, 1).Complete();
            RecordHits(hits);
        }
        finally
        {
            commands.Dispose();
            hits.Dispose();
        }
    }

    /// <summary>Each squad's quota of rays, from its cursor on: centre to one soldier (or hero) of the enemy, at eye height.</summary>
    void BuildRays(in LineOfSightTuning tuning, NativeArray<int> quotas, NativeArray<float3> origins,
        NativeArray<RaycastCommand> commands)
    {
        var query = new QueryParameters(tuning.blockingLayers, false, QueryTriggerInteraction.Ignore, false);
        _rayOwners.Clear();
        for (int s = 0; s < _squads.Count; s++)
        {
            if (quotas[s] == 0) continue;
            var visibility = EntityManager.GetBuffer<SquadVisibilityElement>(_squads[s]);
            var cursor = EntityManager.GetComponentData<SquadVisibilityCursor>(_squads[s]);
            for (int k = 0; k < quotas[s]; k++)
            {
                int pair = (cursor.next + k) % visibility.Length;
                var element = visibility[pair];
                float3 target = SamplePoint(element.enemy, element.sampleCursor) + new float3(0f, tuning.eyeHeight, 0f);
                element.sampleCursor++;
                visibility[pair] = element;
                float3 toTarget = target - origins[s];
                float distance = math.length(toTarget);
                commands[_rayOwners.Count] = new RaycastCommand(origins[s],
                    distance > 0f ? (Vector3)(toTarget / distance) : Vector3.forward, query, distance);
                _rayOwners.Add((_squads[s], pair));
            }
            cursor.next = (cursor.next + quotas[s]) % visibility.Length;
            EntityManager.SetComponentData(_squads[s], cursor);
        }
    }

    /// <summary>A ray that hit nothing is a clear sight for its pair.</summary>
    void RecordHits(NativeArray<RaycastHit> hits)
    {
        DebugRaysThisFrame = _rayOwners.Count;
        for (int r = 0; r < _rayOwners.Count; r++)
        {
            var (squad, pair) = _rayOwners[r];
            var visibility = EntityManager.GetBuffer<SquadVisibilityElement>(squad);
            var element = visibility[pair];
            element.clearMask = LineOfSight.Record(element.clearMask, hits[r].collider == null);
            visibility[pair] = element;
        }
    }

    /// <summary>Centre of the squad's living soldiers.</summary>
    bool TryCentre(Entity squad, out float3 centre)
    {
        centre = float3.zero;
        int alive = 0;
        foreach (var unit in EntityManager.GetBuffer<SquadUnitElement>(squad, true))
        {
            if (!EntityManager.Exists(unit.Value) || !EntityManager.HasComponent<LocalTransform>(unit.Value)
                || EntityManager.HasComponent<IsDeadComponent>(unit.Value)) continue;
            centre += EntityManager.GetComponentData<LocalTransform>(unit.Value).Position;
            alive++;
        }
        if (alive > 0) centre /= alive;
        return alive > 0;
    }

    /// <summary>A hero's position, or one soldier of an enemy squad (the cursor walks through them).</summary>
    float3 SamplePoint(Entity enemy, int sample)
    {
        if (EntityManager.HasBuffer<SquadUnitElement>(enemy))
        {
            var units = EntityManager.GetBuffer<SquadUnitElement>(enemy, true);
            for (int tries = 0; tries < units.Length; tries++)
            {
                Entity unit = units[(sample + tries) % units.Length].Value;
                if (EntityManager.Exists(unit) && EntityManager.HasComponent<LocalTransform>(unit))
                    return EntityManager.GetComponentData<LocalTransform>(unit).Position;
            }
        }
        return EntityManager.Exists(enemy) && EntityManager.HasComponent<LocalTransform>(enemy)
            ? EntityManager.GetComponentData<LocalTransform>(enemy).Position : float3.zero;
    }
}

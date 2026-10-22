using Unity.Entities;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ECS-to-GameObject bridge: reads ProjectileSpawnRequest entities created by
/// RangedAttackSystem, retrieves a pooled GO from ObjectPoolSystem, hands it to
/// ProjectileController.Initialize(), then destroys the request entity.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(RangedAttackSystem))]
public partial class ProjectileSpawnSystem : SystemBase
{
    EndSimulationEntityCommandBufferSystem _ecbSystem;
    private readonly List<Collider> _shooterColliders = new();
    /// <summary>Projectiles spawned by this system, oldest first, for the live cap (L2-7).</summary>
    private readonly Queue<ProjectileController> _live = new();

    protected override void OnCreate()
    {
        _ecbSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        if (ObjectPoolSystem.Instance == null)
            return;

        var ecb = _ecbSystem.CreateCommandBuffer();

        foreach (var (request, entity) in
                 SystemAPI.Query<RefRO<ProjectileSpawnRequest>>().WithEntityAccess())
        {
            var r  = request.ValueRO;
            var go = ObjectPoolSystem.Instance.GetFromPool(r.poolKey.ToString());

            if (go != null)
            {
                var controller = go.GetComponent<ProjectileController>();
                if (controller != null)
                {
                    MakeRoom(CombatTuning.Default.maxLiveProjectiles - 1);
                    _live.Enqueue(controller);
                    _shooterColliders.Clear();
                    if (VisualSyncUtility.TryGetVisual(World, r.shooter, out var shooterVisual))
                        shooterVisual.GetComponentsInChildren(true, _shooterColliders);
                    controller.Initialize(
                        r.shooter,
                        r.target,
                        r.spawnPosition,
                        r.attackDirection,
                        r.damageProfile,
                        r.damageProfileSnapshot,
                        r.sourceTeam,
                        r.multiplier,
                        r.poolKey.ToString(),
                        r.trajectory,
                        _shooterColliders);
                }
            }

            ecb.DestroyEntity(entity);
        }
    }

    /// <summary>
    /// Keeps at most <paramref name="capacity"/> projectiles in flight: forgets the ones that already landed and discards
    /// the oldest still flying (ref.: a hard cap of live projectiles protects against spikes).
    /// </summary>
    void MakeRoom(int capacity)
    {
        int pending = _live.Count;
        for (int i = 0; i < pending; i++)
        {
            var p = _live.Dequeue();
            if (p != null && p.InFlight) _live.Enqueue(p);
        }
        while (_live.Count > capacity)
            _live.Dequeue().Discard();
    }
}
